using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D), typeof(Rigidbody2D))]
public class EnemyBullet : MonoBehaviour
{
    // 초당 이동 거리.
    [SerializeField] float _speed = 5f;

    // 일반 적이 발사할 때 전달하는 조준 대상.
    [SerializeField] PlayerController _playerController;

    // 플레이어 또는 실드에 적용할 피해량.
    [SerializeField] float _damage;

    // 총알의 실제 크기를 이용해 이동 경로를 검사한다.
    BoxCollider2D _bulletCollider;

    // 총알의 앞쪽 위치를 표시하는 자식 Foward 오브젝트.
    [SerializeField] Transform _foward;

    // 사용이 끝난 적 탄환을 원래 풀에 반납한다.
    private PoolObject _poolObject;

    // 보스가 변경한 값을 다음 발사에 남기지 않도록 기본값을 저장한다.
    private float _defaultSpeed;
    private float _defaultDamage;

    // 플레이어 몸과 실드만 검사하는 필터.
    ContactFilter2D _hitFilter;

    // 매 프레임 배열을 새로 만들지 않도록 결과 목록을 재사용한다.
    readonly List<Collider2D> _overlaps = new();
    readonly List<RaycastHit2D> _castHits = new();

    Vector2 _dir;

    // Destroy가 프레임 끝에 실행되더라도 중복 피해를 막는다.
    bool _consumed;

    /// <summary>
    /// 컴포넌트 참조와 초기 실행 상태를 준비한다.
    /// </summary>
    private void Awake()
    {
        _bulletCollider = GetComponent<BoxCollider2D>();
        _bulletCollider.isTrigger = true;

        // 이동은 아래 코드에서 직접 관리한다.
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;

        // 프리팹 기본 설정과 반납 컴포넌트를 최초 한 번 저장한다.
        _poolObject = GetComponent<PoolObject>();
        _defaultSpeed = _speed;
        _defaultDamage = _damage;

        int playerLayer = LayerMask.NameToLayer("Player");
        int shieldLayer = LayerMask.NameToLayer("PlayerShield");

        if (playerLayer < 0 || shieldLayer < 0)
        {
            throw new System.InvalidOperationException(
                "Player와 PlayerShield 레이어가 필요합니다.");
        }

        // 충돌 이벤트용 설정과 별도로 검사할 레이어를 지정한다.
        _hitFilter = new ContactFilter2D();
        _hitFilter.SetLayerMask(
            (1 << playerLayer) | (1 << shieldLayer));

        _hitFilter.useTriggers = true;
    }

    // 일반 적은 현재 플레이어 위치를 기준으로 즉시 방향을 계산한다.
    public void Init(PlayerController playerController)
    {
        _playerController = playerController;

        SetDirection();
    }

    // 보스 패턴은 전달받은 방향, 속도, 공격력을 적용한다.
    public void Init(Vector2 direction, float speed, float damage)
    {
        _dir = direction.normalized;
        _speed = speed;
        _damage = damage;

        RotateToDirection();
    }


    /// <summary>
    /// SetDirection 동작에 필요한 상태와 처리 흐름을 실행한다.
    /// </summary>
    public void SetDirection()
    {
        if (_playerController == null)
        {
            throw new MissingReferenceException(
                $"{name}: 총알의 플레이어 조준 대상이 없습니다.");
        }

        _dir = (
            _playerController.transform.position -
            transform.position).normalized;

        // 확정된 이동 방향을 향하도록 총알을 회전시킨다.
        RotateToDirection();
    }

    /// <summary>
    /// 매 프레임 입력과 경과 시간에 따른 상태를 갱신한다.
    /// </summary>
    private void Update()
    {
        if (_consumed || Time.timeScale <= 0f)
        {
            return;
        }

        // 이동한 플레이어와 갱신된 실드 윤곽을 물리 검사에 반영한다.
        Physics2D.SyncTransforms();

        // 총알이 생성됐거나 플레이어가 움직여 이미 겹친 경우도 처리한다.
        if (TryHitOverlappingTarget())
        {
            return;
        }

        Vector2 movement = _dir * _speed * Time.deltaTime;
        float distance = movement.magnitude;

        if (distance > 0f)
        {
            _castHits.Clear();

            // 총알 중심의 선이 아니라 총알 콜라이더 전체를 이동시켜 검사한다.
            _bulletCollider.Cast(
                movement / distance,
                _hitFilter,
                _castHits,
                distance);

            // 실드와 몸 중 실제로 먼저 만나는 대상을 처리한다.
            _castHits.Sort(
                (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < _castHits.Count; i++)
            {
                // 아직 총알이 도착하지 않은 위치이므로 경로 검사 좌표를 사용한다.
                RaycastHit2D hit = _castHits[i];

                Vector2 hitPoint = HitEffectUtility.GetCastPoint(
                    _bulletCollider, hit);

                if (TryHitTarget(hit.collider, hitPoint))
                {
                    return;
                }
            }

            transform.position += (Vector3)movement;
        }

        // 기존 화면 밖 제거 범위를 유지한다.
        Vector3 position = transform.position;

        if (position.x > 9f || position.x < -9f ||
            position.y > 5f || position.y < -5f)
        {
            Consume();
        }
    }

    // 이동하기 전부터 겹쳐 있는 대상을 처리한다.
    /// <summary>
    /// 이동하기 전부터 겹쳐 있는 대상을 처리한다.
    /// </summary>
    private bool TryHitOverlappingTarget()
    {
        _overlaps.Clear();
        _bulletCollider.Overlap(_hitFilter, _overlaps);

        // 이미 몸과 실드 양쪽에 겹쳤다면 활성 실드를 우선한다.
        for (int i = 0; i < _overlaps.Count; i++)
        {
            Collider2D target = _overlaps[i];

            if (target.TryGetComponent(out Shield shield) &&
                TryHitTarget(target))
            {
                return true;
            }
        }

        // 실드로 막지 못한 경우 플레이어 몸을 검사한다.
        for (int i = 0; i < _overlaps.Count; i++)
        {
            if (TryHitTarget(_overlaps[i]))
            {
                return true;
            }
        }

        return false;
    }

    // 이미 계산된 충돌 위치를 받아 피해 또는 방어를 처리한다.
    /// <summary>
    /// 이미 계산된 충돌 위치를 받아 피해 또는 방어를 처리한다.
    /// </summary>
    private bool TryHitTarget(Collider2D target, Vector2 hitPoint)
    {
        if (_consumed || target == null)
        {
            return false;
        }

        // 실드 콜라이더를 플레이어 몸으로 해석하지 않는다.
        if (target.TryGetComponent(out Shield shield))
        {
            // 실드가 실제로 막은 위치를 전달한다.
            if (!shield.TryBlockDamage(_damage, hitPoint))
            {
                return false;
            }

            Consume();
            return true;
        }

        PlayerController player =
            target.GetComponentInParent<PlayerController>();

        if (player == null)
        {
            return false;
        }

        // 플레이어의 기존 무적·피격 처리를 그대로 사용한다.
        player.TakeHit(_damage);
        Consume();

        return true;
    }

    // 충돌하거나 화면을 벗어난 탄환을 한 번만 반납한다.
    private void Consume()
    {
        if (_consumed)
        {
            return;
        }

        _consumed = true;
        _bulletCollider.enabled = false;

        _poolObject.ReturnToPool();
    }

    // 이미 겹친 대상은 현재 두 콜라이더로 명중 위치를 계산한다.
    /// <summary>
    /// 이미 겹친 대상은 현재 두 콜라이더로 명중 위치를 계산한다.
    /// </summary>
    private bool TryHitTarget(Collider2D target)
    {
        if (_consumed || target == null)
        {
            return false;
        }

        Vector2 hitPoint = HitEffectUtility.GetSurfacePoint(
            _bulletCollider, target);

        return TryHitTarget(target, hitPoint);
    }

    // Foward가 가리키는 앞쪽을 실제 이동 방향에 맞춘다.
    /// <summary>
    /// Foward가 가리키는 앞쪽을 실제 이동 방향에 맞춘다.
    /// </summary>
    private void RotateToDirection()
    {
        // 이동 방향이 없으면 현재 회전을 유지한다.
        if (_dir.sqrMagnitude < 0.0001f)
        {
            return;
        }

        // 앞쪽 기준이 연결되지 않았다면 설정 오류를 알린다.
        if (_foward == null)
        {
            throw new MissingReferenceException(
                $"{name}: 자식 Foward를 연결하세요.");
        }

        // 총알 중심에서 Foward까지의 월드 방향이 현재 앞쪽이다.
        Vector2 currentForward = _foward.position - transform.position;

        // Foward가 중심과 겹치면 앞쪽 방향을 계산할 수 없다.
        if (currentForward.sqrMagnitude < 0.0001f)
        {
            throw new System.InvalidOperationException(
                $"{name}: Foward를 총알 중심에서 앞쪽으로 이동하세요.");
        }

        // 현재 앞쪽에서 이동 방향까지 필요한 회전각을 구한다.
        float angle = Vector2.SignedAngle(currentForward, _dir);

        // 루트를 회전해 스프라이트와 박스 콜라이더를 함께 정렬한다.
        transform.Rotate(0f, 0f, angle, Space.World);
    }

    // 풀에서 꺼낼 때 이전 탄환의 상태를 초기화한다.
    private void OnEnable()
    {
        _consumed = false;
        _bulletCollider.enabled = true;

        _playerController = null;
        _dir = Vector2.zero;

        _speed = _defaultSpeed;
        _damage = _defaultDamage;

        _overlaps.Clear();
        _castHits.Clear();
    }
}
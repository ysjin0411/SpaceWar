using System;
using System.Collections.Generic;
using UnityEngine;

// 스프라이트 충격파를 이동시키고 이동 경로의 충돌을 검사한다.
// 물리 콜라이더 대신 BoxCast로 직접 검사하므로 Rigidbody2D는 필요 없다.
[DisallowMultipleComponent]
public sealed class BossAlienResonanceWave : MonoBehaviour
{
    // 자식 Visual의 SpriteRenderer다. 원본 색을 그대로 사용한다.
    [SerializeField] private SpriteRenderer _visual;

    // 충격파를 발사한 보스다.
    private Boss_Alien _owner;

    // 이번 발사에서 고정할 이동 방향과 판정 크기다.
    private Vector2 _direction;
    private Vector2 _hitSize;
    private float _angle;

    // 설정 에셋에서 복사한 속도·피해와 남은 이동 거리다.
    private float _speed;
    private float _damage;
    private float _remainingDistance;

    // 초기화 전 이동과 한 충격파의 중복 피해를 막는다.
    private bool _running;
    private bool _hitConsumed;

    // 플레이어 몸과 방패만 검사한다.
    private ContactFilter2D _filter;

    // 매 프레임 새 배열을 만들지 않고 결과 목록을 재사용한다.
    private readonly List<RaycastHit2D> _hits =
        new List<RaycastHit2D>();

    /// <summary>
    /// 컴포넌트 참조와 초기 실행 상태를 준비한다.
    /// </summary>
    private void Awake()
    {
        // 이동 루트와 외형을 분리해 크기 조절이 피해 판정에 영향을 주지 않게 한다.
        if (_visual == null || _visual.sprite == null ||
            _visual.transform == transform ||
            !_visual.transform.IsChildOf(transform))
        {
            throw new InvalidOperationException(
                $"{name}: 자식 Visual의 SpriteRenderer와 스프라이트를 연결하세요.");
        }

        _visual.enabled = false;

        _filter = new ContactFilter2D();
        _filter.SetLayerMask(
            LayerMask.GetMask("Player", "PlayerShield"));
        _filter.useTriggers = true;
    }

    // 패턴이 균열 위치·표면 방향·전투 경계까지의 거리를 전달한다.
    /// <summary>
    /// 패턴이 균열 위치·표면 방향·전투 경계까지의 거리를 전달한다.
    /// </summary>
    public void Initialize(
        Boss_Alien owner,
        BossAlienResonanceConfig config,
        Vector2 origin,
        Vector2 direction,
        float travelDistance)
    {
        if (_running)
        {
            throw new InvalidOperationException(
                $"{name}: 이미 실행 중인 충격파입니다.");
        }

        if (owner == null || config == null ||
            direction.sqrMagnitude < 0.0001f ||
            !Positive(travelDistance) ||
            !Positive(config.WaveSpeed) ||
            !Positive(config.WaveWidth) ||
            !Positive(config.WaveThickness) ||
            config.WaveDamage <= 0)
        {
            throw new InvalidOperationException(
                $"{name}: 충격파의 참조·방향·거리·설정을 확인하세요.");
        }

        _owner = owner;
        _direction = direction.normalized;
        _angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;

        _speed = config.WaveSpeed;
        _damage = config.WaveDamage;
        _remainingDistance = travelDistance;
        _hitConsumed = false;

        // 로컬 X는 이동 방향, 로컬 Y는 충격파의 너비 방향이다.
        _hitSize = new Vector2(
            config.WaveThickness, config.WaveWidth);

        transform.SetPositionAndRotation(
            new Vector3(origin.x, origin.y, 0f),
            Quaternion.Euler(0f, 0f, _angle));

        // 스프라이트는 위아래로 긴 형태이며 밝은 앞면이 +X를 향한다.
        // 스프라이트 영역을 실제 판정 크기인 두께 × 너비에 맞춘다.
        Vector2 spriteSize = _visual.sprite.bounds.size;
        _visual.transform.localPosition = Vector3.zero;
        _visual.transform.localRotation = Quaternion.identity;
        _visual.transform.localScale = new Vector3(
            config.WaveThickness / spriteSize.x,
            config.WaveWidth / spriteSize.y,
            1f);

        _visual.enabled = true;
        _running = true;
    }

    /// <summary>
    /// 매 프레임 입력과 경과 시간에 따른 상태를 갱신한다.
    /// </summary>
    private void Update()
    {
        if (!_running)
        {
            return;
        }

        // 플레이어나 보스의 사망·비활성화 시 공격을 남기지 않는다.
        if (_owner == null || !_owner.CanRunArmPattern)
        {
            Destroy(gameObject);
            return;
        }

        if (Time.deltaTime <= 0f)
        {
            return;
        }

        float distance = Mathf.Min(
            _speed * Time.deltaTime, _remainingDistance);

        Vector2 position = transform.position;

        // 이동 전 위치부터 이동 후 위치까지 전체 경로를 검사한다.
        if (!_hitConsumed)
        {
            CheckHit(position, distance);
        }

        transform.position += (Vector3)(_direction * distance);
        _remainingDistance -= distance;

        if (_remainingDistance <= 0f)
        {
            Destroy(gameObject);
        }
    }

    // 경로상의 가까운 접촉부터 처리한다.
    /// <summary>
    /// 경로상의 가까운 접촉부터 처리한다.
    /// </summary>
    private void CheckHit(Vector2 origin, float distance)
    {
        Physics2D.SyncTransforms();
        _hits.Clear();

        Physics2D.BoxCast(
            origin,
            _hitSize,
            _angle,
            _direction,
            _filter,
            _hits,
            distance);

        _hits.Sort(CompareHits);

        foreach (RaycastHit2D hit in _hits)
        {
            if (hit.collider == null)
            {
                continue;
            }

            // 방패가 먼저 충격파를 막았다면 몸에는 피해를 주지 않는다.
            if (hit.collider.TryGetComponent(out Shield shield))
            {
                if (shield.TryBlockDamage(_damage, hit.point))
                {
                    _hitConsumed = true;
                    return;
                }

                continue;
            }

            PlayerController player =
                hit.collider.GetComponentInParent<PlayerController>();

            if (player == null ||
                !player.isActiveAndEnabled ||
                player.IsDead)
            {
                continue;
            }

            // 피해 이벤트에서 다시 호출되더라도 중복 적용하지 않는다.
            // 기존 잡기 보호와 피격 무적 규칙은 TakeHit이 처리한다.
            _hitConsumed = true;
            player.TakeHit(_damage);
            return;
        }
    }

    // 거리 순으로 정렬하고, 같은 거리면 방패 접촉을 우선한다.
    /// <summary>
    /// 거리 순으로 정렬하고, 같은 거리면 방패 접촉을 우선한다.
    /// </summary>
    private static int CompareHits(RaycastHit2D a, RaycastHit2D b)
    {
        int order = a.distance.CompareTo(b.distance);

        if (order != 0)
        {
            return order;
        }

        bool aShield = a.collider != null &&
            a.collider.TryGetComponent<Shield>(out _);
        bool bShield = b.collider != null &&
            b.collider.TryGetComponent<Shield>(out _);

        return bShield.CompareTo(aShield);
    }

    // 속도와 크기에 사용할 수 있는 유효한 양수인지 검사한다.
    /// <summary>
    /// 속도와 크기에 사용할 수 있는 유효한 양수인지 검사한다.
    /// </summary>
    private static bool Positive(float value)
    {
        return !float.IsNaN(value) &&
            !float.IsInfinity(value) &&
            value > 0f;
    }

    /// <summary>
    /// 비활성화 시 이벤트 연결과 실행 상태를 정리한다.
    /// </summary>
    private void OnDisable()
    {
        // 제거 또는 비활성화 직후에는 추가 피해를 검사하지 않는다.
        _running = false;
    }
}
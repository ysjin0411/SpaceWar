using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 빠르게 직진하며 여러 적을 관통하는 탄환이다.
/// 같은 탄환은 동일한 적에게 한 번만 피해를 준다.
/// </summary>
public class Sniper : Projectile
{
    // 관통탄이 피해를 줄 적의 레이어다.
    [SerializeField] LayerMask _enemyLayerMask;

    // 탄환이 지나간 자리에 생성할 잔상 프리팹이다.
    [SerializeField] SniperTrailEffect _trailPrefab;

    // 잔상 사이의 월드 거리다.
    // 작을수록 촘촘하게 이어지지만 생성량도 증가한다.
    [Min(0.01f)]
    [SerializeField] float _trailSpacing = 0.4f;

    // 마지막으로 잔상을 생성한 월드 위치다.
    Vector3 _lastTrailPosition;

    // 잔상이 탄환의 발사 방향을 향하도록 사용할 회전값이다.
    Quaternion _trailRotation;

    // 발사 초기화가 끝난 뒤에만 잔상을 생성한다.
    bool _isTrailReady;

    // 탄환 하나가 이미 타격한 적들을 기억한다.
    // 적에게 Collider가 여러 개 있어도 중복 피해를 막는다.
    readonly HashSet<IDamageable> _damagedTargets = new();

    // 이동 경로 검사 결과를 재사용한다.
    readonly List<RaycastHit2D> _castResults = new();

    // 실제 탄환 크기로 이동 경로를 검사할 Collider다.
    Collider2D _projectileCollider;

    // Enemy 레이어와 Trigger를 포함하는 검사 조건이다.
    ContactFilter2D _hitFilter;

    // 적을 맞혀도 탄환을 제거하지 않는다.
    protected override bool DestroyAfterHit => false;

    protected override void Awake()
    {
        // 부모의 Rigidbody2D 연결과 Trigger 검사를 실행한다.
        base.Awake();

        // 잔상 프리팹 연결 누락을 확인한다.
        if (_trailPrefab == null)
        {
            throw new System.InvalidOperationException(
                $"{name}의 Sniper에 Trail Prefab이 연결되지 않았습니다.");
        }

        // 0 이하 간격은 잔상 생성 반복문이 끝나지 않으므로 막는다.
        if (_trailSpacing <= 0f)
        {
            throw new System.InvalidOperationException(
                $"{name}의 Trail Spacing은 0보다 커야 합니다.");
        }

        _projectileCollider = GetComponent<Collider2D>();

        // 경로 검사에서 적의 Trigger Collider도 감지한다.
        _hitFilter = new ContactFilter2D();
        _hitFilter.SetLayerMask(_enemyLayerMask);
        _hitFilter.useTriggers = true;
    }

    /// <summary>
    /// 발사 방향과 최초 속도를 적용한다.
    /// </summary>
    protected override void OnInitialized()
    {
        // 탄환 한 발의 타격 기록을 초기화한다.
        _damagedTargets.Clear();

        // 오른쪽을 향한 탄환 이미지를 발사 방향으로 회전한다.
        float angle = Mathf.Atan2(
            Direction.y,
            Direction.x) * Mathf.Rad2Deg;

        Body.rotation = angle;

        // 초기화 직후부터 일정한 속도로 이동한다.
        Body.linearVelocity = Direction * MoveSpeed;

        // 잔상은 탄환과 동일한 발사 방향으로 배치한다.
        _trailRotation = Quaternion.Euler(0f, 0f, angle);

        // 발사 위치부터 거리 측정을 시작한다.
        _lastTrailPosition = transform.position;
        _isTrailReady = true;

        // 발사 시작점에도 첫 잔상을 남긴다.
        SpawnTrail(_lastTrailPosition);
    }

    /// <summary>
    /// 화면에 표시되는 탄환 위치를 기준으로
    /// 실제로 지나온 구간에 일정 거리마다 잔상을 남긴다.
    /// </summary>
    private void LateUpdate()
    {
        if (!_isTrailReady || Time.timeScale <= 0f)
        {
            return;
        }

        Vector3 currentPosition = transform.position;

        Vector3 remainingPath =
            currentPosition - _lastTrailPosition;

        float remainingDistance = remainingPath.magnitude;

        // 다음 잔상을 생성할 거리만큼 이동하지 않았다면 기다린다.
        if (remainingDistance < _trailSpacing)
        {
            return;
        }

        Vector3 pathDirection =
            remainingPath / remainingDistance;

        // 빠른 이동으로 여러 간격을 지나도
        // 그 사이의 위치를 빠짐없이 채운다.
        while (remainingDistance >= _trailSpacing)
        {
            _lastTrailPosition +=
                pathDirection * _trailSpacing;

            SpawnTrail(_lastTrailPosition);

            remainingDistance -= _trailSpacing;
        }

        // 마지막 생성 위치를 유지하므로
        // 남은 짧은 거리는 다음 프레임 이동량과 합쳐진다.
    }

    /// <summary>
    /// 탄환과 독립된 잔상을 월드 위치에 생성한다.
    /// 잔상은 자체적으로 4컷을 재생하고 사라진다.
    /// </summary>
    private void SpawnTrail(Vector3 worldPosition)
    {
        // 부모를 지정하지 않아 탄환이나 플레이어를 따라가지 않는다.
        Instantiate(
            _trailPrefab,
            worldPosition,
            _trailRotation);
    }

    /// <summary>
    /// 다음 물리 프레임에 이동할 구간의 적을 검사하고 직진한다.
    /// </summary>
    protected override void MoveProjectile()
    {
        float moveDistance =
            MoveSpeed * Time.fixedDeltaTime;

        _castResults.Clear();

        // 탄환 Collider 모양을 진행 방향으로 훑는다.
        // 프레임 사이에 지나쳐 버릴 수 있는 작은 적도 검사한다.
        _projectileCollider.Cast(
            Direction,
            _hitFilter,
            _castResults,
            moveDistance);

        for (int index = 0;
             index < _castResults.Count;
             index++)
        {
            // 경로 검사에서 얻은 실제 명중 위치를 함께 전달한다.
            RaycastHit2D hit = _castResults[index];

            TryHitCollider(
                hit.collider,
                HitEffectUtility.GetCastPoint(_projectileCollider, hit));
        }

        // 적을 맞혀도 속도를 유지하며 계속 전진한다.
        Body.linearVelocity =
            Direction * MoveSpeed;
    }

    protected override void OnTriggerEnter2D(Collider2D collision)
    {
        // 실제 겹침으로 감지한 경우 상대 표면의 위치를 사용한다.
        TryHitCollider(
            collision,
            HitEffectUtility.GetSurfacePoint(
                _projectileCollider, collision));
    }

    private void TryHitCollider(
    Collider2D collision,
    Vector2 hitPoint)
    {
        // 설정한 공격 대상 레이어만 처리한다.
        int layerBit = 1 << collision.gameObject.layer;

        if ((_enemyLayerMask.value & layerBit) == 0)
        {
            return;
        }

        IDamageable target =
            collision.GetComponentInParent<IDamageable>();

        if (target == null || target.IsDead)
        {
            return;
        }

        // 부모의 공통 히트 이펙트 생성에 사용할 위치다.
        HitPoint = hitPoint;

        // 기존 중복 타격 방지를 거쳐 피해와 이펙트를 한 번 적용한다.
        HandleTargetHit(target);
    }

    /// <summary>
    /// 경로 검사와 Trigger가 같은 적을 감지해도 한 번만 타격한다.
    /// </summary>
    protected override void HandleTargetHit(
        IDamageable target)
    {
        // 이미 맞힌 적이면 추가 피해를 주지 않는다.
        if (!_damagedTargets.Add(target))
        {
            return;
        }

        // 부모의 공통 공격력 적용 처리를 사용한다.
        base.HandleTargetHit(target);
    }
}
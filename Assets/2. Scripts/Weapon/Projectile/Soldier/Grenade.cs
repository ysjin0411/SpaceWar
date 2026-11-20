using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>발사 순간의 조준 방향으로 직진하고 적 접촉 시에만 범위 폭발한다.</summary>
public class Grenade : Projectile
{
    // 폭발 반경과 피해 대상 레이어.
    [Min(0.1f), SerializeField] private float _explosionRadius = 2f;
    [SerializeField] private LayerMask _enemyLayerMask;
    // 본체가 반납되어도 재생하는 독립 폭발 연출.
    [SerializeField] private SpriteExplosionEffect _explosionEffectPrefab;
    // 이동과 폭발 중복을 막는 발사별 상태.
    private bool _isFlying;
    private bool _hasExploded;
    // 비행 경로 검색 조건과 재사용 결과 목록.
    private ContactFilter2D _flightFilter;
    private readonly List<RaycastHit2D> _castHits = new();
    private readonly List<Collider2D> _overlaps = new();
    private readonly HashSet<IDamageable> _damagedTargets = new();
    // 실제 카메라 영역과 이미지 전체의 이탈을 검사한다.
    private Camera _worldCamera;
    private SpriteRenderer _flightRenderer;
    private readonly Plane[] _cameraPlanes = new Plane[6];

    /// <summary>중력 없는 직선 비행과 검색 조건을 초기화한다.</summary>
    protected override void OnInitialized()
    {
        // 풀 재사용 시 이전 발사의 상태를 비운다.
        _hasExploded = false;
        _isFlying = false;
        _damagedTargets.Clear();
        if (_explosionEffectPrefab == null || MoveSpeed <= 0f || _enemyLayerMask.value == 0)
            throw new InvalidOperationException($"{name}: 수류탄 속도, 적 레이어, 폭발 이펙트를 확인해주세요.");
        _worldCamera = Camera.main;
        _flightRenderer = GetComponentInChildren<SpriteRenderer>();
        if (_worldCamera == null || _flightRenderer == null)
            throw new InvalidOperationException($"{name}: MainCamera와 수류탄 Renderer가 필요합니다.");
        // 위치는 MoveProjectile에서 관리하며 물리 중력과 회전은 사용하지 않는다.
        Body.bodyType = RigidbodyType2D.Kinematic;
        Body.gravityScale = 0f;
        Body.linearVelocity = Vector2.zero;
        Body.angularVelocity = 0f;
        _flightFilter = new ContactFilter2D();
        _flightFilter.SetLayerMask(_enemyLayerMask);
        _flightFilter.useTriggers = true;
        _isFlying = true;
    }

    /// <summary>출발 위치와 이동 경로에서 살아 있는 첫 적을 검사한다.</summary>
    protected override void MoveProjectile()
    {
        if (!_isFlying || _hasExploded) return;
        Physics2D.SyncTransforms();
        // 생성 위치부터 겹친 적도 놓치지 않는다.
        _overlaps.Clear();
        HitCollider.Overlap(_flightFilter, _overlaps);
        foreach (Collider2D hit in _overlaps)
        {
            IDamageable target = hit == null ? null : hit.GetComponentInParent<IDamageable>();
            if (target == null || target.IsDead) continue;
            Explode(target);
            return;
        }
        // 빠른 이동 중 통과하는 적은 Collider 전체의 Cast로 찾는다.
        float distance = MoveSpeed * Time.fixedDeltaTime;
        _castHits.Clear();
        HitCollider.Cast(Direction, _flightFilter, _castHits, distance);
        IDamageable nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (RaycastHit2D hit in _castHits)
        {
            if (hit.collider == null || hit.distance >= nearestDistance) continue;
            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
            if (target == null || target.IsDead) continue;
            nearest = target;
            nearestDistance = hit.distance;
        }
        if (nearest != null)
        {
            // 최초 접촉 위치에서 범위 폭발한다.
            Body.position += Direction * nearestDistance;
            Physics2D.SyncTransforms();
            Explode(nearest);
            return;
        }
        // 중력이나 조준선의 이후 변화 없이 발사 방향을 유지한다.
        Body.position += Direction * distance;
    }

    /// <summary>기존 시간 수명 대신 카메라 밖 완전 이탈로 반환한다.</summary>
    protected override void Update()
    {
        if (!_isFlying || _hasExploded || Time.timeScale <= 0f) return;
        // 부모 Update를 호출하지 않아 기존 3초 수명으로 조기 소멸하지 않는다.
        GeometryUtility.CalculateFrustumPlanes(_worldCamera, _cameraPlanes);
        if (!GeometryUtility.TestPlanesAABB(_cameraPlanes, _flightRenderer.bounds))
        {
            _isFlying = false;
            ReturnToPool();
        }
    }

    /// <summary>명중한 적과 폭발 범위 안의 적에게 각각 한 번 피해를 준다.</summary>
    private void Explode(IDamageable directTarget)
    {
        if (_hasExploded || !_isFlying) return;
        _hasExploded = true;
        _isFlying = false;
        Vector2 position = Body.position;
        // 피해로 전투가 종료되기 전에 독립 폭발 연출을 만든다.
        SpriteExplosionEffect effect = Instantiate(_explosionEffectPrefab, position, Quaternion.identity);
        effect.Play();
        GameAudio.Play(GameSound.GrenadeExplosion);
        _overlaps.Clear();
        Physics2D.OverlapCircle(position, _explosionRadius, _flightFilter, _overlaps);
        _damagedTargets.Clear();
        // Cast 경계에서 검출한 직접 명중 대상도 포함하되 중복 피해는 막는다.
        if (directTarget != null && !directTarget.IsDead) _damagedTargets.Add(directTarget);
        foreach (Collider2D hit in _overlaps)
        {
            IDamageable target = hit == null ? null : hit.GetComponentInParent<IDamageable>();
            if (target != null && !target.IsDead) _damagedTargets.Add(target);
        }
        foreach (IDamageable target in _damagedTargets)
        {
            // 앞선 피해로 사망한 대상은 건너뛴다.
            if (!target.IsDead) target.TakeHit(Damage);
        }
        // 폭발 후 본체는 기존 풀로 반환한다.
        ReturnToPool();
    }

    /// <summary>실제 물리 접촉도 동일한 폭발 경로로 처리한다.</summary>
    protected override void OnTriggerEnter2D(Collider2D collision)
    {
        if (!_isFlying || _hasExploded) return;
        if ((_enemyLayerMask.value & (1 << collision.gameObject.layer)) == 0) return;
        IDamageable target = collision.GetComponentInParent<IDamageable>();
        if (target != null && !target.IsDead) Explode(target);
    }

    /// <summary>풀 반환 시 비행 상태와 물리 움직임을 정리한다.</summary>
    protected override void OnDisable()
    {
        _isFlying = false;
        // 부모가 속도와 예약 작업을 정리한다.
        base.OnDisable();
    }

    /// <summary>Scene에서 폭발 범위를 표시한다.</summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.25f, 0f, 1f);
        Gizmos.DrawWireSphere(transform.position, _explosionRadius);
    }
}

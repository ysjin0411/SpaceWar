using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// FirePoint에서 조준 방향으로 투척된 뒤
/// Rigidbody2D 중력을 받아 포물선으로 이동하고,
/// 신관 시간이 끝나면 주변 적에게 범위 피해를 주는 수류탄이다.
/// </summary>
public class Grenade : Projectile
{
    [Header("수류탄 투척 설정")]

    // 수류탄이 날아가는 동안 초당 회전하는 각도다.
    // 양수면 반시계 방향, 음수면 시계 방향으로 회전한다.
    [SerializeField] float _rotationSpeed;

    // 발사 방향으로 이 거리만큼 직진한 뒤 중력을 적용한다.
    // 월드 거리 기준이며, 위쪽으로 발사해도 동일한 거리를 직진한다.
    [Min(0f)]
    [SerializeField] float _gravityStartDistance = 3f;

    // 프리팹 Rigidbody2D에 설정된 원래 중력 배율이다.
    float _flightGravityScale;

    // 중력 없이 직진해야 하는 남은 시간이다.
    float _straightFlightRemaining;

    // 조준선에서도 같은 직진 거리를 사용한다.
    public float GravityStartDistance => _gravityStartDistance;

    [Header("수류탄 폭발 설정")]

    // 수류탄을 던진 순간부터 폭발하기까지 걸리는 시간이다.
    [Min(0.1f)]
    [SerializeField] float _fuseTime = 1.2f;

    // 폭발할 때 적에게 피해를 주는 원형 범위다.
    [Min(0.1f)]
    [SerializeField] float _explosionRadius = 2f;

    // 폭발 피해를 검사할 Enemy 레이어다.
    [SerializeField] LayerMask _enemyLayerMask;

    // 수류탄이 폭발할 때 생성할 독립적인 폭발 이펙트 프리팹이다.
    [SerializeField] SpriteExplosionEffect _explosionEffectPrefab;

    // 이미 폭발 처리를 마쳤는지 확인하여 중복 폭발을 방지한다.
    bool _hasExploded;

    // 조준선이 실제 수류탄과 같은 최초 투척 속도를 사용하게 제공한다.
    public float LaunchSpeed => MoveSpeed;

    // 조준선이 수류탄 폭발 시점까지 궤적을 표시하게 제공한다.
    public float FuseTime => _fuseTime;

    // 조준선이 Rigidbody2D와 같은 중력 배율을 사용하게 제공한다.
    public float GravityScale
    {
        get
        {
            Rigidbody2D grenadeBody =
                GetComponent<Rigidbody2D>();

            return grenadeBody.gravityScale;
        }
    }

    /// <summary>
    /// Weapon이 공격력과 조준 방향을 전달한 직후 실행된다.
    /// Rigidbody2D에 최초 투척 속도와 회전 속도를 적용한다.
    /// </summary>
    protected override void OnInitialized()
    {
        // 폭발 이펙트가 없으면 정상적인 수류탄 공격을 완성할 수 없다.
        if (_explosionEffectPrefab == null)
        {
            throw new InvalidOperationException(
                $"{name}의 Grenade에 Explosion Effect Prefab이 연결되지 않았습니다.");
        }

        // 거리와 속도로 직진 시간을 계산하므로 양수 속도가 필요하다.
        if (MoveSpeed <= 0f)
        {
            throw new InvalidOperationException(
                $"{name}의 Grenade Move Speed는 0보다 커야 합니다.");
        }

        // Inspector에 설정한 중력을 저장한다.
        _flightGravityScale = Body.gravityScale;

        // 일정 속도에서 거리 / 속도는 해당 거리를 이동하는 시간이다.
        _straightFlightRemaining =
            _gravityStartDistance / MoveSpeed;

        // 직진 구간에서는 중력을 끄고,
        // 직진 거리가 0이면 처음부터 중력을 사용한다.
        Body.gravityScale =
            _straightFlightRemaining > 0f
                ? 0f
                : _flightGravityScale;

        // 전달받은 조준 방향으로 발사한다.
        Body.linearVelocity =
            Direction * MoveSpeed;

        // 수류탄이 날아가는 동안 회전하게 만든다.
        Body.angularVelocity =
            _rotationSpeed;

        // 설정한 신관 시간이 지나면 폭발한다.
        Invoke(
            nameof(Explode),
            _fuseTime);
    }

    /// <summary>
    /// 직진 구간이 끝나면 중력을 적용한다.
    /// 경계가 물리 프레임 중간에 걸리면 해당 시간 비율만큼 적용한다.
    /// </summary>
    protected override void MoveProjectile()
    {
        if (_hasExploded)
        {
            return;
        }

        float stepTime = Time.fixedDeltaTime;

        // 이번 물리 프레임 중 직진에 사용할 시간이다.
        float straightTime = Mathf.Min(
            _straightFlightRemaining,
            stepTime);

        _straightFlightRemaining = Mathf.Max(
            0f,
            _straightFlightRemaining - stepTime);

        // 직진 구간에서는 0, 낙하 구간에서는 원래 중력이다.
        // 경계 프레임은 남은 시간 비율만큼 중력을 적용한다.
        float gravityTimeRatio =
            (stepTime - straightTime) / stepTime;

        Body.gravityScale =
            _flightGravityScale * gravityTimeRatio;
    }

    /// <summary>
    /// 신관 시간이 끝나면 폭발 범위 안의 적에게 한 번씩 피해를 주고
    /// 독립적인 폭발 이펙트를 생성한다.
    /// </summary>
    private void Explode()
    {
        // 같은 수류탄이 두 번 폭발하는 것을 방지한다.
        if (_hasExploded)
        {
            return;
        }

        _hasExploded = true;

        Vector2 explosionPosition =
            transform.position;

        // 폭발 범위 안에 있는 Enemy 레이어의 Collider를 모두 가져온다.
        Collider2D[] hitColliders =
            Physics2D.OverlapCircleAll(
                explosionPosition,
                _explosionRadius,
                _enemyLayerMask);

        // 하나의 적이 Collider를 여러 개 가지고 있어도
        // 폭발 피해는 한 번만 적용하기 위해 사용한다.
        HashSet<IDamageable> damagedTargets =
            new HashSet<IDamageable>();

        foreach (Collider2D hitCollider in hitColliders)
        {
            // Collider 자신이나 부모에서 피해를 받을 대상을 찾는다.
            IDamageable target =
                hitCollider.GetComponentInParent<IDamageable>();

            // 피해 대상이 아니거나 이미 사망했다면 건너뛴다.
            if (target == null || target.IsDead)
            {
                continue;
            }

            // 같은 대상의 다른 Collider를 다시 찾은 경우 건너뛴다.
            if (!damagedTargets.Add(target))
            {
                continue;
            }

            // WeaponRuntimeState에서 전달받은 현재 공격력을 적용한다.
            target.TakeHit(Damage);
        }

        // 수류탄과 독립된 폭발 이펙트를 생성한다.
        // 수류탄 본체가 제거되어도 이펙트는 자체 Tween을 끝까지 재생한다.
        SpriteExplosionEffect explosionEffect =
            Instantiate(
                _explosionEffectPrefab,
                explosionPosition,
                Quaternion.identity);

        explosionEffect.Play();

        // 폭발한 수류탄의 물리 이동을 즉시 멈춘다.
        Body.linearVelocity = Vector2.zero;
        Body.angularVelocity = 0f;

        // 수류탄 본체를 제거한다.
        // 화면에 보이는 폭발은 SpriteExplosionEffect가 Tween으로 자연스럽게 사라진다.
        Destroy(gameObject);
    }

    /// <summary>
    /// 수류탄이 살아 있는 적에게 닿으면
    /// 남은 신관 시간과 관계없이 충돌 위치에서 즉시 폭발한다.
    /// </summary>
    protected override void OnTriggerEnter2D(
        Collider2D collision)
    {
        // 충돌한 Collider 또는 부모에서 피해 대상을 찾는다.
        // 일반 Enemy와 BossEnemy를 모두 처리할 수 있다.
        IDamageable target =
            collision.GetComponentInParent<IDamageable>();

        // 적이 아닌 오브젝트와 충돌한 경우에는
        // 기존 포물선 이동과 신관 시간을 계속 유지한다.
        if (target == null)
        {
            return;
        }

        // 이미 사망한 적의 Collider에는 반응하지 않는다.
        if (target.IsDead)
        {
            return;
        }

        // 직접 충돌 피해를 따로 적용하지 않고
        // 충돌한 현재 위치를 중심으로 범위 폭발을 발생시킨다.
        // 충돌한 적도 폭발 범위 안에 있으므로 피해는 한 번만 받는다.
        Explode();
    }

    /// <summary>
    /// Scene 뷰에서 실제 폭발 피해 범위를 확인한다.
    /// 게임 화면에는 표시되지 않는다.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(
            1f,
            0.25f,
            0f,
            1f);

        Gizmos.DrawWireSphere(
            transform.position,
            _explosionRadius);
    }
}
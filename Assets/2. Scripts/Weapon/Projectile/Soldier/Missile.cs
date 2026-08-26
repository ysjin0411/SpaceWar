using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 가장 가까운 적을 추적하고,
/// 적에게 명중하면 주변 적에게 범위 피해를 주는 미사일.
/// </summary>
public class Missile : Projectile
{
    [Header("미사일 추적 설정")]
    [Tooltip("미사일이 초당 회전할 수 있는 각도")]
    [Min(0f)]
    [SerializeField] float _rotationSpeed = 360f;

    [Tooltip("미사일이 자동으로 적을 찾는 최대 거리")]
    [Min(0f)]
    [SerializeField] float _targetSearchRadius = 15f;

    [Header("미사일 폭발 설정")]
    [Tooltip("미사일이 명중했을 때 피해를 주는 폭발 범위")]
    [Min(0f)]
    [SerializeField] float _explosionRadius = 1.5f;

    [Tooltip("Enemy 레이어만 선택한다.")]
    [SerializeField] LayerMask _enemyLayerMask;
    // 적에게 명중했을 때 생성할 독립적인 폭발 이펙트 프리팹이다.
    [SerializeField] SpriteExplosionEffect _explosionEffectPrefab;

    // 현재 미사일이 추적하고 있는 대상의 Transform
    Transform _target;

    /// <summary>
    /// Weapon에서 공격력과 발사 방향을 받은 직후 실행된다.
    /// 미사일이 처음 추적할 적을 찾는다.
    /// </summary>
    protected override void OnInitialized()
    {
        FindNearestEnemy();
    }

    /// <summary>
    /// Projectile의 FixedUpdate에서 계속 호출된다.
    /// 적을 향해 방향을 회전시키고 미사일을 이동시킨다.
    /// </summary>
    protected override void MoveProjectile()
    {
        // 추적 중인 적이 죽거나 사라졌다면 새로운 적을 찾는다.
        if (_target == null)
        {
            FindNearestEnemy();
        }

        // 추적할 적이 있다면 해당 적을 향해 방향을 회전한다.
        if (_target != null)
        {
            RotateTowardsTarget();
        }

        // 현재 진행 방향으로 미사일을 이동시킨다.
        Body.linearVelocity = Direction * MoveSpeed;
    }

    /// <summary>
    /// 탐색 범위 안에서 가장 가까운 IDamageable 대상을 찾는다.
    /// 일반 적과 보스를 모두 탐색한다.
    /// </summary>
    private void FindNearestEnemy()
    {
        // 아직 추적 대상이 없다는 뜻으로 null을 넣는다.
        _target = null;

        // 탐색 범위 안에 있는 Enemy 레이어의 Collider를 가져온다.
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(
            transform.position,
            _targetSearchRadius,
            _enemyLayerMask);

        // 하나의 적이 여러 Collider를 가졌을 때 중복 검사하지 않는다.
        HashSet<IDamageable> checkedTargets =
            new HashSet<IDamageable>();

        float nearestDistanceSqr =
            _targetSearchRadius * _targetSearchRadius;

        foreach (Collider2D hitCollider in hitColliders)
        {
            // Collider 또는 부모에서 피해를 받을 대상을 찾는다.
            IDamageable target =
                hitCollider.GetComponentInParent<IDamageable>();

            if (target == null || target.IsDead)
            {
                continue;
            }

            // 같은 대상의 다른 Collider라면 다시 검사하지 않는다.
            if (!checkedTargets.Add(target))
            {
                continue;
            }

            Transform targetTransform = target.TargetTransform;

            if (targetTransform == null)
            {
                continue;
            }

            Vector2 offset =
                targetTransform.position - transform.position;

            float distanceSqr = offset.sqrMagnitude;

            if (distanceSqr > nearestDistanceSqr)
            {
                continue;
            }

            nearestDistanceSqr = distanceSqr;
            _target = targetTransform;
        }
    }

    /// <summary>
    /// 미사일의 진행 방향을 추적 대상 쪽으로 서서히 회전시킨다.
    /// </summary>
    private void RotateTowardsTarget()
    {
        // 현재 위치에서 추적 대상까지의 방향을 구한다.
        Vector2 targetDirection =
            (Vector2)(_target.position - transform.position);

        // 적과 미사일의 위치가 완전히 같다면 회전하지 않는다.
        if (targetDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        // 방향 벡터의 길이를 1로 만들어 방향 정보만 사용한다.
        targetDirection.Normalize();

        // 현재 진행 방향을 각도로 변환한다.
        float currentAngle =
            Mathf.Atan2(Direction.y, Direction.x) * Mathf.Rad2Deg;

        // 적이 있는 방향을 각도로 변환한다.
        float targetAngle =
            Mathf.Atan2(targetDirection.y, targetDirection.x) *
            Mathf.Rad2Deg;

        // 한 물리 프레임 동안 회전할 수 있는 최대 각도를 계산한다.
        float maxRotation =
            _rotationSpeed * Time.fixedDeltaTime;

        // 적 방향을 향해 제한된 속도로 각도를 변경한다.
        float nextAngle = Mathf.MoveTowardsAngle(
            currentAngle,
            targetAngle,
            maxRotation);

        // 계산한 각도를 다시 방향 벡터로 변환한다.
        float nextAngleRadians = nextAngle * Mathf.Deg2Rad;

        Direction = new Vector2(
            Mathf.Cos(nextAngleRadians),
            Mathf.Sin(nextAngleRadians));

        // 미사일 이미지도 실제 진행 방향을 바라보게 회전한다.
        Body.SetRotation(nextAngle);
    }

    /// <summary>
    /// 미사일이 대상에게 명중하면 주변의 일반 적과 보스에게 범위 피해를 준다.
    /// </summary>
    protected override void HandleTargetHit(IDamageable hitTarget)
    {
        Transform hitTransform = hitTarget.TargetTransform;

        if (hitTransform == null)
        {
            return;
        }

        Vector2 explosionPosition = hitTransform.position;

        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(
            explosionPosition,
            _explosionRadius,
            _enemyLayerMask);

        // 하나의 대상이 여러 Collider를 가져도 피해는 한 번만 적용한다.
        HashSet<IDamageable> damagedTargets =
            new HashSet<IDamageable>();

        foreach (Collider2D hitCollider in hitColliders)
        {
            IDamageable target =
                hitCollider.GetComponentInParent<IDamageable>();

            if (target == null || target.IsDead)
            {
                continue;
            }

            if (!damagedTargets.Add(target))
            {
                continue;
            }

            target.TakeHit(Damage);
        }

        // 미사일 오브젝트와 별개로 폭발 이펙트를 생성한다.
        // 미사일이 바로 제거되어도 폭발 연출은 끝까지 재생된다.
        SpriteExplosionEffect explosionEffect =
            Instantiate(
                _explosionEffectPrefab,
                explosionPosition,
                Quaternion.identity);

        explosionEffect.Play();
    }

    /// <summary>
    /// Scene 뷰에서 탐색 범위와 폭발 범위를 확인할 수 있게 표시한다.
    /// 게임 실행 결과에는 나타나지 않는다.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // 노란색 원은 적 탐색 범위다.
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(
            transform.position,
            _targetSearchRadius);

        // 빨간색 원은 폭발 피해 범위다.
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(
            transform.position,
            _explosionRadius);
    }
}
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

    // 현재 미사일이 추적하고 있는 적을 저장한다.
    Enemy _target;

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
    /// 현재 미사일 주변에서 가장 가까운 Enemy를 찾는다.
    /// FastEnemy, BossEnemy 등 Enemy를 상속받은 적을 모두 찾을 수 있다.
    /// </summary>
    private void FindNearestEnemy()
    {
        // 현재 씬에 활성화되어 있는 모든 Enemy를 가져온다.
        Enemy[] enemies =
            FindObjectsByType<Enemy>(FindObjectsSortMode.None);

        // 아직 추적 대상이 없다는 뜻으로 null을 넣는다.
        _target = null;

        // 탐색 반경의 제곱값을 저장한다.
        // 거리 비교에서 제곱근 계산을 피하기 위해 sqrMagnitude와 비교한다.
        float nearestDistanceSqr =
            _targetSearchRadius * _targetSearchRadius;

        // 발견한 모든 적을 확인한다.
        foreach (Enemy enemy in enemies)
        {
            // 안전을 위해 사라진 적은 건너뛴다.
            if (enemy == null)
            {
                continue;
            }

            // 현재 미사일에서 적까지 향하는 벡터를 구한다.
            Vector2 offset =
                enemy.transform.position - transform.position;

            // 현재 미사일과 적 사이 거리의 제곱값을 구한다.
            float distanceSqr = offset.sqrMagnitude;

            // 현재까지 발견한 적보다 멀다면 선택하지 않는다.
            if (distanceSqr > nearestDistanceSqr)
            {
                continue;
            }

            // 더 가까운 적을 새로운 추적 대상으로 저장한다.
            nearestDistanceSqr = distanceSqr;
            _target = enemy;
        }
    }

    /// <summary>
    /// 미사일의 진행 방향을 추적 대상 쪽으로 서서히 회전시킨다.
    /// </summary>
    private void RotateTowardsTarget()
    {
        // 현재 위치에서 추적 대상까지의 방향을 구한다.
        Vector2 targetDirection =
            (Vector2)(_target.transform.position - transform.position);

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
    /// 미사일이 적에게 명중했을 때 호출된다.
    /// 명중 지점을 중심으로 폭발 범위 안의 Enemy에게 피해를 준다.
    /// </summary>
    protected override void HandleEnemyHit(Enemy hitEnemy)
    {
        // 처음 충돌한 적의 위치를 폭발 중심으로 사용한다.
        Vector2 explosionPosition = hitEnemy.transform.position;

        // 폭발 범위 안에 있으면서 Enemy 레이어인 Collider를 가져온다.
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(
            explosionPosition,
            _explosionRadius,
            _enemyLayerMask);

        // 하나의 적에게 여러 Collider가 있을 경우
        // 피해가 여러 번 들어가지 않도록 처리한 적을 저장한다.
        HashSet<Enemy> damagedEnemies = new HashSet<Enemy>();

        // 폭발 범위 안의 모든 Collider를 확인한다.
        foreach (Collider2D hitCollider in hitColliders)
        {
            // Collider 또는 부모 오브젝트에서 Enemy를 찾는다.
            Enemy enemy =
                hitCollider.GetComponentInParent<Enemy>();

            // Enemy가 아니면 피해를 처리하지 않는다.
            if (enemy == null)
            {
                continue;
            }

            // 이미 피해를 준 적이라면 중복 피해를 주지 않는다.
            if (!damagedEnemies.Add(enemy))
            {
                continue;
            }

            // Weapon에서 전달받은 현재 공격력만큼 피해를 준다.
            enemy.TakeHit(Damage);
        }
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
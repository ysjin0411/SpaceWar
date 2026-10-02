using System;
using UnityEngine;

public sealed class TowerBulletAttack : TowerAttack
{
    // 기존 EnemyBullet 컴포넌트가 붙은 총알 프리팹이다.
    [SerializeField] private EnemyBullet _bulletPrefab;

    // 이 타워의 총알 생성 위치다.
    [SerializeField] private Transform _firePoint;

    // 한 번의 발사 이벤트에서 생성할 총알 수다.
    [SerializeField, Min(1)] private int _bulletCount = 3;

    // 가장 왼쪽 총알부터 오른쪽 총알까지의 전체 각도다.
    [SerializeField, Range(0f, 180f)] private float _spreadAngle = 30f;

    // 생성한 총알에 전달할 이동 속도다.
    [SerializeField, Min(0.1f)] private float _bulletSpeed = 5f;

    // 생성한 총알에 전달할 피해량이다.
    [SerializeField, Min(1)] private float _bulletDamage = 5;

    /// <summary>
    /// 컴포넌트 참조와 초기 실행 상태를 준비한다.
    /// </summary>
    protected override void Awake()
    {
        base.Awake();

        // 발사에 반드시 필요한 참조를 검사한다.
        if (_bulletPrefab == null || _firePoint == null)
        {
            throw new InvalidOperationException(
                $"{name}: 총알 프리팹과 FirePoint를 연결하세요.");
        }
    }

    /// <summary>
    /// ExecuteAttack 동작에 필요한 상태와 처리 흐름을 실행한다.
    /// </summary>
    protected override void ExecuteAttack(PlayerController targetPlayer)
    {
        // 발사 순간의 플레이어 위치를 기준으로 조준한다.
        Vector2 direction =
            targetPlayer.transform.position - _firePoint.position;

        // 발사점과 플레이어가 겹치면 발사점의 왼쪽 방향을 사용한다.
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = -_firePoint.right;
        }

        direction.Normalize();

        for (int i = 0; i < _bulletCount; i++)
        {
            // 한 발이면 정면, 여러 발이면 전체 각도에 균등하게 배치한다.
            float angle = _bulletCount == 1
                ? 0f
                : -_spreadAngle * 0.5f
                  + _spreadAngle * i / (_bulletCount - 1);

            Vector2 shotDirection =
                Quaternion.Euler(0f, 0f, angle) * direction;

            // 포탑의 탄환 프리팹에 해당하는 풀에서 오브젝트를 꺼낸다.
            GameObject bulletObject = ProjectilePoolManager.Instance.Spawn(
                _bulletPrefab.gameObject,
                _firePoint.position,
                Quaternion.identity);

            // 탄환의 방향과 공격 정보를 설정할 컴포넌트를 가져온다.
            EnemyBullet bullet = bulletObject.GetComponent<EnemyBullet>();

            // 기존 EnemyBullet의 방향 지정 초기화를 그대로 사용한다.
            bullet.Init(
                shotDirection,
                _bulletSpeed,
                _bulletDamage);
        }
    }
}
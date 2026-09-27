using System;
using System.Collections;
using UnityEngine;

[CreateAssetMenu(
    fileName = "BossCruiserBurstShotPattern",
    menuName = "BossPattern/Cruiser/Burst Shot")]
public sealed class BossCruiserBurstShotPattern : BossPattern
{
    // 입에서 발사할 EnemyBigBullet 프리팹이다.
    [SerializeField]
    private EnemyBullet _bulletPrefab;

    // 패턴을 한 번 실행할 때 발사하는 총 탄 수다.
    [SerializeField, Min(1)]
    private int _shotCount = 3;

    // 한 발을 발사한 뒤 다음 발까지 기다리는 시간이다.
    [SerializeField, Min(0.01f)]
    private float _shotInterval = 0.2f;

    // 발사된 탄이 초당 이동하는 거리다.
    [SerializeField, Min(0.01f)]
    private float _bulletSpeed = 5f;

    // 탄이 플레이어나 방패에 전달하는 피해량이다.
    [SerializeField, Min(0)]
    private float _bulletDamage = 20;

    // 일반 패턴 실행기가 호출하는 연속 발사 동작이다.
    /// <summary>
    /// 일반 패턴 실행기가 호출하는 연속 발사 동작이다.
    /// </summary>
    public override IEnumerator Execute(BossPatternContext context)
    {
        // 모든 보스가 상속하는 공통 타입으로 실행 주체를 가져온다.
        BossEnemy boss = context.Boss;

        // 잘못된 설정으로 탄이 생성되기 전에 확인한다.
        if (_bulletPrefab == null ||
            _shotCount <= 0 ||
            !(_shotInterval > 0f) ||
            float.IsInfinity(_shotInterval) ||
            !(_bulletSpeed > 0f) ||
            float.IsInfinity(_bulletSpeed) ||
            _bulletDamage < 0)
        {
            throw new InvalidOperationException(
                $"{name}: 탄 프리팹, 발사 수, 간격, 속도, 피해량을 확인하세요.");
        }

        // 발사 사이에 재사용할 대기 객체다.
        // 게임 시간 기준이므로 일시정지하면 대기도 멈춘다.
        WaitForSeconds shotWait = new WaitForSeconds(_shotInterval);

        for (int i = 0; i < _shotCount; i++)
        {
            // 보스가 살아 있고 활성화된 동안에만 다음 탄을 발사한다.
            if (!CanFire(boss) || context.TargetPlayer == null)
            {
                yield break;
            }

            // 해당 보스에 연결된 발사 위치를 사용한다.
            Transform firePoint = boss.FirePoint;

            // 발사 위치가 없어졌거나 비활성화됐다면 발사를 종료한다.
            if (firePoint == null || !firePoint.gameObject.activeInHierarchy)
            {
                yield break;
            }

            // 보스가 움직일 수 있으므로 매번 현재 입 위치를 가져온다.
            Vector3 spawnPosition = firePoint.position;

            // 이번 탄은 발사 순간의 플레이어 위치를 향한다.
            Vector2 direction =
                (Vector2)context.TargetPlayer.transform.position -
                (Vector2)spawnPosition;

            // 플레이어와 발사 위치가 같으면 기본 방향인 왼쪽으로 발사한다.
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector2.left;
            }

            // 부모 없이 생성하여 발사 후 보스의 이동에 끌려가지 않게 한다.
            EnemyBullet bullet = Instantiate(
                _bulletPrefab,
                spawnPosition,
                Quaternion.identity);

            // 기존 EnemyBullet에 방향, 속도, 피해량을 전달한다.
            // 이동과 충돌 처리는 EnemyBullet이 담당한다.
            bullet.Init(direction, _bulletSpeed, _bulletDamage);

            // 마지막 탄까지 발사했다면 추가 대기 없이 패턴을 끝낸다.
            if (i < _shotCount - 1)
            {
                yield return shotWait;
            }
        }
    }

    // 보스 종류와 관계없이 생존 상태와 활성화 상태를 확인한다.
    // 일반 패턴의 사용 시점은 각 보스의 CanUseNormalPatterns가 결정한다.
    /// <summary>
    /// 일반 패턴의 사용 시점은 각 보스의 CanUseNormalPatterns가 결정한다.
    /// </summary>
    private static bool CanFire(BossEnemy boss)
    {
        return boss != null &&
               boss.isActiveAndEnabled &&
               !boss.IsDead;
    }
}
using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Boss Dog의 회전 난사 특수 패턴이다.
/// 현재 단계에서는 시스템 경고 후 스폰 위치로 이동한다.
/// </summary>
[CreateAssetMenu(
    fileName = "BossDogSpiralShotPattern",
    menuName = "BossPattern/Dog/Spiral Shot")]
public sealed class BossDogSpiralShotPattern : BossPattern
{
    [Header("시스템 경고")]

    // 이 패턴을 예고하는 시스템 메시지다.
    [TextArea]
    [SerializeField]
    string _systemMessage;

    [Header("스폰 위치 이동")]

    // 경고가 끝난 뒤 스폰 위치로 이동하는 데 걸리는 시간이다.
    [Min(0.01f)]
    [SerializeField]
    float _moveToSpawnDuration = 0.4f;

    [Header("난사 총알 설정")]

    // 기존 EnemyBullet 프리팹을 연결한다.
    [SerializeField]
    EnemyBullet _bulletPrefab;

    // 이 패턴에서 발사하는 총알의 이동 속도다.
    [Min(0.01f)]
    [SerializeField]
    float _bulletSpeed = 5f;

    // 이 패턴에서 발사하는 총알 한 발의 피해량이다.
    [Min(0)]
    [SerializeField]
    float _bulletDamage = 5;

    [Header("회전 난사")]

    // 회전하며 총알을 발사하는 총 시간이다.
    [Min(0.01f)]
    [SerializeField]
    float _shootDuration = 5f;

    // 초당 회전 각도다. 음수로 설정하면 반대로 회전한다.
    [SerializeField]
    float _rotationSpeed = 180f;

    // 한 묶음의 총알을 발사하는 시간 간격이다.
    [Min(0.01f)]
    [SerializeField]
    float _fireInterval = 0.1f;

    // 한 번에 균등한 각도로 발사할 총알 수다.
    // 6이면 60도 간격으로 6발을 발사한다.
    [Min(1)]
    [SerializeField]
    int _bulletsPerShot = 6;

    /// <summary>
    /// 경고 완료를 기다린 뒤 보스를 스폰 위치로 이동시킨다.
    /// </summary>
    public override IEnumerator Execute(BossPatternContext context)
    {
        // 실행에 필요한 정보가 전달됐는지 검사한다.
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        // 다른 보스에 잘못 장착하는 것을 방지한다.
        if (!(context.Boss is Boss_Dog))
        {
            throw new InvalidOperationException(
                $"{name} 패턴은 Boss_Dog만 사용할 수 있습니다.");
        }

        // 경고 완료 여부는 이번 실행의 지역 변수로 관리한다.
        // 공유 Config에 보스별 실행 상태를 저장하지 않는다.
        bool isDialogueCompleted = false;

        Action handleDialogueCompleted =
            () => isDialogueCompleted = true;

        // 경고를 요청하기 전에 완료 이벤트를 구독한다.
        context.Boss.SystemDialogueCompleted +=
            handleDialogueCompleted;

        try
        {
            // 기존 시스템 UI에 이 패턴의 경고 메시지를 전달한다.
            context.Boss.RequestSystemDialogue(_systemMessage);

            // 경고가 끝나거나 보스가 사망할 때까지 기다린다.
            yield return new WaitUntil(() =>
                isDialogueCompleted || context.Boss.IsDead);
        }
        finally
        {
            // 대기 구간을 빠져나갈 때 완료 이벤트 구독을 해제한다.
            context.Boss.SystemDialogueCompleted -=
                handleDialogueCompleted;
        }

        // 사망했다면 이후 행동은 실행하지 않는다.
        if (context.Boss.IsDead)
        {
            yield break;
        }

        // 보스의 최초 스폰 위치로 이동한다.
        yield return MoveToSpawnPosition(context);

        if (context.Boss.IsDead)
        {
            yield break;
        }

        // 스폰 위치에서 회전 난사가 끝날 때까지 기다린다.
        // 완료되면 Execute가 끝나고 Runner가 다음 행동을 진행한다.
        yield return RotateAndShoot(context);
    }

    /// <summary>
    /// 일정 시간 동안 보스 외형을 회전시키면서 총알을 발사한다.
    /// </summary>
    private IEnumerator RotateAndShoot(BossPatternContext context)
    {
        // 잘못된 설정으로 무한 반복하거나 발사에 실패하지 않도록 검사한다.
        if (_bulletPrefab == null)
        {
            throw new InvalidOperationException(
                $"{name}에 Bullet Prefab이 연결되지 않았습니다.");
        }

        if (_shootDuration <= 0f ||
            _fireInterval <= 0f ||
            _bulletsPerShot < 1)
        {
            throw new InvalidOperationException(
                $"{name}의 난사 시간, 발사 간격, 총알 개수를 확인하세요.");
        }

        // Rigidbody가 있는 루트 대신 외형만 회전시킨다.
        Transform visual = context.VisualTransform;

        // 패턴이 끝났을 때 복원할 회전을 저장한다.
        Quaternion originalRotation = visual.localRotation;

        // 총알 방향은 월드 좌표를 사용하므로 시작 월드 각도를 저장한다.
        float startWorldAngle = visual.eulerAngles.z;

        // 실행 상태는 Config 필드가 아닌 지역 변수로 관리한다.
        float elapsedTime = 0f;
        float nextFireTime = 0f;

        try
        {
            while (elapsedTime < _shootDuration)
            {
                // 사망하면 난사를 종료한다.
                if (context.Boss.IsDead)
                {
                    yield break;
                }

                // 경과 시간에 맞춰 외형을 Z축으로 회전시킨다.
                float rotationAngle = _rotationSpeed * elapsedTime;

                visual.localRotation =
                    originalRotation *
                    Quaternion.Euler(0f, 0f, rotationAngle);

                // 이번 시점까지 발사해야 할 총알 묶음을 생성한다.
                // 각 묶음은 예정된 발사 시각에 해당하는 각도를 사용한다.
                while (nextFireTime <= elapsedTime &&
                       nextFireTime < _shootDuration)
                {
                    float fireAngle =
                        startWorldAngle +
                        _rotationSpeed * nextFireTime;

                    FireBulletGroup(context, fireAngle);

                    nextFireTime += _fireInterval;
                }

                // 다음 물리 업데이트까지 양보한다.
                yield return new WaitForFixedUpdate();
                elapsedTime += Time.fixedDeltaTime;
            }
        }
        finally
        {
            // 이 실행 구간을 빠져나갈 때 외형의 원래 회전을 복원한다.
            if (visual != null)
            {
                visual.localRotation = originalRotation;
            }
        }
    }

    /// <summary>
    /// 보스 위치에서 균등한 각도 간격으로 총알 한 묶음을 발사한다.
    /// </summary>
    private void FireBulletGroup(
        BossPatternContext context,
        float baseAngle)
    {
        // 원 한 바퀴를 총알 수로 나눠 발사 간격을 구한다.
        float angleStep = 360f / _bulletsPerShot;

        Vector3 spawnPosition = context.Boss.transform.position;

        for (int i = 0; i < _bulletsPerShot; i++)
        {
            // 이번 총알의 월드 발사 각도를 계산한다.
            float angle = baseAngle + angleStep * i;
            float radians = angle * Mathf.Deg2Rad;

            // 각도를 길이가 1인 방향 벡터로 변환한다.
            Vector2 direction = new Vector2(
                Mathf.Cos(radians),
                Mathf.Sin(radians));

            // 총알은 보스의 자식으로 만들지 않는다.
            // 발사 후 보스가 회전해도 총알은 독립적으로 직진한다.
            EnemyBullet bullet = Instantiate(
                _bulletPrefab,
                spawnPosition,
                Quaternion.identity);

            // 이 Config의 속도와 피해량을 생성된 총알에 전달한다.
            bullet.Init(direction, _bulletSpeed, _bulletDamage);
        }
    }

    /// <summary>
    /// Rigidbody2D를 이용해 최초 스폰 위치로 이동한다.
    /// </summary>
    private IEnumerator MoveToSpawnPosition(BossPatternContext context)
    {
        // 시작 위치와 목표 위치를 이번 이동 동안 고정한다.
        Vector2 startPosition = context.Rigidbody.position;
        Vector2 targetPosition = context.Boss.MovementCenter;

        float elapsedTime = 0f;

        while (elapsedTime < _moveToSpawnDuration)
        {
            // 이동 도중 사망했다면 더 이상 위치를 변경하지 않는다.
            if (context.Boss.IsDead)
            {
                yield break;
            }

            elapsedTime += Time.fixedDeltaTime;

            // 이동 진행률을 0~1 범위로 계산한다.
            float moveRatio = Mathf.Clamp01(
                elapsedTime / _moveToSpawnDuration);

            // 물리 업데이트마다 목표를 향해 조금씩 이동한다.
            context.Rigidbody.MovePosition(
                Vector2.Lerp(
                    startPosition,
                    targetPosition,
                    moveRatio));

            yield return new WaitForFixedUpdate();
        }
    }
}
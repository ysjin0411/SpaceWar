using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 지정 위치로 이동한 뒤 수직으로 내려오면서 왼쪽으로 발사한다.
/// 여러 보스가 공통으로 사용할 수 있는 일반 패턴이다.
/// </summary>
[CreateAssetMenu(
    fileName = "BossVerticalShotPattern",
    menuName = "BossPattern/Vertical Shot")]
public sealed class BossVerticalShotPattern : BossPattern
{
    [Header("시작 위치 이동")]

    // 하강을 시작할 월드 좌표다. 스폰 위치 기준 Offset이 아니다.
    [SerializeField]
    Vector2 _startPosition = new Vector2(5f, 3f);

    // 시작 위치로 이동하는 속도다.
    [Min(0.01f)]
    [SerializeField]
    float _positioningSpeed = 10f;

    // 이동 사격을 끝낼 월드 좌표다.
    [Header("도착 위치")]
    [SerializeField]
    Vector2 _endPosition = new Vector2();

    // 시작 위치에서 도착 위치까지 사격하며 이동하는 속도다.
    [Min(0.01f)]
    [SerializeField]
    float _shootMoveSpeed = 2f;

    [Header("총알 발사")]

    // 기존 EnemyBullet 프리팹을 연결한다.
    [SerializeField]
    EnemyBullet _bulletPrefab;

    // 하강 시작 시 첫 발을 쏜 뒤, 이 간격으로 반복 발사한다.
    [Min(0.01f)]
    [SerializeField]
    float _fireInterval = 0.2f;

    // 이 패턴에서 생성하는 총알의 이동 속도다.
    [Min(0.01f)]
    [SerializeField]
    float _bulletSpeed = 5f;

    // 이 패턴에서 생성하는 총알 한 발의 피해량이다.
    [Min(0)]
    [SerializeField]
    float _bulletDamage = 5;

    /// <summary>
    /// 시작 위치 이동이 끝나면 하강과 발사를 함께 실행한다.
    /// </summary>
    public override IEnumerator Execute(BossPatternContext context)
    {
        // 잘못된 실행 정보나 Config 설정을 검사한다.
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (_bulletPrefab == null)
        {
            throw new InvalidOperationException(
                $"{name}에 Bullet Prefab이 연결되지 않았습니다.");
        }

        if (_positioningSpeed <= 0f ||
            _shootMoveSpeed <= 0f ||
            _fireInterval <= 0f ||
            _bulletSpeed <= 0f ||
            _bulletDamage < 0)
        {
            throw new InvalidOperationException(
                $"{name}의 속도, 발사 간격, 피해량을 확인하세요.");
        }

        // 시작 위치로 이동하는 동안은 발사하지 않는다.
        yield return MoveToStart(context);

        if (context.Boss.IsDead)
        {
            yield break;
        }

        // 도착 위치까지 이동하며 발사하고, 완료될 때까지 기다린다.
        yield return MoveAndShoot(context);

        // Execute가 끝나면 Runner가 다음 행동을 진행한다.
    }

    /// <summary>
    /// 보스를 Config에 설정된 시작 좌표로 이동시킨다.
    /// </summary>
    private IEnumerator MoveToStart(BossPatternContext context)
    {
        while (true)
        {
            // 사망 시 이동을 종료한다.
            if (context.Boss.IsDead)
            {
                yield break;
            }

            Vector2 currentPosition = context.Rigidbody.position;

            // 시작 위치에 충분히 가까워지면 마지막 위치를 맞춘다.
            if ((currentPosition - _startPosition).sqrMagnitude
                <= 0.0001f)
            {
                context.Rigidbody.MovePosition(_startPosition);

                // 마지막 이동이 물리에 반영된 뒤 다음 단계로 넘어간다.
                yield return new WaitForFixedUpdate();
                yield break;
            }

            // 일정 속도로 시작 위치를 향해 이동한다.
            Vector2 nextPosition = Vector2.MoveTowards(
                currentPosition,
                _startPosition,
                _positioningSpeed * Time.fixedDeltaTime);

            context.Rigidbody.MovePosition(nextPosition);

            yield return new WaitForFixedUpdate();
        }
    }

    /// <summary>
    /// 도착 좌표를 향해 직선으로 이동하면서 일정 간격으로 발사한다.
    /// </summary>
    private IEnumerator MoveAndShoot(BossPatternContext context)
    {
        // 이동 사격을 시작할 때 첫 발을 발사한다.
        float fireTimer = 0f;

        while (true)
        {
            // 사망했다면 이동과 발사를 종료한다.
            if (context.Boss.IsDead)
            {
                yield break;
            }

            Vector2 currentPosition = context.Rigidbody.position;

            // 도착 위치에 가까워지면 위치를 정확히 맞추고 종료한다.
            // 시작과 도착이 같을 때도 발사 없이 종료한다.
            if ((currentPosition - _endPosition).sqrMagnitude
                <= 0.0001f)
            {
                context.Rigidbody.MovePosition(_endPosition);

                // 마지막 위치가 물리에 반영된 뒤 패턴을 끝낸다.
                yield return new WaitForFixedUpdate();
                yield break;
            }

            // 발사 간격마다 왼쪽으로 총알을 생성한다.
            while (fireTimer <= 0f)
            {
                FireBullet(context);
                fireTimer += _fireInterval;
            }

            // X와 Y를 함께 계산하여 도착 위치로 직선 이동한다.
            Vector2 nextPosition = Vector2.MoveTowards(
                currentPosition,
                _endPosition,
                _shootMoveSpeed * Time.fixedDeltaTime);

            context.Rigidbody.MovePosition(nextPosition);

            yield return new WaitForFixedUpdate();

            // 물리 업데이트 시간만큼 발사 타이머를 줄인다.
            fireTimer -= Time.fixedDeltaTime;
        }
    }

    /// <summary>
    /// 현재 보스 위치에서 왼쪽으로 직진하는 총알을 생성한다.
    /// </summary>
    private void FireBullet(BossPatternContext context)
    {
        // Rigidbody의 현재 위치를 기준으로 발사한다.
        Vector3 spawnPosition = new Vector3(
            context.Rigidbody.position.x,
            context.Rigidbody.position.y,
            context.Boss.transform.position.z);

        // 현재 보스 위치에 발사할 탄환을 풀에서 꺼낸다.
        GameObject bulletObject = ProjectilePoolManager.Instance.Spawn(
            _bulletPrefab.gameObject,
            spawnPosition,
            Quaternion.identity);

        // 발사 정보를 설정할 탄환 컴포넌트를 가져온다.
        EnemyBullet bullet = bulletObject.GetComponent<EnemyBullet>();

        // 플레이어 위치와 관계없이 왼쪽으로 발사한다.
        // 총알 속도와 피해량은 이 Config의 값을 사용한다.
        bullet.Init(Vector2.left, _bulletSpeed, _bulletDamage);
    }
}
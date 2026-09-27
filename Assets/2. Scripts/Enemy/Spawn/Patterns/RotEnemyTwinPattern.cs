using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 생성된 RotEnemy에게 전달하는 Twin 패턴 설정이다.
/// </summary>
[Serializable]
public struct RotEnemyTwinSettings
{
    // 첫 도착 위치까지 수직 이동하는 속도다.
    [Min(0.01f)] public float EnterSpeed;

    // 첫 도착 위치에서 오른쪽 원으로 후퇴하는 속도다.
    [Min(0.01f)] public float RetreatSpeed;

    // 잔류 원의 월드 중심 좌표다.
    public Vector2 StayCenter;

    // 잔류 원의 반지름이다.
    [Min(0.1f)] public float StayRadius;

    // 잔류 중 다음 목적지를 고르는 최대 거리다.
    [Min(0.1f)] public float WanderStep;

    // 잔류 이동의 최대 속도다.
    [Min(0.01f)] public float WanderSpeed;

    // 잔류 이동의 속도와 방향을 부드럽게 변경하는 시간이다.
    [Min(0.01f)] public float WanderSmoothTime;

    // 첫 공격 시작부터 퇴장까지의 시간이다. 후퇴 시간도 포함한다.
    [Min(0.1f)] public float AttackDuration;

    // 공격 종료 후 오른쪽으로 퇴장하는 속도다.
    [Min(0.01f)] public float ExitSpeed;

    // 적 전체가 화면 밖으로 나간 뒤 제거할 X 좌표다.
    public float ExitX;

    // 첫 도착 위치에서 부채꼴의 중앙 방향을 정할 월드 좌표다.
    public Vector2 AimPoint;

    // 부채꼴의 전체 각도다.
    [Range(1f, 180f)] public float SectorAngle;

    // 첫 도착 위치를 꼭짓점으로 하는 부채꼴의 반지름이다.
    [Min(0.01f)] public float SectorRadius;

    // 이번 패턴에서 사용할 총알 프리팹이다.
    public EnemyBullet BulletPrefab;

    // 한 번에 동시에 발사할 총알 수다.
    [Min(1)] public int BulletsPerShot;

    // 공격 애니메이션을 시작하는 간격이다.
    [Min(0.01f)] public float ShotInterval;

    // 총알의 이동 속도다.
    [Min(0.01f)] public float BulletSpeed;

    // 총알 하나의 피해량이다.
    [Min(0)] public float BulletDamage;
}

/// <summary>
/// 위·아래 RotEnemy 부대를 생성하고 행동 설정을 전달한다.
/// </summary>
[CreateAssetMenu(
    fileName = "RotEnemyTwinPattern",
    menuName = "EnemyPattern/RotEnemy Twin")]
public sealed class RotEnemyTwinPattern : EnemySpawnPattern
{
    // 생성할 Normal_rot 프리팹이다.
    [SerializeField] RotEnemy _enemyPrefab;

    // 각 부대에서 다음 적이 생성되기까지의 간격이다.
    [Min(0.01f)]
    [SerializeField] float _spawnInterval = 0.2f;

    [Header("위쪽 부대")]

    // 위쪽 부대의 적 수다.
    [Min(1)]
    [SerializeField] int _upperCount = 5;

    // 위쪽 부대의 첫 생성 좌표다.
    [SerializeField] Vector2 _upperStart = new Vector2(1f, 6f);

    // 위쪽 부대의 개체별 생성 좌표 간격이다.
    [SerializeField] Vector2 _upperOffset = new Vector2(0.5f, 0.5f);

    // 위쪽 부대가 내려와 처음 도착할 Y 좌표다.
    [SerializeField] float _upperEndY = 1.5f;

    [Header("아래쪽 부대")]

    // 아래쪽 부대의 적 수다.
    [Min(1)]
    [SerializeField] int _lowerCount = 5;

    // 아래쪽 부대의 첫 생성 좌표다.
    [SerializeField] Vector2 _lowerStart = new Vector2(1f, -6f);

    // 아래쪽 부대의 개체별 생성 좌표 간격이다.
    [SerializeField] Vector2 _lowerOffset = new Vector2(0.5f, -0.5f);

    // 아래쪽 부대가 올라와 처음 도착할 Y 좌표다.
    [SerializeField] float _lowerEndY = -1.5f;

    [Header("공통 행동 설정")]

    // 생성된 각 적에게 복사해서 전달할 설정이다.
    [SerializeField]
    RotEnemyTwinSettings _settings =
        new RotEnemyTwinSettings
        {
            EnterSpeed = 3f,
            RetreatSpeed = 2f,
            StayCenter = new Vector2(6f, 0f),
            StayRadius = 1f,
            WanderStep = 0.5f,
            WanderSpeed = 0.8f,
            WanderSmoothTime = 0.25f,
            AttackDuration = 8f,
            ExitSpeed = 4f,
            ExitX = 11f,
            AimPoint = new Vector2(-6f, 0f),
            SectorAngle = 60f,
            SectorRadius = 5f,
            BulletsPerShot = 3,
            ShotInterval = 0.6f,
            BulletSpeed = 5f,
            BulletDamage = 10
        };

    /// <summary>
    /// 설정을 검사하고 두 부대의 순차 생성을 시작한다.
    /// </summary>
    public override void Execute(EnemySpawnContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        ValidateSettings();
        context.RunSequence(SpawnSequence(context));
    }

    /// <summary>
    /// 필수 참조와 이동·공격 수치를 검사한다.
    /// </summary>
    private void ValidateSettings()
    {
        if (_enemyPrefab == null || _settings.BulletPrefab == null)
        {
            throw new InvalidOperationException(
                $"{name}: 적 프리팹과 총알 프리팹을 연결하세요.");
        }

        if (_spawnInterval <= 0f ||
            _upperCount < 1 || _lowerCount < 1 ||
            _settings.EnterSpeed <= 0f ||
            _settings.RetreatSpeed <= 0f ||
            _settings.StayRadius <= 0f ||
            _settings.WanderStep <= 0f ||
            _settings.WanderSpeed <= 0f ||
            _settings.WanderSmoothTime <= 0f ||
            _settings.AttackDuration <= 0f ||
            _settings.ExitSpeed <= 0f ||
            _settings.SectorRadius <= 0f ||
            _settings.ShotInterval <= 0f ||
            _settings.BulletSpeed <= 0f ||
            _settings.BulletsPerShot < 1 ||
            _settings.BulletDamage < 0 ||
            _settings.SectorAngle < 1f ||
            _settings.SectorAngle > 180f)
        {
            throw new InvalidOperationException(
                $"{name}: 패턴 설정의 수치 범위를 확인하세요.");
        }

        if (_settings.ExitX <=
            _settings.StayCenter.x + _settings.StayRadius)
        {
            throw new InvalidOperationException(
                $"{name}: ExitX는 잔류 원의 오른쪽 끝보다 커야 합니다.");
        }

        ValidateSquad(
            _upperCount, _upperStart, _upperOffset, _upperEndY, true);

        ValidateSquad(
            _lowerCount, _lowerStart, _lowerOffset, _lowerEndY, false);
    }

    /// <summary>
    /// 모든 적이 올바른 방향으로 진입하고 오른쪽으로 후퇴하는지 검사한다.
    /// </summary>
    private void ValidateSquad(
        int count,
        Vector2 start,
        Vector2 offset,
        float endY,
        bool upper)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 spawn = start + offset * i;

            bool invalidY = upper
                ? spawn.y <= endY
                : spawn.y >= endY;

            if (invalidY ||
                spawn.x >= _settings.StayCenter.x - _settings.StayRadius ||
                _settings.AimPoint.x >= spawn.x)
            {
                throw new InvalidOperationException(
                    $"{name}: 생성 위치, EndY, 조준 좌표, 잔류 원을 확인하세요.");
            }
        }
    }

    /// <summary>
    /// 위·아래 부대의 같은 순번을 함께 생성한다.
    /// </summary>
    private IEnumerator SpawnSequence(EnemySpawnContext context)
    {
        int count = Mathf.Max(_upperCount, _lowerCount);
        WaitForSeconds wait = new WaitForSeconds(_spawnInterval);

        for (int i = 0; i < count; i++)
        {
            if (i < _upperCount)
            {
                SpawnOne(
                    context,
                    _upperStart + _upperOffset * i,
                    _upperEndY);
            }

            if (i < _lowerCount)
            {
                SpawnOne(
                    context,
                    _lowerStart + _lowerOffset * i,
                    _lowerEndY);
            }

            if (i + 1 < count)
            {
                yield return wait;
            }
        }
    }

    /// <summary>
    /// 적을 생성하고 첫 도착 좌표와 행동 설정을 전달한다.
    /// </summary>
    private void SpawnOne(
        EnemySpawnContext context,
        Vector2 spawn,
        float endY)
    {
        RotEnemy enemy = context.SpawnEnemy(
            _enemyPrefab,
            spawn,
            Quaternion.identity);

        enemy.InitializeTwinPattern(
            new Vector2(spawn.x, endY),
            _settings);
    }
}
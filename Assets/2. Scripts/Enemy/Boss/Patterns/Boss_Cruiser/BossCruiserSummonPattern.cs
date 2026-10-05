using System;
using System.Collections;
using UnityEngine;

[CreateAssetMenu(
    fileName = "BossCruiserSummonPattern",
    menuName = "BossPattern/Cruiser/Small Enemy Summon")]
public sealed class BossCruiserSummonPattern : BossPattern
{
    [Serializable]
    private sealed class HoleSettings
    {
        // 이 구멍에서 패턴 전체 동안 소환할 총 마릿수.
        // 0이면 해당 구멍에서는 소환하지 않는다.
        [Min(0)]
        public int TotalCount = 5;

        // 소환 위치를 기준으로 첫 번째 제어점까지의 월드 방향 거리.
        // Y를 높이면 구멍에서 위로 솟아오르는 모양이 된다.
        public Vector2 Control1Offset = new Vector2(0f, 2f);

        // 두 번째 제어점까지의 월드 방향 거리.
        // X를 음수로 설정하면 왼쪽으로 휘어진다.
        public Vector2 Control2Offset = new Vector2(-2f, 3f);

        // 소환 위치를 기준으로 곡선 이동을 끝낼 위치.
        public Vector2 EndOffset = new Vector2(-4f, 1f);

        // 첫 번째 적과 마지막 적의 도착 위치 사이의 전체 간격.
        // 같은 구멍에서 나온 적들이 조금씩 다른 곡선으로 퍼지게 한다.
        public Vector2 EndSpread = new Vector2(0f, 1.5f);

        // 적 한 마리가 곡선을 따라 이동하는 시간.
        [Min(0.01f)]
        public float CurveDuration = 1.2f;
    }

    // 소환을 시작하기 전에 표시할 시스템 경고.
    [SerializeField, TextArea]
    private string _systemMessage = "경고! 크루저에서 자폭 기체가 출격합니다.";

    // 기존 자폭 동작이 설정된 SmallEnemy 프리팹.
    [SerializeField]
    private SmallEnemy _enemyPrefab;

    // 경고가 끝난 뒤 세 구멍이 함께 사용하는 소환 시간.
    [SerializeField, Min(0.01f)]
    private float _summonDuration = 5f;

    // 구멍별 총 마릿수와 곡선 모양을 각각 설정한다.
    [SerializeField] private HoleSettings _holeA = new HoleSettings();
    [SerializeField] private HoleSettings _holeB = new HoleSettings();
    [SerializeField] private HoleSettings _holeC = new HoleSettings();

    public override IEnumerator Execute(BossPatternContext context)
    {
        Boss_Cruiser cruiser = context.Boss as Boss_Cruiser;

        if (cruiser == null)
        {
            throw new InvalidOperationException(
                $"{name}: Boss_Cruiser 전용 패턴입니다.");
        }

        // 대기 중 페이즈가 달라졌거나 보스가 사망했다면 실행하지 않는다.
        if (!CanExecute(cruiser))
        {
            yield break;
        }

        if (_enemyPrefab == null ||
            string.IsNullOrWhiteSpace(_systemMessage) ||
            !IsPositiveFinite(_summonDuration))
        {
            throw new InvalidOperationException(
                $"{name}: 프리팹, 경고 문구, 소환 시간을 확인하세요.");
        }

        // 실행마다 별도의 상태를 만들어 패턴 에셋에 실행 정보를 남기지 않는다.
        HoleSettings[] settings = { _holeA, _holeB, _holeC };
        Transform[] points = new Transform[3];
        int[] spawnedCounts = new int[3];

        bool hasEnemiesToSpawn = false;

        for (int i = 0; i < settings.Length; i++)
        {
            HoleSettings hole = settings[i];

            if (hole == null || hole.TotalCount < 0)
            {
                throw new InvalidOperationException(
                    $"{name}: 구멍 {i}의 설정을 확인하세요.");
            }

            // 사용하지 않는 구멍은 위치 연결도 요구하지 않는다.
            if (hole.TotalCount == 0)
            {
                continue;
            }

            if (!IsPositiveFinite(hole.CurveDuration))
            {
                throw new InvalidOperationException(
                    $"{name}: 구멍 {i}의 Curve Duration을 확인하세요.");
            }

            points[i] = cruiser.GetSummonPoint(i);
            hasEnemiesToSpawn = true;
        }

        // 세 구멍이 모두 0마리라면 경고 없이 종료한다.
        if (!hasEnemiesToSpawn)
        {
            yield break;
        }

        bool dialogueCompleted = false;

        Action handleDialogueCompleted =
            () => dialogueCompleted = true;

        cruiser.SystemDialogueCompleted += handleDialogueCompleted;

        try
        {
            // 기존 보스 시스템 다이얼로그를 사용한다.
            cruiser.RequestSystemDialogue(_systemMessage);

            while (!dialogueCompleted)
            {
                if (!CanExecute(cruiser))
                {
                    yield break;
                }

                yield return null;
            }

            if (!CanExecute(cruiser))
            {
                yield break;
            }

            // 경고 시간은 소환 시간에 포함하지 않는다.
            // 게임 시간 기준이므로 일시정지 중에는 시간표도 멈춘다.
            double startTime = Time.timeAsDouble;

            while (true)
            {
                if (!CanExecute(cruiser))
                {
                    yield break;
                }

                double elapsed = Time.timeAsDouble - startTime;

                for (int i = 0; i < settings.Length; i++)
                {
                    HoleSettings hole = settings[i];

                    if (hole.TotalCount == 0)
                    {
                        continue;
                    }

                    // 간격은 별도로 입력하지 않고 총 마릿수로 계산한다.
                    double interval =
                        (double)_summonDuration / hole.TotalCount;

                    // 첫 번째 적의 예정 시간은 0초다.
                    // 프레임이 지연되어도 예정된 마릿수를 누락하지 않는다.
                    while (spawnedCounts[i] < hole.TotalCount &&
                           elapsed >= spawnedCounts[i] * interval)
                    {
                        SpawnOne(
                            context,
                            cruiser,
                            points[i],
                            hole,
                            spawnedCounts[i]);

                        spawnedCounts[i]++;
                    }
                }

                // 마지막 적이 먼저 나왔더라도 설정한 소환 시간까지 유지한다.
                if (elapsed >= _summonDuration)
                {
                    break;
                }

                yield return null;
            }
        }
        finally
        {
            // 정상 완료와 중도 취소 모두 이벤트 구독을 정리한다.
            if (cruiser != null)
            {
                cruiser.SystemDialogueCompleted -= handleDialogueCompleted;
            }
        }

        // 이미 생성된 적은 각자 곡선 이동과 자폭을 계속 진행한다.
    }

    private void SpawnOne(
        BossPatternContext context,
        Boss_Cruiser cruiser,
        Transform point,
        HoleSettings settings,
        int spawnIndex)
    {
        if (point == null || !point.gameObject.activeInHierarchy)
        {
            throw new InvalidOperationException(
                $"{name}: 소환 중 구멍 위치가 사라지거나 비활성화됐습니다.");
        }

        // 매번 생성 시점의 구멍 위치를 사용한다.
        Vector3 spawnPosition = point.position;
        Vector2 origin = spawnPosition;

        // 한 마리면 중앙, 여러 마리면 -0.5~0.5 사이에 균등 배치한다.
        float spreadRatio = settings.TotalCount <= 1
            ? 0f
            : (float)spawnIndex / (settings.TotalCount - 1) - 0.5f;

        Vector2 spread = settings.EndSpread * spreadRatio;

        // SmallEnemy 풀에서 빌려 소환 위치에 활성화한다.
        GameObject enemyObject = ProjectilePoolManager.Instance.Spawn(
            _enemyPrefab.gameObject,
            spawnPosition,
            Quaternion.identity);

        // 빌린 오브젝트의 적 컴포넌트를 가져온다.
        SmallEnemy enemy = enemyObject.GetComponent<SmallEnemy>();

        cruiser.RegisterSummonedEnemy(enemy);

        // 기존 일반 적 생성과 동일하게 플레이어와 GameManager를 전달한다.
        enemy.Init(context.TargetPlayer, context.GameManager);

        // 곡선이 끝나면 SmallEnemy의 기존 조준·돌진·자폭으로 이어진다.
        enemy.InitializeCurvedSuicidePattern(
            origin + settings.Control1Offset,
            origin + settings.Control2Offset + spread,
            origin + settings.EndOffset + spread,
            settings.CurveDuration);
    }

    // 2페이즈에서 살아 있는 크루저만 소환할 수 있다.
    private static bool CanExecute(Boss_Cruiser cruiser)
    {
        return cruiser != null &&
               cruiser.isActiveAndEnabled &&
               !cruiser.IsDead &&
               cruiser.CurrentPhase == Boss_Cruiser.CruiserPhase.Entity;
    }

    // 시간이 0, 음수, NaN, 무한대로 설정되는 것을 막는다.
    private static bool IsPositiveFinite(float value)
    {
        return value > 0f && !float.IsInfinity(value);
    }
}
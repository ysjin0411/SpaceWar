using System;
using System.Collections;
using UnityEngine;

// 플레이어의 과거 위치를 기록하고 기록 순서대로 공격한다.
// 실행 상태는 지역 변수에 보관하여 공유 에셋에 남기지 않는다.
[CreateAssetMenu(
    menuName = "Boss/Alien/Phase2/Trace Pattern",
    fileName = "BossAlienTracePattern")]
public sealed class BossAlienTracePattern : BossPattern
{
    // 이동 흔적을 기록하기 전에 표시할 시스템 경고 문구다.
    [SerializeField, TextArea(2, 3)]
    private string _warningMessage =
        "경고! 이동 흔적이 감지됩니다. 기록된 위치에서 벗어나십시오.";

    // 기록된 위치의 원형 공격 범위를 표시한다.
    [SerializeField]
    private BossAlienTraceWarning _warningPrefab;

    // Rise → Peak → Fade 순서로 표시할 발톱 효과다.
    [SerializeField]
    private BossAlienTraceClaw _clawPrefab;

    // 한 번의 패턴에서 기록할 위치 개수다.
    [SerializeField, Min(1)]
    private int _recordCount = 6;

    // 각 위치에 Aim 경고를 표시한 뒤 발톱이 나오기까지의 시간이다.
    // 모든 기록이 끝날 때까지 기다리지 않고, 각 경고마다 따로 계산한다.
    [SerializeField, Min(0.01f)]
    private float _warningDuration = 0.2f;

    // 다음 위치를 기록하고 경고를 생성하는 간격이다.
    // 각 경고가 동일한 지연 뒤 공격하므로 공격 사이의 간격도 된다.
    [SerializeField, Min(0.01f)]
    private float _strikeInterval = 0.3f;

    // 발톱이 올라오기 시작한 뒤 피해 판정까지의 시간이다.
    [SerializeField, Min(0.01f)]
    private float _riseDuration = 0.12f;

    // 피해 판정 후 완전히 솟은 모습을 유지하는 시간이다.
    [SerializeField, Min(0.01f)]
    private float _peakDuration = 0.18f;

    // 사라지는 이미지를 유지하는 시간이다.
    [SerializeField, Min(0.01f)]
    private float _fadeDuration = 0.15f;

    // 모든 발톱이 사라진 뒤 보스가 쉬는 시간이다.
    [SerializeField, Min(0.01f)]
    private float _recoveryDuration = 0.6f;

    // 경고와 실제 원형 피해 판정에 함께 사용하는 반경이다.
    [SerializeField, Min(0.01f)]
    private float _radius = 1.2f;

    // 각 기록 위치에서 한 번 요청하는 피해량이다.
    [SerializeField, Min(0)]
    private float _damage = 10;

    /// <summary>
    /// Execute 동작에 필요한 상태와 처리 흐름을 실행한다.
    /// </summary>
    public override IEnumerator Execute(BossPatternContext context)
    {
        // 에일리언 본체에서만 실행할 수 있다.
        Boss_AlienBody boss = context.Boss as Boss_AlienBody;

        if (boss == null)
        {
            throw new InvalidOperationException(
                $"{name}: Boss_AlienBody 전용 패턴입니다.");
        }

        if (!boss.CanRunPhaseTwoPattern)
            yield break;

        ValidateSettings();

        // 본체 자세와 플레이어의 실제 물리 좌표를 가져온다.
        BossAlienPhaseTwoVisual visual =
            context.VisualTransform.GetComponent<BossAlienPhaseTwoVisual>();

        Rigidbody2D targetBody =
            context.TargetPlayer.GetComponent<Rigidbody2D>();

        if (visual == null || targetBody == null)
        {
            throw new MissingComponentException(
                $"{name}: 본체 표현 컴포넌트와 플레이어 Rigidbody2D를 확인하세요.");
        }

        // 실행 중 생성한 모든 오브젝트를 보관하여 중단 시에도 정리한다.
        Vector2[] positions = new Vector2[_recordCount];
        BossAlienTraceWarning[] warnings =
            new BossAlienTraceWarning[_recordCount];
        BossAlienTraceClaw[] claws =
            new BossAlienTraceClaw[_recordCount];

        // 각 발톱의 진행 단계다.
        // 0: 생성 전, 1: Rise, 2: Peak, 3: Fade, 4: 제거 완료.
        int[] stages = new int[_recordCount];
        bool beganAction = false;

        // 이번 실행에서 시스템 경고의 점멸 완료 여부를 보관한다.
        bool dialogueCompleted = false;

        // 이번 패턴이 실제로 요청한 경고만 취소하도록 기록한다.
        bool dialogueRequested = false;

        // 요청 전에 구독하고, 종료 또는 중단 시 같은 핸들러를 해제한다.
        Action onDialogueCompleted = () => dialogueCompleted = true;

        try
        {
            // 경고를 보여주는 동안 본체의 이동을 정지한다.
            context.Rigidbody.linearVelocity = Vector2.zero;

            // 경고 요청 전에 완료 이벤트를 구독하여 신호를 놓치지 않는다.
            boss.SystemDialogueCompleted += onDialogueCompleted;
            boss.RequestSystemDialogue(_warningMessage);

            // 요청이 성공한 뒤에만 이번 패턴의 경고로 기록한다.
            dialogueRequested = true;

            // 시스템 경고의 점멸이 끝날 때까지 위치 기록을 시작하지 않는다.
            while (!dialogueCompleted)
            {
                // 경고 중 전투가 끝났다면 패턴도 중단한다.
                if (!CanContinue(boss))
                    yield break;

                yield return null;
            }

            // 경고가 끝난 뒤에는 다른 메시지의 완료 신호를 받지 않는다.
            boss.SystemDialogueCompleted -= onDialogueCompleted;

            // UI는 일시정지 중에도 재생되므로 공격 시작은 별도로 기다린다.
            while (CanContinue(boss) && Time.deltaTime <= 0f)
                yield return null;

            if (!CanContinue(boss) || visual == null)
                yield break;

            beganAction = visual.BeginAction("Phase2_Trace", "Observe");

            if (!beganAction)
                yield break;

            // 이전 패턴에서 남았을 수 있는 물리 이동 속도를 제거한다.
            context.Rigidbody.linearVelocity = Vector2.zero;

            // 먼저 손을 땅에 박는다.
            // 경고가 뜬 뒤 기다리는 시간에 삽입 애니메이션이 추가되지 않게 한다.
            if (!CanContinue(boss) ||
                visual == null ||
                !visual.PlayTraceInsert())
            {
                yield break;
            }
            GameAudio.Play(GameSound.AlienP2TraceInsert);

            // 손을 박는 애니메이션이 끝날 때까지 기다린다.
            while (true)
            {
                if (!CanContinue(boss) ||
                    visual == null ||
                    !visual.TryGetTraceInsertProgress(out float progress))
                {
                    yield break;
                }

                if (progress >= 1f)
                    break;

                yield return null;
            }

            // 이동 경로를 기록하고 공격하는 동안 손을 박은 자세를 유지한다.
            if (!visual.PlayTraceHold())
                yield break;

            // 각 경고가 실제로 생성된 시각을 저장한다.
            // 프레임이 늦어져도 새 경고의 회피 시간이 생략되지 않게 한다.
            float[] warningTimes = new float[positions.Length];

            // 기록과 공격이 공유하는 게임 시간이다.
            float elapsed = 0f;

            // 다음 경고를 생성할 시각이다. 첫 경고는 즉시 생성한다.
            float nextRecordTime = 0f;

            // 실제로 위치와 경고가 생성된 개수다.
            int recordedCount = 0;

            // 사라지는 동작까지 모두 끝난 공격의 개수다.
            int completedCount = 0;

            // 경고 생성과 기존 경고의 공격 진행을 함께 처리한다.
            while (completedCount < positions.Length)
            {
                if (!CanContinue(boss) ||
                    visual == null ||
                    targetBody == null)
                {
                    yield break;
                }

                // 일시정지 중에는 기록, 생성, 피해 처리를 진행하지 않는다.
                if (Time.deltaTime <= 0f)
                {
                    yield return null;
                    continue;
                }

                // 일정 간격마다 플레이어의 현재 위치에 새 경고를 생성한다.
                // 지난 위치를 추측해서 채우지 않고 실제 관측한 위치만 기록한다.
                if (recordedCount < positions.Length &&
                    elapsed >= nextRecordTime)
                {
                    int index = recordedCount;

                    // 예측하지 않고 현재 물리 좌표를 저장한다.
                    positions[index] = targetBody.position;
                    warningTimes[index] = elapsed;

                    // Initialize는 Aim 이미지와 색상으로 경고를 시작한다.
                    // Trace 전용 경고는 생성 위치에서 Aim 표시만 유지한다.
                    warnings[index] = Instantiate(_warningPrefab);
                    warnings[index].Initialize(
                        boss, positions[index], _radius);
                    GameAudio.Play(GameSound.AlienP2TraceWarning);

                    // 이후 이 경고의 위치는 갱신하지 않는다.
                    recordedCount++;

                    // 프레임 지연 후 여러 경고가 한꺼번에 생기지 않게 한다.
                    nextRecordTime = elapsed + _strikeInterval;
                }

                // 이미 생성한 경고들의 공격을 각각 진행한다.
                for (int i = 0; i < recordedCount; i++)
                {
                    if (stages[i] == 4)
                        continue;

                    // 피해를 주기 전 경고가 사라졌다면 보이지 않는 공격을 막는다.
                    if (stages[i] < 2 && warnings[i] == null)
                        yield break;

                    // 각 경고의 생성 시각으로부터 발톱 생성 시점을 계산한다.
                    float localTime =
                        elapsed - warningTimes[i] - _warningDuration;

                    // 이 경고의 대기 시간이 아직 끝나지 않았다.
                    if (localTime < 0f)
                        continue;

                    // 경고를 생성했던 고정 위치에서 발톱이 올라오기 시작한다.
                    if (stages[i] == 0)
                    {
                        claws[i] = Instantiate(_clawPrefab);
                        claws[i].Initialize(boss, positions[i], _radius);
                        stages[i] = 1;
                    }

                    // 공격 효과가 사라졌다면 피해 처리도 중단한다.
                    if (claws[i] == null)
                        yield break;

                    // 발톱이 완전히 솟는 순간 피해를 한 번 적용한다.
                    if (stages[i] == 1 && localTime >= _riseDuration)
                    {
                        claws[i].ShowPeak();
                        GameAudio.Play(GameSound.AlienP2TraceStrike);

                        warnings[i].Remove();
                        warnings[i] = null;

                        // 피해 처리 전에 단계를 바꿔 중복 피해 요청을 막는다.
                        stages[i] = 2;

                        BossAlienExplosionDamage.Apply(
                            positions[i], _radius, _damage);

                        // 이번 공격으로 플레이어가 사망했다면 후속 처리를 멈춘다.
                        if (!CanContinue(boss))
                            yield break;
                    }

                    // 솟아 있는 시간이 지나면 사라지는 모습을 표시한다.
                    if (stages[i] == 2 &&
                        localTime >= _riseDuration + _peakDuration)
                    {
                        claws[i].ShowFade();
                        stages[i] = 3;
                    }

                    // 사라지는 동작까지 끝난 공격을 정리한다.
                    if (stages[i] == 3 &&
                        localTime >=
                        _riseDuration + _peakDuration + _fadeDuration)
                    {
                        claws[i].Remove();
                        claws[i] = null;
                        stages[i] = 4;
                        completedCount++;
                    }
                }

                // 다음 프레임까지 흐른 게임 시간을 반영한다.
                yield return null;
                elapsed += Time.deltaTime;
            }

            // 모든 지면 발톱이 사라지면 손을 빼는 동작을 시작한다.
            if (!CanContinue(boss) ||
                visual == null ||
                !visual.PlayTraceWithdraw())
            {
                yield break;
            }
            GameAudio.Play(GameSound.AlienP2TraceWithdraw);

            // 회수 시간과 손 빼기 애니메이션이 모두 끝날 때까지 기다린다.
            float recoveryElapsed = 0f;

            while (true)
            {
                // 전투 중단이나 다른 애니메이션으로의 전환을 확인한다.
                if (!CanContinue(boss) ||
                    visual == null ||
                    !visual.TryGetTraceWithdrawProgress(out float progress))
                {
                    yield break;
                }

                // 설정 시간이 짧아도 손 빼기 동작을 중간에 자르지 않는다.
                if (progress >= 1f && recoveryElapsed >= _recoveryDuration)
                    break;

                yield return null;
                recoveryElapsed += Time.deltaTime;
            }
        }
        finally
        {
            if (boss != null)
            {
                // 중단된 패턴이 이후 완료 신호를 받지 않도록 먼저 구독을 해제한다.
                boss.SystemDialogueCompleted -= onDialogueCompleted;

                // 이번 패턴의 경고가 아직 진행 중이면 UI 연출도 취소한다.
                // 이미 정상 종료된 경고에는 취소 요청을 보내지 않는다.
                if (dialogueRequested && !dialogueCompleted)
                {
                    boss.CancelSystemDialogue();
                }
            }

            // 정상 종료, 보스 사망, 패턴 취소 모두 같은 방식으로 정리한다.
            for (int i = 0; i < positions.Length; i++)
            {
                if (warnings[i] != null)
                    warnings[i].Remove();

                if (claws[i] != null)
                    claws[i].Remove();
            }

            if (beganAction && context.Rigidbody != null)
                context.Rigidbody.linearVelocity = Vector2.zero;

            // 사망 상태에서는 표현 컴포넌트가 Idle 복귀를 막는다.
            if (beganAction && visual != null)
                visual.EndAction();
        }
    }

    // 보스 또는 플레이어가 전투를 계속할 수 있는지 확인한다.
    /// <summary>
    /// 보스 또는 플레이어가 전투를 계속할 수 있는지 확인한다.
    /// </summary>
    private static bool CanContinue(Boss_AlienBody boss)
    {
        return boss != null && boss.CanRunPhaseTwoPattern;
    }

    // 누락된 프리팹과 잘못된 설정값을 실행 전에 확인한다.
    /// <summary>
    /// 누락된 프리팹과 잘못된 설정값을 실행 전에 확인한다.
    /// </summary>
    private void ValidateSettings()
    {
        // 문구 누락으로 경고 없이 특수패턴이 시작되지 않도록 확인한다.
        if (string.IsNullOrWhiteSpace(_warningMessage))
        {
            throw new InvalidOperationException(
                $"{name}: Warning Message를 입력하세요.");
        }

        if (_warningPrefab == null || _clawPrefab == null)
        {
            throw new InvalidOperationException(
                $"{name}: Warning Prefab과 Claw Prefab을 연결하세요.");
        }

        if (_recordCount < 1 || _damage < 0 ||
            !IsPositiveFinite(_warningDuration) ||
            !IsPositiveFinite(_strikeInterval) ||
            !IsPositiveFinite(_riseDuration) ||
            !IsPositiveFinite(_peakDuration) ||
            !IsPositiveFinite(_fadeDuration) ||
            !IsPositiveFinite(_recoveryDuration) ||
            !IsPositiveFinite(_radius))
        {
            throw new InvalidOperationException(
                $"{name}: 개수, 시간, 반경, 피해 설정을 확인하세요.");
        }
    }

    // 시간과 반경은 0보다 큰 유한한 값만 허용한다.
    /// <summary>
    /// 시간과 반경은 0보다 큰 유한한 값만 허용한다.
    /// </summary>
    private static bool IsPositiveFinite(float value)
    {
        return value > 0f &&
            !float.IsNaN(value) &&
            !float.IsInfinity(value);
    }
}
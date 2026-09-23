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
    private BossAlienLeapWarning _warningPrefab;

    // Rise → Peak → Fade 순서로 표시할 발톱 효과다.
    [SerializeField]
    private BossAlienTraceClaw _clawPrefab;

    // 한 번의 패턴에서 기록할 위치 개수다.
    [SerializeField, Min(1)]
    private int _recordCount = 6;

    // 플레이어 위치를 기록하는 시간 간격이다.
    [SerializeField, Min(0.01f)]
    private float _recordInterval = 0.35f;

    // 마지막 위치를 기록한 뒤 공격 시작까지 제공하는 시간이다.
    [SerializeField, Min(0.01f)]
    private float _lockDuration = 0.6f;

    // 다음 위치의 발톱이 솟기 시작하는 시간 간격이다.
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
    private int _damage = 10;

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
        BossAlienLeapWarning[] warnings =
            new BossAlienLeapWarning[_recordCount];
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

            // 첫 위치는 즉시 기록하고, 이후에는 일정 간격으로 기록한다.
            for (int i = 0; i < positions.Length; i++)
            {
                if (i > 0)
                    yield return WaitForBattleTime(boss, _recordInterval);

                // 일시정지 중에는 새로운 위치를 기록하지 않는다.
                while (CanContinue(boss) && Time.deltaTime <= 0f)
                    yield return null;

                if (!CanContinue(boss) || targetBody == null)
                    yield break;

                // 예측이나 벽 보정 없이 그 순간의 실제 좌표를 저장한다.
                // 저장한 좌표는 이후 플레이어가 움직여도 바뀌지 않는다.
                positions[i] = targetBody.position;

                warnings[i] = Instantiate(_warningPrefab);
                warnings[i].Initialize(boss, positions[i], _radius);
                warnings[i].LockPosition(positions[i]);
            }

            // 위치 기록을 마치고 발동 준비 자세로 바꾼다.
            if (!CanContinue(boss) ||
                visual == null ||
                !visual.ShowPose("Phase2_Trace", "Invoke"))
            {
                yield break;
            }

            yield return WaitForBattleTime(boss, _lockDuration);

            if (!CanContinue(boss) ||
                visual == null ||
                !visual.ShowPose("Phase2_Trace", "Release"))
            {
                yield break;
            }

            float elapsed = 0f;
            int completedCount = 0;

            // 하나의 시간축에서 여러 발톱의 단계를 관리한다.
            // 앞 발톱이 사라지기 전에도 다음 발톱이 올라올 수 있다.
            while (completedCount < positions.Length)
            {
                if (!CanContinue(boss))
                    yield break;

                // 일시정지 중에는 생성, 단계 전환, 피해 판정을 멈춘다.
                if (Time.deltaTime <= 0f)
                {
                    yield return null;
                    continue;
                }

                for (int i = 0; i < positions.Length; i++)
                {
                    if (stages[i] == 4)
                        continue;

                    // 각 위치의 시작 시간을 기록 순서에 따라 뒤로 미룬다.
                    float localTime = elapsed - i * _strikeInterval;

                    if (localTime < 0f)
                        continue;

                    // 해당 위치에서 발톱이 솟기 시작한다.
                    if (stages[i] == 0)
                    {
                        claws[i] = Instantiate(_clawPrefab);
                        claws[i].Initialize(boss, positions[i], _radius);
                        stages[i] = 1;
                    }

                    // 예상치 못하게 효과가 사라지면 보이지 않는 공격을 중단한다.
                    if (claws[i] == null)
                        yield break;

                    // 완전히 솟는 순간 경고를 지우고 피해를 한 번 판정한다.
                    if (stages[i] == 1 && localTime >= _riseDuration)
                    {
                        claws[i].ShowPeak();

                        if (warnings[i] != null)
                        {
                            warnings[i].Remove();
                            warnings[i] = null;
                        }

                        // 피해 처리 전에 단계를 변경하여 중복 요청을 막는다.
                        stages[i] = 2;

                        BossAlienExplosionDamage.Apply(
                            positions[i], _radius, _damage);

                        // 이번 피해로 플레이어가 사망했다면 후속 공격을 중단한다.
                        if (!CanContinue(boss))
                            yield break;
                    }

                    // Peak 유지 시간이 지나면 사라지는 이미지로 바꾼다.
                    if (stages[i] == 2 &&
                        localTime >= _riseDuration + _peakDuration)
                    {
                        claws[i].ShowFade();
                        stages[i] = 3;
                    }

                    // Fade가 끝난 발톱을 제거한다.
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

            // 마지막 공격 이후 빈틈을 제공한다.
            yield return WaitForBattleTime(boss, _recoveryDuration);
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

    // 전투 중단을 매 프레임 확인하면서 게임 시간 기준으로 기다린다.
    private static IEnumerator WaitForBattleTime(
        Boss_AlienBody boss, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (!CanContinue(boss))
                yield break;

            yield return null;
            elapsed += Time.deltaTime;
        }
    }

    // 보스 또는 플레이어가 전투를 계속할 수 있는지 확인한다.
    private static bool CanContinue(Boss_AlienBody boss)
    {
        return boss != null && boss.CanRunPhaseTwoPattern;
    }

    // 누락된 프리팹과 잘못된 설정값을 실행 전에 확인한다.
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
            !IsPositiveFinite(_recordInterval) ||
            !IsPositiveFinite(_lockDuration) ||
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
    private static bool IsPositiveFinite(float value)
    {
        return value > 0f &&
            !float.IsNaN(value) &&
            !float.IsInfinity(value);
    }
}
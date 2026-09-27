using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 시스템 경고 후 게임을 정지하고 정신 혼란 UI를 실행한다.
/// 실패 시 피해와 화면 흔들림, 실패 알림을 처리한다.
/// </summary>
[CreateAssetMenu(
    fileName = "BossHeadConfusionPattern",
    menuName = "BossPattern/Head/Mental Confusion")]
public sealed class BossHeadConfusionPattern : BossPattern
{
    // 패턴 시작 전에 표시할 시스템 알림이다.
    [TextArea]
    [SerializeField] string _startMessage = "정신 간섭 감지! 입력에 집중하세요.";

    // 패턴 실패 시 화면 흔들림과 함께 표시할 알림이다.
    [TextArea]
    [SerializeField] string _failureMessage = "정신 방어 실패! 충격이 발생합니다.";

    // 키 입력 화면 전체가 들어 있는 프리팹이다.
    [SerializeField] MentalConfusionUI _uiPrefab;

    // 이번 패턴에서 맞혀야 할 알파벳 개수다.
    [Min(1)]
    [SerializeField] int _keyCount = 10;

    // 알파벳을 추첨할 목록이다. 중복 문자를 넣지 않는다.
    [SerializeField] string _keyPool = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    // 모든 알파벳을 입력해야 하는 제한 시간이다.
    [Min(0.01f)]
    [SerializeField] float _timeLimit = 8f;

    // 오답을 입력할 때 하나씩 차감할 Life다.
    [Min(1)]
    [SerializeField] int _lifeCount = 3;

    // 시간 초과 또는 Life 소진 시 한 번 적용할 피해량이다.
    [Min(1)]
    [SerializeField] float _failureDamage = 20;

    // 실패 시 카메라가 흔들리는 시간이다.
    [Min(0.01f)]
    [SerializeField] float _shakeDuration = 0.3f;

    // 카메라 흔들림의 월드 좌표 크기다.
    [Min(0f)]
    [SerializeField] float _shakeStrength = 0.12f;

    /// <summary>
    /// Execute 동작에 필요한 상태와 처리 흐름을 실행한다.
    /// </summary>
    public override IEnumerator Execute(BossPatternContext context)
    {
        ValidateSettings(context);

        BossEnemy boss = context.Boss;
        PlayerController player = context.TargetPlayer;
        Camera gameCamera = Camera.main;

        if (gameCamera == null)
        {
            throw new InvalidOperationException(
                $"{name}: MainCamera 태그가 있는 카메라가 필요합니다.");
        }

        MentalConfusionUI ui = null;
        Tween shake = null;

        bool dialogueCompleted = false;
        bool ownsPause = false;
        bool cameraMoved = false;

        float previousTimeScale = 1f;
        Vector3 cameraPosition = Vector3.zero;

        // 기존 시스템 알림 완료 이벤트를 재사용한다.
        Action onDialogueCompleted = () => dialogueCompleted = true;
        boss.SystemDialogueCompleted += onDialogueCompleted;

        try
        {
            // 이미 일시정지나 증강 중이라면 종료될 때까지 기다린다.
            while (CanContinue(boss, player) &&
                   (Time.timeScale <= 0f || !player.CanControl))
            {
                yield return null;
            }

            if (!CanContinue(boss, player))
            {
                yield break;
            }

            // 시작 경고가 끝난 다음 입력 패턴으로 넘어간다.
            boss.RequestSystemDialogue(_startMessage);

            while (CanContinue(boss, player) && !dialogueCompleted)
            {
                yield return null;
            }

            if (!CanContinue(boss, player))
            {
                yield break;
            }

            // 경고 도중 다른 일시정지가 열렸다면 먼저 기다린다.
            while (CanContinue(boss, player) &&
                   (Time.timeScale <= 0f || !player.CanControl))
            {
                yield return null;
            }

            if (!CanContinue(boss, player))
            {
                yield break;
            }

            // 기존 게임 속도를 보관하고 전투 시간을 멈춘다.
            previousTimeScale = Time.timeScale;
            ownsPause = true;
            Time.timeScale = 0f;

            // UI는 unscaledDeltaTime과 독립 Tween으로 계속 진행한다.
            ui = Instantiate(_uiPrefab);
            ui.Begin(CreateSequence(), _timeLimit, _lifeCount);

            while (CanContinue(boss, player) &&
                   ui != null &&
                   ui.isActiveAndEnabled &&
                   !ui.IsFinished)
            {
                yield return null;
            }

            // UI 제거나 보스·플레이어 제거는 실패 피해 없이 취소한다.
            if (!CanContinue(boss, player) ||
                ui == null ||
                !ui.isActiveAndEnabled ||
                ui.Result == ConfusionResult.Cancelled)
            {
                yield break;
            }

            bool failed = ui.Result == ConfusionResult.Failed;

            // 마지막 키 연출까지 완료된 입력 화면을 닫는다.
            ui.gameObject.SetActive(false);
            Destroy(ui.gameObject);
            ui = null;

            if (!failed)
            {
                // 성공은 finally에서 게임을 재개한 뒤 일반 모드로 복귀한다.
                yield break;
            }

            // 기존 피격 로직으로 실패 피해를 한 번 적용한다.
            // 게임 시간은 계속 정지되어 있어 적들의 공격은 진행되지 않는다.
            player.TakeHit(_failureDamage);

            // 카메라 원위치를 보관해 중단되더라도 정확하게 복원한다.
            cameraPosition = gameCamera.transform.localPosition;
            cameraMoved = true;

            shake = gameCamera.transform
                .DOShakePosition(
                    _shakeDuration,
                    new Vector3(_shakeStrength, _shakeStrength, 0f),
                    18,
                    90f,
                    false,
                    true)
                .SetUpdate(true);

            // 피해와 화면 흔들림에 맞춰 두 번째 시스템 알림을 표시한다.
            dialogueCompleted = false;
            boss.RequestSystemDialogue(_failureMessage);

            // 치명적인 피해여도 실패 연출은 끝까지 보여준다.
            // 사망 시 게임 시간은 finally에서도 재개하지 않는다.
            while (IsBossAlive(boss) &&
                   (!dialogueCompleted ||
                    (shake != null &&
                     shake.IsActive() &&
                     !shake.IsComplete())))
            {
                yield return null;
            }
        }
        finally
        {
            // 정상 종료와 중단 모두 같은 정리 경로를 사용한다.
            if (boss != null)
            {
                boss.SystemDialogueCompleted -= onDialogueCompleted;
            }

            if (shake != null)
            {
                shake.Kill();
            }

            if (cameraMoved && gameCamera != null)
            {
                gameCamera.transform.localPosition = cameraPosition;
            }

            if (ui != null)
            {
                ui.gameObject.SetActive(false);
                Destroy(ui.gameObject);
            }

            // 이 패턴이 직접 정지한 경우에만 시간을 복구한다.
            // 실패 피해로 플레이어가 죽었다면 사망 정지를 유지한다.
            if (ownsPause)
            {
                Time.timeScale =
                    player != null && player.IsDead
                        ? 0f
                        : previousTimeScale;
            }
        }
    }

    /// <summary>
    /// 설정한 후보에서 이번 패턴의 알파벳 문자열을 추첨한다.
    /// 같은 알파벳이 연속으로 나오는 것도 허용한다.
    /// </summary>
    private string CreateSequence()
    {
        string pool = _keyPool.ToUpperInvariant();
        char[] letters = new char[_keyCount];

        for (int i = 0; i < letters.Length; i++)
        {
            int index = UnityEngine.Random.Range(0, pool.Length);
            letters[i] = pool[index];
        }

        return new string(letters);
    }

    /// <summary>
    /// 입력 패턴을 계속 진행할 수 있는 상태인지 확인한다.
    /// </summary>
    private static bool CanContinue(
        BossEnemy boss,
        PlayerController player)
    {
        return IsBossAlive(boss) &&
               player != null &&
               player.isActiveAndEnabled &&
               !player.IsDead;
    }

    /// <summary>
    /// IsBossAlive 동작에 필요한 상태와 처리 흐름을 실행한다.
    /// </summary>
    private static bool IsBossAlive(BossEnemy boss)
    {
        return boss != null &&
               boss.isActiveAndEnabled &&
               !boss.IsDead;
    }

    /// <summary>
    /// 연결과 패턴 설정을 실행 전에 확인한다.
    /// </summary>
    private void ValidateSettings(BossPatternContext context)
    {
        if (context == null ||
            _uiPrefab == null ||
            !_uiPrefab.gameObject.activeSelf ||
            !_uiPrefab.enabled ||
            string.IsNullOrWhiteSpace(_startMessage) ||
            string.IsNullOrWhiteSpace(_failureMessage) ||
            string.IsNullOrWhiteSpace(_keyPool))
        {
            throw new InvalidOperationException(
                $"{name}: 활성화된 UI 프리팹과 알림·키 목록을 확인하세요.");
        }

        if (!PauseRandomMovement ||
            _keyCount <= 0 ||
            _lifeCount <= 0 ||
            _timeLimit <= 0f ||
            _failureDamage <= 0 ||
            _shakeDuration <= 0f ||
            _shakeStrength < 0f)
        {
            throw new InvalidOperationException(
                $"{name}: Pause Random Movement와 패턴 수치를 확인하세요.");
        }

        // 후보가 중복되면 특정 알파벳의 추첨 확률이 높아지므로 막는다.
        string pool = _keyPool.ToUpperInvariant();

        for (int i = 0; i < pool.Length; i++)
        {
            if (pool[i] < 'A' ||
                pool[i] > 'Z' ||
                pool.IndexOf(pool[i]) != i)
            {
                throw new InvalidOperationException(
                    $"{name}: Key Pool은 중복 없는 A~Z로 입력하세요.");
            }
        }
    }
}
using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// 보스 경고와 플레이 안내가 시스템 다이어로그 프레임을 공유한다.
/// 보스 경고만 완료 시 보스 패턴에 진행 신호를 전달한다.
/// </summary>
public class SystemDialogueUI : MonoBehaviour
{
    [Header("시스템 알림 UI")]

    // 배경 프레임과 현재 선택된 텍스트를 함께 점멸시킨다.
    [SerializeField]
    CanvasGroup _dialogueCanvasGroup;

    // 보스 패턴의 경고 내용을 표시한다.
    [SerializeField]
    TMP_Text _dialogueText;

    // 조작 방법과 플레이 안내 내용을 표시한다.
    [SerializeField]
    TMP_Text _assistantText;

    [Header("점멸 설정")]

    // 한 번 밝아지거나 어두워지는 데 걸리는 시간이다.
    [Min(0.01f)]
    [SerializeField]
    float _blinkDuration = 0.15f;

    // 메시지가 나타났다가 사라지는 동작의 반복 횟수다.
    [Min(1)]
    [SerializeField]
    int _blinkCount = 3;

    [Header("보스 연결")]

    // 현재 스테이지의 보스 생성과 페이즈 교체를 전달받는다.
    [SerializeField]
    BossEncounterManager _bossEncounterManager;

    // 에일리언 2페이즈 진입 시 표시할 조작 안내다.
    [SerializeField]
    string _phaseTwoAssistantMessage =
        "X키를 눌러서 좌우반전을 활용하세요!";

    // 시간제 안내를 요청한 컴포넌트다.
    // 해당 요청자만 자신의 안내를 취소할 수 있다.
    private MonoBehaviour _timedAssistantOwner;

    // 보스 경고나 기존 안내가 끝난 뒤 표시할 문구다.
    private string _timedAssistantMessage = string.Empty;

    // 안내가 실제로 표시된 게임 시간만 차감한다.
    private float _timedAssistantRemaining;

    // 현재 공유 대화창에 시간제 안내를 표시 중인지 구분한다.
    private bool _isTimedAssistantVisible;

    // 현재 실행 중인 점멸 연출이다.
    Sequence _dialogueSequence;

    // 현재 경고 이벤트를 구독 중인 보스다.
    BossEnemy _boundBoss;

    // 같은 보스에 대한 2페이즈 안내가 중복 요청되는 것을 방지한다.
    BossEnemy _assistedBoss;

    // 현재 표시 중인 메시지가 보스 경고인지 구분한다.
    bool _isWarningPlaying;

    // 표시를 기다리거나 경고 때문에 중단된 안내 문구다.
    string _pendingAssistantMessage = string.Empty;

    // 보스 경고가 정상적으로 끝났을 때만 발생한다.
    public event Action MessageBlinkCompleted;

    private void Awake()
    {
        // 필요한 연결이 빠졌다면 실행 초기에 원인을 알린다.
        if (_bossEncounterManager == null ||
            _dialogueCanvasGroup == null ||
            _dialogueText == null ||
            _assistantText == null)
        {
            throw new InvalidOperationException(
                $"{name}의 보스 관리자, CanvasGroup, 경고 텍스트, " +
                "AssistantText 연결을 확인하세요.");
        }

        // 두 용도의 텍스트는 서로 다른 오브젝트여야 한다.
        if (_dialogueText.gameObject == _assistantText.gameObject)
        {
            throw new InvalidOperationException(
                "경고 텍스트와 AssistantText는 별도 오브젝트여야 합니다.");
        }

        // 시작할 때 프레임과 두 텍스트를 숨긴다.
        StopDisplay();
    }

    private void OnEnable()
    {
        // 보스 생성과 페이즈 교체 모두 같은 연결 절차를 사용한다.
        _bossEncounterManager.BossSpawned += HandleBossSpawned;
        _bossEncounterManager.BossReplaced += HandleBossSpawned;

        // 보스 생성 이후 UI가 활성화된 경우도 연결한다.
        if (_bossEncounterManager.CurrentBoss != null)
        {
            HandleBossSpawned(_bossEncounterManager.CurrentBoss);
        }
    }

    private void OnDisable()
    {
        // 비활성화된 UI가 보스 이벤트를 계속 받지 않게 한다.
        if (_bossEncounterManager != null)
        {
            _bossEncounterManager.BossSpawned -= HandleBossSpawned;
            _bossEncounterManager.BossReplaced -= HandleBossSpawned;
        }

        UnbindBoss();
        ClearMessages();

        // UI 자체가 종료될 때는 시간제 요청도 제거한다.
        ClearTimedAssistant();
    }

    /// <summary>
    /// 보스 경고를 표시한다.
    /// 진행 중인 안내보다 우선하며, 정상 종료 시 보스에게 알린다.
    /// </summary>
    public void PlayMessage(string message)
    {
        ValidateMessage(message);

        // 경고끼리 덮어쓰면 기존 패턴이 완료 신호를 잃으므로 막는다.
        if (_isWarningPlaying)
        {
            throw new InvalidOperationException(
                "이미 다른 보스 경고가 재생 중입니다.");
        }

        // 안내는 대기 문구를 남긴 채 중단하여 경고 후 다시 표시한다.
        StopDisplay();

        // 요청 시점의 보스를 기억하여 다른 보스에게 완료를 보내지 않는다.
        BossEnemy requestingBoss = _boundBoss;

        StartDisplay(message, true, () =>
        {
            // 경고를 요청했던 보스가 여전히 유효할 때만 진행시킨다.
            if (requestingBoss != null &&
                requestingBoss == _boundBoss &&
                !requestingBoss.IsDead)
            {
                requestingBoss.NotifySystemDialogueCompleted();
                MessageBlinkCompleted?.Invoke();
            }

            // 경고 중 미뤄 둔 안내가 있으면 이어서 표시한다.
            TryPlayAssistant();
        });
    }

    /// <summary>
    /// 플레이 안내를 요청한다.
    /// 보스 경고 중이면 기다리고, 안내끼리는 최신 내용으로 교체한다.
    /// </summary>
    public void PlayAssistantMessage(string message)
    {
        ValidateMessage(message);

        // 안내는 공격 진행을 기다리는 메시지가 아니므로 최신 내용을 유지한다.
        _pendingAssistantMessage = message;

        if (_isWarningPlaying)
        {
            return;
        }

        StopDisplay();
        TryPlayAssistant();
    }

    // 경고가 없고 UI가 활성화된 경우에만 대기 중인 안내를 표시한다.
    private void TryPlayAssistant()
    {
        if (!isActiveAndEnabled ||
            _dialogueSequence != null ||
            string.IsNullOrEmpty(_pendingAssistantMessage))
        {
            return;
        }

        StartDisplay(_pendingAssistantMessage, false, () =>
        {
            // 정상적으로 끝난 안내만 대기 목록에서 제거한다.
            // 안내 완료는 보스에게 전달하지 않는다.
            _pendingAssistantMessage = string.Empty;
        });
    }

    // 메시지 종류에 맞는 텍스트를 선택하고 공통 점멸 연출을 실행한다.
    private void StartDisplay(
        string message,
        bool isWarning,
        Action onCompleted)
    {
        _isWarningPlaying = isWarning;

        // 두 텍스트가 같은 프레임 안에서 겹치지 않게 한다.
        _dialogueText.gameObject.SetActive(isWarning);
        _assistantText.gameObject.SetActive(!isWarning);

        TMP_Text targetText =
            isWarning ? _dialogueText : _assistantText;

        targetText.text = message;
        _dialogueCanvasGroup.alpha = 0f;

        _dialogueSequence = DOTween.Sequence();

        // 기존 경고처럼 게임 시간이 멈춰 있어도 점멸은 진행한다.
        _dialogueSequence.SetUpdate(true);

        _dialogueSequence.Append(
            _dialogueCanvasGroup
                .DOFade(1f, _blinkDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(_blinkCount * 2, LoopType.Yoyo));

        _dialogueSequence.OnComplete(() =>
        {
            // 콜백에서 다음 메시지를 요청할 수 있도록 상태를 먼저 정리한다.
            _dialogueSequence = null;
            _isWarningPlaying = false;
            _dialogueCanvasGroup.alpha = 0f;

            _dialogueText.gameObject.SetActive(false);
            _assistantText.gameObject.SetActive(false);

            onCompleted.Invoke();
        });
    }

    // 완료 콜백을 실행하지 않고 현재 표시만 중단한다.
    private void StopDisplay()
    {
        // 다른 메시지가 대화창을 사용하면 시간제 안내는 잠시 숨긴다.
        // 요청 내용과 남은 시간은 유지하여 나중에 이어서 표시한다.
        _isTimedAssistantVisible = false;

        if (_dialogueSequence != null)
        {
            _dialogueSequence.Kill(false);
            _dialogueSequence = null;
        }

        _isWarningPlaying = false;

        if (_dialogueCanvasGroup != null)
        {
            _dialogueCanvasGroup.alpha = 0f;
        }

        if (_dialogueText != null)
        {
            _dialogueText.gameObject.SetActive(false);
        }

        if (_assistantText != null)
        {
            _assistantText.gameObject.SetActive(false);
        }
    }

    // 보스 교체, 처치, UI 종료 시 대기 중인 안내까지 정리한다.
    private void ClearMessages()
    {
        _pendingAssistantMessage = string.Empty;
        StopDisplay();
    }

    // 새 보스에 이벤트를 연결하고 에일리언 2페이즈 안내를 요청한다.
    private void HandleBossSpawned(BossEnemy spawnedBoss)
    {
        if (spawnedBoss == null || spawnedBoss == _boundBoss)
        {
            return;
        }

        ClearMessages();
        UnbindBoss();

        _boundBoss = spawnedBoss;

        _boundBoss.SystemDialogueRequested += PlayMessage;
        _boundBoss.SystemDialogueCancelRequested +=
            HandleSystemDialogueCancelRequested;
        _boundBoss.Defeated += HandleBossDefeated;

        // 현재 프로젝트에서 Boss_AlienBody가 에일리언 2페이즈 본체다.
        if (spawnedBoss is Boss_AlienBody &&
            !spawnedBoss.IsDead &&
            spawnedBoss != _assistedBoss)
        {
            _assistedBoss = spawnedBoss;
            PlayAssistantMessage(_phaseTwoAssistantMessage);
        }
    }

    // 패턴 취소는 경고만 중단하며 정상 완료 신호를 보내지 않는다.
    private void HandleSystemDialogueCancelRequested()
    {
        if (!_isWarningPlaying)
        {
            return;
        }

        StopDisplay();
        TryPlayAssistant();
    }

    // 처치된 보스에 연결된 경고와 안내를 정리한다.
    private void HandleBossDefeated(BossEnemy defeatedBoss)
    {
        if (defeatedBoss != _boundBoss)
        {
            return;
        }

        ClearMessages();
        UnbindBoss();
    }

    // 이전 보스의 이벤트가 현재 UI에 영향을 주지 않도록 구독을 해제한다.
    private void UnbindBoss()
    {
        if (_boundBoss == null)
        {
            return;
        }

        _boundBoss.SystemDialogueRequested -= PlayMessage;
        _boundBoss.SystemDialogueCancelRequested -=
            HandleSystemDialogueCancelRequested;
        _boundBoss.Defeated -= HandleBossDefeated;

        _boundBoss = null;
    }

    // 내용이 없는 메시지 요청을 차단한다.
    private static void ValidateMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException(
                "시스템 알림 메시지가 비어 있습니다.",
                nameof(message));
        }
    }

    // 일정 시간 표시할 안내를 등록한다.
    // 현재 진행 중인 보스 경고나 기존 안내는 중단하지 않는다.
    public void ShowTimedAssistant(
        MonoBehaviour owner,
        string message,
        float duration)
    {
        if (owner == null)
        {
            throw new ArgumentNullException(nameof(owner));
        }

        ValidateMessage(message);

        if (float.IsNaN(duration) ||
            float.IsInfinity(duration) ||
            duration <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        // 이전 시간제 안내만 정리하고 새로운 요청을 등록한다.
        ClearTimedAssistant();

        _timedAssistantOwner = owner;
        _timedAssistantMessage = message;
        _timedAssistantRemaining = duration;
    }

    // 요청자가 자신의 안내만 취소한다.
    // 보스 경고와 다른 요청자의 안내는 건드리지 않는다.
    public void CancelTimedAssistant(MonoBehaviour owner)
    {
        if (owner == null || _timedAssistantOwner != owner)
        {
            return;
        }

        ClearTimedAssistant();
    }

    private void Update()
    {
        // 요청자가 파괴됐거나 안내 시간이 끝났다면 정리한다.
        if (_timedAssistantOwner == null ||
            _timedAssistantRemaining <= 0f)
        {
            ClearTimedAssistant();
            return;
        }

        // 보스 경고와 기존 점멸 안내가 공유 대화창을 우선 사용한다.
        // 이 동안에는 시간제 안내의 남은 시간을 차감하지 않는다.
        if (_isWarningPlaying || _dialogueSequence != null)
        {
            return;
        }

        // 기존 조작 안내가 대기 중이면 먼저 표시한다.
        if (!string.IsNullOrEmpty(_pendingAssistantMessage))
        {
            TryPlayAssistant();
            return;
        }

        if (!_isTimedAssistantVisible)
        {
            _dialogueText.gameObject.SetActive(false);
            _assistantText.gameObject.SetActive(true);

            _assistantText.text = _timedAssistantMessage;
            _dialogueCanvasGroup.alpha = 1f;

            _isTimedAssistantVisible = true;
        }

        // 증강 선택처럼 게임이 멈추면 안내 시간도 멈춘다.
        _timedAssistantRemaining -= Time.deltaTime;

        if (_timedAssistantRemaining <= 0f)
        {
            ClearTimedAssistant();
        }
    }

    // 시간제 요청을 제거한다.
    // 현재 보스 경고를 표시 중이라면 그 화면은 유지한다.
    private void ClearTimedAssistant()
    {
        if (_isTimedAssistantVisible)
        {
            StopDisplay();
        }

        _timedAssistantOwner = null;
        _timedAssistantMessage = string.Empty;
        _timedAssistantRemaining = 0f;
    }
}
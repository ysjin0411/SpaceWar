using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// 화면 상단에 시스템 알림 메시지를 표시하고 점멸시킨다.
/// 보스 패턴은 반환된 연출 시간만큼 기다린 후 다음 행동을 진행한다.
/// </summary>
public class SystemDialogueUI : MonoBehaviour
{
    [Header("시스템 알림 UI")]

    // 프레임과 자식 텍스트를 함께 점멸시키는 CanvasGroup이다.
    [SerializeField]
    CanvasGroup _dialogueCanvasGroup;

    // 시스템 알림 내용을 표시하는 TMP 텍스트다.
    [SerializeField]
    TMP_Text _dialogueText;

    [Header("점멸 설정")]

    // 한 번 밝아지거나 어두워지는 데 걸리는 시간이다.
    [Min(0.01f)]
    [SerializeField]
    float _blinkDuration = 0.15f;

    // 시스템 알림이 점멸하는 횟수다.
    [Min(1)]
    [SerializeField]
    int _blinkCount = 3;


    // 현재 실행 중인 시스템 알림 점멸 연출이다.
    Sequence _dialogueSequence;

    [Header("보스 연결")]

    // 현재 스테이지에서 생성되는 보스를 전달받는 관리자다.
    [SerializeField]
    BossEncounterManager _bossEncounterManager;

    // 현재 시스템 알림 이벤트가 연결된 보스다.
    BossEnemy _boundBoss;

    // 시스템 알림 메시지의 점멸이 완전히 끝났을 때 발생한다.
    // 보스는 이 이벤트를 받은 후 실제 특수 패턴을 이어서 실행한다.
    public event Action MessageBlinkCompleted;

    private void Awake()
    {
        if (_bossEncounterManager == null)
        {
            throw new InvalidOperationException(
                $"{name}에 BossEncounterManager가 설정되지 않았습니다.");
        }

        if (_dialogueCanvasGroup == null)
        {
            throw new InvalidOperationException(
                $"{name}에 Dialogue CanvasGroup이 설정되지 않았습니다.");
        }

        if (_dialogueText == null)
        {
            throw new InvalidOperationException(
                $"{name}에 Dialogue Text가 설정되지 않았습니다.");
        }

        // 게임 시작 시 시스템 알림을 숨긴다.
        _dialogueCanvasGroup.alpha = 0f;
    }

    /// <summary>
    /// 시스템 알림 메시지를 설정하고 지정된 횟수만큼 점멸시킨다.
    /// 점멸이 끝나면 MessageBlinkCompleted 이벤트를 발생시킨다.
    /// </summary>
    public void PlayMessage(
        string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException(
                "시스템 알림 메시지가 비어 있습니다.",
                nameof(message));
        }

        // 기존 메시지를 강제로 종료하면 기다리던 보스 패턴이
        // 영원히 완료 이벤트를 받지 못할 수 있으므로 중복 실행을 막는다.
        if (_dialogueSequence != null &&
            _dialogueSequence.IsActive())
        {
            throw new InvalidOperationException(
                "이미 다른 시스템 알림 메시지가 점멸 중입니다.");
        }

        // 이번에 출력할 시스템 알림 내용을 설정한다.
        _dialogueText.text = message;

        // 항상 투명한 상태에서 점멸을 시작한다.
        _dialogueCanvasGroup.alpha = 0f;

        _dialogueSequence = DOTween.Sequence();

        // 정신 혼란으로 게임이 멈춰 있어도 시스템 알림은 재생한다.
        _dialogueSequence.SetUpdate(true);

        // 투명 상태에서 나타났다가 다시 사라지는 동작을
        // 설정된 점멸 횟수만큼 반복한다.
        _dialogueSequence.Append(
            _dialogueCanvasGroup
                .DOFade(1f, _blinkDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(
                    _blinkCount * 2,
                    LoopType.Yoyo));

        _dialogueSequence.OnComplete(() =>
        {
            // 점멸 완료 후 시스템 알림을 완전히 숨긴다.
            _dialogueCanvasGroup.alpha = 0f;
            _dialogueSequence = null;

            // 기다리고 있는 보스에게 시스템 알림이 끝났음을 알린다.
            MessageBlinkCompleted?.Invoke();
        });
    }

    private void OnEnable()
    {
        // 새로운 보스가 생성되면 해당 보스의 시스템 알림 이벤트를 연결한다.
        _bossEncounterManager.BossSpawned +=
            HandleBossSpawned;

        // 페이즈 교체 시 이전 메시지를 정리하고 새 보스에 연결한다.
        _bossEncounterManager.BossReplaced += HandleBossReplaced;

        // 메시지 점멸 완료 이벤트를 보스에게 전달한다.
        MessageBlinkCompleted +=
            HandleMessageBlinkCompleted;

        // UI가 나중에 활성화된 경우 이미 생성된 보스와 연결한다.
        if (_bossEncounterManager.CurrentBoss != null)
        {
            HandleBossSpawned(
                _bossEncounterManager.CurrentBoss);
        }
    }

    private void OnDisable()
    {
        // 더 이상 새로운 보스 생성 이벤트를 받지 않는다.
        if (_bossEncounterManager != null)
        {
            _bossEncounterManager.BossSpawned -=
                HandleBossSpawned;

            // 페이즈 교체 알림 구독도 함께 해제한다.
            _bossEncounterManager.BossReplaced -= 
                HandleBossReplaced;
        }

        // 자체 점멸 완료 이벤트 구독을 해제한다.
        MessageBlinkCompleted -=
            HandleMessageBlinkCompleted;

        // 현재 연결된 보스의 이벤트를 해제한다.
        UnbindBoss();

        // UI가 비활성화되면 실행 중인 점멸 연출을 정리한다.
        _dialogueSequence?.Kill();
        _dialogueSequence = null;

        if (_dialogueCanvasGroup != null)
        {
            _dialogueCanvasGroup.alpha = 0f;
        }
    }

    /// <summary>
    /// 새로운 보스가 생성되면 시스템 알림 이벤트를 연결한다.
    /// </summary>
    private void HandleBossSpawned(
        BossEnemy spawnedBoss)
    {
        if (spawnedBoss == null)
        {
            return;
        }

        // 이전 보스가 연결되어 있다면 먼저 해제한다.
        UnbindBoss();

        _boundBoss = spawnedBoss;

        // 보스가 요청한 메시지를 SystemDialogueUI가 전달받는다.
        _boundBoss.SystemDialogueRequested +=
            HandleSystemDialogueRequested;

        // 현재 보스가 요청하는 경고 취소도 전달받는다.
        _boundBoss.SystemDialogueCancelRequested +=
            HandleSystemDialogueCancelRequested;

        // 보스가 처치되면 이벤트 연결을 해제한다.
        _boundBoss.Defeated +=
            HandleBossDefeated;
    }

    // 이전 페이즈의 메시지 완료 신호가 새 보스에게 전달되지 않게 한다.
    private void HandleBossReplaced(BossEnemy replacementBoss)
    {
        if (replacementBoss == null)
            return;

        // 완료 콜백을 실행하지 않고 이전 메시지 연출을 중단한다.
        _dialogueSequence?.Kill();
        _dialogueSequence = null;

        // 이전 페이즈의 경고 문구를 화면에서 숨긴다.
        if (_dialogueCanvasGroup != null)
        {
            _dialogueCanvasGroup.alpha = 0f;
        }

        // 기존 함수를 통해 이전 구독을 해제하고 본체에 연결한다.
        HandleBossSpawned(replacementBoss);
    }

    /// <summary>
    /// 보스가 요청한 시스템 알림 메시지를 점멸시킨다.
    /// </summary>
    private void HandleSystemDialogueRequested(
        string message)
    {
        PlayMessage(message);
    }

    // 패턴 취소 시 점멸을 중단하고 시스템 경고를 즉시 숨긴다.
    private void HandleSystemDialogueCancelRequested()
    {
        if (_dialogueSequence != null)
        {
            // 정상 완료 콜백을 실행하지 않고 연출만 종료한다.
            _dialogueSequence.Kill(false);
            _dialogueSequence = null;
        }

        if (_dialogueCanvasGroup != null)
        {
            _dialogueCanvasGroup.alpha = 0f;
        }

        // 취소를 정상 완료로 알리지 않는다.
        // 중단된 패턴이 경고 완료로 오인하여 공격을 시작하면 안 된다.
    }

    /// <summary>
    /// 시스템 알림 점멸이 끝나면 현재 연결된 보스에게 전달한다.
    /// </summary>
    private void HandleMessageBlinkCompleted()
    {
        if (_boundBoss == null ||
            _boundBoss.IsDead)
        {
            return;
        }

        // 기다리고 있는 보스 특수 패턴에 점멸 완료를 알린다.
        _boundBoss.NotifySystemDialogueCompleted();
    }

    /// <summary>
    /// 현재 연결된 보스가 처치되면 이벤트 연결을 해제한다.
    /// </summary>
    private void HandleBossDefeated(
        BossEnemy defeatedBoss)
    {
        if (defeatedBoss != _boundBoss)
        {
            return;
        }

        UnbindBoss();
    }

    /// <summary>
    /// 현재 보스와 연결된 시스템 알림 이벤트를 모두 해제한다.
    /// </summary>
    private void UnbindBoss()
    {
        if (_boundBoss == null)
        {
            return;
        }

        _boundBoss.SystemDialogueRequested -=
            HandleSystemDialogueRequested;

        // 이전 보스의 취소 요청이 새 보스의 경고에 영향을 주지 않게 한다.
        _boundBoss.SystemDialogueCancelRequested -=
            HandleSystemDialogueCancelRequested;

        _boundBoss.Defeated -=
            HandleBossDefeated;

        _boundBoss = null;
    }
}
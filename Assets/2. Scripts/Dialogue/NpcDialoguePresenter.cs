using UnityEngine;
using System.Collections.Generic;

// 대화 데이터와 View를 연결하고 대화 진행을 관리한다.
public class NpcDialoguePresenter : MonoBehaviour
{
    // 화면 표시와 클릭 전달을 담당하는 View.
    [SerializeField] private NpcDialogueUI _view;

    // 퀘스트 이름 목록과 복귀 버튼을 표시하는 View.
    [SerializeField] private NpcQuestListView _questListView;

    // 퀘스트 설명 완료 후 수락·거절을 표시할 View.
    [SerializeField] private NpcQuestDecisionView _decisionView;

    // NPC 기능 창을 열고 조작 잠금을 이어받을 Presenter.
    [SerializeField] private TownNpcActionPresenter _actionPresenter;

    // 초당 표시할 글자 수. Inspector에서 조절한다.
    [SerializeField, Min(1f)]
    private float _charactersPerSecond = 40f;

    // 현재 사용하는 대화 데이터와 대상.
    private DialogueData _data;
    private TownNpc _currentNpc;
    private TownPlayerController _lockedPlayer;

    // 현재 NPC에게 선택해서 대화할 수 있는 퀘스트 목록.
    private readonly List<QuestData> _dialogueQuests =
        new List<QuestData>();

    // 선택한 퀘스트. 없으면 일반 대화를 표시 중이다.
    private QuestData _selectedQuest;

    // 현재 줄과 글자 출력 진행 상태.
    private int _lineIndex;
    private int _characterCount;
    private int _visibleCharacters;
    private float _characterProgress;

    // 대화를 열 때 플레이어의 상호작용 키를 전달받는다.
    private KeyCode _interactionKey;

    // 같은 프레임의 클릭과 키 입력이 중복 처리되지 않도록 한다.
    private int _lastAdvanceFrame = -1;

    // 닫은 프레임에 같은 입력으로 다시 열리는 것을 막는다.
    private int _lastCloseFrame = -1;

    // 수락·거절 직후의 한 줄 응답 대사를 표시 중인지 구분한다.
    private bool _isDecisionResponse;

    // 외부에서 대화 진행 여부를 확인한다.
    public bool IsOpen { get; private set; }

    // 아직 현재 문장을 전부 표시하지 않았는지 확인한다.
    private bool IsTyping => _visibleCharacters < _characterCount;

    // 시작 가능한 퀘스트의 소개 대화를 끝까지 읽었을 때만 수락·거절을 허용한다.
    private bool CanDecideQuest =>
        IsOpen &&
        !_isDecisionResponse &&
        _selectedQuest != null &&
        QuestService.GetState(_selectedQuest) == QuestState.Available &&
        _data != null &&
        _data == _selectedQuest.OfferDialogue &&
        !IsTyping &&
        _lineIndex == _data.LineCount - 1;

    // 필요한 View 연결을 확인한다.
    private void Awake()
    {
        if (_view == null ||
            _questListView == null ||
            _decisionView == null)
        {
            Debug.LogError(
                "NpcDialoguePresenter의 View 연결을 모두 확인해주세요.", this);

            enabled = false;
        }
    }

    // 화면에서 발생한 입력을 구독한다.
    private void OnEnable()
    {
        if (_view != null)
        {
            // 대화 View에서 발생한 입력을 구독한다.
            _view.AdvanceRequested += Advance;
            _view.CloseRequested += Close;
            _view.ActionRequested += HandleActionRequested;
        }

        if (_questListView != null)
        {
            _questListView.QuestSelected += HandleQuestSelected;
            _questListView.BackRequested += ReturnToDefault;
        }

        // 수락은 저장 처리로, 거절은 일반 화면 복귀로 연결한다.
        if (_decisionView != null)
        {
            _decisionView.AcceptRequested += HandleAcceptQuest;
            // 거절 멘트를 표시하는 메서드로 연결한다.
            _decisionView.DeclineRequested += HandleDeclineQuest;
        }
    }

    // 입력 구독과 진행 중인 대화를 정리한다.
    private void OnDisable()
    {
        if (_view != null)
        {
            // 등록했던 대화 View 입력을 해제한다.
            _view.AdvanceRequested -= Advance;
            _view.CloseRequested -= Close;
            _view.ActionRequested -= HandleActionRequested;
        }

        if (_questListView != null)
        {
            _questListView.QuestSelected -= HandleQuestSelected;
            _questListView.BackRequested -= ReturnToDefault;
        }

        // 등록한 선택 이벤트를 해제한다.
        if (_decisionView != null)
        {
            _decisionView.AcceptRequested -= HandleAcceptQuest;
            // 등록한 동일한 메서드를 해제한다.
            _decisionView.DeclineRequested -= HandleDeclineQuest;
        }

        Close();
    }

    // 대화 상태와 입력을 확인하고 타이핑 및 결정 버튼을 갱신한다.
    private void Update()
    {
        if (!IsOpen)
        {
            return;
        }

        if (_view == null || !_view.IsVisible ||
            _currentNpc == null || !_currentNpc.isActiveAndEnabled ||
            _currentNpc.DialogueVisual == null ||
            _lockedPlayer == null || !_lockedPlayer.isActiveAndEnabled)
        {
            Close();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
            return;
        }

        if (Input.GetKeyDown(_interactionKey))
        {
            Advance();
        }

        if (IsTyping)
        {
            // 프레임 속도와 관계없이 일정한 속도로 글자를 표시한다.
            _characterProgress +=
                Mathf.Max(1f, _charactersPerSecond) * Time.unscaledDeltaTime;

            _visibleCharacters = Mathf.Min(
                _characterCount, Mathf.FloorToInt(_characterProgress));

            _view.SetVisibleCharacterCount(_visibleCharacters);
        }

        // 마지막 문장의 타이핑이 끝난 시점에 표시한다.
        _decisionView.SetVisible(CanDecideQuest);
    }

    // 일반 대화를 열고 현재 레벨에 맞는 퀘스트 목록을 표시한다.
    public bool TryOpen(
        TownNpc npc,
        TownPlayerController player,
        KeyCode interactionKey)
    {
        if (!isActiveAndEnabled || IsOpen ||
            Time.frameCount == _lastCloseFrame ||
            _view == null || !_view.IsReady || !_view.isActiveAndEnabled ||
            _questListView == null ||
            _decisionView == null || !_decisionView.IsReady ||
            !_decisionView.isActiveAndEnabled || 
            !_questListView.IsReady ||
            !_questListView.isActiveAndEnabled ||
            npc == null || !npc.isActiveAndEnabled ||
            player == null || !player.CanControl)
        {
            return false;
        }

        DialogueData data = npc.DefaultDialogue;

        if (data == null || !data.TryGetLine(0, out DialogueLine firstLine))
        {
            Debug.LogWarning(
                "NPC의 Default Dialogue와 라인 배열을 확인해주세요.", npc);

            return false;
        }

        if (npc.DialogueVisual == null)
        {
            Debug.LogWarning(
                "NPC의 Dialogue Visual을 연결해주세요.", npc);

            return false;
        }

        // 이번에 시작할 대화와 NPC, 플레이어를 저장한다.
        _data = data;
        _currentNpc = npc;
        _lockedPlayer = player;
        _interactionKey = interactionKey;

        // 새 대화는 퀘스트 선택과 응답 표시가 없는 상태로 시작한다.
        _selectedQuest = null;
        _isDecisionResponse = false; // 추가할 줄.

        // 첫 번째 대사부터 시작하고 같은 프레임의 중복 입력을 막는다.
        _lineIndex = 0;
        _lastAdvanceFrame = Time.frameCount;

        IsOpen = true;
        _lockedPlayer.SetControlEnabled(false);

        _view.Show(npc.DisplayName, npc.DialogueVisual);
        BeginLine(firstLine);

        RefreshQuestList();
        return true;
    }

    // 일반 대화에서 NPC 고유 기능을 실행한다.
    private void HandleActionRequested()
    {
        if (!IsOpen ||
            _selectedQuest != null ||
            _currentNpc == null ||
            _lockedPlayer == null)
        {
            return;
        }

        if (_actionPresenter == null)
        {
            Debug.LogError(
                "NpcDialoguePresenter의 Action Presenter를 연결해주세요.",
                this);

            return;
        }

        // 기능 창이 열리지 않았다면 현재 대화를 유지한다.
        if (!_actionPresenter.TryOpen(
                _currentNpc.ActionType, _lockedPlayer))
        {
            return;
        }

        // 기능 Presenter가 조작 잠금을 넘겨받았다.
        // 아래 Close()에서 조작을 풀지 않도록 참조를 비운다.
        _lockedPlayer = null;

        // 대화 UI와 대화 데이터만 정리한다.
        Close();
    }

    // 일반 대화에서는 NPC 고유 기능을 표시한다.
    // 퀘스트 설명 중이거나 기능이 없는 NPC라면 숨긴다.
    private void RefreshActionButton()
    {
        if (!IsOpen ||
            _currentNpc == null ||
            _selectedQuest != null)
        {
            _view.SetAction(string.Empty, false);
            return;
        }

        string label;

        switch (_currentNpc.ActionType)
        {
            case NpcActionType.Shop:
                label = "상점 이용하기";
                break;

            case NpcActionType.Stage:
                label = "스테이지 선택하기";
                break;

            case NpcActionType.Achievement:
                label = "업적 확인하기";
                break;

            default:
                _view.SetAction(string.Empty, false);
                return;
        }

        _view.SetAction(label, true);
    }

    // 새 대사를 시작하고 현재 대화 종류에 맞춰 버튼을 갱신한다.
    private void BeginLine(DialogueLine line)
    {
        // 퀘스트 수락·거절은 마지막 줄을 모두 출력한 뒤 표시한다.
        _decisionView.SetVisible(false);

        _visibleCharacters = 0;
        _characterProgress = 0f;
        _characterCount = _view.PrepareLine(line.Text);

        // 일반 대화 진입, 퀘스트 선택, 일반 대화 복귀에 모두 적용된다.
        RefreshActionButton();
    }

    // 시작 가능·진행 중·완료 상태에 맞는 퀘스트 버튼을 표시한다.
    private void RefreshQuestList()
    {
        _dialogueQuests.Clear();

        SaveManager saveManager = SaveManager.Instance;

        if (saveManager == null || !saveManager.IsLoggedIn)
        {
            _questListView.ShowQuests(_dialogueQuests);
            return;
        }

        foreach (QuestData quest in _currentNpc.Quests)
        {
            if (quest == null || _dialogueQuests.Contains(quest))
            {
                continue;
            }

            // 현재 상태에서 실제로 표시할 대화가 있는 퀘스트만 넣는다.
            if (TryGetQuestDialogue(quest, out _, out _))
            {
                _dialogueQuests.Add(quest);
            }
        }

        _questListView.ShowQuests(_dialogueQuests);
    }

    // 클릭 시점의 최신 퀘스트 상태를 확인해서 해당 대화를 시작한다.
    private void HandleQuestSelected(QuestData quest)
    {
        if (!IsOpen || _isDecisionResponse ||
            _selectedQuest != null ||
            quest == null || !_dialogueQuests.Contains(quest))
        {
            return;
        }

        if (!TryGetQuestDialogue(
                quest,
                out DialogueData dialogue,
                out DialogueLine firstLine))
        {
            RefreshQuestList();
            return;
        }

        _selectedQuest = quest;
        _data = dialogue;
        _lineIndex = 0;

        // 퀘스트 버튼을 누른 입력으로 첫 줄까지 넘기지 않는다.
        _lastAdvanceFrame = Time.frameCount;

        _questListView.ShowQuestDialogue();
        BeginLine(firstLine);
    }

    // 수락과 저장이 성공하면 해당 퀘스트의 수락 멘트를 표시한다.
    private void HandleAcceptQuest()
    {
        if (!CanDecideQuest ||
            Time.frameCount == _lastAdvanceFrame)
        {
            return;
        }

        // 같은 프레임에 발생한 중복 입력을 막는다.
        _lastAdvanceFrame = Time.frameCount;

        QuestData quest = _selectedQuest;

        if (!QuestService.TryAccept(quest, out string error))
        {
            // 저장 실패 시에는 응답 대사로 넘어가지 않고 재시도를 허용한다.
            _decisionView.ShowError(error);
            return;
        }

        BeginDecisionResponse(quest.AcceptResponse);
    }

    // 거절은 진행 상태를 변경하지 않고 거절 멘트만 표시한다.
    private void HandleDeclineQuest()
    {
        if (!CanDecideQuest ||
            Time.frameCount == _lastAdvanceFrame)
        {
            return;
        }

        _lastAdvanceFrame = Time.frameCount;

        BeginDecisionResponse(_selectedQuest.DeclineResponse);
    }

    // Config의 문자열을 기존 대화창에서 한 줄 대사로 재생한다.
    private void BeginDecisionResponse(string message)
    {

        _isDecisionResponse = true;

        // 이번 대사는 DialogueData의 줄 배열을 사용하지 않는다.
        _data = null;
        _lineIndex = 0;
        _lastAdvanceFrame = Time.frameCount;

        // 응답 중에는 목록과 뒤로가기 버튼을 숨긴다.
        _questListView.ResetView();

        // 선택 퀘스트는 유지하므로 NPC 기능 버튼도 숨겨진 상태를 유지한다.
        // 기존 타이핑 기능과 NPC 애니메이션을 그대로 사용한다.
        BeginLine(new DialogueLine(message));
    }

    // 수락 상태를 변경하지 않고 일반 대화와 퀘스트 목록으로 돌아간다.
    private void ReturnToDefault()
    {
        if (!IsOpen || _selectedQuest == null || _isDecisionResponse)
        {
            return;
        }

        DialogueData data = _currentNpc.DefaultDialogue;

        if (data == null || !data.TryGetLine(0, out DialogueLine firstLine))
        {
            Close();
            return;
        }

        // 일반 대화로 복귀했으므로 수락·거절 응답 모드를 끝낸다.
        _isDecisionResponse = false;

        _selectedQuest = null;
        _data = data;
        _lineIndex = 0;
        _lastAdvanceFrame = Time.frameCount;

        BeginLine(firstLine);
        RefreshQuestList();
    }

    // 타이핑 중에는 문장을 완성하고, 일반 대화에서는 다음 줄로 진행한다.
    // 수락·거절 응답이 완성되면 현재 메시지를 유지한다.
    private void Advance()
    {
        // 대화가 닫혀 있거나 같은 프레임에 이미 처리한 입력이면 무시한다.
        if (!IsOpen || Time.frameCount == _lastAdvanceFrame)
        {
            return;
        }

        _lastAdvanceFrame = Time.frameCount;

        // 출력 중인 문장은 클릭 한 번으로 모두 표시한다.
        if (IsTyping)
        {
            _visibleCharacters = _characterCount;
            _characterProgress = _characterCount;
            _view.SetVisibleCharacterCount(_characterCount);
            return;
        }

        // 수락·거절 메시지 뒤에는 다른 대화로 진행하지 않는다.
        // 대화 종료는 기존 닫기 처리에서 담당한다.
        if (_isDecisionResponse)
        {
            return;
        }

        // 일반 대화와 퀘스트 소개 대화만 다음 줄로 진행한다.
        if (_data != null &&
            _data.TryGetLine(_lineIndex + 1, out DialogueLine nextLine))
        {
            _lineIndex++;
            BeginLine(nextLine);
        }
    }

    // 대화와 퀘스트 목록을 정리하고 플레이어 조작을 복원한다.
    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;

        // 대화가 닫힐 때 수락·거절 영역도 정리한다.
        if (_decisionView != null)
        {
            _decisionView.SetVisible(false);
        }

        _lastCloseFrame = Time.frameCount;

        if (_questListView != null && _questListView.IsReady)
        {
            _questListView.ResetView();
        }

        if (_view != null)
        {
            _view.Hide();
        }

        if (_lockedPlayer != null)
        {
            _lockedPlayer.SetControlEnabled(true);
        }

        // 대화가 끝났으므로 목록과 선택 상태를 정리한다.
        _dialogueQuests.Clear();
        _selectedQuest = null;
        _isDecisionResponse = false; // 추가할 줄.

        // 이전 대화에서 사용한 참조를 해제한다.
        _data = null;
        _currentNpc = null;
        _lockedPlayer = null;
    }

    // 현재 퀘스트 상태에 맞는 대화와 첫 번째 줄을 가져온다.
    private bool TryGetQuestDialogue(
        QuestData quest,
        out DialogueData dialogue,
        out DialogueLine firstLine)
    {
        dialogue = null;
        firstLine = default;

        if (quest == null)
        {
            return false;
        }

        // 저장된 상태에 따라 사용할 Config를 선택한다.
        switch (QuestService.GetState(quest))
        {
            case QuestState.Available:
                dialogue = quest.OfferDialogue;
                break;

            case QuestState.InProgress:
                dialogue = quest.InProgressDialogue;
                break;

            case QuestState.ReadyToTurnIn:
            case QuestState.RewardClaimed:
                dialogue = quest.CompletionDialogue;
                break;

            default:
                // 잠긴 퀘스트는 대화 목록에 표시하지 않는다.
                return false;
        }

        // 누락된 Config를 다른 대화로 대체하지 않고 설정 오류를 알린다.
        if (dialogue == null ||
            !dialogue.TryGetLine(0, out firstLine))
        {
            Debug.LogWarning(
                $"퀘스트 '{quest.QuestName}'의 현재 상태에 해당하는 Dialogue와 Lines를 확인해주세요.",
                quest);

            return false;
        }

        return true;
    }
}
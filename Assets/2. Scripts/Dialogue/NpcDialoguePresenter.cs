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

    // 현재 일반 화면에서 선택할 수 있는 퀘스트 목록.
    private readonly List<QuestData> _availableQuests =
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

    // 외부에서 대화 진행 여부를 확인한다.
    public bool IsOpen { get; private set; }

    // 아직 현재 문장을 전부 표시하지 않았는지 확인한다.
    private bool IsTyping => _visibleCharacters < _characterCount;

    // 선택한 퀘스트의 마지막 문장을 모두 표시했을 때만 결정할 수 있다.
    private bool CanDecideQuest =>
        IsOpen &&
        _selectedQuest != null &&
        _data != null &&
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
            _decisionView.DeclineRequested += ReturnToDefault;
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
            _decisionView.DeclineRequested -= ReturnToDefault;
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

        _data = data;
        _currentNpc = npc;
        _lockedPlayer = player;
        _interactionKey = interactionKey;
        _selectedQuest = null;
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

    // 일반 화면에서 표시할 퀘스트 목록을 현재 User 레벨로 구성한다.
    private void RefreshQuestList()
    {
        _availableQuests.Clear();

        SaveManager saveManager = SaveManager.Instance;

        // 로그인하지 않았다면 사용자 레벨을 확정할 수 없으므로 숨긴다.
        if (saveManager == null || !saveManager.IsLoggedIn)
        {
            _questListView.ShowQuests(_availableQuests);
            return;
        }

        for (int i = 0; i < _currentNpc.Quests.Count; i++)
        {
            QuestData quest = _currentNpc.Quests[i];

            // 레벨뿐 아니라 이미 수락했는지도 함께 검사한다.
            if (quest == null ||
                QuestService.GetState(quest) != QuestState.Available ||
                _availableQuests.Contains(quest))
            {
                continue;
            }

            // 눌러도 재생할 대사가 없는 항목은 표시하지 않는다.
            if (quest.OfferDialogue == null ||
                !quest.OfferDialogue.TryGetLine(0, out DialogueLine firstLine))
            {
                continue;
            }

            _availableQuests.Add(quest);
        }

        _questListView.ShowQuests(_availableQuests);
    }

    // 일반 대화에서 선택한 퀘스트의 소개 대화로 전환한다.
    private void HandleQuestSelected(QuestData quest)
    {
        if (!IsOpen || _selectedQuest != null ||
            quest == null || !_availableQuests.Contains(quest))
        {
            return;
        }

        // 목록 표시 후 상태가 바뀌었을 수 있으므로 다시 검사한다.
        if (QuestService.GetState(quest) != QuestState.Available)
        {
            RefreshQuestList();
            return;
        }

        DialogueData offer = quest.OfferDialogue;

        if (offer == null || !offer.TryGetLine(0, out DialogueLine firstLine))
        {
            return;
        }

        _selectedQuest = quest;
        _data = offer;
        _lineIndex = 0;

        // 퀘스트를 고른 입력으로 첫 문장까지 넘기지 않게 한다.
        _lastAdvanceFrame = Time.frameCount;

        _questListView.ShowQuestDialogue();
        BeginLine(firstLine);
    }

    // 마지막 설명까지 읽은 퀘스트를 수락한다.
    private void HandleAcceptQuest()
    {
        if (!CanDecideQuest ||
            Time.frameCount == _lastAdvanceFrame)
        {
            return;
        }

        if (!QuestService.TryAccept(_selectedQuest, out string error))
        {
            // 실패하면 설명과 버튼을 유지해서 다시 시도할 수 있게 한다.
            _decisionView.ShowError(error);
            return;
        }

        // 저장 후 일반 화면으로 돌아가 목록을 다시 계산한다.
        // 수락한 퀘스트는 Available이 아니므로 목록에서 제외된다.
        ReturnToDefault();
    }

    // 수락 상태를 변경하지 않고 일반 대화와 퀘스트 목록으로 돌아간다.
    private void ReturnToDefault()
    {
        if (!IsOpen || _selectedQuest == null)
        {
            return;
        }

        DialogueData data = _currentNpc.DefaultDialogue;

        if (data == null || !data.TryGetLine(0, out DialogueLine firstLine))
        {
            Close();
            return;
        }

        _selectedQuest = null;
        _data = data;
        _lineIndex = 0;
        _lastAdvanceFrame = Time.frameCount;

        BeginLine(firstLine);
        RefreshQuestList();
    }

    // 출력 중이면 문장 완성, 출력이 끝났으면 다음 문장으로 이동한다.
    private void Advance()
    {
        if (!IsOpen || Time.frameCount == _lastAdvanceFrame)
        {
            return;
        }

        _lastAdvanceFrame = Time.frameCount;

        if (IsTyping)
        {
            _visibleCharacters = _characterCount;
            _characterProgress = _characterCount;
            _view.SetVisibleCharacterCount(_characterCount);

            // 이번 입력은 문장 완성에만 사용한다.
            return;
        }

        if (_data.TryGetLine(_lineIndex + 1, out DialogueLine nextLine))
        {
            _lineIndex++;
            BeginLine(nextLine);
        }

        // 마지막 문장이면 내용을 유지한다.
        // 이용하기·퀘스트 선택 항목은 다음 단계에서 연결한다.
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

        _availableQuests.Clear();
        _selectedQuest = null;
        _data = null;
        _currentNpc = null;
        _lockedPlayer = null;
    }
}
using System.Collections.Generic;
using UnityEngine;

// 퀘스트 상태를 읽어 탭별 목록과 상세 정보를 View에 전달한다.
public class QuestJournalPresenter : MonoBehaviour
{
    // 퀘스트창 표시 담당.
    [SerializeField] private QuestJournalView _view;

    // 전체 퀘스트 설정 목록.
    [SerializeField] private QuestDatabase _database;

    // 창을 여는 동안 조작을 잠글 플레이어.
    [SerializeField] private TownPlayerController _player;

    // 퀘스트를 제공하는 마을 NPC들.
    // 각 NPC의 Quests 목록에서 선택한 퀘스트의 제공자를 찾는다.
    [SerializeField] private TownNpc[] _questNpcs = new TownNpc[0];

    // 퀘스트 포기 여부를 묻는 확인창.
    [SerializeField] private ConfirmationDialogUI _confirmationDialog;

    // 현재 탭에 표시할 퀘스트들.
    private readonly List<QuestData> _visibleQuests = new List<QuestData>();

    // Database에 같은 ID가 중복 등록돼도 목록에는 한 번만 표시한다.
    private readonly HashSet<string> _listedIds = new HashSet<string>();

    // 현재 선택한 퀘스트와 탭.
    private QuestData _selectedQuest;
    private QuestState _tab = QuestState.Available;

    // 창이 열렸는지와 마지막으로 확인한 User 레벨.
    private bool _isOpen;
    private int _lastUserLevel;

    // 필수 연결을 확인한다.
    private void Awake()
    {
        if (_view == null || _database == null || _player == null || _confirmationDialog == null)
        {
            Debug.LogError(
                "QuestJournalPresenter의 Inspector 연결을 확인해주세요.",
                this);

            enabled = false;
        }
    }

    // 목록 클릭과 저장 완료된 퀘스트 변경을 구독한다.
    private void OnEnable()
    {
        if (_view != null)
        {
            _view.QuestSelected += SelectQuest;
            // 포기 버튼 입력을 구독한다.
            _view.AbandonRequested += HandleAbandonRequested;
        }

        QuestService.QuestChanged += HandleQuestChanged;
    }

    // 닫기 입력과 열린 동안의 레벨 변경을 확인한다.
    private void Update()
    {
        if (!_isOpen)
        {
            return;
        }

        SaveManager saveManager = SaveManager.Instance;

        if (!_view.IsVisible ||
            _player == null || !_player.isActiveAndEnabled ||
            saveManager == null || !saveManager.IsLoggedIn)
        {
            Close();
            return;
        }

        // 확인창 응답을 기다리는 동안 뒤쪽 퀘스트창의 Esc 입력을 막는다.
        if (_confirmationDialog.IsOpen)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
            return;
        }

        // User 레벨이 바뀌면 시작 가능 목록도 다시 계산한다.
        if (_lastUserLevel != saveManager.CurrentUserLevel)
        {
            RefreshList();
        }
    }

    // 퀘스트 버튼에서 호출한다.
    public void Open()
    {
        SaveManager saveManager = SaveManager.Instance;

        if (!isActiveAndEnabled || _isOpen ||
            !_view.IsReady || !_view.isActiveAndEnabled ||
            !_player.isActiveAndEnabled || !_player.CanControl ||
            saveManager == null || !saveManager.IsLoggedIn)
        {
            return;
        }

        // 다른 대화창이 조작을 잠근 동안에는 열지 않는다.
        _isOpen = true;
        _player.SetControlEnabled(false);

        _view.Show();
        ShowAvailable();
    }

    // 시작 가능 탭의 버튼에서 호출한다.
    public void ShowAvailable()
    {
        ChangeTab(QuestState.Available);
    }

    // 진행 중 탭의 버튼에서 호출한다.
    public void ShowActive()
    {
        ChangeTab(QuestState.InProgress);
    }

    // 완료 탭의 버튼에서 호출한다.
    public void ShowCompleted()
    {
        ChangeTab(QuestState.RewardClaimed);
    }

    // 목록을 전환하면서 현재 탭의 선택 표시를 갱신한다.
    private void ChangeTab(QuestState tab)
    {
        if (!_isOpen)
        {
            return;
        }

        _tab = tab;
        _selectedQuest = null;

        // 상세 패널을 클릭해도 현재 탭의 강조가 유지된다.
        _view.SetSelectedTab(_tab);

        RefreshList();
    }

    // 전체 Config와 사용자 저장 상태를 조합해서 목록을 만든다.
    private void RefreshList()
    {
        _visibleQuests.Clear();
        _listedIds.Clear();

        _lastUserLevel = SaveManager.Instance.CurrentUserLevel;

        for (int i = 0; i < _database.Quests.Count; i++)
        {
            QuestData quest = _database.Quests[i];

            if (quest == null ||
                string.IsNullOrWhiteSpace(quest.Id) ||
                !_listedIds.Add(quest.Id))
            {
                continue;
            }

            QuestState state = QuestService.GetState(quest);

            // 보상 미수령 퀘스트도 진행 중 탭에서 확인한다.
            bool matches = state == _tab ||
                (_tab == QuestState.InProgress &&
                 state == QuestState.ReadyToTurnIn);

            if (matches)
            {
                _visibleQuests.Add(quest);
            }
        }

        // 현재 탭에 맞는 슬롯을 다시 생성한다.
        _view.RenderList(_visibleQuests);

        // 사용자가 선택한 퀘스트가 현재 목록에도 있을 때만 복원한다.
        if (_selectedQuest != null &&
            _visibleQuests.Contains(_selectedQuest))
        {
            SelectQuest(_selectedQuest);
        }
        else
        {
            // 첫 항목을 자동 선택하지 않고 클릭을 기다린다.
            _selectedQuest = null;
            _view.SetSelectedQuest(null);
            _view.HideDetails();
        }
    }

    // 선택한 퀘스트의 실제 진행 상태에 맞춰 상세 정보를 표시한다.
    private void SelectQuest(QuestData quest)
    {
        if (!_isOpen || quest == null || !_visibleQuests.Contains(quest))
        {
            return;
        }

        _selectedQuest = quest;

        // 현재 플레이어의 진행 상태를 한 번 조회한다.
        QuestState state = QuestService.GetState(quest);

        string stateText;

        switch (state)
        {
            case QuestState.Available:
                stateText = "시작가능";
                break;

            case QuestState.InProgress:
                stateText = "진행중";
                break;

            case QuestState.ReadyToTurnIn:
                stateText = "목표 달성";
                break;

            case QuestState.RewardClaimed:
                stateText = "완료";
                break;

            default:
                stateText = "수락 불가";
                break;
        }

        // 선택한 슬롯 하나만 파란색으로 표시한다.
        _view.SetSelectedQuest(quest);

        // 해당 퀘스트를 제공하는 NPC의 현재 애니메이션 대상을 찾는다.
        SpriteRenderer npcVisual = FindQuestNpcVisual(quest);

        // 실제 상태에 맞는 설명을 Config에서 가져온다.
        string description = quest.GetDescription(state);

        _view.ShowDetails(quest, stateText, description, npcVisual);

        // 진행 중 탭에 속하는 두 상태에서 받을 보상을 표시한다.
        bool showRewards =
            state == QuestState.InProgress ||
            state == QuestState.ReadyToTurnIn;

        _view.SetRewards(quest, showRewards);

        // 새로 선택한 퀘스트에는 이전 오류를 표시하지 않는다.
        _view.ShowAbandonError(string.Empty);

        // 실제 상태가 진행 중일 때만 포기를 허용한다.
        // 같은 진행 중 탭에 포함되는 '목표 달성' 상태는 제외한다.
        _view.SetAbandonAvailable(state == QuestState.InProgress);
    }

    // 저장 상태 변경이 발생하면 열린 창을 갱신한다.
    private void HandleQuestChanged(string questId)
    {
        if (_isOpen)
        {
            RefreshList();
        }
    }

    // 닫기 버튼과 Esc에서 공통으로 사용한다.
    public void Close()
    {
        if (!_isOpen)
        {
            return;
        }

        _isOpen = false;

        // 퀘스트창이 닫히면 남아 있는 확인 요청도 취소한다.
        if (_confirmationDialog != null)
        {
            _confirmationDialog.Cancel();
        }

        if (_view != null && _view.IsReady)
        {
            _view.Hide();
        }

        if (_player != null)
        {
            _player.SetControlEnabled(true);
        }

        _selectedQuest = null;
    }

    // 창 관리자가 꺼져도 구독과 조작 잠금이 남지 않게 한다.
    private void OnDisable()
    {
        if (_view != null)
        {
            _view.QuestSelected -= SelectQuest;
            // 포기 버튼 입력 구독을 해제한다.
            _view.AbandonRequested -= HandleAbandonRequested;
        }

        QuestService.QuestChanged -= HandleQuestChanged;
        Close();
    }

    // 선택한 퀘스트를 담당하는 NPC의 애니메이션 표시 대상을 찾는다.
    private SpriteRenderer FindQuestNpcVisual(QuestData quest)
    {
        foreach (TownNpc npc in _questNpcs)
        {
            if (npc == null)
            {
                continue;
            }

            foreach (QuestData npcQuest in npc.Quests)
            {
                if (npcQuest == quest)
                {
                    return npc.DialogueVisual;
                }
            }
        }

        // NPC 연결이 없어도 제목과 설명은 표시하고 초상화만 비운다.
        Debug.LogWarning(
            $"퀘스트 '{quest.QuestName}'의 NPC 연결을 확인해주세요.", this);

        return null;
    }

    // 오른쪽 접기 버튼에서 호출한다.
    public void CollapseDetails()
    {
        if (!_isOpen)
        {
            return;
        }

        // 이후 목록이 갱신되어도 접은 상세 패널을 자동으로 열지 않는다.
        _selectedQuest = null;
        _view.SetSelectedQuest(null);
        _view.HideDetails();
    }

    // 선택한 퀘스트를 포기할지 기존 예/아니오 확인창으로 묻는다.
    private void HandleAbandonRequested()
    {
        if (!_isOpen || _selectedQuest == null ||
            _confirmationDialog.IsOpen)
        {
            return;
        }

        // 확인창을 연 뒤 선택 상태가 바뀌어도 원래 퀘스트를 대상으로 한다.
        QuestData quest = _selectedQuest;

        if (QuestService.GetState(quest) != QuestState.InProgress)
        {
            RefreshList();
            return;
        }

        _view.ShowAbandonError(string.Empty);

        _confirmationDialog.Show(
            $"'{quest.QuestName}' 퀘스트를 정말 포기하시겠습니까?\n" +
            "현재 진행 기록이 초기화되며 다시 수락할 수 있습니다.",
            accepted =>
            {
                // 아니오를 선택하거나 퀘스트창이 닫혔다면 변경하지 않는다.
                if (!accepted || !_isOpen)
                {
                    return;
                }

                // 예를 눌렀을 때 상태를 다시 검사하고 저장까지 처리한다.
                if (!QuestService.TryAbandon(quest, out string error))
                {
                    _view.ShowAbandonError(error);
                    return;
                }

                // 포기한 퀘스트를 시작 가능 탭에서 바로 확인한다.
                ShowAvailable();
                SelectQuest(quest);
            });
    }
}
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
        if (_view == null || _database == null || _player == null)
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

    // 탭을 바꾸면 해당 목록의 첫 항목을 선택한다.
    private void ChangeTab(QuestState tab)
    {
        if (!_isOpen)
        {
            return;
        }

        _tab = tab;
        _selectedQuest = null;
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

        _view.RenderList(_visibleQuests);

        if (_visibleQuests.Count == 0)
        {
            _selectedQuest = null;
            _view.ShowDetails(
                string.Empty, string.Empty,
                "이 목록에 해당하는 퀘스트가 없습니다.");

            return;
        }

        // 상태 갱신 후에도 기존 선택이 남아 있으면 유지한다.
        if (_selectedQuest == null || !_visibleQuests.Contains(_selectedQuest))
        {
            _selectedQuest = _visibleQuests[0];
        }

        SelectQuest(_selectedQuest);
    }

    // 선택한 퀘스트의 이름, 상태, 설명을 표시한다.
    private void SelectQuest(QuestData quest)
    {
        if (!_isOpen || quest == null || !_visibleQuests.Contains(quest))
        {
            return;
        }

        _selectedQuest = quest;

        string stateText;

        switch (QuestService.GetState(quest))
        {
            case QuestState.Available:
                stateText = $"시작 가능 · 필요 User 레벨 {quest.RequiredUserLevel}";
                break;

            case QuestState.InProgress:
                stateText = "진행 중";
                break;

            case QuestState.ReadyToTurnIn:
                stateText = "목표 달성 · 보상 수령 가능";
                break;

            case QuestState.RewardClaimed:
                stateText = "완료";
                break;

            default:
                stateText = "시작 불가";
                break;
        }

        _view.ShowDetails(quest.QuestName, stateText, quest.Description);
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
        }

        QuestService.QuestChanged -= HandleQuestChanged;
        Close();
    }
}
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 퀘스트 목록과 선택한 퀘스트의 상세 화면을 표시한다.
public class QuestJournalView : MonoBehaviour
{
    // 퀘스트 창 전체 루트.
    [SerializeField] private GameObject _root;

    // 스크롤 Viewport 아래의 QuestJournalContent.
    [SerializeField] private Transform _content;

    // 생성할 QuestSlot 프리팹의 QuestSlotView.
    [SerializeField] private QuestSlotView _rowPrefab;

    // 목록의 스크롤 위치를 처음으로 되돌릴 ScrollRect.
    [SerializeField] private ScrollRect _scrollRect;

    // 오른쪽 상세 패널 전체.
    [SerializeField] private GameObject _informationPanel;

    // 상세 패널에서 퀘스트 정보를 표시하는 텍스트.
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _stateText;
    [SerializeField] private TMP_Text _descriptionText;

    // 상세 패널에서 NPC 애니메이션 프레임을 표시할 Image.
    [SerializeField] private Image _npcImage;

    // 시작가능 목록으로 전환하는 탭 버튼.
    [SerializeField] private Button _availableTab;

    // 진행중 목록으로 전환하는 탭 버튼.
    [SerializeField] private Button _inProgressTab;

    // 완료 목록으로 전환하는 탭 버튼.
    [SerializeField] private Button _completedTab;

    // 현재 보고 있는 탭에 적용할 밝은 청록색.
    [SerializeField]
    private Color _selectedTabColor =
        new Color(0.35f, 0.85f, 1f, 1f);

    // 선택하지 않은 탭에 적용할 어두운 회색.
    [SerializeField]
    private Color _unselectedTabColor =
        new Color(0.5f, 0.5f, 0.5f, 1f);

    // 퀘스트와 생성한 슬롯을 연결해 선택 색상을 변경한다.
    private readonly Dictionary<QuestData, QuestSlotView> _rows =
        new Dictionary<QuestData, QuestSlotView>();

    // 현재 상세 화면에 표시하는 NPC의 SpriteRenderer.
    private SpriteRenderer _portraitSource;

    // NPCImage의 원래 크기를 유지하기 위한 Scale.
    private Vector3 _imageScale;

    // 필수 연결이 정상적으로 준비되었는지 알린다.
    public bool IsReady { get; private set; }

    // 퀘스트 창이 실제로 보이는지 확인한다.
    public bool IsVisible =>
        isActiveAndEnabled &&
        _root != null &&
        _root.activeInHierarchy;

    // 클릭한 퀘스트를 Presenter에 전달한다.
    public event Action<QuestData> QuestSelected;

    // Inspector 연결을 검사하고 창을 숨긴다.
    private void Awake()
    {
        if (_root == null ||
            _content == null ||
            _rowPrefab == null ||
            !_rowPrefab.IsConfigured ||
            _scrollRect == null ||
            _informationPanel == null ||
            _nameText == null ||
            _stateText == null ||
            _descriptionText == null ||
            _npcImage == null)
        {
            Debug.LogError(
                "QuestJournalView의 Inspector 연결을 확인해주세요.", this);

            enabled = false;
            return;
        }

        _imageScale = _npcImage.rectTransform.localScale;
        _npcImage.preserveAspect = true;
        _npcImage.raycastTarget = false;

        HideDetails();
        _root.SetActive(false);
        IsReady = true;
    }

    // 창을 열 때 상세 패널은 접힌 상태로 시작한다.
    public void Show()
    {
        HideDetails();
        _root.SetActive(true);
    }

    // 현재 탭에 해당하는 퀘스트 슬롯들을 생성한다.
    public void RenderList(IReadOnlyList<QuestData> quests)
    {
        HideDetails();
        ClearRows();

        foreach (QuestData quest in quests)
        {
            if (quest == null || _rows.ContainsKey(quest))
            {
                continue;
            }

            QuestSlotView row = Instantiate(_rowPrefab, _content);

            // 제목과 클릭 시 전달할 퀘스트를 연결한다.
            row.Bind(quest, RequestSelection);
            row.gameObject.SetActive(true);

            _rows.Add(quest, row);
        }

        // 슬롯 배치와 Content 높이를 반영한 뒤 목록 맨 위로 이동한다.
        Canvas.ForceUpdateCanvases();
        _scrollRect.verticalNormalizedPosition = 1f;
    }

    // 선택한 슬롯 하나만 파랗게 표시한다.
    public void SetSelectedQuest(QuestData selectedQuest)
    {
        foreach (KeyValuePair<QuestData, QuestSlotView> pair in _rows)
        {
            if (pair.Value != null)
            {
                pair.Value.SetSelected(pair.Key == selectedQuest);
            }
        }
    }

    // Presenter가 선택한 상태별 설명과 NPC를 상세 패널에 표시한다.
    public void ShowDetails(
        QuestData quest,
        string stateText,
        string description,
        SpriteRenderer portraitSource)
    {
        // 제목은 퀘스트 상태가 바뀌어도 동일하게 유지한다.
        _nameText.text = quest.QuestName;

        // 상태 표시와 상세 설명은 Presenter가 전달한 내용을 사용한다.
        _stateText.text = stateText;
        _descriptionText.text = description;

        _portraitSource = portraitSource;

        _informationPanel.SetActive(true);
        RefreshPortrait();
    }

    // 오른쪽 상세 패널만 접는다.
    public void HideDetails()
    {
        ClearSelectionInside(_informationPanel.transform);

        _portraitSource = null;
        _npcImage.sprite = null;
        _npcImage.enabled = false;

        _informationPanel.SetActive(false);
    }

    // NPC의 애니메이션이 반영된 스프라이트를 UI에 복사한다.
    private void LateUpdate()
    {
        if (IsVisible && _informationPanel.activeInHierarchy)
        {
            RefreshPortrait();
        }
    }

    // NPC의 위치나 크기는 변경하지 않고 현재 프레임만 표시한다.
    private void RefreshPortrait()
    {
        Sprite sprite =
            _portraitSource != null ? _portraitSource.sprite : null;

        _npcImage.sprite = sprite;
        _npcImage.enabled = sprite != null;

        // 기존 대화창처럼 원본 스프라이트 방향과 UI 크기를 유지한다.
        Vector3 scale = _imageScale;
        scale.x = Mathf.Abs(scale.x);
        scale.y = Mathf.Abs(scale.y);
        _npcImage.rectTransform.localScale = scale;
    }

    // 퀘스트 창 전체를 닫고 생성한 슬롯을 정리한다.
    public void Hide()
    {
        ClearSelectionInside(_root.transform);
        HideDetails();
        ClearRows();

        _root.SetActive(false);
    }

    // 이 View가 생성한 슬롯만 제거한다.
    private void ClearRows()
    {
        ClearSelectionInside(_content);

        foreach (QuestSlotView row in _rows.Values)
        {
            if (row == null)
            {
                continue;
            }

            // Destroy가 실제 처리되기 전에 레이아웃에서 제외한다.
            row.gameObject.SetActive(false);
            Destroy(row.gameObject);
        }

        _rows.Clear();
    }

    // 숨기거나 삭제할 UI에 키보드 선택이 남지 않도록 정리한다.
    private void ClearSelectionInside(Transform parent)
    {
        EventSystem eventSystem = EventSystem.current;

        if (eventSystem == null)
        {
            return;
        }

        GameObject selected = eventSystem.currentSelectedGameObject;

        if (selected != null &&
            selected.transform.IsChildOf(parent))
        {
            eventSystem.SetSelectedGameObject(null);
        }
    }

    // 슬롯에서 받은 클릭을 Presenter에 전달한다.
    private void RequestSelection(QuestData quest)
    {
        QuestSelected?.Invoke(quest);
    }

    // 현재 목록에 해당하는 탭만 밝게 표시한다.
    public void SetSelectedTab(QuestState tab)
    {
        ApplyTabColor(_availableTab, tab == QuestState.Available);
        ApplyTabColor(_inProgressTab, tab == QuestState.InProgress);
        ApplyTabColor(_completedTab, tab == QuestState.RewardClaimed);
    }

    // 버튼 포커스와 관계없이 탭의 선택 색상을 유지한다.
    private void ApplyTabColor(Button button, bool selected)
    {
        if (button == null || button.targetGraphic == null)
        {
            Debug.LogError("퀘스트 탭의 Button과 Target Graphic을 연결해주세요.", this);
            return;
        }

        // 기본 Color Tint가 직접 지정한 색상을 덮어쓰지 않게 한다.
        button.transition = Selectable.Transition.None;

        button.targetGraphic.color =
            selected ? _selectedTabColor : _unselectedTabColor;
    }
}
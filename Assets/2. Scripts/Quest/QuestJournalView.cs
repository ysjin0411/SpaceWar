using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 퀘스트창의 목록과 상세 설명을 표시한다.
public class QuestJournalView : MonoBehaviour
{
    // 퀘스트창 전체 루트.
    [SerializeField] private GameObject _root;

    // 목록 버튼이 생성될 영역과 버튼 프리팹.
    [SerializeField] private Transform _content;
    [SerializeField] private Button _rowPrefab;

    // 선택한 퀘스트의 상세 표시.
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _stateText;
    [SerializeField] private TMP_Text _descriptionText;

    // 이 View가 만든 목록 버튼들.
    private readonly List<Button> _rows = new List<Button>();

    public bool IsReady { get; private set; }

    // 외부에서 루트가 꺼진 경우까지 확인한다.
    public bool IsVisible =>
        isActiveAndEnabled && _root != null && _root.activeInHierarchy;

    // 클릭한 퀘스트를 Presenter에 전달한다.
    public event Action<QuestData> QuestSelected;

    // 필수 연결을 확인하고 창을 숨긴다.
    private void Awake()
    {
        if (_root == null || _content == null || _rowPrefab == null ||
            _nameText == null || _stateText == null ||
            _descriptionText == null ||
            _rowPrefab.GetComponentInChildren<TMP_Text>(true) == null)
        {
            Debug.LogError(
                "QuestJournalView의 Inspector 연결을 확인해주세요.", this);

            enabled = false;
            return;
        }

        _root.SetActive(false);
        IsReady = true;
    }

    // 창을 표시한다.
    public void Show()
    {
        _root.SetActive(true);
    }

    // Presenter가 골라준 퀘스트를 이름 버튼으로 표시한다.
    public void RenderList(IReadOnlyList<QuestData> quests)
    {
        ClearRows();

        for (int i = 0; i < quests.Count; i++)
        {
            QuestData quest = quests[i];

            Button row = Instantiate(_rowPrefab, _content);
            TMP_Text label = row.GetComponentInChildren<TMP_Text>(true);

            label.text = quest.QuestName;
            row.onClick.AddListener(() => RequestSelection(quest));

            row.gameObject.SetActive(true);
            _rows.Add(row);
        }
    }

    // 상태 판단 없이 전달받은 상세 내용을 표시한다.
    public void ShowDetails(string questName, string state, string description)
    {
        _nameText.text = questName;
        _stateText.text = state;
        _descriptionText.text = description;
    }

    // 창을 숨기고 생성한 목록을 정리한다.
    public void Hide()
    {
        ClearSelectionInside(_root.transform);
        ClearRows();
        _root.SetActive(false);
    }

    // 기존 목록을 제거한다.
    private void ClearRows()
    {
        ClearSelectionInside(_content);

        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i] != null)
            {
                _rows[i].gameObject.SetActive(false);
                Destroy(_rows[i].gameObject);
            }
        }

        _rows.Clear();
    }

    // 없어지는 UI에 키보드 선택이 남지 않도록 한다.
    private void ClearSelectionInside(Transform parent)
    {
        EventSystem eventSystem = EventSystem.current;

        if (eventSystem == null)
        {
            return;
        }

        GameObject selected = eventSystem.currentSelectedGameObject;

        if (selected != null && selected.transform.IsChildOf(parent))
        {
            eventSystem.SetSelectedGameObject(null);
        }
    }

    // 목록 클릭을 전달한다.
    private void RequestSelection(QuestData quest)
    {
        if (QuestSelected != null)
        {
            QuestSelected.Invoke(quest);
        }
    }
}
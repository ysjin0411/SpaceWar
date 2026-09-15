using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// NPC 대화창의 퀘스트 이름 목록과 목록 복귀 버튼을 표시한다.
public class NpcQuestListView : MonoBehaviour
{
    // 제목과 목록을 함께 켜고 끄는 루트.
    [SerializeField] private GameObject _questChoicesRoot;

    // 퀘스트 버튼들을 배치할 Vertical Layout Group 오브젝트.
    [SerializeField] private Transform _questContent;

    // 텍스트 색 전환이 설정된 퀘스트 이름 버튼 프리팹.
    [SerializeField] private Button _questButtonPrefab;

    // 이 View가 생성한 버튼만 관리한다.
    private readonly List<Button> _createdButtons = new List<Button>();

    // Presenter가 연결 준비 상태를 확인한다.
    public bool IsReady { get; private set; }

    // 선택 판단은 Presenter에 전달한다.
    public event Action<QuestData> QuestSelected;

    // 연결을 확인하고 버튼 이벤트를 등록한다.
    private void Awake()
    {
        if (_questChoicesRoot == null ||
            _questContent == null ||
            _questButtonPrefab == null ||
            _questButtonPrefab.GetComponentInChildren<TMP_Text>(true) == null)
        {
            Debug.LogError(
                "NpcQuestListView의 연결과 버튼 프리팹의 TMP를 확인해주세요.",
                this);

            enabled = false;
            return;
        }

        ResetView();
        IsReady = true;
    }

    // Presenter가 골라준 퀘스트들을 이름 버튼으로 표시한다.
    public void ShowQuests(IReadOnlyList<QuestData> quests)
    {
        ResetView();

        for (int i = 0; i < quests.Count; i++)
        {
            // 각 버튼이 자신에게 해당하는 퀘스트를 기억하도록 한다.
            QuestData quest = quests[i];

            Button button = Instantiate(_questButtonPrefab, _questContent);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);

            label.text = quest.QuestName;
            button.onClick.AddListener(() => RequestQuest(quest));

            button.gameObject.SetActive(true);
            _createdButtons.Add(button);
        }

        // 표시할 항목이 없으면 제목까지 함께 숨긴다.
        _questChoicesRoot.SetActive(_createdButtons.Count > 0);
    }

    // 퀘스트 대화가 시작되면 퀘스트 선택 목록을 숨긴다.
    // 일반 대화로 돌아가려면 대화창을 닫고 다시 말을 건다.
    public void ShowQuestDialogue()
    {
        // 숨길 목록에 남아 있는 키보드 선택을 해제한다.
        ClearSelection();

        // 대화 중에는 다른 퀘스트를 선택하지 못하게 한다.
        _questChoicesRoot.SetActive(false);
    }

    // 생성한 버튼과 UI 표시 상태를 정리한다.
    public void ResetView()
    {
        ClearSelection();

        for (int i = 0; i < _createdButtons.Count; i++)
        {
            Button button = _createdButtons[i];

            if (button != null)
            {
                // Destroy가 처리되기 전에도 이전 버튼이 보이지 않게 한다.
                button.gameObject.SetActive(false);
                Destroy(button.gameObject);
            }
        }

        _createdButtons.Clear();
        _questChoicesRoot.SetActive(false);
    }

    // 숨길 퀘스트 목록 안에 키보드 선택이 남지 않도록 해제한다.
    private void ClearSelection()
    {
        EventSystem eventSystem = EventSystem.current;

        // EventSystem이 없으면 선택 해제를 생략한다.
        if (eventSystem == null)
        {
            return;
        }

        GameObject selected = eventSystem.currentSelectedGameObject;

        // 현재 선택한 오브젝트가 퀘스트 목록 안에 있을 때만 해제한다.
        if (selected != null &&
            selected.transform.IsChildOf(_questChoicesRoot.transform))
        {
            eventSystem.SetSelectedGameObject(null);
        }
    }

    // 어떤 퀘스트 이름을 눌렀는지 전달한다.
    private void RequestQuest(QuestData quest)
    {
        if (QuestSelected != null)
        {
            QuestSelected.Invoke(quest);
        }
    }
}
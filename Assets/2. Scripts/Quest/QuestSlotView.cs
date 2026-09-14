using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 퀘스트 슬롯 하나의 제목, 선택 색상, 클릭을 처리한다.
public class QuestSlotView : MonoBehaviour
{
    // 슬롯 전체를 클릭하는 버튼.
    [SerializeField] private Button _button;

    // 선택 상태에 따라 색상을 바꿀 슬롯 배경.
    [SerializeField] private Image _background;

    // 프리팹 안의 QuestTitleText를 연결한다.
    [SerializeField] private TMP_Text _titleText;

    // 선택하지 않은 슬롯의 기본 색상.
    [SerializeField] private Color _normalColor = Color.white;

    // 선택한 슬롯에 적용할 파란색.
    [SerializeField]
    private Color _selectedColor =
        new Color(0.25f, 0.65f, 1f, 1f);

    // 이 슬롯이 표시하는 퀘스트 데이터.
    private QuestData _quest;

    // 클릭한 퀘스트를 목록 View에 전달하는 함수.
    private Action<QuestData> _onClicked;

    // 목록을 생성하기 전에 필수 연결을 확인한다.
    public bool IsConfigured =>
        _button != null &&
        _background != null &&
        _titleText != null;

    // 버튼의 기본 색상 전환이 선택 색상을 덮어쓰지 않게 한다.
    private void Awake()
    {
        if (!IsConfigured)
        {
            Debug.LogError("QuestSlotView의 Inspector 연결을 확인해주세요.", this);
            enabled = false;
            return;
        }

        _button.transition = Selectable.Transition.None;
        _button.onClick.AddListener(HandleClick);
    }

    // 생성된 슬롯에 퀘스트와 클릭 처리 함수를 연결한다.
    public void Bind(QuestData quest, Action<QuestData> onClicked)
    {
        _quest = quest;
        _onClicked = onClicked;

        // Config의 Quest Name을 슬롯 제목에 표시한다.
        _titleText.text = quest.QuestName;
        SetSelected(false);
    }

    // 마우스 포커스와 관계없이 선택 색상을 유지한다.
    public void SetSelected(bool selected)
    {
        _background.color = selected ? _selectedColor : _normalColor;
    }

    // 슬롯이 가진 퀘스트를 상위 View에 전달한다.
    private void HandleClick()
    {
        if (_quest != null)
        {
            _onClicked?.Invoke(_quest);
        }
    }

    // 직접 등록한 버튼 이벤트를 정리한다.
    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(HandleClick);
        }

        _onClicked = null;
    }
}
using System.Collections.Generic;
using UnityEngine;

// 필요한 수만큼 보상 슬롯을 준비하고 목록을 표시한다.
public class QuestRewardListView : MonoBehaviour
{
    // 제목과 보상 목록을 포함하는 전체 표시 영역.
    [SerializeField] private GameObject _root;

    // 보상 슬롯을 배치할 부모.
    [SerializeField] private Transform _content;

    // 보상 한 항목을 표시하는 프리팹.
    [SerializeField] private QuestRewardSlotView _slotPrefab;

    // 이미 만든 슬롯을 보관해 다음 표시에도 재사용한다.
    private readonly List<QuestRewardSlotView> _slots =
        new List<QuestRewardSlotView>();

    // 같은 퀘스트를 표시할 때 매 프레임 다시 바인딩하지 않는다.
    private QuestData _boundQuest;

    // 현재 퀘스트에 실제로 표시할 보상이 있는지 기억한다.
    private bool _hasRewards;

    // 다른 View나 Presenter의 Awake에서도 검사할 수 있다.
    // 부모 화면이 숨겨져 있어도 표시할 보상이 있는지 확인한다.
    public bool HasRewards => _hasRewards;

    public bool IsConfigured =>
        _root != null &&
        _content != null &&
        _slotPrefab != null &&
        _slotPrefab.IsConfigured;

    // 퀘스트의 보상 목록을 표시한다.
    public void Show(QuestData quest)
    {
        if (!IsConfigured)
        {
            return;
        }

        if (quest == null)
        {
            Hide();
            return;
        }

        // 퀘스트가 달라졌을 때만 슬롯 내용을 교체한다.
        if (_boundQuest != quest)
        {
            Bind(quest);
        }

        // 보상이 없으면 제목과 배경도 함께 숨긴다.
        if (_root.activeSelf != _hasRewards)
        {
            _root.SetActive(_hasRewards);
        }
    }

    // 보상 수에 맞게 슬롯을 준비하고 내용을 채운다.
    private void Bind(QuestData quest)
    {
        _boundQuest = quest;
        int usedCount = 0;

        if (quest.Rewards != null)
        {
            foreach (QuestRewardData reward in quest.Rewards)
            {
                // 잘못된 항목은 UI에 표시하지 않는다.
                // 실제 지급 가능 여부는 지급 코드가 별도로 검사한다.
                if (reward == null || reward.Amount <= 0)
                {
                    continue;
                }

                // 기존 슬롯이 부족한 경우에만 새 슬롯을 만든다.
                if (usedCount == _slots.Count)
                {
                    QuestRewardSlotView slot =
                        Instantiate(_slotPrefab, _content);

                    _slots.Add(slot);
                }

                QuestRewardSlotView current = _slots[usedCount];
                current.Bind(reward);
                current.gameObject.SetActive(true);
                usedCount++;
            }
        }

        // 이전 퀘스트보다 보상이 적으면 남은 슬롯을 숨긴다.
        for (int i = usedCount; i < _slots.Count; i++)
        {
            _slots[i].gameObject.SetActive(false);
        }

        _hasRewards = usedCount > 0;
    }

    // 슬롯은 재사용할 수 있도록 남겨두고 표시 영역만 숨긴다.
    public void Hide()
    {
        if (_root != null && _root.activeSelf)
        {
            _root.SetActive(false);
        }
    }
}
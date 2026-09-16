using System;
using UnityEngine;

// 수집 퀘스트에서 요구하는 아이템 한 종류와 필요 수량이다.
[Serializable]
public class QuestItemRequirement
{
    // 수집 대상 프리팹에 붙어 있는 Item이다.
    [SerializeField] private Item _item;

    // 이 아이템을 몇 개 보유해야 하는지 지정한다.
    [SerializeField, Min(1)] private int _requiredCount = 1;

    // 드롭 판정과 완료 판정에서 사용하는 정보다.
    public Item Item => _item;
    public int RequiredCount => _requiredCount;

    // 아이템과 저장 ID, 필요 수량이 올바르게 설정됐는지 확인한다.
    public bool IsConfigured =>
        _item != null &&
        !string.IsNullOrWhiteSpace(_item.Id) &&
        _requiredCount >= 1;
}
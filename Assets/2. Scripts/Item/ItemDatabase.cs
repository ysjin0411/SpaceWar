using System;
using System.Collections.Generic;
using UnityEngine;

// 저장된 아이템 ID를 아이템 프리팹의 정보와 연결한다.
[CreateAssetMenu(
    fileName = "ItemDatabase",
    menuName = "GameData/ItemDatabase")]
public class ItemDatabase : ScriptableObject
{
    // 게임에서 사용하는 아이템 프리팹의 Item을 등록한다.
    // 씬에 생성된 아이템이 아니라 Project 창의 프리팹을 연결한다.
    [SerializeField] private Item[] _items = new Item[0];

    // 저장된 ID에 해당하는 아이템 정보를 반환한다.
    // 프리팹을 생성하지 않고 이름, 종류, 아이콘을 조회하는 용도다.
    public Item GetItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            throw new ArgumentException(
                "조회할 아이템 ID가 비어 있습니다.",
                nameof(itemId));
        }

        foreach (Item item in _items)
        {
            if (item == null)
            {
                throw new InvalidOperationException(
                    $"{name}: 등록되지 않은 아이템 항목이 있습니다.");
            }

            if (item.Id == itemId)
            {
                return item;
            }
        }

        // 저장된 ID에 대응하는 프리팹이 누락된 경우를 알린다.
        throw new InvalidOperationException(
            $"{name}: ID가 '{itemId}'인 아이템을 찾을 수 없습니다.");
    }

    // Inspector에서 목록을 변경할 때 잘못된 등록을 확인한다.
    private void OnValidate()
    {
        HashSet<string> registeredIds = new HashSet<string>();

        foreach (Item item in _items)
        {
            if (item == null)
            {
                Debug.LogError(
                    $"{name}: 비어 있는 아이템 항목이 있습니다.", this);
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.Id))
            {
                Debug.LogError(
                    $"{name}: {item.name}의 아이템 ID가 비어 있습니다.", this);
                continue;
            }

            // 서로 다른 아이템이 같은 저장 ID를 사용하지 못하도록 검사한다.
            if (!registeredIds.Add(item.Id))
            {
                Debug.LogError(
                    $"{name}: 아이템 ID '{item.Id}'가 중복됩니다.", this);
            }
        }
    }
}
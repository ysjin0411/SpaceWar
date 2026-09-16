using System;
using System.Collections.Generic;
using UnityEngine;

// 인벤토리의 18칸 배치, 아이템 획득, 슬롯 교환을 처리한다.
public static class InventoryService
{
    // 현재 인벤토리의 총 슬롯 수다.
    public const int Capacity = 18;

    // 저장 원본을 직접 수정하지 않고, 18칸짜리 복사본을 만든다.
    public static List<InventoryItemSaveData> ReadSlots(
        PlayerSaveData saveData)
    {
        if (saveData == null)
        {
            throw new ArgumentException("플레이어 저장 데이터가 없습니다.");
        }

        List<InventoryItemSaveData> source = saveData.inventoryItems;

        // 기존 아이템을 임의로 버리지 않도록 초과 데이터는 오류로 알린다.
        if (source != null && source.Count > Capacity)
        {
            throw new InvalidOperationException(
                "저장된 인벤토리가 18칸을 초과합니다.");
        }

        List<InventoryItemSaveData> result =
            new List<InventoryItemSaveData>(Capacity);

        HashSet<string> usedIds = new HashSet<string>();

        for (int i = 0; i < Capacity; i++)
        {
            InventoryItemSaveData original = null;

            if (source != null && i < source.Count)
            {
                original = source[i];
            }

            // 빈칸도 실제 저장 항목으로 만들어 위치를 유지한다.
            InventoryItemSaveData copy = new InventoryItemSaveData
            {
                itemId = string.Empty,
                count = 0
            };

            if (original != null)
            {
                if (original.count < 0)
                {
                    throw new InvalidOperationException(
                        $"슬롯 {i}의 수량이 음수입니다.");
                }

                if (original.count > 0)
                {
                    if (string.IsNullOrWhiteSpace(original.itemId) ||
                        !usedIds.Add(original.itemId))
                    {
                        throw new InvalidOperationException(
                            $"슬롯 {i}의 아이템 ID가 비어 있거나 중복됩니다.");
                    }

                    copy.itemId = original.itemId;
                    copy.count = original.count;
                }
            }

            result.Add(copy);
        }

        return result;
    }

    // 같은 아이템은 기존 칸에 쌓고, 새 아이템은 첫 빈칸에 넣는다.
    public static bool TryAddItem(
        SaveManager saveManager,
        Item item,
        int amount,
        out string error)
    {
        error = string.Empty;

        if (saveManager == null || !saveManager.IsLoggedIn ||
            item == null || string.IsNullOrWhiteSpace(item.Id) ||
            amount <= 0)
        {
            error = "아이템 획득 정보가 올바르지 않습니다.";
            return false;
        }

        try
        {
            List<InventoryItemSaveData> slots =
                ReadSlots(saveManager.PlayerSaveData);

            // 이미 보유한 종류라면 빈칸이 없어도 수량을 추가할 수 있다.
            int index = slots.FindIndex(
                entry => entry.itemId == item.Id);

            if (index < 0)
            {
                index = slots.FindIndex(entry => entry.count == 0);
            }

            if (index < 0)
            {
                error = "인벤토리에 빈 공간이 없습니다.";
                return false;
            }

            slots[index].itemId = item.Id;

            // 정수 범위를 넘으면 잘못된 수량을 저장하지 않는다.
            slots[index].count = checked(slots[index].count + amount);

            return TryCommit(saveManager, slots, out error);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            error = "아이템을 인벤토리에 추가하지 못했습니다.";
            return false;
        }
    }

    // 아이템이 있는 출발 슬롯과 도착 슬롯의 내용을 교환한다.
    public static bool TrySwap(
        SaveManager saveManager,
        int from,
        int to,
        out string error)
    {
        error = string.Empty;

        if (saveManager == null || !saveManager.IsLoggedIn ||
            from < 0 || from >= Capacity ||
            to < 0 || to >= Capacity ||
            from == to)
        {
            error = "교환할 슬롯이 올바르지 않습니다.";
            return false;
        }

        try
        {
            List<InventoryItemSaveData> slots =
                ReadSlots(saveManager.PlayerSaveData);

            // 빈 슬롯에서는 드래그나 교환을 시작할 수 없다.
            if (slots[from].count == 0)
            {
                error = "출발 슬롯이 비어 있습니다.";
                return false;
            }

            // 도착 슬롯은 아이템이 있어도, 비어 있어도 된다.
            InventoryItemSaveData temporary = slots[from];
            slots[from] = slots[to];
            slots[to] = temporary;

            return TryCommit(saveManager, slots, out error);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            error = "슬롯을 교환하지 못했습니다.";
            return false;
        }
    }

    // 수량과 슬롯 위치를 저장하고, 실패하면 기존 목록을 복구한다.
    private static bool TryCommit(
        SaveManager saveManager,
        List<InventoryItemSaveData> next,
        out string error)
    {
        error = string.Empty;

        PlayerSaveData data = saveManager.PlayerSaveData;
        List<InventoryItemSaveData> previous = data.inventoryItems;

        data.inventoryItems = next;

        try
        {
            // 기존 저장 경로가 퀘스트 완료 판정도 함께 수행한다.
            saveManager.SaveToFile();
            return true;
        }
        catch (Exception exception)
        {
            data.inventoryItems = previous;

            Debug.LogException(exception);
            error = "인벤토리 변경 내용을 저장하지 못했습니다.";
            return false;
        }
    }

    // 퀘스트 요구 수량을 차감한 인벤토리 복사본을 만든다.
    // 원본 변경과 파일 저장은 보상 지급 담당자가 한 번에 처리한다.
    public static bool TryBuildQuestTurnInSlots(
        PlayerSaveData saveData,
        QuestData quest,
        out List<InventoryItemSaveData> nextSlots,
        out string error)
    {
        nextSlots = null;
        error = string.Empty;

        if (saveData == null || quest == null)
        {
            error = "퀘스트 제출에 필요한 데이터가 없습니다.";
            return false;
        }

        QuestObjectiveData objective = quest.Objective;

        if (objective == null)
        {
            error = "퀘스트 목표 설정이 없습니다.";
            return false;
        }

        // 아이템 수집 이외의 퀘스트는 인벤토리를 변경하지 않는다.
        if (objective.Type != QuestObjectiveType.ItemOwned)
        {
            nextSlots = saveData.inventoryItems;
            return true;
        }

        if (!objective.IsConfigured)
        {
            error = "퀘스트의 요구 아이템 설정을 확인해주세요.";
            return false;
        }

        try
        {
            // 기존 슬롯 위치를 유지한 독립적인 복사본을 만든다.
            List<InventoryItemSaveData> slots = ReadSlots(saveData);

            foreach (QuestItemRequirement requirement
                     in objective.RequiredItems)
            {
                int index = slots.FindIndex(
                    slot => slot.itemId == requirement.Item.Id);

                // 저장된 완료 상태만 믿지 않고 실제 보유 수량도 검사한다.
                if (index < 0 ||
                    slots[index].count < requirement.RequiredCount)
                {
                    error =
                        $"{requirement.Item.DisplayName}이(가) 부족합니다. " +
                        $"{requirement.RequiredCount}개가 필요합니다.";

                    return false;
                }

                // 해당 퀘스트가 요구한 수량만 차감한다.
                slots[index].count -= requirement.RequiredCount;

                // 모두 제출했다면 같은 위치를 빈 슬롯으로 바꾼다.
                // 리스트에서 제거하지 않으므로 다른 슬롯이 밀리지 않는다.
                if (slots[index].count == 0)
                {
                    slots[index].itemId = string.Empty;
                }
            }

            nextSlots = slots;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            error = "퀘스트에 제출할 아이템을 확인하지 못했습니다.";
            return false;
        }
    }
}
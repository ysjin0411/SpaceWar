using System.Collections.Generic;

[System.Serializable]
public class CharacterLoadoutSaveData
{
    public string characterId;                                    // 캐릭터 ID
    public int level = 1;                                         // 캐릭터 레벨
    public int currentExp = 0;                                    // 현재 경험치
    public List<string> ownedWeaponIds = new();                   // 구매했거나 기본 지급된 무기 ID 목록
    public string[] equippedWeaponIds = new string[3];            // 캐릭터 장착 무기 ID(최대 3개)
}

// 인벤토리에 보유한 아이템 한 종류의 저장 정보다.
[System.Serializable]
public class InventoryItemSaveData
{
    // 프리팹의 Item.Id와 연결되는 고유 ID다.
    public string itemId;

    // 현재 보유한 개수다.
    public int count;
}

/// <summary>
/// 플레이어마다 달라지는 업적 진행 상태를 저장한다.
/// 업적의 이름과 보상 정보는 AchievementData에서 관리한다.
/// </summary>
[System.Serializable]
public class AchievementSaveData
{
    // 어떤 AchievementData의 상태인지 구분하는 고유 ID다.
    public string achievementId;

    // 업적 조건을 한 번이라도 달성했는지 저장한다.
    // 달성 후 조건값이 낮아져도 다시 미달성 상태로 돌아가지 않는다.
    public bool isCompleted;

    // 업적 보상을 이미 수령했는지 저장한다.
    public bool isRewardClaimed;
}

[System.Serializable]
public class PlayerSaveData
{
    // 이 세이브의 소유자인 사용자 ID.
    public string userId = string.Empty;

    // 솔져·워리어 레벨과 별개인 사용자 레벨.
    public int userLevel = 1;

    // 사용자 레벨 상승에 사용할 누적 전 단계의 현재 경험치.
    public int userExp = 0;

    // 사용자가 보유한 골드.
    public int gold = 0;

    // 캐릭터별 성장과 장비 정보.
    public List<CharacterLoadoutSaveData> characterLoadouts = new();

    // 스테이지별 최고 클리어 기록.
    public List<StageClearSaveData> stageClearRecords = new();

    // 사용자가 수락한 퀘스트의 진행 기록.
    public List<QuestSaveData> questRecords = new();

    // 업적 달성과 보상 수령 기록.
    public List<AchievementSaveData> achievementRecords = new();

    // 아이템 ID별 보유 수량을 저장한다.
    public List<InventoryItemSaveData> inventoryItems = new();

    // 지정한 아이템의 현재 보유 수량을 조회한다.
    public int GetItemCount(string itemId)
    {
        // 이전 저장 파일에 인벤토리 데이터가 없으면 0개로 취급한다.
        if (inventoryItems == null || string.IsNullOrWhiteSpace(itemId))
        {
            return 0;
        }

        foreach (InventoryItemSaveData entry in inventoryItems)
        {
            if (entry != null && entry.itemId == itemId)
            {
                return entry.count;
            }
        }

        return 0;
    }
}

/// <summary>
/// 스테이지별 최단 클리어 기록을 저장한다.
/// </summary>
[System.Serializable]
public class StageClearSaveData
{
    // StageSelect에서 사용하는 0부터 시작하는 스테이지 번호
    public int stageIndex;

    // 최단 클리어 시간이다.
    // -1이면 아직 클리어 기록이 없다는 뜻이다.
    public float bestClearTime = -1f;
}
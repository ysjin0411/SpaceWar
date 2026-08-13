using System.Collections.Generic;

[System.Serializable]
public class CharacterLoadoutSaveData
{
    public string characterId;                                    // 캐릭터 ID
    public int level = 1;
    public int currentExp = 0;
    public string[] equippedWeaponIds = new string[3];            // 캐릭터 장착 무기 ID(최대 3개)
}

[System.Serializable]
public class PlayerSaveData
{
    public int gold = 0;

    // 캐릭터마다 별도의 장착 무기 정보를 보관한다. new()를 붙여 처음부터 사용할 수 있는 빈 리스트로 만든다.
    public List<CharacterLoadoutSaveData> characterLoadouts = new();
}
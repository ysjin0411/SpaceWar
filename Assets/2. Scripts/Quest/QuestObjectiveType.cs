// 퀘스트 목표를 어떤 기준으로 검사할지 구분한다.
public enum QuestObjectiveType
{
    // 조건을 아직 지정하지 않은 상태.
    None = 0,

    // 지정한 캐릭터가 목표 레벨에 도달했는지 검사한다.
    CharacterLevel = 1,

    // 지정한 스테이지의 클리어 기록이 있는지 검사한다.
    StageClear = 2,

    // 지정한 캐릭터가 대상 무기를 소유했는지 검사한다.
    WeaponOwned = 3,

    // 퀘스트 수락 후 NPC를 통해 업적창을 열었는지 검사한다.
    OpenAchievementWindow = 4
}
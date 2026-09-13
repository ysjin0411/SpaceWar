// NPC가 제공하는 고유 기능.
// 퀘스트 제공 여부와는 별도로 구분한다.
public enum NpcActionType
{
    None = 0,        // 비서처럼 고유 기능이 없는 NPC.
    Shop = 1,        // 캐릭터 선택 후 장비상점 이용.
    Stage = 2,       // 스테이지 선택.
    Achievement = 3 // 업적 창 열기.
}
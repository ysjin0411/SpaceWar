/// <summary>
/// 카탈로그의 편집용 분류. 기존 숫자는 유지하고 새 분류는 뒤에 추가한다.
/// </summary>
public enum GameAudioCategory
{
    // 아직 분류되지 않은 항목.
    Unassigned = 0,
    // 메뉴 조작과 선택 효과음.
    UI = 1,
    // NPC 대화와 타운 환경음.
    Town = 2,
    // 플레이어 이동과 상태 변화.
    Player = 3,
    // 병사 무기와 궁극기.
    Soldier = 4,
    // 전사 무기와 궁극기.
    Warrior = 5,
    // 일반 적 효과음.
    Enemy = 6,
    // 획득, 성장, 보물상자.
    Reward = 7,
    // 보스별 효과음.
    Dog = 8,
    Head = 9,
    Cruiser = 10,
    Alien = 11,
    // 스테이지 시작, 종료, 경고.
    Stage = 12,
    // 배경음악.
    BGM = 13
}

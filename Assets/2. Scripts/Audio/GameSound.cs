// 파일명과 일대일로 대응하는 사운드 식별자. 기존 숫자는 변경하지 않습니다.
public enum GameSound
{
    None = 0,
    // Town: ui_button_click.wav
    UiButtonClick = 1,
    // Town: ui_window_open.wav
    UiWindowOpen = 2,
    // Town: ui_window_close.wav
    UiWindowClose = 3,
    // Town: npc_dialogue_open.wav
    NpcDialogueOpen = 4,
    // Town: npc_dialogue_next.wav
    NpcDialogueNext = 5,
    // Town: npc_dialogue_close.wav
    NpcDialogueClose = 6,
    // Town: player_footstep_01.wav
    PlayerFootstep01 = 7,
    // Town: player_footstep_02.wav
    PlayerFootstep02 = 8,
    // Town: player_footstep_03.wav
    PlayerFootstep03 = 9,
    // Town: player_jump.wav
    PlayerJump = 10,
    // Town: player_land.wav
    PlayerLand = 11,
    // Town: quest_accept.wav
    QuestAccept = 12,
    // Town: quest_reward.wav
    QuestReward = 13,
    // Town: equipment_equip.wav
    EquipmentEquip = 14,
    // Town: equipment_unequip.wav
    EquipmentUnequip = 15,
    // Town: stage_depart.wav
    StageDepart = 16,
    // Town: ui_action_denied.wav
    UiActionDenied = 17,
    // Town: town_bgm_orbital_rest_loop.wav
    TownBgmOrbitalRestLoop = 18,
    // Town: town_ambience_ventilation_loop.wav
    TownAmbienceVentilationLoop = 19,
    // 기존 MainMenu 효과음 재사용
    UiTyping = 20,
    // 기존 MainMenu 효과음 재사용
    UiLoginSuccess = 21,
    // 기존 MainMenu 효과음 재사용
    UiLoginFailure = 22,
    // 키보드 UI 선택 이동
    UiFocus = 23,
    // 캐릭터·스테이지 선택 확정
    UiConfirm = 24,
    // 뒤로 이동
    UiBack = 25,
    // 잠긴 항목·조건 미충족
    UiLocked = 26,
    // 병사 선택
    CharacterSoldierSelect = 27,
    // 전사 선택
    CharacterWarriorSelect = 28,
    // 장비 강화 성공
    EquipmentUpgradeSuccess = 29,
    // 업적 달성
    AchievementUnlock = 30,
    // 인벤토리 아이템 선택
    InventoryItemSelect = 31,
    // 스테이지 선택 변경
    StageSelectChange = 32,
    // 일시정지
    PauseOpen = 33,
    // 일시정지 해제
    PauseResume = 34,
    // 기본 총알 발사 변형 1
    BulletFire01 = 35,
    // 기본 총알 발사 변형 2
    BulletFire02 = 36,
    // 기본 총알 발사 변형 3
    BulletFire03 = 37,
    // 총알 명중
    BulletHit = 38,
    // 지속 레이저 시작
    LaserStart = 39,
    // 지속 레이저 유지: 발사 중에만 Loop
    LaserSustainLoop = 40,
    // 최대 출력 레이저 유지: 기본 루프와 교체
    LaserMaxChargeLoop = 41,
    // 레이저 종료
    LaserStop = 42,
    // 미사일 발사
    MissileLaunch = 43,
    // 미사일 폭발
    MissileExplosion = 44,
    // 수류탄 투척
    GrenadeThrow = 45,
    // 수류탄 바닥 접촉
    GrenadeBounce = 46,
    // 수류탄 폭발
    GrenadeExplosion = 47,
    // 저격총 강한 단발
    SniperFire = 48,
    // 저격탄 명중
    SniperHit = 49,
    // 대검 1타
    GiantSwordSwing01 = 50,
    // 대검 2타
    GiantSwordSwing02 = 51,
    // 대검 3타
    GiantSwordSwing03 = 52,
    // 대검 명중
    GiantSwordHit = 53,
    // 참격 파동 발사
    SlashWaveLaunch = 54,
    // 참격 파동 명중
    SlashWaveHit = 55,
    // 해머 1타 휘두름
    HammerSwing01 = 56,
    // 해머 2타 휘두름
    HammerSwing02 = 57,
    // 해머 3타 휘두름
    HammerSwing03 = 58,
    // 해머 일반 명중
    HammerHit = 59,
    // 해머 마지막 타격
    HammerFinisher = 60,
    // 방패 전개
    ShieldRaise = 61,
    // 방패 방어 성공
    ShieldBlock = 62,
    // 방패 파괴
    ShieldBreak = 63,
    // 방패 회복 완료
    ShieldRecover = 64,
    // 무기 교체
    WeaponSwitch = 65,
    // 플레이어 피격: 음성 없음
    PlayerHurt = 66,
    // 플레이어 사망 에너지 소멸
    PlayerDeath = 67,
    // 일반 적 피격 1
    EnemyHit01 = 68,
    // 일반 적 피격 2
    EnemyHit02 = 69,
    // 일반 적 피격 3
    EnemyHit03 = 70,
    // 일반 적 처치
    EnemyDeath = 71,
    // 자폭 적 폭발
    EnemySuicideExplosion = 72,
    // 골드 획득
    PickupGold = 73,
    // 경험치 획득
    PickupExperience = 74,
    // 궁극기 충전 아이템
    PickupUltimate = 75,
    // 레벨업
    LevelUp = 76,
    // 업그레이드 선택 확정
    UpgradeSelect = 77,
    // 궁극기 사용 가능
    UltimateReady = 78,
    // 보물상자 등장
    TreasureAppear = 79,
    // 상자 개봉 충전 1초 기준: 실제 진행 시간에 맞춰 조절
    TreasureCharge = 80,
    // 상자 개봉 성공
    TreasureOpen = 81,
    // 전사 궁극기 시작
    UltimateWarriorCast = 82,
    // 파일명은 Drop이지만 현재 게임에서는 수평으로 이동하는 궁극기 검 발사에 사용
    UltimateSwordDrop = 83,
    // 궁극기 검 명중
    UltimateSwordImpact = 84,
    // 병사 궁극기 전개
    UltimateSoldierLaunch = 85,
    // 병사 궁극기 폭격 1발: 전체 30발 동시 재생 금지
    UltimateBombardImpact = 86,
    // 스테이지 클리어
    StageClear = 87,
    // 스테이지 실패
    StageFailed = 88,
    // 보스 등장 알림
    BossWarning = 89,
    // Dog 등장: 합성 괴수 으르렁
    DogIntroGrowl = 90,
    // Dog 돌진 시작
    DogCharge = 91,
    // Dog 나선탄 발사 묶음
    DogSpiralShot = 92,
    // Dog 충돌
    DogImpact = 93,
    // Head 레이저 예고: 실제 예고 시간에 맞춤
    HeadLaserCharge = 94,
    // Head 레이저 유지
    HeadLaserLoop = 95,
    // Head 혼란 발동
    HeadConfusion = 96,
    // Head 블랙홀 존재 중 유지
    HeadBlackholeLoop = 97,
    // 블랙홀 소멸
    HeadBlackholeCollapse = 98,
    // Cruiser 포탑 발사
    CruiserTurretFire = 99,
    // Cruiser 대형탄 충전: 실제 패턴 시간에 맞춤
    CruiserBigshotCharge = 100,
    // Cruiser 대형탄 발사
    CruiserBigshotFire = 101,
    // Cruiser 소환 신호
    CruiserSummon = 102,
    // Cruiser 3.5초 전환 애니메이션용
    CruiserTransition3p5s = 103,
    // Cruiser 파괴: 3.5초 잔향
    CruiserDeathExplosion = 104,
    // Alien 미사일 발사구 열림
    AlienMissileOpen = 105,
    // Alien 미사일 발사
    AlienMissileLaunch = 106,
    // Alien 탄막 폭발
    AlienBarrageBlast = 107,
    // Alien 잡기 준비
    AlienGrabPrepare = 108,
    // Alien 잡기 성공
    AlienGrabCatch = 109,
    // Alien 던지기
    AlienGrabThrow = 110,
    // Alien 공명 준비: 실제 패턴 시간에 맞춤
    AlienResonanceCharge = 111,
    // Alien 공명 파동 1회
    AlienResonanceWave = 112,
    // Alien 2페이즈 도약
    AlienP2Leap = 113,
    // Alien 2페이즈 도약 착지
    AlienP2Land = 114,
    // Alien 2페이즈 콤보 1타
    AlienP2Claw01 = 115,
    // Alien 2페이즈 콤보 2타
    AlienP2Claw02 = 116,
    // Alien 2페이즈 콤보 3타
    AlienP2Claw03 = 117,
    // Alien 추적 발톱 삽입
    AlienP2TraceInsert = 118,
    // Alien 추적 공격 예고
    AlienP2TraceWarning = 119,
    // Alien 추적 발톱 타격
    AlienP2TraceStrike = 120,
    // Alien 추적 발톱 회수
    AlienP2TraceWithdraw = 121,
    // Alien 최종 사망
    AlienDeath = 122,
    // 전환 시작에 1회. 4.208333초에 하울링 폭발, 5초 종료. Animator speed=1 기준
    AlienTransitionFull5s = 123,
    // HowlFX 첫 프레임에 재생하는 단독 하울링. full_5s와 중복 재생하지 않음
    AlienHowlSync0p792s = 124,
    // 긴 잔향 대안. 4.208초에 시작하면 전환 후까지 지속되므로 별도 유지 재생기 필요
    AlienHowlExtended2p4s = 125,
    // MainMenu 대체 BGM: 기존 음악과 택일
    MainmenuSignalLoop = 126,
    // EquipmentManage·CharacterSelect 단독 실행용. Town 추가 씬에서는 Town BGM 유지 권장
    PreparationTerminalLoop = 127,
    // StageSelect 단독 실행용. Town 위에서 열면 Town BGM 유지
    StageSelectNavigationLoop = 128,
    // Stage1 일반 전투
    Stage1FrontierLoop = 129,
    // Stage2 일반 전투
    Stage2BiomechanicalLoop = 130,
    // Stage1 중간 보스 Dog
    BossDogPursuitLoop = 131,
    // Stage1 최종 보스 Head
    BossHeadDistortionLoop = 132,
    // Stage2 중간 보스 Cruiser
    BossCruiserSiegeLoop = 133,
    // Stage2 Alien 1페이즈
    BossAlienPhase1Loop = 134,
    // Stage2 Alien 2페이즈: 하울링 후 시작
    BossAlienPhase2Loop = 135,
    // 정신간섭에서 글자 하나를 정확히 입력했을 때의 확인음.
    HeadConfusionCorrect = 136,
    // 정신간섭에서 잘못 입력해 Life가 감소했을 때의 경고음.
    HeadConfusionWrong = 137,
    // 던져진 플레이어가 처음 벽에 충돌한 순간.
    AlienWallCrash = 138,
    // 3스택 공명 공격 예약 알림.
    AlienCrackReady = 139,
    // 공명 중 실제 균열 폭발.
    AlienCrackBurst = 140,
    // Dog 전용 낮은 으르렁 사망음.
    DogDeath = 141,
    // Head 전용 연속 경고 사망음.
    HeadDeath = 142,
    // 크루저의 자폭 적 한 마리가 소환될 때의 분출음.
    CruiserMinionLaunch = 143,
    // 헤드의 전자 신호 등장음.
    HeadIntro = 144,
    // 크루저의 함선 엔진과 경적 등장음.
    CruiserIntro = 145,
    // 에일리언 1페이즈의 중장비 기동 등장음.
    AlienRobotIntro = 146,
}

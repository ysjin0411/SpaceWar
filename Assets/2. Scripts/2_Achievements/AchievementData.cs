using UnityEngine;

/// <summary>
/// 업적을 달성하기 위해 확인할 조건 종류다.
/// 새로운 조건이 필요하면 이 목록에 추가한다.
/// </summary>
public enum AchievementConditionType
{
    StageClearWithinTime,    // 특정 스테이지를 제한 시간 이내로 클리어
    CharacterLevelReached   // 특정 캐릭터가 목표 레벨에 도달
}

/// <summary>
/// 업적 완료 후 지급할 보상 종류다.
/// 현재는 골드만 사용하지만 이후 다른 보상을 추가할 수 있다.
/// </summary>
public enum AchievementRewardType
{
    Gold    // 골드 보상
}

/// <summary>
/// 모든 플레이어에게 공통으로 적용되는 업적 원본 데이터다.
/// 업적의 완료 여부나 보상 수령 여부는 이곳에 저장하지 않는다.
/// 변경되는 플레이어 상태는 SaveData에 저장한다.
/// </summary>
[CreateAssetMenu(
    fileName = "AchievementData",
    menuName = "GameData/AchievementData")]
public class AchievementData : ScriptableObject
{
    [Header("기본 정보")]

    // 세이브 데이터에서 업적을 구분할 고유 ID다.
    // 에셋을 만든 후에는 세이브 호환성을 위해 변경하지 않는다.
    [SerializeField] string _id;

    // 업적 슬롯에 표시할 업적 이름이다.
    [SerializeField] string _achievementName;

    // 업적 슬롯에 표시할 조건 설명이다.
    [TextArea(2, 4)]
    [SerializeField] string _description;

    // 업적 슬롯에 표시할 아이콘이다.
    [SerializeField] Sprite _icon;

    [Header("달성 조건")]

    // 이 업적이 어떤 조건을 검사할지 지정한다.
    [SerializeField] AchievementConditionType _conditionType;

    // Stage 조건에서 검사할 0부터 시작하는 스테이지 인덱스다.
    // Stage 1은 0, Stage 2는 1로 입력한다.
    [Min(0)]
    [SerializeField] int _targetStageIndex;

    // Stage 조건에서 허용할 최대 클리어 시간이다.
    // 실제 클리어 시간이 이 값 이하일 때 달성된다.
    [Min(0.01f)]
    [SerializeField] float _targetClearTimeSeconds = 180f;

    // 캐릭터 레벨 조건에서 검사할 캐릭터의 고유 ID다.
    // Soldier는 현재 프로젝트 기준으로 "soldier"를 사용한다.
    [SerializeField] string _targetCharacterId;

    // 캐릭터 레벨 조건에서 달성해야 할 최소 레벨이다.
    [Min(1)]
    [SerializeField] int _targetCharacterLevel = 1;

    [Header("보상")]

    // 업적 완료 후 지급할 보상의 종류다.
    [SerializeField] AchievementRewardType _rewardType;

    // 업적 완료 후 지급할 보상의 수량이다.
    [Min(1)]
    [SerializeField] int _rewardValue;

    // 외부에서는 Config 값을 읽기만 할 수 있다.
    public string Id => _id;
    public string AchievementName => _achievementName;
    public string Description => _description;
    public Sprite Icon => _icon;

    public AchievementConditionType ConditionType =>
        _conditionType;

    public int TargetStageIndex =>
        _targetStageIndex;

    public float TargetClearTimeSeconds =>
        _targetClearTimeSeconds;

    public string TargetCharacterId =>
        _targetCharacterId;

    public int TargetCharacterLevel =>
        _targetCharacterLevel;

    public AchievementRewardType RewardType =>
        _rewardType;

    public int RewardValue =>
        _rewardValue;
}
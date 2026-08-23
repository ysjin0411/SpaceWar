using UnityEngine;

/// <summary>
/// 증강이 실행할 효과 종류다.
/// </summary>
public enum UpgradeType
{
    MoveSpeed,
    FireRate,
    Damage,
    MaxHealthAndFullHeal,
    NaturalGaugeChargeRate
}

/// <summary>
/// 증강 효과가 적용될 대상이다.
/// </summary>
public enum UpgradeTarget
{
    Player,
    Weapon
}

[CreateAssetMenu(
    fileName = "UpgradeData",
    menuName = "UpgradeData/UpgradeData")]
public class UpgradeData : ScriptableObject
{
    [Header("증강 표시 정보")]

    // 저장 및 비교에 사용하는 고유 ID
    [SerializeField] private string _id;

    // 슬롯에 표시할 증강 이름
    [SerializeField] private string _name;

    // 슬롯에 표시할 증강 설명
    [TextArea(2, 5)]
    [SerializeField] private string _info;

    // 슬롯에 표시할 증강 아이콘
    [SerializeField] private Sprite _icon;

    [Header("증강 적용 정보")]

    // 증강이 수행할 효과 종류
    [SerializeField] private UpgradeType _type;

    // 증강 효과가 적용될 대상
    [SerializeField] private UpgradeTarget _target;

    // 이동속도, 공격력, 최대체력, 게이지 충전속도 등에 사용할 효과 수치
    [Min(0f)]
    [SerializeField] private float _value;

    public string UpgradeId => _id;
    public string UpgradeName => _name;
    public string UpgradeInfo => _info;
    public Sprite UpgradeIcon => _icon;

    public UpgradeType Type => _type;
    public UpgradeTarget Target => _target;
    public float UpgradeValue => _value;
}
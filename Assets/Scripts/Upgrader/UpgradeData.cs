using UnityEngine;

public enum UpgradeType
{
    MoveSpeed,
    FireRate,
    Damage
}
public enum UpgradeTarget
{
    Player,
    Weapon
}

[CreateAssetMenu(fileName = "UpgradeData", menuName = "UpgradeData/UpgradeData")]
public class UpgradeData : ScriptableObject
{
    [Header("증강 정보")]
    [SerializeField] string _id;
    [SerializeField] string _name;
    [SerializeField] string _info;
    [SerializeField] Sprite _icon;

    [Header("증강 종류")]
    [SerializeField] UpgradeType _type;
    [SerializeField] UpgradeTarget _target;

    [Header("증강 수치")]
    [SerializeField] float _upgradeSpeedValue;          // 이속 증가 밸류
    [SerializeField] float _upgradeDamageValue;        // 공격력 증가 밸류
    [SerializeField] float _upgradeFireRateValue;      // 연사 속도 증가 밸류

    public UpgradeTarget Target => _target;
    public UpgradeType Type => _type;
    public float UpgradeSpeedValue => _upgradeSpeedValue;          
    public float UpgradeDamageValue => _upgradeDamageValue;       
    public float UpgradeFireRateValue => _upgradeFireRateValue;   // 연사 속도 증가 밸류
    public string UpgradeId => _id;
    public string UpgradeName => _name;
    public string UpgradeInfo => _info;
    public Sprite UpgradeIcon => _icon;

}

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
    [SerializeField] float _value;

    public UpgradeTarget Target => _target;
    public UpgradeType Type => _type;
    public float Value => _value;
    public string UpgradeId => _id;
    public string UpgradeName => _name;
    public string UpgradeInfo => _info;
    public Sprite UpgradeIcon => _icon;

}

using UnityEngine;

[CreateAssetMenu(fileName = "UpgradeDataMap", menuName = "UpgradeDataMap/UpgradeDataMap")]
public class UpgradeMap : ScriptableObject
{
    [Header("증강 목록")]
    [SerializeField] UpgradeData[] _upgradeDatas;

    [Header("업그레이드")]
    [SerializeField] UpgradeMap _upgradeMap;
    UpgradeData[] _selectedUpgrades = new UpgradeData[3];
    bool _isSelecting;

    public bool IsSelecting => _isSelecting;
}

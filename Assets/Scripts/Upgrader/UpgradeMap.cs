using UnityEngine;

[CreateAssetMenu(fileName = "UpgradeDataMap", menuName = "UpgradeDataMap/UpgradeDataMap")]
public class UpgradeMap : ScriptableObject
{
    [Header("증강 목록")]
    [SerializeField] UpgradeData[] _upgradeDatas;
}

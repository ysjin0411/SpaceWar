using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임에서 사용할 전체 UpgradeData 목록을 보관하는 저장소다.
/// 랜덤 추첨이나 증강 적용은 수행하지 않는다.
/// </summary>
[CreateAssetMenu(
    fileName = "UpgradeDataMap",
    menuName = "UpgradeDataMap/UpgradeDataMap")]
public class UpgradeMap : ScriptableObject
{
    [Header("전체 증강 데이터")]

    // 게임에서 등장할 수 있는 전체 증강 목록
    [SerializeField] private UpgradeData[] _upgradeDatas;

    // 현재 등록된 전체 증강 개수
    public int Count =>
        _upgradeDatas != null
            ? _upgradeDatas.Length
            : 0;

    /// <summary>
    /// 원본 배열이 외부에서 변경되지 않도록 복사본을 반환한다.
    /// UpgradeModel은 이 복사본으로 랜덤 추첨을 수행한다.
    /// </summary>
    public UpgradeData[] CreateSnapshot()
    {
        ValidateData();

        UpgradeData[] snapshot =
            new UpgradeData[_upgradeDatas.Length];

        Array.Copy(
            _upgradeDatas,
            snapshot,
            _upgradeDatas.Length);

        return snapshot;
    }

    /// <summary>
    /// 비어 있는 데이터와 중복 ID가 있는지 확인한다.
    /// </summary>
    private void ValidateData()
    {
        if (_upgradeDatas == null ||
            _upgradeDatas.Length == 0)
        {
            throw new InvalidOperationException(
                $"{name}에 증강 데이터가 등록되지 않았습니다.");
        }

        HashSet<string> registeredIds = new();

        for (int index = 0;
             index < _upgradeDatas.Length;
             index++)
        {
            UpgradeData upgradeData =
                _upgradeDatas[index];

            if (upgradeData == null)
            {
                throw new InvalidOperationException(
                    $"{name}의 {index}번 증강 데이터가 비어 있습니다.");
            }

            if (string.IsNullOrWhiteSpace(
                upgradeData.UpgradeId))
            {
                throw new InvalidOperationException(
                    $"{upgradeData.name}의 UpgradeId가 비어 있습니다.");
            }

            if (!registeredIds.Add(
                upgradeData.UpgradeId))
            {
                throw new InvalidOperationException(
                    $"중복된 증강 ID가 있습니다: {upgradeData.UpgradeId}");
            }
        }
    }
}
using System;
using UnityEngine;

/// <summary>
/// 게임에 존재하는 모든 AchievementData를 보관한다.
/// 업적창에서는 이 데이터베이스의 등록 순서대로 슬롯을 생성한다.
/// </summary>
[CreateAssetMenu(
    fileName = "AchievementDatabase",
    menuName = "GameData/AchievementDatabase")]
public class AchievementDatabase : ScriptableObject
{
    [Header("게임에 존재하는 전체 업적")]

    // 업적창에 표시할 순서대로 AchievementData를 등록한다.
    [SerializeField] AchievementData[] _achievementDatas;

    // 데이터베이스에 등록된 전체 업적 개수다.
    public int Count => _achievementDatas.Length;

    /// <summary>
    /// 등록 순서에 해당하는 업적 데이터를 반환한다.
    /// 동적으로 업적 슬롯을 생성할 때 사용한다.
    /// </summary>
    public AchievementData GetAchievementDataAt(int index)
    {
        // 배열 범위를 벗어난 번호는 잘못된 요청이다.
        if (index < 0 ||
            index >= _achievementDatas.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index),
                index,
                "업적 인덱스가 데이터베이스 범위를 벗어났습니다.");
        }

        // 현재 순서에 등록된 업적 데이터를 가져온다.
        AchievementData achievementData =
            _achievementDatas[index];

        // Inspector 연결 누락을 즉시 확인한다.
        if (achievementData == null)
        {
            throw new InvalidOperationException(
                $"AchievementDatabase의 {index}번 업적이 비어 있습니다.");
        }

        return achievementData;
    }

    /// <summary>
    /// 세이브 데이터에 저장된 ID와 일치하는 업적을 반환한다.
    /// </summary>
    public AchievementData GetAchievementData(
        string achievementId)
    {
        // 빈 ID로는 업적을 찾을 수 없다.
        if (string.IsNullOrWhiteSpace(achievementId))
        {
            throw new ArgumentException(
                "찾을 업적 ID가 비어 있습니다.",
                nameof(achievementId));
        }

        // 등록된 업적을 처음부터 확인한다.
        for (int index = 0;
             index < _achievementDatas.Length;
             index++)
        {
            AchievementData achievementData =
                GetAchievementDataAt(index);

            // 동일한 ID의 업적을 찾으면 반환한다.
            if (achievementData.Id == achievementId)
            {
                return achievementData;
            }
        }

        throw new InvalidOperationException(
            $"AchievementDatabase에서 ID가 " +
            $"'{achievementId}'인 업적을 찾을 수 없습니다.");
    }
}
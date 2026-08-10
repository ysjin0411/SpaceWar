using System;
using UnityEngine;

/// <summary>
/// 게임에 존재하는 모든 WeaponData를 보관하고, 무기 ID를 이용해 WeaponData를 찾아주는 데이터베이스
/// </summary>
[CreateAssetMenu(fileName = "WeaponDatabase", menuName = "GameData/WeaponDatabase")]
public class WeaponDatabase : ScriptableObject
{
    [Header("게임에 존재하는 전체 무기 데이터")]
    [SerializeField] WeaponData[] _weaponDatas;

    /// <summary>
    /// 저장된 무기 ID와 일치하는 WeaponData를 반환한다.
    /// </summary>
    /// <param name="weaponId">세이브 데이터에 저장된 무기 ID</param>
    public WeaponData GetWeaponData(string weaponId)
    {
        // 데이터베이스에 등록된 무기를 처음부터 확인한다.
        for (int i = 0; i < _weaponDatas.Length; i++)
        {
            // 현재 순서의 WeaponData를 가져온다.
            WeaponData weaponData = _weaponDatas[i];

            // 배열에 비어 있는 항목이 있으면 Inspector 연결 실수이므로 오류를 발생시킨다.
            if (weaponData == null)
            {
                //Debug.LogWarning($"WeaponDatabase의 {i}번 무기 데이터가 비어 있습니다.");
                throw new InvalidOperationException($"WeaponDatabase의 {i}번 무기 데이터가 비어 있습니다.");
            }

            // 찾고 있는 ID와 현재 무기의 ID가 같으면 해당 WeaponData를 반환한다.
            if (weaponData.Id == weaponId)
            {
                return weaponData;
            }
        }

        // 반복문이 끝날 때까지 찾지 못했다면 세이브 ID가 잘못됐거나 데이터베이스 등록이 빠진 것이다.
        throw new InvalidOperationException($"WeaponDatabase에서 ID가 '{weaponId}'인 무기를 찾을 수 없습니다.");
    }
}
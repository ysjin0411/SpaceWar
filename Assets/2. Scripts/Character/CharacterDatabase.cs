using System;
using UnityEngine;

/// <summary>
/// 게임에 존재하는 모든 CharacterData를 보관한다.
/// 캐릭터 선택 화면에서는 선택한 순번을 CharacterData로 변환하고, Stage에서는 저장된 캐릭터 ID를 CharacterData로 변환할 때 사용한다.
/// 예: "soldier" > Soldier CharacterData > Soldier 캐릭터 프리팹
/// </summary>
[CreateAssetMenu(fileName = "CharacterDatabase", menuName = "GameData/CharacterDatabase")]
public class CharacterDatabase : ScriptableObject
{
    [Header("게임에 존재하는 전체 캐릭터")]

    [Tooltip("캐릭터 선택 화면에 표시할 순서대로 등록한다.")]
    [SerializeField] CharacterData[] _characterDatas;

    /// <summary>
    /// 데이터베이스에 등록된 캐릭터 수를 반환한다. 캐릭터 선택 인덱스의 범위를 검사할 때 사용한다.
    /// </summary>
    public int Count => _characterDatas.Length;

    /// <summary>
    /// 캐릭터 선택 화면에서 선택한 순번에 해당하는 CharacterData를 반환한다.
    /// </summary>
    /// <param name="index">캐릭터 선택 순번. Soldier가 0, Vampire가 1인 방식</param>
    public CharacterData GetCharacterDataAt(int index)
    {
        // 캐릭터 배열의 범위를 벗어난 인덱스는 코드 오류다.
        if (index < 0 || index >= _characterDatas.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index),index,"캐릭터 인덱스가 데이터베이스 범위를 벗어났습니다.");
        }

        // 선택된 순번의 캐릭터 데이터를 가져온다.
        CharacterData characterData = _characterDatas[index];

        // 인스펙터에서 캐릭터 연결을 빠뜨렸다면 이후에 널레퍼런스가 발생하기 전에 원인을 알려준다.
        if (characterData == null)
        {
            throw new InvalidOperationException($"CharacterDatabase의 {index}번 캐릭터가 비어 있습니다.");
        }

        // 캐릭터데이터 자료형인 인덱스번쨰 캐릭터 데이터 반환
        return characterData;
    }

    /// <summary>
    /// 캐릭터 ID와 일치하는 CharacterData를 반환한다.
    /// Stage에 진입했을 때 GameSessionManager에 저장된 SelectedCharacterId를 CharacterData로 변환할 때 사용한다.
    /// </summary>
    /// <param name="characterId">찾을 캐릭터의 고유 ID</param>
    public CharacterData GetCharacterData(string characterId)
    {
        // 빈 ID는 실제 캐릭터를 찾을 수 없는 잘못된 요청이다.
        if (string.IsNullOrWhiteSpace(characterId))
        {
            throw new ArgumentException("찾을 캐릭터 ID가 비어 있습니다.",nameof(characterId));
        }

        // 등록된 전체 캐릭터를 순서대로 확인한다.
        for (int i = 0; i < _characterDatas.Length; i++)
        {
            CharacterData characterData = _characterDatas[i];

            // 배열 중간에 비어 있는 항목이 있으면 인스펙터 설정이 잘못된 것이다.
            if (characterData == null)
            {
                throw new InvalidOperationException($"CharacterDatabase의 {i}번 캐릭터가 비어 있습니다.");
            }

            // 찾으려는 ID와 현재 캐릭터 ID가 같으면 반환한다.
            if (characterData.Id == characterId)
            {
                return characterData;
            }
        }

        // 끝까지 찾지 못했다면 ID가 잘못됐거나
        // 데이터베이스에 캐릭터 등록이 빠진 것이다.
        throw new InvalidOperationException($"CharacterDatabase에서 ID가 '{characterId}'인 캐릭터를 찾을 수 없습니다.");
    }
}
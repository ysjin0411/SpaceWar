using System;
using UnityEngine;

/// <summary>
/// EquipmentManage 씬에서 선택한 캐릭터의 장비 정보를 관리한다.
/// 처리 순서:
/// 1. GameSessionManager에서 선택한 캐릭터 ID를 가져온다.
/// 2. CharacterDatabase에서 해당 CharacterData를 찾는다.
/// 3. SaveManager에서 해당 캐릭터의 장착정보를 가져온다.
/// </summary>
public class EquipmentManager : MonoBehaviour
{
    [Header("캐릭터 데이터")]
    [Tooltip("선택한 캐릭터 ID를 CharacterData로 변환할 데이터베이스")]
    [SerializeField] CharacterDatabase _characterDatabase;

    // EquipmentCharacterSelect에서 선택한 캐릭터의 원본 데이터
    CharacterData _selectedCharacterData;

    // JSON에서 불러온 선택 캐릭터의 장착정보
    CharacterLoadoutSaveData _selectedCharacterLoadout;

    /// <summary>
    /// 현재 장비를 편집하고 있는 캐릭터의 원본 데이터.
    /// 이후 캐릭터 이름, 아이콘, 사용 가능한 무기를 UI에 표시할 때 사용한다.
    /// </summary>
    public CharacterData SelectedCharacterData =>
        _selectedCharacterData;

    /// <summary>
    /// 현재 캐릭터의 저장된 장착정보.
    /// 이후 Slot 1~3의 아이콘을 표시할 때 사용한다.
    /// </summary>
    public CharacterLoadoutSaveData SelectedCharacterLoadout =>
        _selectedCharacterLoadout;

    private void Start()
    {
        InitializeSelectedCharacter();
    }

    /// <summary>
    /// 캐릭터 선택 화면에서 전달된 캐릭터 ID를 이용해
    /// 캐릭터 원본 데이터와 JSON 장착정보를 준비한다.
    /// </summary>
    private void InitializeSelectedCharacter()
    {
        // Bootstrap 씬을 거치지 않았다면 세션 정보가 존재하지 않는다.
        if (GameSessionManager.Instance == null)
        {
            throw new InvalidOperationException(
                "GameSessionManager가 없습니다. Bootstrap 씬부터 실행해야 합니다.");
        }

        // Bootstrap 씬을 거치지 않았다면 세이브 데이터가 존재하지 않는다.
        if (SaveManager.Instance == null)
        {
            throw new InvalidOperationException(
                "SaveManager가 없습니다. Bootstrap 씬부터 실행해야 합니다.");
        }

        // 캐릭터 ID를 CharacterData로 변환할 데이터베이스가 필요하다.
        if (_characterDatabase == null)
        {
            throw new InvalidOperationException(
                "EquipmentManager에 CharacterDatabase가 연결되지 않았습니다.");
        }

        // EquipmentCharacterSelect에서 선택한 캐릭터 ID를 가져온다.
        string selectedCharacterId =
            GameSessionManager.Instance.SelectedCharacterId;

        // 캐릭터를 선택하지 않고 EquipmentManage로 들어온 경우다.
        if (string.IsNullOrWhiteSpace(selectedCharacterId))
        {
            throw new InvalidOperationException(
                "장비를 편집할 캐릭터가 선택되지 않았습니다.");
        }

        // 캐릭터 ID를 실제 CharacterData로 변환한다.
        _selectedCharacterData =
            _characterDatabase.GetCharacterData(selectedCharacterId);

        // 선택한 캐릭터의 JSON 장착정보를 가져온다.
        // 기존 정보가 없다면 빈 슬롯 3개를 가진 장착정보가 생성된다.
        _selectedCharacterLoadout =
            SaveManager.Instance.GetOrCreateCharacterLoadout(
                selectedCharacterId);

        Debug.Log(
            $"장비 관리 캐릭터를 불러왔습니다: " +
            $"{_selectedCharacterData.CharacterName}");
    }
}
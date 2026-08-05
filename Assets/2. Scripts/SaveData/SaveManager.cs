using System;
using UnityEngine;

/// <summary>
/// 플레이어의 세이브 데이터를 메모리에서 관리한다.
/// 캐릭터 선택과 캐릭터별 장착 무기 정보를 저장한다.
/// 실제 파일 저장과 불러오기는 이후 단계에서 추가한다.
/// </summary>
public class SaveManager : MonoBehaviour
{
    // 다른 씬에서도 SaveManager에 접근하기 위한 현재 인스턴스
    public static SaveManager Instance { get; private set; }

    [Header("현재 플레이어 세이브 데이터")]
    [SerializeField] PlayerSaveData _playerSaveData = new();

    // 다른 클래스에는 읽기 전용으로 현재 세이브 데이터를 공개한다.
    public PlayerSaveData PlayerSaveData => _playerSaveData;

    private void Awake()
    {
        // 이미 SaveManager가 있는데 또 생성됐다면 새로 생성된 중복 SaveManager만 제거한다.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // 현재 SaveManager를 전역 접근 인스턴스로 등록한다.
        Instance = this;
    }

    private void OnDestroy()
    {
        // 현재 등록된 인스턴스 자신이 파괴되는 경우에만 정적 참조를 비운다.
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 캐릭터 ID에 해당하는 장착 정보를 찾아 반환한다.
    /// 아직 장착 정보가 없다면 새로 만들어서 반환한다.
    /// </summary>
    /// <param name="characterId">장착 정보를 찾을 캐릭터 ID</param>
    public CharacterLoadoutSaveData GetOrCreateCharacterLoadout(string characterId)
    {
        // 비어 있는 캐릭터 ID는 정상적인 데이터가 아니므로 호출 위치를 확인할 수 있도록 예외를 발생시킨다.
        if (string.IsNullOrWhiteSpace(characterId))
        {
            throw new ArgumentException("캐릭터 ID가 비어 있습니다.", nameof(characterId));
        }

        // 기존 캐릭터 장착 정보 중 같은 ID가 있는지 확인한다.
        for (int i = 0; i < _playerSaveData.characterLoadouts.Count; i++)
        {
            // 캐릭터별 장착 정보 목록에서 하나씩 꺼낸다.
            CharacterLoadoutSaveData loadout = _playerSaveData.characterLoadouts[i];

            // 같은 캐릭터 ID의 장착 정보를 찾았다면 새로 만들지 않고 기존 데이터를 반환한다.
            if (loadout.characterId == characterId)
            {
                return loadout;
            }
        }

        // 해당 캐릭터의 장착 정보가 아직 없으므로 새로 만든다.
        CharacterLoadoutSaveData newLoadout = new CharacterLoadoutSaveData();

        // 새 장착 정보가 어떤 캐릭터의 것인지 ID를 저장한다.
        newLoadout.characterId = characterId;

        // 플레이어의 캐릭터별 장착 정보 목록에 추가한다.
        _playerSaveData.characterLoadouts.Add(newLoadout);

        // 생성한 장착 정보를 호출한 곳에 반환한다.
        return newLoadout;
    }

    /// <summary>
    /// 현재 사용할 캐릭터를 선택한다. 해당 캐릭터의 장착 정보가 없다면 함께 생성한다.
    /// </summary>
    /// <param name="characterId">선택할 캐릭터 ID</param>
    public void SelectCharacter(string characterId)
    {
        // 선택할 캐릭터의 장착 정보를 준비한다.
        GetOrCreateCharacterLoadout(characterId);

        // 현재 선택한 캐릭터 ID를 세이브 데이터에 기록한다.
        _playerSaveData.selectedCharacterId = characterId;
    }

    /// <summary>
    /// 지정한 캐릭터의 지정한 슬롯에 무기를 장착한다.
    /// </summary>
    /// <param name="characterId">무기를 장착할 캐릭터 ID</param>
    /// <param name="slotIndex">장착 슬롯 번호: 0, 1, 2</param>
    /// <param name="weaponId">장착할 무기의 고유 ID</param>
    public void EquipWeapon(string characterId, int slotIndex, string weaponId)
    {
        // 비어 있는 무기 ID는 장착 요청으로 인정하지 않는다.
        if (string.IsNullOrWhiteSpace(weaponId))
        {
            throw new ArgumentException("장착할 무기 ID가 비어 있습니다.", nameof(weaponId));
        }

        // 캐릭터의 장착 정보를 가져온다.
        CharacterLoadoutSaveData loadout = GetOrCreateCharacterLoadout(characterId);

        // 슬롯 번호가 0~2 범위를 벗어나면 코드 오류다.
        if (slotIndex < 0 || slotIndex >= loadout.equippedWeaponIds.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "무기 슬롯 번호가 범위를 벗어났습니다.");
        }

        // 지정한 슬롯에 무기 ID를 기록한다.
        loadout.equippedWeaponIds[slotIndex] = weaponId;
    }

    /// <summary>
    /// 지정한 캐릭터의 무기 슬롯을 비운다.
    /// </summary>
    /// <param name="characterId">무기를 해제할 캐릭터 ID</param>
    /// <param name="slotIndex">비울 슬롯 번호: 0, 1, 2</param>
    public void UnequipWeapon(string characterId, int slotIndex)
    {
        // 캐릭터의 장착 정보를 가져온다.
        CharacterLoadoutSaveData loadout = GetOrCreateCharacterLoadout(characterId);

        // 슬롯 번호가 범위를 벗어나면 코드 오류다.
        if (slotIndex < 0 || slotIndex >= loadout.equippedWeaponIds.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "무기 슬롯 번호가 범위를 벗어났습니다.");
        }

        // null을 넣어 해당 슬롯을 장착 해제 상태로 만든다.
        loadout.equippedWeaponIds[slotIndex] = null;
    }
}
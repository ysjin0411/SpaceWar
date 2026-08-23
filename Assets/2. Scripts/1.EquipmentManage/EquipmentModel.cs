using System;

/// <summary>
/// 장비 관리 화면에서 무기가 가질 수 있는 상태
/// </summary>
public enum EquipmentItemState
{
    Locked,         // 캐릭터 레벨이 부족하여 아직 구매할 수 없는 상태
    Owned,          // 이미 보유하고 있는 상태
    Purchasable,    // 레벨과 골드 조건을 만족하여 구매할 수 있는 상태
    NotEnoughGold   // 레벨 조건은 만족하지만 골드가 부족한 상태
}


    /// <summary>
    /// EquipmentManage 화면에서 사용하는 데이터와 장비 규칙을 담당한다.
    /// Unity UI는 직접 제어하지 않는다.
    /// </summary>
    public class EquipmentModel
{
    // 현재 장비를 관리하는 캐릭터의 원본 데이터
    readonly CharacterData _characterData;

    // 현재 캐릭터의 레벨, 보유 무기, 장착 무기가 저장된 데이터
    readonly CharacterLoadoutSaveData _loadout;

    // 골드와 JSON 저장을 담당하는 저장 관리자
    readonly SaveManager _saveManager;

    // 현재 장비를 관리하는 캐릭터 데이터
    public CharacterData CharacterData => _characterData;

    // 현재 캐릭터의 저장 데이터
    public CharacterLoadoutSaveData Loadout => _loadout;

    // 현재 캐릭터의 레벨
    public int CharacterLevel => _loadout.level;

    // 현재 플레이어가 보유한 골드
    public int CurrentGold => _saveManager.CurrentGold;

    // 현재 캐릭터가 사용할 수 있는 전체 무기 목록
    public WeaponData[] AvailableWeapons =>
        _characterData.AvailableWeapons;

    /// <summary>
    /// 장비 관리 화면에서 사용할 Model을 생성한다.
    /// 생성할 때 기본 보유 무기도 저장 데이터에 등록한다.
    /// </summary>
    public EquipmentModel(
        CharacterData characterData,
        CharacterLoadoutSaveData loadout,
        SaveManager saveManager)
    {
        // 캐릭터 원본 데이터가 없다면 장비 화면을 구성할 수 없다.
        if (characterData == null)
        {
            throw new ArgumentNullException(nameof(characterData));
        }

        // 캐릭터 저장 데이터가 없다면 보유 및 장착 상태를 확인할 수 없다.
        if (loadout == null)
        {
            throw new ArgumentNullException(nameof(loadout));
        }

        // 저장 관리자가 없다면 구매 및 저장을 처리할 수 없다.
        if (saveManager == null)
        {
            throw new ArgumentNullException(nameof(saveManager));
        }

        // 전달받은 데이터를 Model 내부에 보관한다.
        _characterData = characterData;
        _loadout = loadout;
        _saveManager = saveManager;

        // WeaponData에서 기본 보유로 설정한 무기를 저장 데이터에 등록한다.
        InitializeOwnedWeapons();
    }

    /// <summary>
    /// WeaponData에서 기본 보유로 설정된 무기를
    /// 현재 캐릭터의 보유 무기 목록에 등록한다.
    /// </summary>
    private void InitializeOwnedWeapons()
    {
        // 사용할 수 있는 무기 배열이 없다면 캐릭터 데이터 설정 오류다.
        if (_characterData.AvailableWeapons == null)
        {
            throw new InvalidOperationException(
                $"{_characterData.CharacterName}의 AvailableWeapons가 비어 있습니다.");
        }

        // 실제로 보유 목록이 변경됐는지 확인한다.
        bool saveDataChanged = false;

        // 현재 캐릭터가 사용할 수 있는 전체 무기를 순서대로 확인한다.
        for (int i = 0;
             i < _characterData.AvailableWeapons.Length;
             i++)
        {
            // 현재 순서의 무기 데이터를 가져온다.
            WeaponData weaponData =
                _characterData.AvailableWeapons[i];

            // 배열에 빈 무기가 들어 있다면 Inspector 설정 오류다.
            if (weaponData == null)
            {
                throw new InvalidOperationException(
                    $"{_characterData.CharacterName}의 " +
                    $"AvailableWeapons {i}번 항목이 비어 있습니다.");
            }

            // 기본 보유 무기가 아니라면 저장 목록에 추가하지 않는다.
            if (!weaponData.IsInitiallyOwned)
            {
                continue;
            }

            // 이미 보유 목록에 있는 무기는 중복으로 추가하지 않는다.
            if (_loadout.ownedWeaponIds.Contains(weaponData.Id))
            {
                continue;
            }

            // 기본 보유 무기의 ID를 보유 목록에 추가한다.
            _loadout.ownedWeaponIds.Add(weaponData.Id);

            // 저장 데이터가 변경됐다고 기록한다.
            saveDataChanged = true;
        }

        // 새로운 기본 보유 무기가 추가된 경우에만 JSON 파일을 저장한다.
        if (saveDataChanged)
        {
            _saveManager.SaveToFile();
        }
    }

    /// <summary>
    /// 현재 캐릭터가 해당 무기를 보유하고 있는지 확인한다.
    /// </summary>
    public bool IsOwned(WeaponData weaponData)
    {
        // 확인할 무기 데이터가 없다면 잘못된 호출이다.
        if (weaponData == null)
        {
            throw new ArgumentNullException(nameof(weaponData));
        }

        // SaveManager를 통해 보유 무기 ID 목록을 확인한다.
        return _saveManager.OwnsWeapon(
            _characterData.Id,
            weaponData.Id);
    }

    /// <summary>
    /// 현재 캐릭터의 레벨, 보유 상태, 골드를 이용해
    /// 지정한 무기의 장비 화면 상태를 반환한다.
    /// </summary>
    public EquipmentItemState GetItemState(
        WeaponData weaponData)
    {
        // 상태를 확인할 무기 데이터가 없다면 잘못된 호출이다.
        if (weaponData == null)
        {
            throw new ArgumentNullException(nameof(weaponData));
        }

        // 캐릭터 레벨이 요구 레벨보다 낮으면 잠금 상태다.
        // 잠금 상태여도 슬롯 선택과 상세 정보 표시는 가능하다.
        if (CharacterLevel <
            weaponData.RequiredCharacterLevel)
        {
            return EquipmentItemState.Locked;
        }

        // 레벨 조건을 만족하고 이미 보유한 무기라면 보유 상태다.
        if (IsOwned(weaponData))
        {
            return EquipmentItemState.Owned;
        }

        // 보유하지 않았고 현재 골드가 가격 이상이면 구매할 수 있다.
        if (CurrentGold >= weaponData.Price)
        {
            return EquipmentItemState.Purchasable;
        }

        // 레벨 조건은 만족했지만 골드가 부족한 상태다.
        return EquipmentItemState.NotEnoughGold;
    }

    /// <summary>
    /// 지정한 무기의 현재 상태를 확인하고 구매를 시도한다.
    /// 구매 규칙은 Model이 판단하고 실제 저장은 SaveManager가 담당한다.
    /// </summary>
    /// <param name="weaponData">구매할 무기 데이터</param>
    /// <returns>구매에 성공하면 true, 구매할 수 없으면 false</returns>
    public bool TryPurchase(WeaponData weaponData)
    {
        // 구매할 무기 데이터가 없다면 잘못된 호출이다.
        if (weaponData == null)
        {
            throw new ArgumentNullException(nameof(weaponData));
        }

        // 잠금, 보유 중, 골드 부족 상태에서는 구매하지 않는다.
        if (GetItemState(weaponData) !=
            EquipmentItemState.Purchasable)
        {
            return false;
        }

        // 레벨과 골드 조건을 만족한 경우 SaveManager에 구매를 요청한다.
        return _saveManager.TryPurchaseWeapon(
            _characterData.Id,
            weaponData.Id,
            weaponData.Price);
    }

    /// <summary>
    /// 지정한 장착 슬롯에 들어 있는 무기 데이터를 반환한다.
    /// 빈 슬롯이라면 null을 반환한다.
    /// </summary>
    /// <param name="slotIndex">장착 슬롯 번호: 0, 1, 2</param>
    /// <returns>장착된 무기 데이터 또는 빈 슬롯일 경우 null</returns>
    public WeaponData GetEquippedWeaponAt(int slotIndex)
    {
        // 장착 슬롯 배열의 범위를 벗어난 번호는 사용할 수 없다.
        if (slotIndex < 0 ||
            slotIndex >= _loadout.equippedWeaponIds.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slotIndex),
                slotIndex,
                "장착 슬롯 번호가 범위를 벗어났습니다.");
        }

        // 해당 슬롯에 저장된 무기 ID를 가져온다.
        string equippedWeaponId =
            _loadout.equippedWeaponIds[slotIndex];

        // 저장된 무기 ID가 없다면 현재 슬롯은 빈 슬롯이다.
        if (string.IsNullOrWhiteSpace(equippedWeaponId))
        {
            return null;
        }

        // 현재 캐릭터가 사용할 수 있는 무기 중에서
        // 저장된 ID와 일치하는 WeaponData를 찾는다.
        for (int i = 0; i < AvailableWeapons.Length; i++)
        {
            // 현재 순서의 무기 데이터를 가져온다.
            WeaponData weaponData =
                AvailableWeapons[i];

            // AvailableWeapons에 빈 데이터가 있다면 Inspector 설정 오류다.
            if (weaponData == null)
            {
                throw new InvalidOperationException(
                    $"{_characterData.CharacterName}의 " +
                    $"AvailableWeapons {i}번 항목이 비어 있습니다.");
            }

            // 저장된 무기 ID와 같은 WeaponData를 찾으면 반환한다.
            if (weaponData.Id == equippedWeaponId)
            {
                return weaponData;
            }
        }

        // 저장 파일에는 무기 ID가 있지만 현재 캐릭터의
        // AvailableWeapons에서 찾을 수 없는 비정상 상태다.
        throw new InvalidOperationException(
            $"장착 무기 ID '{equippedWeaponId}'를 " +
            $"{_characterData.CharacterName}의 AvailableWeapons에서 " +
            "찾을 수 없습니다.");
    }

    /// <summary>
    /// 보유 중인 무기를 지정한 장착 슬롯에 장착한다.
    /// 장착에 성공하면 변경된 장착 정보를 저장한다.
    /// </summary>
    /// <param name="weaponData">장착할 무기 데이터</param>
    /// <param name="slotIndex">장착할 슬롯 번호: 0, 1, 2</param>
    /// <returns>장착 정보가 변경되면 true, 장착할 수 없으면 false</returns>
    public bool TryEquipWeapon(
        WeaponData weaponData,
        int slotIndex)
    {
        // 장착할 무기 데이터가 없다면 잘못된 호출이다.
        if (weaponData == null)
        {
            throw new ArgumentNullException(nameof(weaponData));
        }

        // 장착 슬롯 배열의 범위를 벗어난 번호는 사용할 수 없다.
        if (slotIndex < 0 ||
            slotIndex >= _loadout.equippedWeaponIds.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slotIndex),
                slotIndex,
                "장착 슬롯 번호가 범위를 벗어났습니다.");
        }

        // 잠금 상태이거나 구매하지 않은 무기는 장착할 수 없다.
        if (GetItemState(weaponData) !=
            EquipmentItemState.Owned)
        {
            return false;
        }

        // 같은 무기가 다른 슬롯에 이미 장착되어 있는지 확인한다.
        for (int i = 0;
             i < _loadout.equippedWeaponIds.Length;
             i++)
        {
            // 현재 슬롯에 같은 무기 ID가 없다면 다음 슬롯을 확인한다.
            if (_loadout.equippedWeaponIds[i] != weaponData.Id)
            {
                continue;
            }

            // 대상 슬롯에 이미 같은 무기가 장착된 경우에도
            // 저장 데이터가 변경되지 않으므로 false를 반환한다.
            if (i == slotIndex)
            {
                return false;
            }

            // 다른 슬롯에 같은 무기가 있다면 중복 장착하지 않는다.
            return false;
        }

        // 모든 장착 조건을 만족하면 SaveManager에 장착을 요청한다.
        // 대상 슬롯에 있던 기존 무기 ID는 새로운 무기 ID로 교체된다.
        _saveManager.EquipWeapon(
            _characterData.Id,
            slotIndex,
            weaponData.Id);

        // 장착 및 저장이 정상적으로 완료됐다.
        return true;
    }

    /// <summary>
    /// 두 장착 슬롯의 무기를 서로 교환한다.
    /// 출발 슬롯이 비어 있거나 같은 슬롯을 지정하면 교환하지 않는다.
    /// </summary>
    /// <param name="sourceSlotIndex">드래그를 시작한 장착 슬롯 번호</param>
    /// <param name="targetSlotIndex">무기를 드롭한 장착 슬롯 번호</param>
    /// <returns>실제로 슬롯 교환이 이루어졌다면 true</returns>
    public bool TrySwapEquippedWeaponSlots(
        int sourceSlotIndex,
        int targetSlotIndex)
    {
        // 출발 슬롯 번호가 장착 슬롯 배열 범위를 벗어나면 잘못된 호출이다.
        if (sourceSlotIndex < 0 ||
            sourceSlotIndex >= _loadout.equippedWeaponIds.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceSlotIndex),
                sourceSlotIndex,
                "출발 장착 슬롯 번호가 범위를 벗어났습니다.");
        }

        // 도착 슬롯 번호가 장착 슬롯 배열 범위를 벗어나면 잘못된 호출이다.
        if (targetSlotIndex < 0 ||
            targetSlotIndex >= _loadout.equippedWeaponIds.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetSlotIndex),
                targetSlotIndex,
                "도착 장착 슬롯 번호가 범위를 벗어났습니다.");
        }

        // 출발 슬롯과 도착 슬롯이 같으면 변경할 내용이 없다.
        if (sourceSlotIndex == targetSlotIndex)
        {
            return false;
        }

        // 출발 슬롯에 장착된 무기가 없다면 드래그 교환을 진행할 수 없다.
        if (string.IsNullOrWhiteSpace(
            _loadout.equippedWeaponIds[sourceSlotIndex]))
        {
            return false;
        }

        // SaveManager를 통해 두 슬롯의 무기 ID를 교환하고 JSON에 저장한다.
        _saveManager.SwapEquippedWeapons(
            _characterData.Id,
            sourceSlotIndex,
            targetSlotIndex);

        // 장착 슬롯 교환이 정상적으로 완료됐다.
        return true;
    }

}
using System;

/// <summary>
/// EquipmentManage 화면의 Model과 View를 연결한다.
/// UI 입력을 Model에 전달하고 처리 결과를 View에 반영한다.
/// </summary>
public class EquipmentPresenter
{
    // 장비 데이터와 구매 및 장착 규칙을 담당하는 Model
    readonly EquipmentModel _model;

    // 실제 EquipmentManage UI 표시를 담당하는 View
    readonly EquipmentManageView _view;

    // 현재 중앙 슬롯에서 선택된 무기 데이터
    WeaponData _selectedWeaponData;

    /// <summary>
    /// 연결할 Model과 View를 전달받아 Presenter를 생성한다.
    /// </summary>
    public EquipmentPresenter(
        EquipmentModel model,
        EquipmentManageView view)
    {
        // Model이 없으면 장비 데이터와 규칙을 처리할 수 없다.
        if (model == null)
        {
            throw new ArgumentNullException(nameof(model));
        }

        // View가 없으면 처리 결과를 화면에 표시할 수 없다.
        if (view == null)
        {
            throw new ArgumentNullException(nameof(view));
        }

        // 전달받은 Model과 View를 Presenter 내부에 저장한다.
        _model = model;
        _view = view;

        // 중복 연결을 방지하기 위해 기존 이벤트 연결을 먼저 제거한다.
        _view.WeaponSelected -=
            HandleWeaponSelected;

        // 중앙 슬롯에서 선택된 무기를 Presenter가 전달받도록 연결한다.
        _view.WeaponSelected +=
            HandleWeaponSelected;

        // 중복 구독을 방지하기 위해 기존 구매 요청 이벤트 연결을 제거한다.
        _view.PurchaseRequested -=
            HandlePurchaseRequested;

        // 구매 버튼을 눌렀을 때 Presenter가 요청을 받도록 연결한다.
        _view.PurchaseRequested +=
            HandlePurchaseRequested;

        // 중복 연결을 방지하기 위해 기존 무기 장착 요청 이벤트를 제거한다.
        _view.WeaponEquipRequested -=
            HandleWeaponEquipRequested;

        // View에서 무기 드롭이 발생하면 Presenter가 장착 요청을 처리한다.
        _view.WeaponEquipRequested +=
            HandleWeaponEquipRequested;

        // 중복 연결을 방지하기 위해 기존 장착 슬롯 교환 이벤트를 제거한다.
        _view.EquippedWeaponSwapRequested -=
            HandleEquippedWeaponSwapRequested;

        // View에서 슬롯 교환이 요청되면 Presenter가 Model에 전달한다.
        _view.EquippedWeaponSwapRequested +=
            HandleEquippedWeaponSwapRequested;
    }

    /// <summary>
    /// EquipmentManage 화면에 처음 필요한 정보를 표시한다.
    /// </summary>
    public void Initialize()
    {
        // 최초 화면에는 선택된 무기가 없으므로 오른쪽 상세 패널을 숨긴다.
        _view.HideWeaponDetail();

        // 화면에 처음 진입했을 때 무기 툴팁이 보이지 않도록 초기화한다.
        _view.HideWeaponTooltip();

        // 선택한 캐릭터의 이미지, 이름, 현재 레벨을 표시한다.
        _view.ShowCharacter(
            _model.CharacterData,
            _model.CharacterLevel);

        // 플레이어가 현재 보유한 골드를 표시한다.
        _view.ShowGold(_model.CurrentGold);

        // 현재 장착된 무기 3개를 왼쪽 슬롯에 표시한다.
        RefreshEquippedWeapons();

        // 현재 캐릭터가 사용할 수 있는 무기 개수만큼
        // 중앙 무기 슬롯을 최초 한 번 생성한다.
        _view.CreateWeaponSlots(
            _model.AvailableWeapons.Length);

        // 현재 캐릭터가 사용할 수 있는 전체 무기를 중앙 슬롯에 표시한다.
        RefreshWeaponSlots();

        // 중앙 무기 슬롯의 선택 이벤트를 View와 연결한다.
        _view.InitializeWeaponSlotEvents();

        // 왼쪽 장착 슬롯에 번호를 부여하고 드롭 이벤트를 연결한다.
        _view.InitializeEquippedWeaponSlotEvents();

        // 우측 가격 프레임의 버튼 클릭 이벤트를 View 내부에서 연결한다.
        _view.InitializePurchaseButtonEvent();
    }

    /// <summary>
    /// 지정한 무기가 현재 드래그 가능한 상태인지 확인한다.
    /// </summary>
    public bool CanDragWeapon(WeaponData weaponData)
    {
        // 무기 데이터가 없다면 드래그할 수 없다.
        if (weaponData == null)
        {
            return false;
        }

        // 구매 완료 후 보유 중인 무기만 드래그할 수 있다.
        return _model.GetItemState(weaponData) ==
               EquipmentItemState.Owned;
    }

    /// <summary>
    /// 선택한 무기의 구매를 시도하고 성공한 경우 골드 UI를 갱신한다.
    /// </summary>
    public bool TryPurchaseWeapon(
        WeaponData weaponData)
    {
        // Model에서 구매할 수 없다고 판단하면 UI를 변경하지 않는다.
        if (!_model.TryPurchase(weaponData))
        {
            return false;
        }

        // 구매로 변경된 현재 골드를 화면에 다시 표시한다.
        _view.ShowGold(_model.CurrentGold);

        // 구매한 무기가 Purchasable 상태에서 Owned 상태로 보이도록
        // 중앙 무기 슬롯 전체를 다시 표시한다.
        RefreshWeaponSlots();

        // 구매한 무기의 오른쪽 상세 정보도 Owned 상태로 갱신한다.
        HandleWeaponSelected(weaponData);

        // 구매가 정상적으로 완료됐다.
        return true;
    }

    /// <summary>
    /// 드래그한 무기를 지정한 장착 슬롯에 장착한다.
    /// 장착에 성공하면 왼쪽 장착 슬롯 전체를 다시 표시한다.
    /// </summary>
    public bool TryEquipWeapon(
        WeaponData weaponData,
        int slotIndex)
    {
        // Model에서 장착할 수 없다고 판단하면 UI를 변경하지 않는다.
        if (!_model.TryEquipWeapon(
            weaponData,
            slotIndex))
        {
            return false;
        }

        // 장착 결과를 왼쪽 장착 슬롯 3개에 다시 표시한다.
        // 추후 무기 교환 기능이 생겨도 전체 슬롯이 올바르게 갱신된다.
        RefreshEquippedWeapons();

        // 장착과 UI 갱신이 정상적으로 완료됐다.
        return true;
    }

    /// <summary>
    /// 두 장착 슬롯의 무기를 교환하고
    /// 성공하면 장착 슬롯 UI 전체를 다시 표시한다.
    /// </summary>
    public bool TrySwapEquippedWeaponSlots(
        int sourceSlotIndex,
        int targetSlotIndex)
    {
        // Model에서 교환할 수 없다고 판단하면 UI를 갱신하지 않는다.
        if (!_model.TrySwapEquippedWeaponSlots(
            sourceSlotIndex,
            targetSlotIndex))
        {
            return false;
        }

        // 저장 데이터의 새로운 장착 순서에 맞춰
        // 장착 슬롯 1~3의 아이콘과 이름을 모두 다시 표시한다.
        RefreshEquippedWeapons();

        // 장착 슬롯 교환이 성공적으로 완료됐다.
        return true;
    }

    /// <summary>
    /// View에서 전달받은 무기와 장착 슬롯 번호를 사용해
    /// 실제 무기 장착을 요청한다.
    /// </summary>
    private void HandleWeaponEquipRequested(
        WeaponData weaponData,
        int equippedSlotIndex)
    {
        // 기존 장착 함수를 통해 Model의 장착 데이터와 UI를 갱신한다.
        TryEquipWeapon(
            weaponData,
            equippedSlotIndex);
    }

    /// <summary>
    /// View에서 전달받은 출발 슬롯과 도착 슬롯 번호를 사용해
    /// 두 장착 무기의 교환을 요청한다.
    /// </summary>
    private void HandleEquippedWeaponSwapRequested(
        int sourceSlotIndex,
        int targetSlotIndex)
    {
        // Model의 교환 규칙과 저장 처리를 거쳐 장착 UI를 갱신한다.
        TrySwapEquippedWeaponSlots(
            sourceSlotIndex,
            targetSlotIndex);
    }

    /// <summary>
    /// 우측 구매 버튼이 눌렸을 때 현재 선택된 무기의 구매를 시도한다.
    /// </summary>
    private void HandlePurchaseRequested()
    {
        // 아직 중앙 슬롯에서 선택된 무기가 없다면 구매할 대상이 없다.
        if (_selectedWeaponData == null)
        {
            return;
        }

        // 현재 선택된 무기의 구매를 시도한다.
        // 구매 성공 시 TryPurchaseWeapon 내부에서 골드와 슬롯 UI도 갱신된다.
        TryPurchaseWeapon(_selectedWeaponData);
    }

    /// <summary>
    /// 중앙 슬롯에서 무기가 선택됐을 때
    /// 해당 무기의 현재 상태를 오른쪽 상세 패널에 표시한다.
    /// </summary>
    private void HandleWeaponSelected(
        WeaponData weaponData)
    {
        // 선택된 무기 데이터가 없다면 상세 정보를 표시하지 않는다.
        if (weaponData == null)
        {
            _selectedWeaponData = null;
            _view.HideWeaponDetail();
            return;
        }

        // 구매 버튼과 이후 입력에서 사용할 수 있도록 선택 무기를 저장한다.
        _selectedWeaponData = weaponData;

        // 선택된 무기의 현재 잠금, 보유, 구매 가능 상태를 가져온다.
        EquipmentItemState itemState =
            _model.GetItemState(weaponData);

        // 오른쪽 패널에 무기 데이터와 현재 상태를 표시한다.
        _view.ShowWeaponDetail(
            weaponData,
            itemState);
    }

    /// <summary>
    /// 현재 캐릭터가 사용할 수 있는 전체 무기를
    /// 중앙 슬롯에 순서대로 표시한다.
    /// </summary>
    private void RefreshWeaponSlots()
    {
        // 현재 캐릭터가 사용할 수 있는 전체 무기 목록을 가져온다.
        WeaponData[] availableWeapons =
            _model.AvailableWeapons;

        // 전체 무기를 중앙 슬롯 순서대로 확인한다.
        for (int slotIndex = 0;
             slotIndex < availableWeapons.Length;
             slotIndex++)
        {
            // 현재 중앙 슬롯에 표시할 무기 데이터를 가져온다.
            WeaponData weaponData =
                availableWeapons[slotIndex];

            // Model을 통해 잠금, 보유, 구매 가능 등의 상태를 판정한다.
            EquipmentItemState itemState =
                _model.GetItemState(weaponData);

            // View가 중앙 슬롯에 무기 아이콘과 상태를 표시하도록 한다.
            _view.ShowWeaponSlot(
                slotIndex,
                weaponData,
                itemState);
        }
    }

    /// <summary>
    /// 현재 저장된 장착 무기 정보를 왼쪽 슬롯 1~3에 표시한다.
    /// </summary>
    private void RefreshEquippedWeapons()
    {
        // 저장 데이터의 장착 슬롯 개수만큼 반복한다.
        for (int slotIndex = 0;
             slotIndex <
             _model.Loadout.equippedWeaponIds.Length;
             slotIndex++)
        {
            // 현재 슬롯에 장착된 WeaponData를 가져온다.
            WeaponData equippedWeapon =
                _model.GetEquippedWeaponAt(slotIndex);

            // 해당 슬롯의 무기 아이콘과 이름을 View에 표시한다.
            _view.ShowEquippedWeapon(
                slotIndex,
                equippedWeapon);
        }

        // 최초 진입, 무기 장착, 슬롯 교환 후 캐릭터 외형도 함께 갱신한다.
        _view.ShowCharacterPreview(
            _model.CharacterData,
            _model.GetEquippedWeaponAt(0));
    }


}
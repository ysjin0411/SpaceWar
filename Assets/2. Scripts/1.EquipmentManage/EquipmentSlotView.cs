using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 중앙 무기 슬롯 하나의 표시와 입력을 담당한다.
/// 선택, 마우스 진입, 드래그 입력을 이벤트로 외부에 전달한다.
/// </summary>
public class EquipmentSlotView :
    MonoBehaviour,
    ISelectHandler,
    IPointerEnterHandler,
    IPointerExitHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    [Header("무기 아이콘")]

    [SerializeField]
    Image _unselectedWeaponIcon; // 선택되지 않은 슬롯에 표시할 무기 아이콘

    [SerializeField]
    Image _selectedWeaponIcon; // 선택된 슬롯에 표시할 무기 아이콘

    [Header("슬롯 상태 오브젝트")]

    [SerializeField]
    GameObject _selectedVisual; // 현재 키보드 또는 마우스로 선택된 슬롯 표시

    [SerializeField]
    GameObject _lockedUnselectedVisual; // 잠금 슬롯이 선택되지 않았을 때 표시할 오브젝트

    [SerializeField]
    GameObject _lockedSelectedVisual; // 잠금 슬롯이 선택됐을 때 표시할 오브젝트

    // 이 View가 중앙 무기 목록에서 담당하는 순서
    int _slotIndex;

    // 이 슬롯에 표시하고 있는 무기 데이터
    WeaponData _weaponData;

    // 현재 무기의 장비 화면 상태
    EquipmentItemState _itemState;

    // 현재 실제 드래그가 진행 중인지 확인하는 값
    bool _isDragging;

    // 중앙 무기 목록에서 이 슬롯이 담당하는 순서
    public int SlotIndex => _slotIndex;

    // 이 슬롯에 연결된 무기 데이터
    public WeaponData WeaponData => _weaponData;

    // 이 슬롯에 연결된 무기의 현재 상태
    public EquipmentItemState ItemState => _itemState;

    // 현재 실제 드래그가 진행 중인지 확인
    public bool IsDragging => _isDragging;

    // 키보드 또는 마우스로 이 슬롯이 선택됐을 때 전달하는 이벤트
    public event Action<EquipmentSlotView> Selected;

    // 마우스 커서가 슬롯 위로 들어왔을 때 전달하는 이벤트
    public event Action<EquipmentSlotView, PointerEventData>
        PointerEntered;

    // 마우스 커서가 슬롯 밖으로 나갔을 때 전달하는 이벤트
    public event Action<EquipmentSlotView> PointerExited;

    // 보유 무기의 드래그가 시작됐을 때 전달하는 이벤트
    public event Action<EquipmentSlotView, PointerEventData>
        DragStarted;

    // 드래그 중 마우스 위치가 변경될 때 전달하는 이벤트
    public event Action<PointerEventData> DragMoved;

    // 드롭 성공 여부와 관계없이 드래그가 끝났을 때 전달하는 이벤트
    public event Action<EquipmentSlotView, PointerEventData>
        DragEnded;

    /// <summary>
    /// 이 슬롯에 표시할 무기와 상태를 설정한다.
    /// </summary>
    public void Bind(
        int slotIndex,
        WeaponData weaponData,
        EquipmentItemState itemState)
    {
        // 슬롯 순서는 0 이상이어야 한다.
        if (slotIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slotIndex),
                slotIndex,
                "중앙 무기 슬롯 번호는 0 이상이어야 합니다.");
        }

        // 중앙 슬롯에는 반드시 표시할 무기 데이터가 있어야 한다.
        if (weaponData == null)
        {
            throw new ArgumentNullException(nameof(weaponData));
        }

        // 전달받은 슬롯 정보와 무기 정보를 저장한다.
        _slotIndex = slotIndex;
        _weaponData = weaponData;
        _itemState = itemState;

        // 모든 상태에서 사용할 무기 아이콘을 설정한다.
        SetIcon(_unselectedWeaponIcon, weaponData.Icon);
        SetIcon(_selectedWeaponIcon, weaponData.Icon);

        // 화면 최초 진입 시에는 모든 슬롯을 선택 해제 상태로 만든다.
        SetSelected(false);

        // 새로운 데이터가 연결될 때 이전 드래그 상태를 초기화한다.
        _isDragging = false;
    }

    /// <summary>
    /// 슬롯의 아이템 상태와 포커스 상태를 조합해
    /// 현재 표시해야 할 슬롯 이미지를 결정한다.
    /// </summary>
    public void SetSelected(bool isSelected)
    {
        // 요구 레벨을 만족하지 못한 무기인지 확인한다.
        bool isLocked =
            _itemState == EquipmentItemState.Locked;

        // 잠기지 않은 슬롯이 선택된 경우에만
        // 일반 선택 이미지를 표시한다.
        _selectedVisual.SetActive(
            !isLocked && isSelected);

        // 잠금 슬롯이 선택되지 않은 경우에만
        // 잠금 미선택 이미지를 표시한다.
        _lockedUnselectedVisual.SetActive(
            isLocked && !isSelected);

        // 잠금 슬롯이 선택된 경우에만
        // 잠금 선택 이미지를 표시한다.
        _lockedSelectedVisual.SetActive(
            isLocked && isSelected);
    }

    /// <summary>
    /// EventSystem이 이 슬롯을 선택했을 때 호출된다.
    /// </summary>
    public void OnSelect(BaseEventData eventData)
    {
        // Presenter가 선택된 무기의 오른쪽 상세 정보를 표시하도록 알린다.
        Selected?.Invoke(this);
    }

    /// <summary>
    /// 마우스 커서가 슬롯 안으로 들어왔을 때 호출된다.
    /// </summary>
    public void OnPointerEnter(
        PointerEventData eventData)
    {
        // 드래그 중에는 툴팁을 다시 표시하지 않는다.
        if (_isDragging)
        {
            return;
        }

        // View가 해당 무기의 툴팁을 표시하도록 알린다.
        PointerEntered?.Invoke(this, eventData);
    }

    /// <summary>
    /// 마우스 커서가 슬롯 밖으로 나갔을 때 호출된다.
    /// </summary>
    public void OnPointerExit(
        PointerEventData eventData)
    {
        // 드래그 중에는 툴팁 상태를 별도로 변경하지 않는다.
        if (_isDragging)
        {
            return;
        }

        // View가 툴팁을 숨기도록 알린다.
        PointerExited?.Invoke(this);
    }

    /// <summary>
    /// 마우스로 무기 슬롯의 드래그를 시작했을 때 호출된다.
    /// </summary>
    public void OnBeginDrag(
        PointerEventData eventData)
    {
        // 보유 중인 무기만 장착 슬롯으로 드래그할 수 있다.
        if (_itemState != EquipmentItemState.Owned)
        {
            return;
        }

        // 실제 드래그가 시작됐다고 기록한다.
        _isDragging = true;

        // View가 툴팁을 숨기고 드롭 가이드 이펙트를 켜도록 알린다.
        DragStarted?.Invoke(this, eventData);
    }

    /// <summary>
    /// 드래그 중 매 프레임 호출된다.
    /// </summary>
    public void OnDrag(
        PointerEventData eventData)
    {
        // 실제 드래그가 시작되지 않았다면 이동을 처리하지 않는다.
        if (!_isDragging)
        {
            return;
        }

        // View가 드래그 아이콘을 현재 마우스 위치로 이동하도록 알린다.
        DragMoved?.Invoke(eventData);
    }

    /// <summary>
    /// 정상 드롭 또는 취소로 드래그가 끝났을 때 호출된다.
    /// </summary>
    public void OnEndDrag(
        PointerEventData eventData)
    {
        // 실제 드래그가 시작되지 않았다면 종료 처리를 하지 않는다.
        if (!_isDragging)
        {
            return;
        }

        // 종료 이벤트가 전달될 때까지는 드래그 상태를 유지한다.
        // View가 이 시점에 툴팁과 드롭 가이드 이펙트를 정리한다.
        DragEnded?.Invoke(this, eventData);

        // 모든 종료 처리가 끝난 뒤 드래그 상태를 해제한다.
        _isDragging = false;
    }

    /// <summary>
    /// 지정한 Image에 무기 아이콘을 표시한다.
    /// </summary>
    private void SetIcon(
        Image targetImage,
        Sprite weaponIcon)
    {
        // Inspector에서 Image 연결이 빠졌다면 즉시 설정 오류를 알린다.
        if (targetImage == null)
        {
            throw new InvalidOperationException(
                "EquipmentSlotView의 무기 아이콘 Image가 연결되지 않았습니다.");
        }

        // 무기 아이콘과 비율 유지 설정을 적용한다.
        targetImage.sprite = weaponIcon;
        targetImage.preserveAspect = true;
        targetImage.enabled = weaponIcon != null;
    }
}
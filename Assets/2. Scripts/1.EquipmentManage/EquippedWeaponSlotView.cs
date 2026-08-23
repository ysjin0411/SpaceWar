using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 왼쪽 장착 무기 슬롯 하나의 드롭 입력과
/// 드롭 위치 안내 이펙트를 담당한다.
/// </summary>
public class EquippedWeaponSlotView :
    MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IDropHandler
{
    [Header("드롭 안내")]

    [SerializeField]
    GameObject _dropGuideEffect; // 무기를 놓을 수 있는 위치임을 알려주는 이펙트

    // 왼쪽 장착 슬롯에서 이 View가 담당하는 번호
    int _slotIndex;

    // 현재 이 장착 슬롯에 들어 있는 무기 데이터
    WeaponData _weaponData;

    // 현재 이 장착 슬롯에서 드래그가 진행 중인지 기록한다.
    bool _isDragging;

    // 현재 드롭 안내 이펙트가 활성화되어 있는지 확인하는 값
    bool _isDropGuideVisible;

    // 이 View가 담당하는 장착 슬롯 번호
    public int SlotIndex => _slotIndex;

    // 현재 이 장착 슬롯에 들어 있는 무기 데이터
    public WeaponData WeaponData => _weaponData;

    // 현재 이 장착 슬롯에서 드래그가 진행 중인지 확인한다.
    public bool IsDragging => _isDragging;

    // 현재 드롭 안내 이펙트가 켜져 있는지 확인
    public bool IsDropGuideVisible =>
        _isDropGuideVisible;

    // 장착 슬롯의 무기를 드래그하기 시작했을 때 View에 전달하는 이벤트
    public event Action<EquippedWeaponSlotView, PointerEventData>
        DragStarted;

    // 장착 슬롯의 무기를 드래그하는 동안 커서 위치를 전달하는 이벤트
    public event Action<PointerEventData>
        DragMoved;

    // 장착 슬롯의 무기 드래그가 끝났을 때 View에 전달하는 이벤트
    public event Action<EquippedWeaponSlotView, PointerEventData>
        DragEnded;

    // 이 장착 슬롯에 드롭이 발생했을 때 View에 전달하는 이벤트
    public event Action<int, PointerEventData>
        DropReceived;

    /// <summary>
    /// 이 View가 담당할 장착 슬롯 번호를 설정한다.
    /// </summary>
    /// <param name="slotIndex">장착 슬롯 번호: 0, 1, 2</param>
    public void Bind(int slotIndex)
    {
        // 장착 슬롯은 0, 1, 2만 사용할 수 있다.
        if (slotIndex < 0 || slotIndex >= 3)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slotIndex),
                slotIndex,
                "장착 슬롯 번호는 0부터 2까지 사용할 수 있습니다.");
        }

        // 전달받은 장착 슬롯 번호를 저장한다.
        _slotIndex = slotIndex;

        // 새로운 화면 초기화 시 이전 드래그 상태를 제거한다.
        _isDragging = false;

        // 화면 최초 진입 시에는 드롭 안내 이펙트를 숨긴다.
        SetDropGuideVisible(false);
    }

    /// <summary>
    /// 현재 장착 슬롯에 들어 있는 무기 데이터를 저장한다.
    /// null을 전달하면 빈 장착 슬롯으로 처리한다.
    /// </summary>
    public void SetWeapon(
        WeaponData weaponData)
    {
        // View가 표시한 무기와 드래그에 사용할 무기 데이터를 동일하게 유지한다.
        _weaponData = weaponData;
    }

    /// <summary>
    /// 무기를 놓을 수 있는 위치를 알려주는 이펙트를 켜거나 끈다.
    /// </summary>
    public void SetDropGuideVisible(bool isVisible)
    {
        // Inspector에서 이펙트 연결이 빠졌다면 설정 오류를 알린다.
        if (_dropGuideEffect == null)
        {
            throw new InvalidOperationException(
                "EquippedWeaponSlotView의 DropGuideEffect가 연결되지 않았습니다.");
        }

        // 현재 드롭 가이드 표시 상태를 저장한다.
        _isDropGuideVisible = isVisible;

        // 드롭 안내 이펙트 오브젝트의 활성 상태를 변경한다.
        _dropGuideEffect.SetActive(isVisible);
    }

    /// <summary>
    /// 장착 중인 무기를 다른 장착 슬롯으로 옮기기 위해
    /// 드래그를 시작했을 때 호출된다.
    /// </summary>
    public void OnBeginDrag(
        PointerEventData eventData)
    {
        // 빈 장착 슬롯에서는 드래그를 시작할 수 없다.
        if (_weaponData == null)
        {
            return;
        }

        // 드롭 처리 시 출발 슬롯을 확인할 수 있도록 드래그 상태를 기록한다.
        _isDragging = true;

        // View가 드래그 아이콘과 장착 위치 가이드를 표시하도록 알린다.
        DragStarted?.Invoke(
            this,
            eventData);
    }

    /// <summary>
    /// 장착 슬롯의 무기를 드래그하는 동안 호출된다.
    /// </summary>
    public void OnDrag(
        PointerEventData eventData)
    {
        // 실제로 드래그가 시작되지 않았다면 이동 이벤트를 전달하지 않는다.
        if (!_isDragging)
        {
            return;
        }

        // View가 드래그 아이콘을 현재 커서 위치로 이동하도록 알린다.
        DragMoved?.Invoke(eventData);
    }

    /// <summary>
    /// 장착 슬롯 교환이 성공하거나 드래그가 취소됐을 때 호출된다.
    /// </summary>
    public void OnEndDrag(
        PointerEventData eventData)
    {
        // 실제로 드래그가 시작되지 않았다면 종료 처리도 하지 않는다.
        if (!_isDragging)
        {
            return;
        }

        // View가 드래그 아이콘과 가이드 이펙트를 숨기도록 알린다.
        DragEnded?.Invoke(
            this,
            eventData);

        // 드롭 처리가 끝난 다음 드래그 상태를 해제한다.
        _isDragging = false;
    }

    /// <summary>
    /// 드래그 중인 UI가 이 장착 슬롯에 놓였을 때 호출된다.
    /// </summary>
    public void OnDrop(PointerEventData eventData)
    {
        // 드래그 중이 아니라면 드롭 요청을 전달하지 않는다.
        if (eventData.pointerDrag == null)
        {
            return;
        }

        // View가 현재 드래그 중인 무기와 이 슬롯 번호를
        // Presenter에 전달할 수 있도록 드롭 이벤트를 발생시킨다.
        DropReceived?.Invoke(
            _slotIndex,
            eventData);
    }

    /// <summary>
    /// 씬 전환이나 오브젝트 비활성화 중에도
    /// 드롭 가이드 이펙트가 남지 않도록 정리한다.
    /// </summary>
    private void OnDisable()
    {
        // 오브젝트가 비활성화되면 진행 중이던 드래그 상태도 해제한다.
        _isDragging = false;

        // 아직 이펙트가 연결되지 않은 편집 중 상태도 허용한다.
        if (_dropGuideEffect == null)
        {
            return;
        }

        // 오브젝트가 비활성화될 때 드롭 가이드도 함께 끈다.
        _isDropGuideVisible = false;
        _dropGuideEffect.SetActive(false);
    }
}
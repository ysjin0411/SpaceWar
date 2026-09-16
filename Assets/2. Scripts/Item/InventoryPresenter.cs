using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 인벤토리 열기·닫기, 입력 차단, 슬롯 표시, 드래그 교환을 조정한다.
public class InventoryPresenter : MonoBehaviour
{
    // 켜고 끌 Canvas_Inventory의 Canvas다.
    [SerializeField] private Canvas _inventoryCanvas;

    // 기존 18개 슬롯이 직접 자식으로 배치된 Content다.
    [SerializeField] private Transform _content;

    // 저장된 아이템 ID로 이름과 아이콘을 찾는 데이터베이스다.
    [SerializeField] private ItemDatabase _database;

    // 인벤토리가 열린 동안 조작을 차단할 Town 플레이어다.
    [SerializeField] private TownPlayerController _player;

    // 화면 왼쪽 가방 버튼이다.
    [SerializeField] private Button _bagButton;

    // 인벤토리 우측 상단 닫기 버튼이다.
    [SerializeField] private Button _closeButton;

    // 가방 버튼에 추가할 Canvas다.
    // 인벤토리가 열렸을 때만 버튼을 차단막 위로 올린다.
    [SerializeField] private Canvas _bagCanvas;

    // 키보드로 배경 UI를 선택하거나 실행하는 것을 차단한다.
    [SerializeField] private EventSystem _eventSystem;

    // 아이템 상세 정보를 표시할 툴팁이다.
    [SerializeField] private InventoryTooltipView _tooltip;

    // Canvas 아래 DraggingSlot의 최상위 RectTransform이다.
    [SerializeField] private RectTransform _draggingSlot;

    // DraggingSlot 자식 Icon의 Image다.
    [SerializeField] private Image _dragIcon;

    // Context Menu로 지급할 아이템 프리팹의 Item이다.
    // ItemDatabase에 등록된 프리팹을 연결한다.
    [SerializeField] private Item _testItem;

    // Context Menu를 한 번 실행할 때 지급할 수량이다.
    [SerializeField, Min(1)] private int _testItemAmount = 1;

    // 기존 슬롯과 현재 표시한 아이템 정보를 보관한다.
    private readonly InventorySlotView[] _slots =
        new InventorySlotView[InventoryService.Capacity];

    private readonly Item[] _items =
        new Item[InventoryService.Capacity];

    // 현재 인벤토리가 구독하는 저장 관리자다.
    private SaveManager _saveManager;

    // 초기화·열림 상태와 열기 전 UI 키보드 탐색 설정이다.
    private bool _ready;
    private bool _isOpen;
    private bool _previousNavigation;

    // 마우스가 올라간 슬롯과 드래그 출발 슬롯이다.
    // -1은 해당 슬롯이 없다는 뜻이다.
    private int _hoverIndex = -1;
    private int _dragIndex = -1;

    // I키와 버튼이 같은 프레임에 들어와 두 번 토글되는 것을 막는다.
    private int _lastToggleFrame = -1;

    // Inspector 활성 상태와 관계없이 닫힌 상태로 준비한다.
    private void Awake()
    {
        if (_inventoryCanvas == null || _content == null ||
            _database == null || _player == null ||
            _bagButton == null || _closeButton == null ||
            _bagCanvas == null || _eventSystem == null ||
            _tooltip == null || _draggingSlot == null || _dragIcon == null)
        {
            throw new InvalidOperationException(
                "InventoryPresenter의 Inspector 연결을 확인해주세요.");
        }

        if (transform.IsChildOf(_inventoryCanvas.transform))
        {
            throw new InvalidOperationException(
                "InventoryManager는 Canvas_Inventory 밖에 배치해주세요.");
        }

        // 현재 Town 씬의 Screen Space Overlay 방식을 사용한다.
        if (_inventoryCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            throw new InvalidOperationException(
                "Canvas_Inventory를 Screen Space Overlay로 설정해주세요.");
        }

        if (_content.childCount != InventoryService.Capacity)
        {
            throw new InvalidOperationException(
                "Content의 직접 자식은 인벤토리 슬롯 18개여야 합니다.");
        }

        for (int i = 0; i < _slots.Length; i++)
        {
            InventorySlotView slot =
                _content.GetChild(i).GetComponent<InventorySlotView>();

            if (slot == null)
            {
                throw new InvalidOperationException(
                    $"Content의 {i}번 자식에 InventorySlotView가 없습니다.");
            }

            _slots[i] = slot;
            slot.Initialize(this, i);
            slot.Bind(null, 0);
        }

        _tooltip.Initialize();

        // 드래그 이미지의 중심을 커서에 맞춘다.
        _draggingSlot.pivot = new Vector2(0.5f, 0.5f);
        _dragIcon.preserveAspect = true;

        // 드래그 이미지가 아래쪽 슬롯의 드롭 판정을 막지 않게 한다.
        foreach (Graphic graphic in
                 _draggingSlot.GetComponentsInChildren<Graphic>(true))
        {
            graphic.raycastTarget = false;
        }

        _draggingSlot.gameObject.SetActive(false);
        _inventoryCanvas.gameObject.SetActive(false);

        // 평소에는 기존 HUD의 정렬을 따른다.
        _bagCanvas.overrideSorting = false;

        _ready = true;
    }

    // 버튼 이벤트를 코드에서 연결한다.
    private void OnEnable()
    {
        if (!_ready)
        {
            return;
        }

        _bagButton.onClick.AddListener(Toggle);
        _closeButton.onClick.AddListener(Close);
    }

    // 인벤토리의 유지 조건과 열기·닫기 키 입력을 확인한다.
    private void Update()
    {
        if (!_ready)
        {
            return;
        }

        // 로그아웃이나 외부 비활성화 시 조작 잠금도 함께 해제한다.
        if (_isOpen &&
            (!_inventoryCanvas.gameObject.activeInHierarchy ||
             _saveManager == null || !_saveManager.IsLoggedIn ||
             SaveManager.Instance != _saveManager ||
             !_player.isActiveAndEnabled))
        {
            Close();
            return;
        }

        // Esc는 열린 인벤토리를 닫기만 한다.
        if (_isOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            // 같은 프레임의 I키나 가방 버튼이 다시 여는 것을 막는다.
            _lastToggleFrame = Time.frameCount;

            // 기존 닫기 처리가 드래그, 툴팁, 조작 잠금까지 정리한다.
            Close();
            return;
        }

        // I키는 인벤토리의 열림 상태를 전환한다.
        if (Input.GetKeyDown(KeyCode.I))
        {
            Toggle();
        }
    }

    // 가방 버튼과 I키가 사용하는 공통 토글이다.
    public void Toggle()
    {
        if (!_ready || !isActiveAndEnabled ||
            _lastToggleFrame == Time.frameCount)
        {
            return;
        }

        _lastToggleFrame = Time.frameCount;

        if (_isOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    // 다른 대화나 창이 플레이어 조작을 잠근 상태에서는 열지 않는다.
    private void Open()
    {
        SaveManager saveManager = SaveManager.Instance;

        if (_isOpen || !_player.isActiveAndEnabled ||
            !_player.CanControl || Time.timeScale == 0f ||
            saveManager == null || !saveManager.IsLoggedIn)
        {
            return;
        }

        _saveManager = saveManager;

        // 데이터 오류가 있으면 조작을 잠그기 전에 알린다.
        try
        {
            Render();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            return;
        }

        _hoverIndex = -1;
        _dragIndex = -1;
        _isOpen = true;

        _previousNavigation = _eventSystem.sendNavigationEvents;
        _eventSystem.sendNavigationEvents = false;
        _eventSystem.SetSelectedGameObject(null);

        _player.SetControlEnabled(false);

        // 열린 상태에서도 가방 버튼으로 다시 닫을 수 있게 한다.
        _bagCanvas.sortingLayerID = _inventoryCanvas.sortingLayerID;
        _bagCanvas.overrideSorting = true;
        _bagCanvas.sortingOrder = _inventoryCanvas.sortingOrder + 1;

        // 기존 SaveToFile이 저장 성공 후 발생시키는 이벤트를 구독한다.
        _saveManager.UserStatusChanged += HandleSaved;

        _inventoryCanvas.gameObject.SetActive(true);
        _tooltip.Hide();
        _draggingSlot.gameObject.SetActive(false);
    }

    // 닫기 버튼, I키, 가방 버튼의 닫기 처리를 통합한다.
    public void Close()
    {
        if (!_ready || !_isOpen)
        {
            return;
        }

        _isOpen = false;
        _hoverIndex = -1;
        _dragIndex = -1;

        if (_saveManager != null)
        {
            _saveManager.UserStatusChanged -= HandleSaved;
        }

        _tooltip.Hide();
        _draggingSlot.gameObject.SetActive(false);
        _inventoryCanvas.gameObject.SetActive(false);

        _bagCanvas.overrideSorting = false;

        _eventSystem.SetSelectedGameObject(null);
        _eventSystem.sendNavigationEvents = _previousNavigation;

        // 이 창이 열리면서 잠근 조작을 복구한다.
        if (_player != null)
        {
            _player.SetControlEnabled(true);
        }
    }

    // 저장된 슬롯 순서대로 기존 18개 UI를 갱신한다.
    private void Render()
    {
        List<InventoryItemSaveData> entries =
            InventoryService.ReadSlots(_saveManager.PlayerSaveData);

        // 먼저 모든 ID를 확인해서 잘못된 데이터로 일부만 갱신하지 않는다.
        for (int i = 0; i < _items.Length; i++)
        {
            _items[i] = entries[i].count > 0
                ? _database.GetItem(entries[i].itemId)
                : null;
        }

        for (int i = 0; i < _slots.Length; i++)
        {
            _slots[i].Bind(_items[i], entries[i].count);
        }
    }

    // 창이 열린 중에 저장 데이터가 바뀌면 현재 표시도 갱신한다.
    private void HandleSaved()
    {
        if (!_isOpen)
        {
            return;
        }

        // 데이터가 변경되면 이전 아이템을 끌던 드래그는 종료한다.
        _dragIndex = -1;
        _draggingSlot.gameObject.SetActive(false);
        _tooltip.Hide();

        Render();
        RefreshTooltip();
    }

    // 슬롯 진입을 기록하고 표시 가능한 경우 툴팁을 연다.
    public void Hover(int index)
    {
        if (!_isOpen)
        {
            return;
        }

        _hoverIndex = index;
        RefreshTooltip();
    }

    // 현재 마우스가 있던 슬롯을 벗어났을 때만 툴팁을 닫는다.
    public void Leave(int index)
    {
        if (_hoverIndex != index)
        {
            return;
        }

        _hoverIndex = -1;
        _tooltip.Hide();
    }

    // 빈칸이나 드래그 중에는 툴팁을 표시하지 않는다.
    private void RefreshTooltip()
    {
        if (!_isOpen || _dragIndex >= 0 ||
            _hoverIndex < 0 || _items[_hoverIndex] == null)
        {
            _tooltip.Hide();
            return;
        }

        _tooltip.Show(
            _items[_hoverIndex],
            _slots[_hoverIndex].Rect);
    }

    // 아이템이 있는 슬롯에서만 드래그를 시작한다.
    public void BeginDrag(int index, Vector2 screenPosition)
    {
        if (!_isOpen || _dragIndex >= 0 || _items[index] == null)
        {
            return;
        }

        _dragIndex = index;
        _tooltip.Hide();

        _dragIcon.sprite = _items[index].Icon;
        _draggingSlot.gameObject.SetActive(true);
        _draggingSlot.SetAsLastSibling();

        MoveDrag(screenPosition);
    }

    // Overlay Canvas의 좌표로 커서 위치를 변환한다.
    public void MoveDrag(Vector2 screenPosition)
    {
        if (!_isOpen || _dragIndex < 0)
        {
            return;
        }

        RectTransform parent = (RectTransform)_draggingSlot.parent;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent, screenPosition, null, out Vector2 localPoint))
        {
            _draggingSlot.localPosition =
                new Vector3(localPoint.x, localPoint.y, 0f);
        }
    }

    // 슬롯에 놓았을 때만 저장 데이터의 두 위치를 교환한다.
    public void Drop(int targetIndex)
    {
        if (!_isOpen || _dragIndex < 0 || _dragIndex == targetIndex)
        {
            return;
        }

        if (!InventoryService.TrySwap(
                _saveManager, _dragIndex, targetIndex, out string error))
        {
            Debug.LogWarning(error, this);
        }
    }

    // 여백에 놓은 경우에는 교환 없이 드래그 표시만 종료한다.
    public void EndDrag()
    {
        _dragIndex = -1;
        _draggingSlot.gameObject.SetActive(false);
        RefreshTooltip();
    }

    // 창 밖으로 포커스를 잃으면 드래그 표시가 남지 않게 한다.
    private void OnApplicationFocus(bool hasFocus)
    {
        if (_ready && !hasFocus)
        {
            EndDrag();
        }
    }

    // 관리자 비활성화나 씬 종료 시 이벤트와 조작 잠금을 정리한다.
    private void OnDisable()
    {
        if (!_ready)
        {
            return;
        }

        _bagButton.onClick.RemoveListener(Toggle);
        _closeButton.onClick.RemoveListener(Close);

        Close();
    }

    // Inspector의 컴포넌트 메뉴에서 지정한 아이템을 지급한다.
    [ContextMenu("인벤토리 테스트/지정 아이템 추가")]
    private void AddTestItem()
    {
        // 편집 상태에서는 실제 저장 데이터를 변경하지 않는다.
        if (!Application.isPlaying)
        {
            Debug.LogWarning("플레이 모드에서 실행해주세요.", this);
            return;
        }

        // 인벤토리 창을 열기 전에도 사용할 수 있도록 직접 조회한다.
        SaveManager saveManager = SaveManager.Instance;

        if (saveManager == null || !saveManager.IsLoggedIn)
        {
            Debug.LogWarning("로그인한 뒤 실행해주세요.", this);
            return;
        }

        if (_database == null ||
            _testItem == null ||
            string.IsNullOrWhiteSpace(_testItem.Id) ||
            _testItemAmount <= 0)
        {
            Debug.LogWarning(
                "Database, Test Item, 아이템 ID, 지급 수량을 확인해주세요.",
                this);
            return;
        }

        // 지급 후 UI에서 찾을 수 있는 아이템인지 먼저 검사한다.
        Item registeredItem;

        try
        {
            registeredItem = _database.GetItem(_testItem.Id);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            return;
        }

        // 다른 프리팹이 같은 ID를 사용한 경우 잘못된 지급을 막는다.
        if (registeredItem != _testItem)
        {
            Debug.LogWarning(
                "Test Item에는 ItemDatabase에 등록한 프리팹을 연결해주세요.",
                this);
            return;
        }

        // 실제 획득과 동일한 경로로 공간 검사, 수량 증가, 저장을 수행한다.
        if (!saveManager.TryAddItem(
                registeredItem, _testItemAmount, out string error))
        {
            Debug.LogWarning(error, this);
            return;
        }

        // 최종 보유 수량을 출력해 테스트 결과를 확인한다.
        int totalCount = saveManager.PlayerSaveData.GetItemCount(
            registeredItem.Id);

        Debug.Log(
            $"{registeredItem.DisplayName} {_testItemAmount}개 추가 완료. " +
            $"현재 보유 수량: {totalCount}개",
            this);
    }
}
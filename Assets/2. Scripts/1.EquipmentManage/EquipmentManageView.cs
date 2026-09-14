using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Linq;
using UnityEngine.U2D.Animation;

/// <summary>
/// EquipmentManage 화면의 UI 표시만 담당한다.
/// 데이터 판단이나 구매 및 장착 규칙은 처리하지 않는다.
/// </summary>
public class EquipmentManageView : MonoBehaviour
{
    [Header("캐릭터 정보")]

    [SerializeField] Image _characterIcon; // 왼쪽에 표시할 현재 캐릭터 이미지

    [SerializeField] TMP_Text _characterNameText; // 현재 캐릭터 이름 텍스트

    [SerializeField] TMP_Text _characterLevelText; // 현재 캐릭터 레벨 텍스트

    [Header("플레이어 재화")]

    [SerializeField] TMP_Text _goldText; // 화면 중앙 상단에 표시할 현재 골드

    [Header("현재 장착 무기")]

    [SerializeField]
    Image[] _equippedWeaponIcons =
        new Image[3]; // 장착 슬롯 1~3의 무기 아이콘

    [SerializeField]
    TMP_Text[] _equippedWeaponNameTexts =
        new TMP_Text[3]; // 장착 슬롯 1~3의 무기 이름

    [SerializeField]
    EquippedWeaponSlotView[] _equippedWeaponSlots =
    new EquippedWeaponSlotView[3]; // 무기를 드롭할 수 있는 왼쪽 장착 슬롯 1~3

    [Header("중앙 무기 슬롯")]

    [SerializeField]
    EquipmentSlotView _weaponSlotPrefab; // 캐릭터의 무기마다 생성할 슬롯 프리팹

    [SerializeField]
    Transform _weaponSlotContainer; // 생성된 무기 슬롯이 배치될 부모 Transform

    // 런타임에 생성한 무기 슬롯을 순서대로 보관한다.
    readonly List<EquipmentSlotView> _weaponSlots =
        new List<EquipmentSlotView>();

    [Header("무기 툴팁")]

    [SerializeField]
    GameObject _tooltipObject; // 마우스를 무기 슬롯에 올렸을 때 활성화할 툴팁 전체 오브젝트

    [SerializeField]
    RectTransform _tooltipRectTransform; // 툴팁을 마우스 위치로 이동시키기 위한 RectTransform

    [SerializeField]
    Image _tooltipWeaponIcon; // 툴팁 상단에 표시할 무기 아이콘

    [SerializeField]
    TMP_Text _tooltipWeaponNameText; // 툴팁 상단에 표시할 무기 이름

    [SerializeField]
    TMP_Text _tooltipInfoText; // 툴팁 중앙에 표시할 무기 설명

    [SerializeField]
    GameObject _tooltipPriceFrame; // 무기를 보유하지 않았을 때 표시할 가격 영역

    [SerializeField]
    TMP_Text _tooltipPriceText; // 툴팁에 표시할 무기 가격

    [SerializeField]
    GameObject _tooltipOwnedObject; // 이미 보유한 무기임을 표시하는 오브젝트

    [SerializeField]
    RectTransform _canvasRectTransform; // 화면 좌표를 Canvas 내부 좌표로 변환할 때 사용하는 Canvas RectTransform

    [SerializeField]
    Canvas _rootCanvas; // 툴팁 위치 계산에 사용할 EquipmentManage의 최상위 Canvas

    [SerializeField]
    Vector2 _tooltipOffset = Vector2.zero; // 슬롯 중앙을 기준으로 툴팁 위치를 추가 조정할 때 사용하는 보정값

    [Header("무기 드래그")]

    [SerializeField]
    RectTransform _dragWeaponIconRectTransform; // 드래그 중 무기 아이콘을 커서 위치로 이동시킬 RectTransform

    [SerializeField]
    Image _dragWeaponIcon; // 드래그 중 커서 위에 표시할 무기 아이콘

    [Header("오른쪽 무기 상세 정보")]

    [SerializeField]
    GameObject _rightBackground; // 무기가 선택됐을 때 활성화할 오른쪽 상세 패널

    [SerializeField]
    Image _detailWeaponIcon; // 선택한 무기의 상세 아이콘

    [SerializeField]
    TMP_Text _detailWeaponNameText; // 선택한 무기의 이름

    [SerializeField]
    TMP_Text _detailInfoText; // 선택한 무기의 설명

    [SerializeField]
    GameObject _detailPriceFrame; // 구매 가능한 무기의 가격 표시 영역

    [SerializeField]
    Button _purchaseButton; // 선택한 무기의 구매 요청을 전달할 버튼

    [SerializeField]
    TMP_Text _detailPriceText; // 선택한 무기의 가격

    [SerializeField]
    TMP_Text _detailQualificationText; // 요구 레벨 또는 구매 불가 이유

    [SerializeField]
    GameObject _detailOwnedObject; // 이미 보유 중인 무기임을 표시하는 영역

    // 상점 씬에서 배치한 이미지의 원래 크기와 위치.
    private Vector3 _previewBaseScale;
    private Vector2 _previewBasePosition;

    // 무기를 바꿀 때 보정값이 누적되지 않도록 기준을 한 번만 저장한다.
    private bool _previewLayoutCaptured;

    // 현재 중앙 무기 슬롯에서 무기를 드래그하고 있는지 기록한다.
    bool _isWeaponDragging;

    // 중앙 슬롯에서 무기가 선택됐을 때 Presenter에 전달하는 이벤트
    public event Action<WeaponData> WeaponSelected;

    // 무기를 왼쪽 장착 슬롯에 드롭했을 때
    // 드롭한 무기와 대상 슬롯 번호를 Presenter에 전달하는 이벤트
    public event Action<WeaponData, int> WeaponEquipRequested;

    // 장착 슬롯끼리 무기를 교환할 때
    // 출발 슬롯 번호와 도착 슬롯 번호를 Presenter에 전달하는 이벤트
    public event Action<int, int> EquippedWeaponSwapRequested;

    // 오른쪽 가격 버튼이 눌렸을 때 Presenter에 전달하는 이벤트
    public event Action PurchaseRequested;

    // 상점 캐릭터 미리보기의 초당 Idle 재생 프레임 수.
    [SerializeField, Min(0.1f)] private float _previewIdleFps = 10f;

    // 현재 첫 번째 장착 무기에 해당하는 캐릭터 애니메이션.
    private Sprite[] _previewFrames = new Sprite[0];

    // 현재 재생 프레임과 다음 프레임까지 누적된 시간.
    private int _previewFrameIndex;
    private float _previewFrameTime;

    // 첫 번째 장착 무기에 맞춰 캐릭터 미리보기를 갱신한다.
    public void ShowCharacterPreview(
        CharacterData character,
        WeaponData firstWeapon)
    {
        // 첫 표시 때 Inspector에서 배치한 상태를 기준으로 저장한다.
        RectTransform previewRect = _characterIcon.rectTransform;

        if (!_previewLayoutCaptured)
        {
            _previewBaseScale = previewRect.localScale;
            _previewBasePosition = previewRect.anchoredPosition;
            _previewLayoutCaptured = true;
        }

        // 무기가 없으면 기본 배치를 사용한다.
        float scale = 1f;
        Vector2 offset = Vector2.zero;

        if (firstWeapon != null)
        {
            scale = firstWeapon.EquipmentPreviewScale;
            offset = firstWeapon.EquipmentPreviewOffset;
        }

        // 무기 교체마다 원래 배치에서 다시 계산한다.
        previewRect.localScale = _previewBaseScale * scale;
        previewRect.anchoredPosition = _previewBasePosition + offset;

        _previewFrames = new Sprite[0];
        _previewFrameIndex = 0;
        _previewFrameTime = 0f;

        // 첫 슬롯이 비어 있으면 캐릭터의 기본 이미지를 표시한다.
        _characterIcon.sprite = character.Icon;
        _characterIcon.preserveAspect = true;
        _characterIcon.enabled = character.Icon != null;

        if (firstWeapon == null)
        {
            return;
        }

        SpriteLibraryAsset library = firstWeapon.PlayerSpriteLibrary;
        string category = character.PreviewIdleCategory;

        if (library == null || string.IsNullOrWhiteSpace(category))
        {
            throw new InvalidOperationException(
                $"{character.CharacterName}의 미리보기 Library와 Idle 카테고리를 확인해주세요.");
        }

        // 캐릭터 선택 화면과 같은 순서로 Idle 프레임을 가져온다.
        string[] labels = library.GetCategoryLabelNames(category)
            .OrderBy(label => label, StringComparer.Ordinal)
            .ToArray();

        if (labels.Length == 0)
        {
            throw new InvalidOperationException(
                $"{library.name}에 {category} 프레임이 없습니다.");
        }

        Sprite[] frames = new Sprite[labels.Length];

        for (int i = 0; i < labels.Length; i++)
        {
            frames[i] = library.GetSprite(category, labels[i]);

            if (frames[i] == null)
            {
                throw new InvalidOperationException(
                    $"{library.name}의 {category}/{labels[i]} 이미지가 없습니다.");
            }
        }

        _previewFrames = frames;
        _characterIcon.sprite = frames[0];
        _characterIcon.enabled = true;
    }

    // 상점 창에 표시된 캐릭터의 Idle 애니메이션을 재생한다.
    private void Update()
    {
        if (_previewFrames.Length == 0 ||
            !_characterIcon.isActiveAndEnabled)
        {
            return;
        }

        float interval = 1f / Mathf.Max(0.1f, _previewIdleFps);
        _previewFrameTime += Time.unscaledDeltaTime;

        if (_previewFrameTime < interval)
        {
            return;
        }

        // 프레임이 지연되어도 재생 속도가 느려지지 않도록 처리한다.
        int steps = Mathf.FloorToInt(_previewFrameTime / interval);
        _previewFrameTime %= interval;

        _previewFrameIndex =
            (_previewFrameIndex + steps) % _previewFrames.Length;

        _characterIcon.sprite = _previewFrames[_previewFrameIndex];
    }

    /// <summary>
    /// 왼쪽 캐릭터 이미지, 이름, 레벨을 표시한다.
    /// </summary>
    public void ShowCharacter(
        CharacterData characterData,
        int characterLevel)
    {
        // 표시할 캐릭터 데이터가 없다면 잘못된 호출이다.
        if (characterData == null)
        {
            throw new ArgumentNullException(nameof(characterData));
        }

        // 캐릭터 이미지를 표시한다.
        _characterIcon.sprite = characterData.Icon;
        _characterIcon.preserveAspect = true;
        _characterIcon.enabled = characterData.Icon != null;

        // 캐릭터 이름을 표시한다.
        _characterNameText.text =
            characterData.CharacterName;

        // 캐릭터의 현재 레벨을 표시한다.
        _characterLevelText.text =
            characterLevel.ToString();
    }

    /// <summary>
    /// 현재 플레이어가 보유한 골드를 표시한다.
    /// </summary>
    public void ShowGold(int gold)
    {
        // 골드를 천 단위 쉼표가 포함된 문자열로 표시한다.
        _goldText.text = gold.ToString("N0");
    }

    /// <summary>
    /// 지정한 장착 슬롯에 무기 아이콘과 이름을 표시한다.
    /// </summary>
    public void ShowEquippedWeapon(
        int slotIndex,
        WeaponData weaponData)
    {
        // 아이콘 배열과 이름 배열에서 모두 사용할 수 있는지 확인한다.
        if (slotIndex < 0 ||
            slotIndex >= _equippedWeaponIcons.Length ||
            slotIndex >= _equippedWeaponNameTexts.Length ||
            slotIndex >= _equippedWeaponSlots.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slotIndex),
                slotIndex,
                "장착 무기 UI 슬롯 번호가 범위를 벗어났습니다.");
        }

        // 지정한 슬롯의 아이콘과 이름 텍스트를 가져온다.
        Image weaponIcon =
            _equippedWeaponIcons[slotIndex];

        TMP_Text weaponNameText =
            _equippedWeaponNameTexts[slotIndex];

        // 현재 순서의 드래그 가능한 장착 슬롯 View를 가져온다.
        EquippedWeaponSlotView equippedWeaponSlot =
            _equippedWeaponSlots[slotIndex];

        // Inspector에서 장착 슬롯을 연결하지 않았다면 설정 오류를 알린다.
        if (equippedWeaponSlot == null)
        {
            throw new InvalidOperationException(
                $"장착 무기 슬롯 {slotIndex}번이 연결되지 않았습니다.");
        }

        // 화면에 표시하는 무기와 드래그에 사용할 무기 데이터를 동일하게 저장한다.
        // weaponData가 null이면 빈 장착 슬롯로 설정된다.
        equippedWeaponSlot.SetWeapon(weaponData);

        // 빈 장착 슬롯이면 아이콘과 이름을 숨긴다.
        if (weaponData == null)
        {
            weaponIcon.sprite = null;
            weaponIcon.enabled = false;
            weaponNameText.text = string.Empty;
            return;
        }

        // 장착된 무기의 아이콘을 표시한다.
        weaponIcon.sprite = weaponData.Icon;
        weaponIcon.preserveAspect = true;
        weaponIcon.enabled = weaponData.Icon != null;

        // 장착된 무기의 이름을 표시한다.
        weaponNameText.text =
            weaponData.WeaponName;
    }

    /// <summary>
    /// 중앙 무기 슬롯 하나에 무기 데이터와 현재 상태를 표시한다.
    /// </summary>
    /// <param name="slotIndex">중앙 무기 슬롯 번호</param>
    /// <param name="weaponData">표시할 무기 데이터</param>
    /// <param name="itemState">잠금, 보유, 구매 가능 등의 현재 상태</param>
    public void ShowWeaponSlot(
        int slotIndex,
        WeaponData weaponData,
        EquipmentItemState itemState)
    {
        // 중앙 무기 슬롯 배열의 범위를 벗어난 번호는 사용할 수 없다.
        if (slotIndex < 0 ||
            slotIndex >= _weaponSlots.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slotIndex),
                slotIndex,
                "중앙 무기 슬롯 번호가 범위를 벗어났습니다.");
        }

        // 해당 순서의 중앙 무기 슬롯 View를 가져온다.
        EquipmentSlotView weaponSlot =
            _weaponSlots[slotIndex];

        // Inspector에서 슬롯 연결이 빠졌다면 설정 오류를 알린다.
        if (weaponSlot == null)
        {
            throw new InvalidOperationException(
                $"중앙 무기 슬롯 {slotIndex}번이 연결되지 않았습니다.");
        }

        // 중앙 슬롯에 무기 데이터와 현재 상태를 전달한다.
        // Bind 과정에서 아이콘, 잠금 표시, 초기 선택 해제가 적용된다.
        weaponSlot.Bind(
            slotIndex,
            weaponData,
            itemState);

        // 구매 후 슬롯을 다시 표시하는 경우에도
        // EventSystem에서 선택 중인 슬롯의 선택 이미지는 유지한다.
        bool isCurrentlySelected =
            EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject ==
            weaponSlot.gameObject;

        weaponSlot.SetSelected(isCurrentlySelected);
    }

    /// <summary>
    /// 중앙 무기 슬롯 5개의 선택 이벤트를 View와 연결한다.
    /// Presenter 초기화 과정에서 한 번 호출한다.
    /// </summary>
    public void InitializeWeaponSlotEvents()
    {
        // 중앙 무기 슬롯을 순서대로 확인한다.
        for (int i = 0; i < _weaponSlots.Count; i++)
        {
            // 현재 순서의 중앙 무기 슬롯을 가져온다.
            EquipmentSlotView weaponSlot =
                _weaponSlots[i];

            // Inspector에서 슬롯 연결이 빠졌다면 설정 오류를 알린다.
            if (weaponSlot == null)
            {
                throw new InvalidOperationException(
                    $"중앙 무기 슬롯 {i}번이 연결되지 않았습니다.");
            }

            // 중복 구독을 방지하기 위해 기존 연결을 먼저 제거한다.
            weaponSlot.Selected -=
                HandleWeaponSlotSelected;

            // 슬롯 선택 이벤트를 View의 처리 함수와 연결한다.
            weaponSlot.Selected +=
                HandleWeaponSlotSelected;

            // 중복 연결을 방지하기 위해 기존 마우스 진입 이벤트를 제거한다.
            weaponSlot.PointerEntered -=
                HandleWeaponSlotPointerEntered;

            // 슬롯에 마우스가 들어오면 해당 무기의 툴팁을 표시한다.
            weaponSlot.PointerEntered +=
                HandleWeaponSlotPointerEntered;

            // 중복 연결을 방지하기 위해 기존 마우스 이탈 이벤트를 제거한다.
            weaponSlot.PointerExited -=
                HandleWeaponSlotPointerExited;

            // 슬롯에서 마우스가 나가면 툴팁을 숨긴다.
            weaponSlot.PointerExited +=
                HandleWeaponSlotPointerExited;

            // 중복 연결을 방지하기 위해 기존 드래그 시작 이벤트를 제거한다.
            weaponSlot.DragStarted -=
                HandleWeaponDragStarted;

            // 무기 드래그가 시작되면 드래그 UI를 표시한다.
            weaponSlot.DragStarted +=
                HandleWeaponDragStarted;

            // 중복 연결을 방지하기 위해 기존 드래그 이동 이벤트를 제거한다.
            weaponSlot.DragMoved -=
                HandleWeaponDragMoved;

            // 드래그 중 아이콘을 커서 위치로 이동시킨다.
            weaponSlot.DragMoved +=
                HandleWeaponDragMoved;

            // 중복 연결을 방지하기 위해 기존 드래그 종료 이벤트를 제거한다.
            weaponSlot.DragEnded -=
                HandleWeaponDragEnded;

            // 드래그가 끝나면 드래그 UI와 가이드를 숨긴다.
            weaponSlot.DragEnded +=
                HandleWeaponDragEnded;
        }
        // 화면 초기화 시에는 드래그 상태와 드래그 아이콘을 숨긴 상태로 시작한다.
        _isWeaponDragging = false;
        _dragWeaponIcon.gameObject.SetActive(false);
    }

    /// <summary>
    /// 왼쪽 장착 슬롯 3개에 슬롯 번호를 부여하고
    /// 각 슬롯의 드롭 이벤트를 View의 처리 함수와 연결한다.
    /// </summary>
    public void InitializeEquippedWeaponSlotEvents()
    {
        // 장착 슬롯은 저장 데이터 구조와 동일하게 정확히 3개여야 한다.
        if (_equippedWeaponSlots.Length != 3)
        {
            throw new InvalidOperationException(
                "장착 무기 슬롯은 정확히 3개여야 합니다.");
        }

        // 장착 슬롯을 순서대로 초기화한다.
        for (int slotIndex = 0;
             slotIndex < _equippedWeaponSlots.Length;
             slotIndex++)
        {
            // 현재 순서의 장착 슬롯 View를 가져온다.
            EquippedWeaponSlotView equippedSlot =
                _equippedWeaponSlots[slotIndex];

            // Inspector에서 슬롯을 연결하지 않았다면 설정 오류를 알린다.
            if (equippedSlot == null)
            {
                throw new InvalidOperationException(
                    $"장착 무기 슬롯 {slotIndex}번이 연결되지 않았습니다.");
            }

            // 배열 순서에 맞춰 장착 슬롯 번호 0, 1, 2를 부여한다.
            // Bind 안에서 드롭 가이드도 비활성화된다.
            equippedSlot.Bind(slotIndex);

            // 중복 연결을 방지하기 위해 기존 드롭 이벤트를 제거한다.
            equippedSlot.DropReceived -=
                HandleEquippedWeaponDropReceived;

            // 장착 슬롯에서 발생한 드롭을 View가 전달받도록 연결한다.
            equippedSlot.DropReceived +=
                HandleEquippedWeaponDropReceived;

            // 중복 연결을 방지하기 위해 장착 슬롯의 드래그 시작 이벤트를 제거한다.
            equippedSlot.DragStarted -=
                HandleEquippedWeaponDragStarted;

            // 장착 중인 무기를 잡으면 공통 드래그 UI를 표시한다.
            equippedSlot.DragStarted +=
                HandleEquippedWeaponDragStarted;

            // 중앙 슬롯과 동일한 드래그 아이콘 이동 함수를 사용한다.
            equippedSlot.DragMoved -=
                HandleWeaponDragMoved;

            equippedSlot.DragMoved +=
                HandleWeaponDragMoved;

            // 중복 연결을 방지하기 위해 장착 슬롯의 드래그 종료 이벤트를 제거한다.
            equippedSlot.DragEnded -=
                HandleEquippedWeaponDragEnded;

            // 장착 슬롯 드래그가 끝나면 공통 드래그 UI를 숨긴다.
            equippedSlot.DragEnded +=
                HandleEquippedWeaponDragEnded;
        }
    }

    /// <summary>
    /// 장착 슬롯에 무기가 드롭됐을 때 출발 위치를 확인한다.
    /// 중앙 무기 슬롯에서 왔다면 장착 요청을 전달하고,
    /// 다른 장착 슬롯에서 왔다면 슬롯 교환 요청을 전달한다.
    /// </summary>
    private void HandleEquippedWeaponDropReceived(
        int targetSlotIndex,
        PointerEventData eventData)
    {
        // 드래그를 시작한 UI 오브젝트가 없다면 처리하지 않는다.
        if (eventData == null ||
            eventData.pointerDrag == null)
        {
            return;
        }

        // 먼저 중앙 무기 목록에서 시작된 드래그인지 확인한다.
        EquipmentSlotView draggedWeaponSlot =
            eventData.pointerDrag.GetComponentInParent<EquipmentSlotView>();

        // 중앙 무기 슬롯에서 시작된 정상적인 드래그라면
        // 기존 무기 장착 요청을 Presenter에 전달한다.
        if (draggedWeaponSlot != null &&
            draggedWeaponSlot.IsDragging &&
            draggedWeaponSlot.WeaponData != null)
        {
            WeaponEquipRequested?.Invoke(
                draggedWeaponSlot.WeaponData,
                targetSlotIndex);

            // 중앙 슬롯 드래그 처리가 끝났으므로
            // 장착 슬롯 교환 검사는 진행하지 않는다.
            return;
        }

        // 중앙 슬롯이 아니라면 장착 슬롯에서 시작된 드래그인지 확인한다.
        EquippedWeaponSlotView draggedEquippedSlot =
            eventData.pointerDrag
                .GetComponentInParent<EquippedWeaponSlotView>();

        // 장착 슬롯에서 시작된 정상적인 드래그가 아니라면 처리하지 않는다.
        if (draggedEquippedSlot == null ||
            !draggedEquippedSlot.IsDragging ||
            draggedEquippedSlot.WeaponData == null)
        {
            return;
        }

        // 출발 슬롯과 도착 슬롯이 같으면 교환할 필요가 없다.
        if (draggedEquippedSlot.SlotIndex ==
            targetSlotIndex)
        {
            return;
        }

        // Presenter에 출발 슬롯 번호와 도착 슬롯 번호를 전달한다.
        EquippedWeaponSwapRequested?.Invoke(
            draggedEquippedSlot.SlotIndex,
            targetSlotIndex);
    }

    /// <summary>
    /// 중앙 무기 슬롯의 드래그가 시작됐을 때
    /// 해당 무기를 공통 드래그 UI에 전달한다.
    /// </summary>
    private void HandleWeaponDragStarted(
        EquipmentSlotView weaponSlot,
        PointerEventData eventData)
    {
        // 드래그할 무기 데이터가 없다면 처리하지 않는다.
        if (weaponSlot == null ||
            weaponSlot.WeaponData == null)
        {
            return;
        }

        // 중앙 슬롯과 장착 슬롯이 동일한 드래그 UI 로직을 사용한다.
        BeginWeaponDrag(
            weaponSlot.WeaponData,
            eventData);
    }

    /// <summary>
    /// 무기를 드래그하는 동안 드래그 아이콘을 현재 커서 위치로 이동시킨다.
    /// </summary>
    private void HandleWeaponDragMoved(
        PointerEventData eventData)
    {
        // 실제 드래그 중이 아니라면 아이콘을 이동시키지 않는다.
        if (!_isWeaponDragging)
        {
            return;
        }

        MoveDragWeaponIcon(eventData);
    }

    /// <summary>
    /// 중앙 무기 슬롯의 드래그가 끝났을 때 공통 드래그 UI를 종료한다.
    /// </summary>
    private void HandleWeaponDragEnded(
        EquipmentSlotView weaponSlot,
        PointerEventData eventData)
    {
        EndWeaponDrag();
    }

    /// <summary>
    /// 현재 장착 중인 무기의 드래그가 시작됐을 때
    /// 해당 무기를 공통 드래그 UI에 전달한다.
    /// </summary>
    private void HandleEquippedWeaponDragStarted(
        EquippedWeaponSlotView equippedSlot,
        PointerEventData eventData)
    {
        // 빈 슬롯이거나 무기 데이터가 없다면 처리하지 않는다.
        if (equippedSlot == null ||
            equippedSlot.WeaponData == null)
        {
            return;
        }

        // 중앙 슬롯과 동일한 드래그 아이콘 및 가이드 표시를 사용한다.
        BeginWeaponDrag(
            equippedSlot.WeaponData,
            eventData);
    }

    /// <summary>
    /// 장착 슬롯에서 시작한 드래그가 끝났을 때 공통 드래그 UI를 종료한다.
    /// </summary>
    private void HandleEquippedWeaponDragEnded(
        EquippedWeaponSlotView equippedSlot,
        PointerEventData eventData)
    {
        EndWeaponDrag();
    }

    /// <summary>
    /// 드래그 출발 위치와 관계없이 공통으로 사용하는
    /// 무기 드래그 아이콘과 장착 위치 가이드를 표시한다.
    /// </summary>
    private void BeginWeaponDrag(
        WeaponData weaponData,
        PointerEventData eventData)
    {
        // 표시할 무기 데이터가 없다면 잘못된 호출이다.
        if (weaponData == null)
        {
            throw new ArgumentNullException(nameof(weaponData));
        }

        // 드래그 중에는 다른 슬롯의 툴팁이 나타나지 않도록 기록한다.
        _isWeaponDragging = true;

        // 무기를 들고 있는 동안 기존 툴팁을 숨긴다.
        HideWeaponTooltip();

        // 커서 위에 드래그 중인 무기 아이콘을 표시한다.
        _dragWeaponIcon.sprite =
            weaponData.Icon;

        _dragWeaponIcon.preserveAspect = true;
        _dragWeaponIcon.enabled =
            weaponData.Icon != null;

        _dragWeaponIcon.gameObject.SetActive(true);

        // 드래그가 시작된 현재 커서 위치로 아이콘을 이동시킨다.
        MoveDragWeaponIcon(eventData);

        // 무기를 드롭할 수 있는 장착 슬롯 3개를 모두 강조한다.
        SetDropGuideEffectsVisible(true);
    }

    /// <summary>
    /// 드래그 성공 여부와 관계없이 드래그 UI와 가이드를 정리한다.
    /// </summary>
    private void EndWeaponDrag()
    {
        // 공통 드래그 상태를 종료한다.
        _isWeaponDragging = false;

        // 커서를 따라다니던 무기 아이콘을 숨기고 데이터를 비운다.
        _dragWeaponIcon.gameObject.SetActive(false);
        _dragWeaponIcon.sprite = null;

        // 모든 장착 슬롯의 드롭 가이드를 숨긴다.
        SetDropGuideEffectsVisible(false);
    }

    /// <summary>
    /// 드래그 아이콘을 PointerEventData가 가진 현재 화면 좌표로 이동시킨다.
    /// </summary>
    private void MoveDragWeaponIcon(
        PointerEventData eventData)
    {
        // 유효한 포인터 데이터가 없다면 이동할 수 없다.
        if (eventData == null)
        {
            return;
        }

        // Screen Space Overlay에서는 카메라가 필요하지 않고,
        // 그 외 Canvas 모드에서는 Canvas에 설정된 카메라를 사용한다.
        Camera uiCamera =
            _rootCanvas.renderMode ==
            RenderMode.ScreenSpaceOverlay
                ? null
                : _rootCanvas.worldCamera;

        // 현재 커서의 화면 좌표를 최상위 Canvas 내부 좌표로 변환한다.
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRectTransform,
            eventData.position,
            uiCamera,
            out Vector2 canvasLocalPosition))
        {
            // DragWeaponIcon의 Anchor가 Canvas 중앙이므로
            // 변환된 Canvas 좌표를 anchoredPosition에 적용한다.
            _dragWeaponIconRectTransform.anchoredPosition =
                canvasLocalPosition;
        }
    }

    /// <summary>
    /// 왼쪽 장착 슬롯 3개의 드롭 가이드 표시 상태를 한 번에 변경한다.
    /// </summary>
    private void SetDropGuideEffectsVisible(
        bool isVisible)
    {
        // 모든 장착 슬롯의 드롭 가이드를 동일한 상태로 변경한다.
        for (int i = 0;
             i < _equippedWeaponSlots.Length;
             i++)
        {
            EquippedWeaponSlotView equippedSlot =
                _equippedWeaponSlots[i];

            // Inspector 연결 누락은 설정 오류이므로 즉시 알린다.
            if (equippedSlot == null)
            {
                throw new InvalidOperationException(
                    $"장착 무기 슬롯 {i}번이 연결되지 않았습니다.");
            }

            equippedSlot.SetDropGuideVisible(isVisible);
        }
    }

    /// <summary>
    /// 마우스가 중앙 무기 슬롯에 들어왔을 때
    /// 해당 슬롯의 무기 정보를 툴팁으로 표시한다.
    /// </summary>
    private void HandleWeaponSlotPointerEntered(
        EquipmentSlotView weaponSlot,
        PointerEventData eventData)
    {
        // 드래그 중이거나 유효한 무기 데이터가 없다면 툴팁을 표시하지 않는다.
        if (_isWeaponDragging ||
            weaponSlot == null ||
            weaponSlot.WeaponData == null)
        {
            return;
        }

        // 마우스가 올라간 슬롯을 EventSystem의 현재 선택 대상으로 만든다.
        // OnSelect가 호출되면서 일반 슬롯과 잠금 슬롯 모두
        // 자신의 선택 상태에 맞는 이미지를 표시한다.
        if (EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject !=
            weaponSlot.gameObject)
        {
            EventSystem.current.SetSelectedGameObject(
                weaponSlot.gameObject);
        }

        // 슬롯이 가진 무기 정보와 현재 상태를 툴팁에 표시한다.
        ShowWeaponTooltip(
            weaponSlot.WeaponData,
            weaponSlot.ItemState);

        // 툴팁의 왼쪽 위 모서리를 슬롯의 정중앙에 배치한다.
        MoveWeaponTooltipToSlotCenter(weaponSlot);
    }

    /// <summary>
    /// 마우스가 중앙 무기 슬롯에서 나갔을 때 툴팁을 숨긴다.
    /// </summary>
    private void HandleWeaponSlotPointerExited(
        EquipmentSlotView weaponSlot)
    {
        HideWeaponTooltip();
    }

    /// <summary>
    /// 전달받은 무기 데이터를 툴팁 UI에 표시한다.
    /// </summary>
    private void ShowWeaponTooltip(
        WeaponData weaponData,
        EquipmentItemState itemState)
    {
        // 표시할 무기 데이터가 없다면 잘못된 호출이다.
        if (weaponData == null)
        {
            throw new ArgumentNullException(nameof(weaponData));
        }

        // 툴팁에 무기 아이콘을 표시한다.
        _tooltipWeaponIcon.sprite =
            weaponData.Icon;

        _tooltipWeaponIcon.preserveAspect = true;
        _tooltipWeaponIcon.enabled =
            weaponData.Icon != null;

        // 툴팁에 무기 이름과 설명을 표시한다.
        _tooltipWeaponNameText.text =
            weaponData.WeaponName;

        _tooltipInfoText.text =
            weaponData.Info;

        // 가격은 천 단위 쉼표가 포함된 문자열로 표시한다.
        _tooltipPriceText.text =
            weaponData.Price.ToString("N0");

        // 보유 중인 무기는 가격 대신 보유 중 표시를 보여준다.
        bool isOwned =
            itemState == EquipmentItemState.Owned;

        _tooltipPriceFrame.SetActive(!isOwned);
        _tooltipOwnedObject.SetActive(isOwned);

        // 모든 정보 설정이 끝난 뒤 툴팁 전체를 활성화한다.
        _tooltipObject.SetActive(true);
    }

    /// <summary>
    /// 툴팁의 왼쪽 위 모서리를 전달받은 슬롯의 정중앙에 배치한다.
    /// </summary>
    private void MoveWeaponTooltipToSlotCenter(
        EquipmentSlotView weaponSlot)
    {
        // 슬롯 오브젝트의 RectTransform을 가져온다.
        RectTransform slotRectTransform =
            weaponSlot.transform as RectTransform;

        if (slotRectTransform == null)
        {
            throw new InvalidOperationException(
                "무기 슬롯에서 RectTransform을 찾을 수 없습니다.");
        }

        // Screen Space Overlay에서는 카메라가 필요하지 않다.
        // 그 외 Canvas 모드에서는 Canvas에 설정된 카메라를 사용한다.
        Camera uiCamera =
            _rootCanvas.renderMode ==
            RenderMode.ScreenSpaceOverlay
                ? null
                : _rootCanvas.worldCamera;

        // 슬롯 RectTransform의 실제 정중앙을 월드 좌표로 계산한다.
        Vector3 slotCenterWorldPosition =
            slotRectTransform.TransformPoint(
                slotRectTransform.rect.center);

        // 슬롯 중앙의 월드 좌표를 화면 좌표로 변환한다.
        Vector2 slotCenterScreenPosition =
            RectTransformUtility.WorldToScreenPoint(
                uiCamera,
                slotCenterWorldPosition);

        // 화면 좌표를 최상위 Canvas 내부 좌표로 변환한다.
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRectTransform,
            slotCenterScreenPosition,
            uiCamera,
            out Vector2 canvasLocalPosition))
        {
            // 툴팁 Pivot이 (0, 1)이므로 이 좌표가
            // 툴팁의 왼쪽 위 모서리 위치가 된다.
            _tooltipRectTransform.anchoredPosition =
                canvasLocalPosition + _tooltipOffset;
        }
    }

    /// <summary>
    /// 현재 표시 중인 무기 툴팁을 숨긴다.
    /// </summary>
    public void HideWeaponTooltip()
    {
        // 툴팁 전체 오브젝트를 비활성화한다.
        _tooltipObject.SetActive(false);
    }

    /// <summary>
    /// 오른쪽 가격 버튼의 클릭 이벤트를 View와 연결한다.
    /// Presenter 초기화 과정에서 한 번 호출한다.
    /// </summary>
    public void InitializePurchaseButtonEvent()
    {
        // Inspector에서 구매 버튼 연결이 빠졌다면 설정 오류를 알린다.
        if (_purchaseButton == null)
        {
            throw new InvalidOperationException(
                "오른쪽 상세 정보의 Purchase Button이 연결되지 않았습니다.");
        }

        // 중복 연결을 방지하기 위해 기존 클릭 이벤트를 제거한다.
        _purchaseButton.onClick.RemoveListener(
            HandlePurchaseButtonClicked);

        // 구매 버튼 클릭을 View의 처리 함수와 연결한다.
        _purchaseButton.onClick.AddListener(
            HandlePurchaseButtonClicked);
    }

    /// <summary>
    /// 오른쪽 가격 버튼이 눌렸을 때 Presenter에 구매를 요청한다.
    /// </summary>
    private void HandlePurchaseButtonClicked()
    {
        // Presenter가 현재 선택된 무기의 구매를 처리하도록 알린다.
        PurchaseRequested?.Invoke();
    }

    /// <summary>
    /// 현재 EventSystem에 선택된 UI가 없을 때만
    /// 첫 번째 중앙 무기 슬롯을 선택한다.
    /// </summary>
    /// <returns>첫 번째 슬롯을 새로 선택했다면 true</returns>
    public bool TrySelectFirstWeaponSlot()
    {
        // EventSystem이 없다면 키보드 UI 선택을 처리할 수 없다.
        if (EventSystem.current == null)
        {
            return false;
        }

        // 이미 다른 UI가 선택되어 있다면 현재 선택을 강제로 바꾸지 않는다.
        if (EventSystem.current.currentSelectedGameObject != null)
        {
            return false;
        }

        // 첫 번째 중앙 무기 슬롯이 연결되지 않았다면 설정 오류다.
        if (_weaponSlots.Count == 0 ||
            _weaponSlots[0] == null)
        {
            throw new InvalidOperationException(
                "첫 번째 중앙 무기 슬롯이 연결되지 않았습니다.");
        }

        // 첫 번째 슬롯을 EventSystem의 현재 선택 대상으로 설정한다.
        // 이 과정에서 EquipmentSlotView.OnSelect()가 호출된다.
        EventSystem.current.SetSelectedGameObject(
            _weaponSlots[0].gameObject);

        return true;
    }

    /// <summary>
    /// 중앙 무기 슬롯이 선택됐을 때 선택 이미지를 갱신하고
    /// 선택된 무기 데이터를 Presenter에 전달한다.
    /// </summary>
    private void HandleWeaponSlotSelected(
        EquipmentSlotView selectedSlot)
    {
        // 중앙 슬롯 전체의 선택 이미지를 갱신한다.
        for (int i = 0; i < _weaponSlots.Count; i++)
        {
            EquipmentSlotView weaponSlot =
                _weaponSlots[i];

            // 현재 선택된 슬롯만 SlotSelected를 활성화한다.
            weaponSlot.SetSelected(
                weaponSlot == selectedSlot);
        }

        // Presenter가 오른쪽 상세 정보를 표시하도록
        // 선택된 WeaponData를 전달한다.
        WeaponSelected?.Invoke(
            selectedSlot.WeaponData);
    }

    /// <summary>
    /// 선택한 무기의 아이콘, 이름, 설명, 가격과 현재 상태를
    /// 오른쪽 상세 패널에 표시한다.
    /// </summary>
    public void ShowWeaponDetail(
        WeaponData weaponData,
        EquipmentItemState itemState)
    {
        // 표시할 무기 데이터가 없다면 잘못된 호출이다.
        if (weaponData == null)
        {
            throw new ArgumentNullException(nameof(weaponData));
        }

        // 무기가 선택됐으므로 오른쪽 상세 패널을 활성화한다.
        _rightBackground.SetActive(true);

        // 선택한 무기의 아이콘을 표시한다.
        _detailWeaponIcon.sprite = weaponData.Icon;
        _detailWeaponIcon.preserveAspect = true;
        _detailWeaponIcon.enabled =
            weaponData.Icon != null;

        // 선택한 무기의 이름과 설명을 표시한다.
        _detailWeaponNameText.text =
            weaponData.WeaponName;

        _detailInfoText.text =
            weaponData.Info;

        // 선택한 무기의 가격을 천 단위 쉼표 형식으로 표시한다.
        _detailPriceText.text =
            weaponData.Price.ToString("N0");

        // 실제 구매 가능한 상태에서만 가격 버튼을 누를 수 있다.
        _purchaseButton.interactable =
            itemState == EquipmentItemState.Purchasable;

        // 무기의 현재 상태에 맞게 상태 UI를 표시한다.
        switch (itemState)
        {
            case EquipmentItemState.Locked:
                // 요구 레벨을 달성하지 못한 무기는
                // 가격과 필요한 캐릭터 레벨을 표시한다.
                _detailPriceFrame.SetActive(true);
                _detailOwnedObject.SetActive(false);
                _detailQualificationText.gameObject.SetActive(true);
                _detailQualificationText.text =
                    $"Qualification: Lv {weaponData.RequiredCharacterLevel}";
                break;

            case EquipmentItemState.Owned:
                // 이미 보유한 무기는 가격 대신 보유 중 표시를 보여준다.
                _detailPriceFrame.SetActive(false);
                _detailOwnedObject.SetActive(true);
                _detailQualificationText.gameObject.SetActive(false);
                break;

            case EquipmentItemState.Purchasable:
                // 레벨과 골드 조건을 만족하면 구매 가능 상태를 표시한다.
                _detailPriceFrame.SetActive(true);
                _detailOwnedObject.SetActive(false);
                _detailQualificationText.gameObject.SetActive(true);
                _detailQualificationText.text =
                    "Available For Purchase";
                break;

            case EquipmentItemState.NotEnoughGold:
                // 레벨 조건은 만족했지만 골드가 부족한 상태를 표시한다.
                _detailPriceFrame.SetActive(true);
                _detailOwnedObject.SetActive(false);
                _detailQualificationText.gameObject.SetActive(true);
                _detailQualificationText.text =
                    "Not Enough Gold";
                break;

            default:
                // 새로운 상태가 추가됐지만 View 처리가 없는 경우 오류를 알린다.
                throw new ArgumentOutOfRangeException(
                    nameof(itemState),
                    itemState,
                    "처리하지 않은 장비 아이템 상태입니다.");
        }
    }

    /// <summary>
    /// 선택된 무기가 없을 때 오른쪽 상세 패널을 숨긴다.
    /// </summary>
    public void HideWeaponDetail()
    {
        // 오른쪽 상세 패널 전체를 비활성화한다.
        _rightBackground.SetActive(false);
    }

    /// <summary>
    /// 현재 캐릭터가 사용할 수 있는 무기 개수만큼
    /// 중앙 무기 슬롯을 생성한다.
    /// 화면 초기화 과정에서 한 번만 호출한다.
    /// </summary>
    public void CreateWeaponSlots(int slotCount)
    {
        // 음수 개수의 슬롯은 생성할 수 없다.
        if (slotCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slotCount),
                slotCount,
                "생성할 무기 슬롯 개수는 0 이상이어야 합니다.");
        }

        // 프리팹이 연결되지 않으면 슬롯을 생성할 수 없다.
        if (_weaponSlotPrefab == null)
        {
            throw new InvalidOperationException(
                "EquipmentManageView에 WeaponSlot 프리팹이 연결되지 않았습니다.");
        }

        // 생성 위치가 연결되지 않으면 올바른 UI 계층에 배치할 수 없다.
        if (_weaponSlotContainer == null)
        {
            throw new InvalidOperationException(
                "EquipmentManageView에 무기 슬롯 컨테이너가 연결되지 않았습니다.");
        }

        // 초기화 메서드가 중복 호출되어 슬롯이 두 번 생성되는 것을 방지한다.
        if (_weaponSlots.Count > 0)
        {
            throw new InvalidOperationException(
                "무기 슬롯이 이미 생성되어 있습니다.");
        }

        for (int slotIndex = 0;
             slotIndex < slotCount;
             slotIndex++)
        {
            // 프리팹을 슬롯 컨테이너의 자식으로 생성한다.
            EquipmentSlotView weaponSlot =
                Instantiate(
                    _weaponSlotPrefab,
                    _weaponSlotContainer);

            // Hierarchy에서 슬롯의 순서를 확인하기 쉽도록 이름을 지정한다.
            weaponSlot.name =
                $"WeaponSlot_{slotIndex + 1}";

            // 이후 데이터 갱신과 이벤트 연결에 사용할 수 있도록 보관한다.
            _weaponSlots.Add(weaponSlot);
        }
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 현재 증강 View가 표시하고 있는 선택 단계다.
/// </summary>
public enum UpgradeSelectionStage
{
    None,
    Upgrade,
    Weapon
}

/// <summary>
/// 증강 선택 화면 전체를 담당하는 View다.
/// Presenter가 전달한 데이터에 따라 슬롯 프리팹을 생성하고,
/// 슬롯 클릭 결과를 다시 Presenter에 전달한다.
/// </summary>
public class UpgradeSelectionView : MonoBehaviour
{
    [Header("전체 UI")]

    // 증강 선택 UI 전체 오브젝트
    [SerializeField] private GameObject _upgradeUI;

    [Header("선택 단계")]

    // 1차 증강 선택 화면
    [SerializeField] private GameObject _upgradeSelectPanel;

    // 2차 무기 선택 화면
    [SerializeField] private GameObject _weaponSelectPanel;

    [Header("1차 증강 슬롯")]

    // 1차 증강 슬롯들이 생성될 부모
    [SerializeField] private Transform _upgradeSlotContainer;

    // 1차 증강 슬롯 원본 프리팹
    [SerializeField] private UpgradeChoiceSlotView _upgradeSlotPrefab;

    [Header("2차 무기 슬롯")]

    // 2차 무기 슬롯들이 생성될 부모
    [SerializeField] private Transform _weaponSlotContainer;

    // 2차 무기 슬롯 원본 프리팹
    [SerializeField] private WeaponChoiceSlotView _weaponSlotPrefab;

    [Header("슬롯 등장 연출")]

    // 카드가 최종 위치보다 위쪽에서 출발할 거리
    [SerializeField] private float _entranceVerticalOffset = 500f;

    // 카드 하나가 떨어져 정착하는 시간
    [SerializeField] private float _entranceDuration = 0.55f;

    // 앞 카드와 다음 카드가 떨어지는 시간 간격
    [SerializeField] private float _entranceInterval = 0.16f;

    [Header("슬롯 퇴장 연출")]

    // 선택 확정 후 카드들이 사라지는 전체 시간
    [SerializeField] private float _exitDuration = 0.5f;

    // 선택된 카드가 사라지면서 위로 이동할 거리
    [SerializeField] private float _selectedExitVerticalOffset = 100f;

    [Header("제목 프레임")]

    // 1차 증강 선택 제목 프레임과 텍스트
    [SerializeField] private CanvasGroup _upgradeHeaderCanvasGroup;

    // 2차 무기 선택 제목 프레임과 텍스트
    [SerializeField] private CanvasGroup _weaponHeaderCanvasGroup;

    [Header("제목 전환 연출")]

    // 제목 프레임이 위쪽에서 등장할 거리
    [SerializeField] private float _headerEntranceOffset = 60f;

    // 제목 프레임 등장 시간
    [SerializeField] private float _headerEntranceDuration = 0.35f;

    // 제목 프레임 퇴장 시간
    [SerializeField] private float _headerExitDuration = 0.22f;

    // 제목 등장 후 첫 번째 카드가 떨어지기까지의 대기시간
    [SerializeField] private float _slotEntranceStartDelay = 0.12f;

    // 현재 화면에 표시 중인 증강 선택 단계
    private UpgradeSelectionStage _currentStage =
        UpgradeSelectionStage.None;

    // 등장·퇴장 연출이 끝나 실제 입력을 받을 수 있는지 확인한다.
    private bool _isSelectionInputEnabled;

    // 현재 실행 중인 1차 제목 Tween
    private Tween _upgradeHeaderTween;

    // 현재 실행 중인 2차 제목 Tween
    private Tween _weaponHeaderTween;

    // 현재 생성되어 있는 1차 증강 슬롯 목록
    private readonly List<UpgradeChoiceSlotView> _upgradeSlots = new();

    // 현재 생성되어 있는 2차 무기 슬롯 목록
    private readonly List<WeaponChoiceSlotView> _weaponSlots = new();

    // 1차 증강 슬롯을 클릭했을 때 Presenter에 슬롯 번호를 전달한다.
    public event Action<int> UpgradeChoiceClicked;

    // 2차 무기 슬롯을 클릭했을 때 Presenter에 실제 무기 슬롯 번호를 전달한다.
    public event Action<int> WeaponChoiceClicked;

    private void Update()
    {
        // 카드 등장·퇴장 연출 중이거나 UI가 닫혀 있으면 입력하지 않는다.
        if (!_isSelectionInputEnabled)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SelectSlotByNumber(0);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SelectSlotByNumber(1);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            SelectSlotByNumber(2);
        }
    }

    /// <summary>
    /// 숫자키에 대응하는 실제 슬롯 번호를 선택한다.
    /// 1차와 2차 선택 단계에 맞는 이벤트만 발생시킨다.
    /// </summary>
    private void SelectSlotByNumber(int requestedSlotIndex)
    {
        if (!_isSelectionInputEnabled)
        {
            return;
        }

        if (_currentStage == UpgradeSelectionStage.Upgrade)
        {
            for (int index = 0;
                 index < _upgradeSlots.Count;
                 index++)
            {
                UpgradeChoiceSlotView slot =
                    _upgradeSlots[index];

                if (slot == null ||
                    slot.SlotIndex != requestedSlotIndex)
                {
                    continue;
                }

                // 같은 프레임에 다른 숫자키가 처리되지 않게 즉시 잠근다.
                _isSelectionInputEnabled = false;

                UpgradeChoiceClicked?.Invoke(
                    slot.SlotIndex);

                return;
            }
        }

        if (_currentStage == UpgradeSelectionStage.Weapon)
        {
            for (int index = 0;
                 index < _weaponSlots.Count;
                 index++)
            {
                WeaponChoiceSlotView slot =
                    _weaponSlots[index];

                if (slot == null ||
                    slot.SlotIndex != requestedSlotIndex)
                {
                    continue;
                }

                _isSelectionInputEnabled = false;

                WeaponChoiceClicked?.Invoke(
                    slot.SlotIndex);

                return;
            }
        }
    }

    /// <summary>
    /// 1차 증강 선택지를 생성하고 화면에 표시한다.
    /// 랜덤 선택은 Model이 수행하며 View는 전달받은 결과만 표시한다.
    /// </summary>
    public void ShowUpgradeChoices(
        IReadOnlyList<UpgradeData> upgradeChoices)
    {
        if (upgradeChoices == null)
        {
            throw new ArgumentNullException(nameof(upgradeChoices));
        }

        ValidateUpgradeReferences();
        ClearUpgradeSlots();

        _currentStage = UpgradeSelectionStage.Upgrade;
        _isSelectionInputEnabled = false;

        _upgradeUI.SetActive(true);
        _upgradeSelectPanel.SetActive(true);
        _weaponSelectPanel.SetActive(false);

        // 이전 제목 Tween이 남아 있다면 종료한다.
        _upgradeHeaderTween?.Kill();

        // 1차 증강 제목 프레임 등장 연출을 시작한다.
        _upgradeHeaderTween = CreateHeaderEntrance(
            _upgradeHeaderCanvasGroup);

        for (int slotIndex = 0;
             slotIndex < upgradeChoices.Count;
             slotIndex++)
        {
            UpgradeData upgradeData = upgradeChoices[slotIndex];

            if (upgradeData == null)
            {
                throw new InvalidOperationException(
                    $"{slotIndex}번 증강 선택 데이터가 비어 있습니다.");
            }

            // 현재 선택지 수만큼 슬롯 프리팹을 생성한다.
            UpgradeChoiceSlotView slotView = Instantiate(
                _upgradeSlotPrefab,
                _upgradeSlotContainer);

            slotView.Bind(
                slotIndex,
                upgradeData);

            // 슬롯 클릭 결과를 상위 View로 전달한다.
            slotView.Clicked += HandleUpgradeSlotClicked;

            _upgradeSlots.Add(slotView);
        }

        ConfigureHorizontalNavigation(_upgradeSlots);

        // 생성된 카드들을 왼쪽부터 시간차를 두고 떨어뜨린다.
        PlayEntranceSequence(
            _upgradeSlots,
            _upgradeSlotContainer);
    }

    /// <summary>
    /// 장착된 무기 데이터만 사용해 2차 무기 선택지를 생성한다.
    /// null 무기 슬롯은 생성하지 않는다.
    /// </summary>
    public void ShowWeaponChoices(
        IReadOnlyList<WeaponData> equippedWeapons,
        UpgradeType upgradeType)
    {
        if (equippedWeapons == null)
        {
            throw new ArgumentNullException(nameof(equippedWeapons));
        }

        ValidateWeaponReferences();
        ClearWeaponSlots();

        _currentStage = UpgradeSelectionStage.Weapon;
        _isSelectionInputEnabled = false;

        _upgradeUI.SetActive(true);
        _upgradeSelectPanel.SetActive(false);
        _weaponSelectPanel.SetActive(true);

        // 이전 제목 Tween이 남아 있다면 종료한다.
        _weaponHeaderTween?.Kill();

        // 2차 무기 제목 프레임 등장 연출을 시작한다.
        _weaponHeaderTween = CreateHeaderEntrance(
            _weaponHeaderCanvasGroup);

        for (int weaponSlotIndex = 0;
             weaponSlotIndex < equippedWeapons.Count;
             weaponSlotIndex++)
        {
            WeaponData weaponData =
                equippedWeapons[weaponSlotIndex];

            // 장착되지 않은 무기 슬롯은 선택지로 만들지 않는다.
            if (weaponData == null)
            {
                continue;
            }

            WeaponChoiceSlotView slotView = Instantiate(
                _weaponSlotPrefab,
                _weaponSlotContainer);

            // 표시 번호와 클릭 결과 모두 실제 장착 슬롯 번호를 사용한다.
            slotView.Bind(
                weaponSlotIndex,
                weaponData,
                upgradeType);

            slotView.Clicked += HandleWeaponSlotClicked;

            _weaponSlots.Add(slotView);
        }

        ConfigureHorizontalNavigation(_weaponSlots);

        // 2차 무기 선택 카드도 같은 규칙으로 순차 등장시킨다.
        PlayEntranceSequence(
            _weaponSlots,
            _weaponSlotContainer);
    }

    /// <summary>
    /// 선택 연출이 실행되는 동안 모든 슬롯 입력을 막는다.
    /// 중복 클릭을 방지하기 위해 Presenter가 호출한다.
    /// </summary>
    public void SetAllSlotsInteractable(bool interactable)
    {
        foreach (UpgradeChoiceSlotView slot in _upgradeSlots)
        {
            slot.SetInteractable(interactable);
        }

        foreach (WeaponChoiceSlotView slot in _weaponSlots)
        {
            slot.SetInteractable(interactable);
        }
    }

    /// <summary>
    /// 증강 선택 UI 전체를 닫고 생성된 슬롯을 정리한다.
    /// </summary>
    public void Hide()
    {
        _currentStage = UpgradeSelectionStage.None;
        _isSelectionInputEnabled = false;

        _upgradeHeaderTween?.Kill();
        _weaponHeaderTween?.Kill();

        _upgradeHeaderTween = null;
        _weaponHeaderTween = null;

        ClearUpgradeSlots();
        ClearWeaponSlots();

        _upgradeSelectPanel.SetActive(false);
        _weaponSelectPanel.SetActive(false);
        _upgradeUI.SetActive(false);
    }

    /// <summary>
    /// 생성된 1차 슬롯의 좌우 키보드 이동을 연결한다.
    /// </summary>
    private void ConfigureHorizontalNavigation(
        List<UpgradeChoiceSlotView> slots)
    {
        for (int index = 0; index < slots.Count; index++)
        {
            UpgradeChoiceSlotView leftSlot =
                index > 0
                    ? slots[index - 1]
                    : null;

            UpgradeChoiceSlotView rightSlot =
                index < slots.Count - 1
                    ? slots[index + 1]
                    : null;

            slots[index].SetHorizontalNavigation(
                leftSlot,
                rightSlot);
        }
    }

    /// <summary>
    /// 생성된 2차 슬롯의 좌우 키보드 이동을 연결한다.
    /// </summary>
    private void ConfigureHorizontalNavigation(
        List<WeaponChoiceSlotView> slots)
    {
        for (int index = 0; index < slots.Count; index++)
        {
            WeaponChoiceSlotView leftSlot =
                index > 0
                    ? slots[index - 1]
                    : null;

            WeaponChoiceSlotView rightSlot =
                index < slots.Count - 1
                    ? slots[index + 1]
                    : null;

            slots[index].SetHorizontalNavigation(
                leftSlot,
                rightSlot);
        }
    }

    /// <summary>
    /// 1차 슬롯 클릭을 Presenter용 이벤트로 전달한다.
    /// </summary>
    private void HandleUpgradeSlotClicked(int slotIndex)
    {
        UpgradeChoiceClicked?.Invoke(slotIndex);
    }

    /// <summary>
    /// 2차 슬롯 클릭을 Presenter용 이벤트로 전달한다.
    /// </summary>
    private void HandleWeaponSlotClicked(int weaponSlotIndex)
    {
        WeaponChoiceClicked?.Invoke(weaponSlotIndex);
    }

    /// <summary>
    /// 기존에 생성된 1차 증강 슬롯을 제거한다.
    /// </summary>
    private void ClearUpgradeSlots()
    {
        foreach (UpgradeChoiceSlotView slot in _upgradeSlots)
        {
            if (slot == null)
            {
                continue;
            }

            slot.Clicked -= HandleUpgradeSlotClicked;

            // Destroy는 프레임 마지막에 실행되므로 먼저 비활성화한다.
            slot.gameObject.SetActive(false);
            Destroy(slot.gameObject);
        }

        _upgradeSlots.Clear();
    }

    /// <summary>
    /// 기존에 생성된 2차 무기 슬롯을 제거한다.
    /// </summary>
    private void ClearWeaponSlots()
    {
        foreach (WeaponChoiceSlotView slot in _weaponSlots)
        {
            if (slot == null)
            {
                continue;
            }

            slot.Clicked -= HandleWeaponSlotClicked;

            // Destroy는 프레임 마지막에 실행되므로 먼저 비활성화한다.
            slot.gameObject.SetActive(false);
            Destroy(slot.gameObject);
        }

        _weaponSlots.Clear();
    }

    /// <summary>
    /// 1차 증강 화면의 필수 참조를 확인한다.
    /// </summary>
    private void ValidateUpgradeReferences()
    {
        if (_upgradeUI == null ||
            _upgradeSelectPanel == null ||
            _weaponSelectPanel == null ||
            _upgradeSlotContainer == null ||
            _upgradeSlotPrefab == null)
        {
            throw new InvalidOperationException(
                $"{name}의 1차 증강 View 참조가 모두 연결되지 않았습니다.");
        }
    }

    /// <summary>
    /// 2차 무기 화면의 필수 참조를 확인한다.
    /// </summary>
    private void ValidateWeaponReferences()
    {
        if (_upgradeUI == null ||
            _upgradeSelectPanel == null ||
            _weaponSelectPanel == null ||
            _weaponSlotContainer == null ||
            _weaponSlotPrefab == null)
        {
            throw new InvalidOperationException(
                $"{name}의 2차 무기 View 참조가 모두 연결되지 않았습니다.");
        }
    }

    /// <summary>
    /// 동적으로 생성된 슬롯을 왼쪽부터 차례로 등장시킨다.
    /// 등장 중에는 입력을 막고 마지막 카드가 도착하면 첫 슬롯을 선택한다.
    /// </summary>
    private void PlayEntranceSequence<TSlot>(
        List<TSlot> slots,
        Transform slotContainer)
        where TSlot : SelectableSlotView
    {
        if (slots == null || slots.Count == 0)
        {
            return;
        }

        if (_entranceDuration <= 0f)
        {
            throw new InvalidOperationException(
                "EntranceDuration은 0보다 커야 합니다.");
        }

        // HorizontalLayoutGroup이 각 카드의 최종 위치를 먼저 계산하게 한다.
        Canvas.ForceUpdateCanvases();

        if (slotContainer is RectTransform containerRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(
                containerRect);
        }

        Tween lastEntranceTween = null;

        for (int index = 0;
             index < slots.Count;
             index++)
        {
            TSlot slot = slots[index];

            // 등장 연출 중에는 클릭과 Submit 입력을 차단한다.
            slot.SetInteractable(false);

            // 제목이 먼저 나타난 뒤 카드가 순차적으로 떨어진다.
            float delay =
                _slotEntranceStartDelay +
                index * _entranceInterval;

            lastEntranceTween = slot.PlayEntrance(
                delay,
                _entranceVerticalOffset,
                _entranceDuration);
        }

        if (lastEntranceTween == null)
        {
            return;
        }

        // 가장 마지막 카드가 도착한 뒤 입력을 허용한다.
        lastEntranceTween.OnComplete(() =>
        {
            for (int index = 0;
                 index < slots.Count;
                 index++)
            {
                if (slots[index] != null)
                {
                    slots[index].SetInteractable(true);
                }
            }

            // 카드가 모두 도착한 뒤 숫자키 입력도 허용한다.
            _isSelectionInputEnabled = true;

            // 등장 연출이 끝난 뒤 첫 번째 슬롯에서 Arrow 점멸을 시작한다.
            if (slots.Count > 0 &&
                slots[0] != null)
            {
                slots[0].Select();
            }
        });
    }

    /// <summary>
    /// 전달받은 슬롯 목록에서 선택된 슬롯과 나머지 슬롯의
    /// 퇴장 연출을 실행하고, 선택 슬롯 연출이 끝나면 콜백을 호출한다.
    /// </summary>
    private void PlaySelectionExit<TSlot>(
        List<TSlot> slots,
        int selectedSlotIndex,
        Action onComplete)
        where TSlot : SelectableSlotView
    {
        // 퇴장 연출이 시작되면 숫자키 입력도 즉시 차단한다.
        _isSelectionInputEnabled = false;

        if (slots == null || slots.Count == 0)
        {
            throw new InvalidOperationException(
                "퇴장시킬 선택 슬롯이 없습니다.");
        }

        if (onComplete == null)
        {
            throw new ArgumentNullException(nameof(onComplete));
        }

        TSlot selectedSlot = null;

        // 표시 순서가 아닌 실제 슬롯 번호로 선택된 슬롯을 찾는다.
        for (int index = 0;
             index < slots.Count;
             index++)
        {
            TSlot slot = slots[index];

            if (slot != null &&
                slot.SlotIndex == selectedSlotIndex)
            {
                selectedSlot = slot;
                break;
            }
        }

        if (selectedSlot == null)
        {
            throw new InvalidOperationException(
                $"{selectedSlotIndex}번 선택 슬롯을 찾을 수 없습니다.");
        }

        // EventSystem의 현재 선택을 해제해 화살표 점멸을 종료한다.
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        // 퇴장 중에는 모든 슬롯 입력을 막는다.
        SetAllSlotsInteractable(false);

        Tween selectedExitTween = null;

        for (int index = 0;
             index < slots.Count;
             index++)
        {
            TSlot slot = slots[index];

            if (slot == null)
            {
                continue;
            }

            bool isSelected =
                slot == selectedSlot;

            Tween exitTween = slot.PlayExit(
                isSelected,
                _exitDuration,
                _selectedExitVerticalOffset);

            if (isSelected)
            {
                selectedExitTween = exitTween;
            }
        }

        if (selectedExitTween == null)
        {
            throw new InvalidOperationException(
                "선택 슬롯의 퇴장 Tween이 생성되지 않았습니다.");
        }

        // 선택된 카드의 강조·퇴장이 끝난 뒤 Presenter 흐름을 계속한다.
        selectedExitTween.OnComplete(
            () => onComplete.Invoke());
    }

    /// <summary>
    /// 1차 제목과 증강 카드들을 함께 퇴장시킨다.
    /// </summary>
    public void PlayUpgradeSelectionExit(
        int selectedChoiceIndex,
        Action onComplete)
    {
        _upgradeHeaderTween?.Kill();

        _upgradeHeaderTween = CreateHeaderExit(
            _upgradeHeaderCanvasGroup);

        PlaySelectionExit(
            _upgradeSlots,
            selectedChoiceIndex,
            onComplete);
    }

    /// <summary>
    /// 2차 제목과 무기 카드들을 함께 퇴장시킨다.
    /// </summary>
    public void PlayWeaponSelectionExit(
        int selectedWeaponSlotIndex,
        Action onComplete)
    {
        _weaponHeaderTween?.Kill();

        _weaponHeaderTween = CreateHeaderExit(
            _weaponHeaderCanvasGroup);

        PlaySelectionExit(
            _weaponSlots,
            selectedWeaponSlotIndex,
            onComplete);
    }

    private void OnDestroy()
    {
        _upgradeHeaderTween?.Kill();
        _weaponHeaderTween?.Kill();

        ClearUpgradeSlots();
        ClearWeaponSlots();
    }

    /// <summary>
    /// 제목 프레임이 위쪽에서 내려오며 나타나는 연출을 생성한다.
    /// </summary>
    private Tween CreateHeaderEntrance(
        CanvasGroup headerCanvasGroup)
    {
        if (headerCanvasGroup == null)
        {
            throw new ArgumentNullException(
                nameof(headerCanvasGroup));
        }

        RectTransform headerRect =
            headerCanvasGroup.transform as RectTransform;

        if (headerRect == null)
        {
            throw new MissingComponentException(
                $"{headerCanvasGroup.name}에 RectTransform이 없습니다.");
        }

        // 씬에 배치된 제목의 최종 위치를 보관한다.
        Vector2 destination =
            headerRect.anchoredPosition;

        // 제목이 최종 위치보다 약간 위쪽에서 시작한다.
        headerRect.anchoredPosition =
            destination + Vector2.up * _headerEntranceOffset;

        headerRect.localScale =
            Vector3.one * 0.92f;

        headerCanvasGroup.alpha = 0f;

        Sequence sequence = DOTween.Sequence();

        // 위쪽에서 최종 위치로 부드럽게 내려온다.
        sequence.Join(
            headerRect
                .DOAnchorPos(
                    destination,
                    _headerEntranceDuration)
                .SetEase(Ease.OutBack));

        // 이동하면서 제목 전체가 나타난다.
        sequence.Join(
            headerCanvasGroup
                .DOFade(
                    1f,
                    _headerEntranceDuration * 0.8f)
                .SetEase(Ease.OutQuad));

        // 제목 프레임이 원래 크기로 복구된다.
        sequence.Join(
            headerRect
                .DOScale(
                    Vector3.one,
                    _headerEntranceDuration)
                .SetEase(Ease.OutBack));

        // 인게임 시간이 멈춰 있어도 제목 연출을 실행한다.
        sequence.SetUpdate(true);

        return sequence;
    }

    /// <summary>
    /// 제목 프레임이 축소되며 사라지는 연출을 생성한다.
    /// </summary>
    private Tween CreateHeaderExit(
        CanvasGroup headerCanvasGroup)
    {
        if (headerCanvasGroup == null)
        {
            throw new ArgumentNullException(
                nameof(headerCanvasGroup));
        }

        RectTransform headerRect =
            headerCanvasGroup.transform as RectTransform;

        if (headerRect == null)
        {
            throw new MissingComponentException(
                $"{headerCanvasGroup.name}에 RectTransform이 없습니다.");
        }

        Sequence sequence = DOTween.Sequence();

        sequence.Join(
            headerCanvasGroup
                .DOFade(
                    0f,
                    _headerExitDuration)
                .SetEase(Ease.InQuad));

        sequence.Join(
            headerRect
                .DOScale(
                    0.96f,
                    _headerExitDuration)
                .SetEase(Ease.InQuad));

        sequence.SetUpdate(true);

        return sequence;
    }
}
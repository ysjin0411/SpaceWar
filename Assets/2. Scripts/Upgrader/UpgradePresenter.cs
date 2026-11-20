using System;
using System.Collections.Generic;

/// <summary>
/// UpgradeModel과 UpgradeSelectionView를 연결한다.
/// 사용자의 선택을 받아 Model에 전달하고,
/// 처리 결과에 따라 다음 화면을 View에 요청한다.
/// </summary>
public sealed class UpgradePresenter : IDisposable
{
    // 증강 후보 추첨과 효과 적용을 담당하는 Model
    private readonly UpgradeModel _model;

    // 증강 슬롯 생성과 화면 표시를 담당하는 View
    private readonly UpgradeSelectionView _view;

    // 증강 종료와 게이지 재시작을 담당하는 GameManager
    private readonly GameManager _gameManager;

    // 한 번에 화면에 표시할 증강 후보 개수
    private readonly int _displayChoiceCount;

    // 현재 증강 선택 화면이 열려 있는지 확인한다.
    private bool _isOpen;

    // 연속 클릭으로 같은 증강이 중복 처리되는 것을 방지한다.
    private bool _isInputLocked;

    /// <summary>
    /// Model, View, 스테이지 흐름을 전달받고 View 이벤트를 구독한다.
    /// </summary>
    public UpgradePresenter(
        UpgradeModel model,
        UpgradeSelectionView view,
        GameManager gameManager,
        int displayChoiceCount)
    {
        _model = model ??
            throw new ArgumentNullException(nameof(model));

        _view = view ??
            throw new ArgumentNullException(nameof(view));

        _gameManager = gameManager ??
            throw new ArgumentNullException(nameof(gameManager));

        if (displayChoiceCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(displayChoiceCount),
                "표시할 증강 후보 개수는 1개 이상이어야 합니다.");
        }

        _displayChoiceCount = displayChoiceCount;

        // View는 클릭 사실만 알리고,
        // 실제 선택 결과는 Presenter가 판단한다.
        _view.UpgradeChoiceClicked +=
            HandleUpgradeChoiceClicked;

        _view.WeaponChoiceClicked +=
            HandleWeaponChoiceClicked;
    }

    /// <summary>
    /// 새로운 증강 선택을 시작한다.
    /// Model에서 후보를 추첨하고 View에 표시를 요청한다.
    /// </summary>
    public void Open()
    {
        // 이미 열려 있다면 후보를 다시 추첨하지 않는다.
        if (_isOpen)
        {
            return;
        }

        _isOpen = true;
        _isInputLocked = false;

        IReadOnlyList<UpgradeData> choices =
            _model.DrawUpgradeChoices(
                _displayChoiceCount);

        _view.ShowUpgradeChoices(choices);
    }

    /// <summary>
    /// 1차 증강 슬롯 선택 후 퇴장 연출을 시작한다.
    /// 실제 효과 적용은 연출 완료 콜백에서 처리한다.
    /// </summary>
    private void HandleUpgradeChoiceClicked(
        int choiceIndex)
    {
        if (!_isOpen || _isInputLocked)
        {
            return;
        }

        _isInputLocked = true;
        _view.SetAllSlotsInteractable(false);

        _view.PlayUpgradeSelectionExit(
            choiceIndex,
            () => ResolveUpgradeChoice(choiceIndex));
    }

    /// <summary>
    /// 1차 선택 퇴장 연출이 끝난 뒤 증강 효과를 처리한다.
    /// </summary>
    private void ResolveUpgradeChoice(
        int choiceIndex)
    {
        UpgradeTarget selectedTarget =
            _model.ChooseUpgrade(choiceIndex);

        if (selectedTarget == UpgradeTarget.Player)
        {
            // Player 증강은 즉시 적용됐으므로 전체 선택을 종료한다.
            CompleteUpgradeSelection();
            return;
        }

        if (selectedTarget == UpgradeTarget.Weapon)
        {
            // Weapon 증강은 적용 대상을 정하기 위해 2차 화면으로 넘어간다.
            ShowWeaponSelection();
            return;
        }

        throw new InvalidOperationException(
            $"지원하지 않는 증강 대상입니다: {selectedTarget}");
    }

    /// <summary>
    /// 현재 장착된 무기를 가져와 2차 선택 화면에 표시한다.
    /// </summary>
    private void ShowWeaponSelection()
    {
        IReadOnlyList<WeaponData> equippedWeapons =
            _model.GetEquippedWeapons();

        if (!HasSelectableWeapon(equippedWeapons))
        {
            throw new InvalidOperationException(
                "증강을 적용할 수 있는 장착 무기가 없습니다.");
        }

        // 1차에서 고른 증강 종류를 전달해 해당 설명만 표시한다.
        _view.ShowWeaponChoices(equippedWeapons, _model.PendingWeaponUpgradeType);

        // 새로 생성된 2차 슬롯은 다시 입력받을 수 있다.
        _isInputLocked = false;
    }

    /// <summary>
    /// 2차 무기 슬롯 선택 후 퇴장 연출을 시작한다.
    /// </summary>
    private void HandleWeaponChoiceClicked(
        int weaponSlotIndex)
    {
        if (!_isOpen ||
            _isInputLocked ||
            !_model.HasPendingWeaponUpgrade)
        {
            return;
        }

        _isInputLocked = true;
        _view.SetAllSlotsInteractable(false);

        _view.PlayWeaponSelectionExit(
            weaponSlotIndex,
            () => ResolveWeaponChoice(weaponSlotIndex));
    }

    /// <summary>
    /// 2차 선택 퇴장 연출이 끝난 뒤 선택된 무기에 증강을 적용한다.
    /// </summary>
    private void ResolveWeaponChoice(
        int weaponSlotIndex)
    {
        _model.ApplyPendingWeaponUpgrade(
            weaponSlotIndex);

        CompleteUpgradeSelection();
    }

    /// <summary>
    /// 선택 UI를 닫고 GameManager에 증강 종료를 알린다.
    /// </summary>
    private void CompleteUpgradeSelection()
    {
        GameAudio.Play(GameSound.UpgradeSelect); // 실제 적용 완료.
        _view.Hide();

        _isOpen = false;
        _isInputLocked = false;

        // Time.timeScale 복구와 게이지 재시작은 GameManager가 담당한다.
        _gameManager.ExitUpgrade();
    }

    /// <summary>
    /// 장착 무기 목록에 실제 선택 가능한 무기가 있는지 확인한다.
    /// </summary>
    private bool HasSelectableWeapon(
        IReadOnlyList<WeaponData> equippedWeapons)
    {
        if (equippedWeapons == null)
        {
            return false;
        }

        for (int index = 0;
             index < equippedWeapons.Count;
             index++)
        {
            if (equippedWeapons[index] != null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Presenter가 제거될 때 View 이벤트 구독을 해제한다.
    /// </summary>
    public void Dispose()
    {
        _view.UpgradeChoiceClicked -=
            HandleUpgradeChoiceClicked;

        _view.WeaponChoiceClicked -=
            HandleWeaponChoiceClicked;
    }
}

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전투 중 플레이어와 무기의 증강을 처리한다.
/// 캐릭터는 Stage 시작 시 동적으로 생성되므로, StageManager가 새 PlayerController와 Weapon을 Initialize()를 통해 전달한다.
/// </summary>
public class Upgrader : MonoBehaviour
{
    [Header("런타임 의존성")]
    [Tooltip("현재 Stage의 GameManager")]
    [SerializeField] GameManager _gameManager;
    [Tooltip("StageManager가 생성한 현재 플레이어")]
    [SerializeField] PlayerController _playerController;
    [Tooltip("현재 플레이어 프리팹에 있는 Weapon")]
    [SerializeField] Weapon _weapon;

    [Header("증강 전체 UI")]
    [SerializeField] GameObject _upgradeUI;

    [Header("증강 패널")]
    [SerializeField] GameObject _firstSelectPanel;
    [SerializeField] GameObject _weaponSelectPanel;

    [Header("증강 데이터")]
    [SerializeField] UpgradeData[] _selectedUpgrades = new UpgradeData[3];

    [Header("1차 증강 설명")]
    [SerializeField] TMP_Text[] _firstUpgradeInfoTexts;

    [Header("2차 무기 선택 슬롯")]
    [SerializeField] GameObject[] _weaponSelectSlots;

    [Header("2차 무기 선택 아이콘")]
    [SerializeField] Image[] _weaponSelectIcons;

    [Header("2차 무기 선택 설명")]
    [SerializeField] TMP_Text[] _weaponSelectInfoTexts;

    // 무기 선택 전까지 임시로 보관할 증강 데이터
    UpgradeData _pendingUpgrade;

    // StageManager로부터 새 플레이어를 전달받았는지 확인한다.
    bool _isInitialized;

    /// <summary>
    /// StageManager가 생성한 플레이어와 Weapon을 연결한다.
    /// </summary>
    public void Initialize(GameManager gameManager, PlayerController playerController, Weapon weapon)
    {
        if (gameManager == null)
        {
            throw new ArgumentNullException(nameof(gameManager));
        }

        if (playerController == null)
        {
            throw new ArgumentNullException(nameof(playerController));
        }

        if (weapon == null)
        {
            throw new ArgumentNullException(nameof(weapon));
        }

        // 비활성화된 기존 Player 참조를 새로 생성된 캐릭터의 컴포넌트로 교체한다.
        _gameManager = gameManager;
        _playerController = playerController;
        _weapon = weapon;

        // 이전 전투에서 선택 중이던 증강 데이터를 비운다.
        _pendingUpgrade = null;
        _isInitialized = true;
    }

    /// <summary>
    /// 정지 후 첫 번째 증강 선택 화면을 연다.
    /// </summary>
    public void UpgradeSelectFirst()
    {
        EnsureInitialized();

        Time.timeScale = 0f;

        _upgradeUI.SetActive(true);
        _firstSelectPanel.SetActive(true);
        _weaponSelectPanel.SetActive(false);

        UpdateDataToUpgradePanel();
    }

    /// <summary>
    /// 현재 증강 후보들의 설명을 첫 번째 패널에 표시한다.
    /// </summary>
    private void UpdateDataToUpgradePanel()
    {
        // 데이터와 UI 중 더 적은 개수까지만 처리하여
        // 배열 크기가 다를 때 범위를 벗어나지 않게 한다.
        int displayCount = Mathf.Min(
            _selectedUpgrades.Length,
            _firstUpgradeInfoTexts.Length);

        for (int i = 0; i < displayCount; i++)
        {
            UpgradeData upgradeData =
                _selectedUpgrades[i];

            TMP_Text infoText =
                _firstUpgradeInfoTexts[i];

            if (upgradeData == null || infoText == null)
            {
                continue;
            }

            infoText.text =
                upgradeData.UpgradeInfo;
        }
    }

    /// <summary>
    /// 첫 번째 증강 선택 결과를 처리한다.
    /// </summary>
    /// <param name="index">선택한 증강의 배열 인덱스</param>
    public void FirstSelectUpgrade(int index)
    {
        EnsureInitialized();

        if (index < 0 ||
            index >= _selectedUpgrades.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index),
                index,
                "선택한 증강 인덱스가 범위를 벗어났습니다.");
        }

        UpgradeData selectedData =
            _selectedUpgrades[index];

        if (selectedData == null)
        {
            throw new InvalidOperationException(
                $"{index}번 증강 데이터가 비어 있습니다.");
        }

        // 이동속도 증강은 플레이어에게 바로 적용한다.
        if (selectedData.Type == UpgradeType.MoveSpeed)
        {
            _playerController.AddMoveSpeed(
                selectedData.UpgradeSpeedValue);

            _gameManager.ExitUpgrade();
            return;
        }

        // 연사력과 공격력은 어떤 무기에 적용할지 한 번 더 선택해야 한다.
        _firstSelectPanel.SetActive(false);

        OpenWeaponSelect(selectedData);
    }

    /// <summary>
    /// 무기 증강을 적용할 슬롯을 선택하는 패널을 연다.
    /// </summary>
    private void OpenWeaponSelect(
        UpgradeData upgradeData)
    {
        _pendingUpgrade = upgradeData;

        UpdateWeaponSelectInfo();

        _weaponSelectPanel.SetActive(true);

        _gameManager.SetWeaponSelectPanelState(true);
    }

    /// <summary>
    /// 선택한 장착 슬롯의 무기에
    /// 대기 중인 증강을 적용한다.
    /// </summary>
    /// <param name="slotIndex">장착 무기 슬롯 번호</param>
    public void ChooseWeapon(int slotIndex)
    {
        EnsureInitialized();

        if (_pendingUpgrade == null)
        {
            return;
        }

        WeaponData weaponData = _weapon.GetWeaponDataAtSlot(slotIndex);

        // 빈 슬롯에는 증강을 적용하지 않는다.
        if (weaponData == null)
        {
            return;
        }

        if (_pendingUpgrade.Type == UpgradeType.FireRate)
        {
            _weapon.AddFireRate(slotIndex);
        }
        else if (_pendingUpgrade.Type == UpgradeType.Damage)
        {
            int damageValue = Mathf.RoundToInt(_pendingUpgrade.UpgradeDamageValue);

            _weapon.AddDamage(slotIndex, damageValue);
        }

        _pendingUpgrade = null;

        _gameManager.ExitUpgrade();
    }

    /// <summary>
    /// 현재 실제로 장착된 무기만
    /// 증강 대상 화면에 표시한다.
    /// </summary>
    private void UpdateWeaponSelectInfo()
    {
        for (int slotIndex = 0; slotIndex < _weaponSelectSlots.Length; slotIndex++)
        {
            WeaponData weaponData = _weapon.GetWeaponDataAtSlot(slotIndex);

            bool hasWeapon = weaponData != null;

            GameObject slotObject = _weaponSelectSlots[slotIndex];

            if (slotObject != null)
            {
                slotObject.SetActive(hasWeapon);
            }

            if (!hasWeapon)
            {
                continue;
            }

            // 아이콘 배열에 해당 인덱스가 있는 경우에만 표시한다.
            if (slotIndex < _weaponSelectIcons.Length)
            {
                Image weaponIcon = _weaponSelectIcons[slotIndex];

                if (weaponIcon != null)
                {
                    weaponIcon.sprite = weaponData.Icon;

                    weaponIcon.enabled = weaponData.Icon != null;
                }
            }

            // 설명 배열에 해당 인덱스가 있는 경우에만 표시한다.
            if (slotIndex < _weaponSelectInfoTexts.Length)
            {
                TMP_Text infoText = _weaponSelectInfoTexts[slotIndex];

                if (infoText != null)
                {
                    infoText.text = "강화 대상: " + weaponData.WeaponName;
                }
            }
        }
    }

    /// <summary>
    /// 새 플레이어가 연결되기 전에 증강 기능이 호출되면 원인을 알 수 있는 예외를 발생시킨다.
    /// </summary>
    private void EnsureInitialized()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException(
                "Upgrader가 아직 초기화되지 않았습니다.");
        }
    }
}
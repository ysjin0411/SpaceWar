using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 증강 후보 추첨과 증강 적용 규칙을 담당하는 Model이다.
/// Unity UI, Button, 프리팹, 애니메이션은 알지 못한다.
/// </summary>
public sealed class UpgradeModel
{
    // 전체 증강 데이터 저장소
    private readonly UpgradeMap _upgradeMap;

    // 플레이어 대상 증강을 적용할 런타임 플레이어
    private readonly PlayerController _playerController;

    // 무기 대상 증강을 적용할 런타임 무기 관리자
    private readonly Weapon _weapon;

    // 자연 게이지 충전속도 증강을 적용할 스테이지 관리자
    private readonly GameManager _gameManager;

    // 중복 없는 랜덤 후보 추첨에 사용할 난수 생성기
    private readonly System.Random _random;

    // 현재 화면에 표시된 증강 후보 목록
    private readonly List<UpgradeData> _currentChoices = new();

    // 1차에서 선택됐지만 아직 대상 무기가 정해지지 않은 증강
    private UpgradeData _pendingWeaponUpgrade;

    // Presenter가 현재 선택 후보를 읽을 수 있도록 공개한다.
    public IReadOnlyList<UpgradeData> CurrentChoices =>
        _currentChoices;

    // 현재 2차 무기 선택을 기다리는 증강이 있는지 반환한다.
    public bool HasPendingWeaponUpgrade =>
        _pendingWeaponUpgrade != null;

    // 2차 선택 화면에 표시할 증강 종류. 선택 대기 중일 때만 조회한다.
    public UpgradeType PendingWeaponUpgradeType =>
        _pendingWeaponUpgrade != null
            ? _pendingWeaponUpgrade.Type
            : throw new InvalidOperationException("선택 대기 중인 무기 증강이 없습니다.");

    /// <summary>
    /// Model이 사용할 데이터와 런타임 적용 대상을 전달받는다.
    /// </summary>
    public UpgradeModel(
        UpgradeMap upgradeMap,
        PlayerController playerController,
        Weapon weapon,
        GameManager gameManager,
        System.Random random = null)
    {
        _upgradeMap = upgradeMap ??
            throw new ArgumentNullException(nameof(upgradeMap));

        _playerController = playerController ??
            throw new ArgumentNullException(nameof(playerController));

        _weapon = weapon ??
            throw new ArgumentNullException(nameof(weapon));

        _gameManager = gameManager ??
            throw new ArgumentNullException(nameof(gameManager));

        // 테스트에서는 고정 Seed의 Random을 전달할 수 있다.
        // 실제 게임에서는 전달하지 않으면 새로운 난수 생성기를 사용한다.
        _random = random ?? new System.Random();
    }

    /// <summary>
    /// 전체 N개 증강 중 중복 없이 지정된 개수만큼 추첨한다.
    /// </summary>
    /// <param name="choiceCount">화면에 표시할 후보 개수</param>
    public IReadOnlyList<UpgradeData> DrawUpgradeChoices(
        int choiceCount)
    {
        if (choiceCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(choiceCount),
                "증강 후보 개수는 1개 이상이어야 합니다.");
        }

        // 저장소의 원본 배열을 보호하기 위해 복사본을 가져온다.
        UpgradeData[] candidatePool =
            _upgradeMap.CreateSnapshot();

        if (choiceCount > candidatePool.Length)
        {
            throw new InvalidOperationException(
                $"요청한 후보 개수({choiceCount})가 " +
                $"전체 증강 개수({candidatePool.Length})보다 많습니다.");
        }

        _currentChoices.Clear();
        _pendingWeaponUpgrade = null;

        /*
         * Partial Fisher-Yates Shuffle을 사용한다.
         * 배열 전체를 섞지 않고 필요한 앞쪽 choiceCount개만 확정하므로
         * 중복 없이 후보를 선택할 수 있다.
         */
        for (int index = 0;
             index < choiceCount;
             index++)
        {
            int randomIndex =
                _random.Next(
                    index,
                    candidatePool.Length);

            // 현재 위치와 무작위 위치의 데이터를 교환한다.
            (candidatePool[index], candidatePool[randomIndex]) =
                (candidatePool[randomIndex], candidatePool[index]);

            _currentChoices.Add(
                candidatePool[index]);
        }

        return _currentChoices;
    }

    /// <summary>
    /// 1차 선택 결과를 처리한다.
    /// Player 대상이면 즉시 적용하고, Weapon 대상이면 2차 선택을 준비한다.
    /// </summary>
    /// <returns>선택된 증강이 적용될 대상</returns>
    public UpgradeTarget ChooseUpgrade(int choiceIndex)
    {
        UpgradeData selectedUpgrade =
            GetCurrentChoice(choiceIndex);

        if (selectedUpgrade.Target == UpgradeTarget.Player)
        {
            ApplyPlayerUpgrade(selectedUpgrade);

            _pendingWeaponUpgrade = null;

            return UpgradeTarget.Player;
        }

        if (selectedUpgrade.Target == UpgradeTarget.Weapon)
        {
            // 대상 무기가 결정될 때까지 증강 데이터를 보관한다.
            _pendingWeaponUpgrade = selectedUpgrade;

            return UpgradeTarget.Weapon;
        }

        throw new InvalidOperationException(
            $"지원하지 않는 증강 대상입니다: {selectedUpgrade.Target}");
    }

    /// <summary>
    /// 현재 장착 슬롯의 WeaponData를 실제 슬롯 순서대로 반환한다.
    /// 빈 슬롯은 null로 유지해 실제 무기 슬롯 번호가 바뀌지 않게 한다.
    /// </summary>
    public IReadOnlyList<WeaponData> GetEquippedWeapons()
    {
        WeaponData[] equippedWeapons =
            new WeaponData[_weapon.EquippedSlotCount];

        for (int slotIndex = 0;
             slotIndex < equippedWeapons.Length;
             slotIndex++)
        {
            equippedWeapons[slotIndex] =
                _weapon.GetWeaponDataAtSlot(slotIndex);
        }

        return equippedWeapons;
    }

    /// <summary>
    /// 1차에서 보류한 무기 증강을 선택된 무기에 적용한다.
    /// </summary>
    public void ApplyPendingWeaponUpgrade(
        int weaponSlotIndex)
    {
        if (_pendingWeaponUpgrade == null)
        {
            throw new InvalidOperationException(
                "적용을 기다리는 무기 증강이 없습니다.");
        }

        WeaponData selectedWeapon =
            _weapon.GetWeaponDataAtSlot(weaponSlotIndex);

        if (selectedWeapon == null)
        {
            throw new InvalidOperationException(
                $"{weaponSlotIndex}번 슬롯에는 장착된 무기가 없습니다.");
        }

        switch (_pendingWeaponUpgrade.Type)
        {
            case UpgradeType.FireRate:

                // 무기마다 설정된 연사속도 강화 수치를 한 단계 적용한다.
                _weapon.AddFireRate(weaponSlotIndex);
                break;

            case UpgradeType.Damage:

                // 0.15는 기본 공격력의 15% 증가를 의미한다.
                float damageAmount = _pendingWeaponUpgrade.UpgradeValue;

                _weapon.AddDamageRatio(
                    weaponSlotIndex,
                    damageAmount);
                break;

            default:

                throw new InvalidOperationException(
                    $"무기에 적용할 수 없는 증강입니다: " +
                    $"{_pendingWeaponUpgrade.Type}");
        }

        // 같은 증강이 중복 적용되지 않도록 적용 후 제거한다.
        _pendingWeaponUpgrade = null;
    }

    /// <summary>
    /// 현재 선택된 Player 대상 증강을 실제 플레이어에게 적용한다.
    /// </summary>
    private void ApplyPlayerUpgrade(
        UpgradeData upgradeData)
    {
        // / </summary>
        switch (upgradeData.Type)
        {
            case UpgradeType.MoveSpeed:

                _playerController.AddMoveSpeed(
                    upgradeData.UpgradeValue);
                break;

            case UpgradeType.MaxHealthAndFullHeal:

                float healthAmount = upgradeData.UpgradeValue;

                _playerController.IncreaseMaxHpAndFullHeal(
                    healthAmount);
                break;

            case UpgradeType.NaturalGaugeChargeRate:

                _gameManager.IncreaseNaturalGaugeChargeRate(
                    upgradeData.UpgradeValue);
                break;

            default:

                throw new InvalidOperationException(
                    $"플레이어에게 적용할 수 없는 증강입니다: " +
                    $"{upgradeData.Type}");
        }
    }

    /// <summary>
    /// 현재 화면에 표시된 후보에서 지정된 증강을 찾는다.
    /// </summary>
    private UpgradeData GetCurrentChoice(int choiceIndex)
    {
        if (choiceIndex < 0 ||
            choiceIndex >= _currentChoices.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(choiceIndex),
                choiceIndex,
                "현재 증강 후보 범위를 벗어났습니다.");
        }

        UpgradeData selectedUpgrade =
            _currentChoices[choiceIndex];

        if (selectedUpgrade == null)
        {
            throw new InvalidOperationException(
                $"{choiceIndex}번 증강 후보가 비어 있습니다.");
        }

        return selectedUpgrade;
    }
}

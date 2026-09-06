using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

/// <summary>
/// 인게임에서 현재 사용 중인 무기와
/// 캐릭터가 장착한 무기 3개의 아이콘을 표시한다.
/// </summary>
public class WeaponUI : MonoBehaviour
{
    [Header("현재 사용 중인 무기")]

    [Tooltip("현재 선택해서 사용 중인 무기 아이콘")]
    [SerializeField]
    private Image _currentWeapon;

    // 현재 무기가 Grenade일 때 충전 상태를 표시한다.
    [SerializeField]
    private WeaponChargeView _currentWeaponChargeView;

    [Header("Tab 장착 무기 목록")]

    [Tooltip("JSON에 저장된 장착 슬롯 순서대로 연결한다.")]
    [SerializeField]
    private Image[] _equippedWeaponIcons = new Image[3];

    // Tab의 각 장착 무기 아이콘 위에 표시할 충전 UI다.
    // Equipped Weapon Icons와 동일한 슬롯 순서로 연결한다.
    [SerializeField]
    private WeaponChargeView[] _equippedWeaponChargeViews =
        new WeaponChargeView[3];


    /// <summary>
    /// 현재 사용 중인 무기의 아이콘을 갱신한다.
    /// </summary>
    public void UpdateCurrentWeapon(Sprite weaponIcon)
    {
        SetWeaponIcon(
            _currentWeapon,
            weaponIcon);
    }


    /// <summary>
    /// JSON 장착 슬롯에서 변환된 WeaponData 3개를
    /// Tab 무기 목록 UI에 표시한다.
    /// </summary>
    public void UpdateEquippedWeapons(
        WeaponData[] equippedWeaponDatas)
    {
        // 전달받은 장착 무기 배열이 없는지 확인한다.
        if (equippedWeaponDatas == null)
        {
            throw new ArgumentNullException(
                nameof(equippedWeaponDatas),
                "장착 무기 데이터 배열이 전달되지 않았습니다.");
        }

        // Inspector의 아이콘 배열이 없는지 확인한다.
        if (_equippedWeaponIcons == null)
        {
            throw new InvalidOperationException(
                "WeaponUI의 Equipped Weapon Icons가 연결되지 않았습니다.");
        }

        // 장착 무기 개수와 UI 아이콘 개수가 같은지 확인한다.
        if (_equippedWeaponIcons.Length !=
            equippedWeaponDatas.Length)
        {
            throw new InvalidOperationException(
                $"장착 무기 개수({equippedWeaponDatas.Length})와 " +
                $"Tab 무기 아이콘 개수({_equippedWeaponIcons.Length})가 다릅니다.");
        }

        // 장착 슬롯 순서대로 아이콘을 갱신한다.
        for (int slotIndex = 0;
             slotIndex < equippedWeaponDatas.Length;
             slotIndex++)
        {
            // 현재 슬롯의 WeaponData를 가져온다.
            WeaponData weaponData =
                equippedWeaponDatas[slotIndex];

            // 현재 슬롯에 표시할 아이콘을 가져온다.
            Sprite weaponIcon =
                weaponData != null
                    ? weaponData.Icon
                    : null;

            // 같은 슬롯 번호의 Image에 아이콘을 표시한다.
            SetWeaponIcon(
                _equippedWeaponIcons[slotIndex],
                weaponIcon);
        }
    }

    /// <summary>
    /// 현재 무기와 Tab 장착 슬롯의 충전 상태를 갱신한다.
    /// 무기 데이터가 아직 초기화되지 않은 슬롯은 빈 슬롯으로 표시한다.
    /// </summary>
    public void UpdateChargeDisplays(
        WeaponRuntimeState currentWeapon,
        IReadOnlyList<WeaponRuntimeState> equippedWeapons)
    {
        if (_currentWeaponChargeView == null)
        {
            throw new InvalidOperationException(
                "WeaponUI의 Current Weapon Charge View가 연결되지 않았습니다.");
        }

        if (equippedWeapons == null)
        {
            throw new ArgumentNullException(
                nameof(equippedWeapons),
                "장착 무기 목록이 전달되지 않았습니다.");
        }

        if (_equippedWeaponChargeViews == null ||
            _equippedWeaponChargeViews.Length !=
            _equippedWeaponIcons.Length)
        {
            throw new InvalidOperationException(
                "WeaponUI의 장착 무기 아이콘과 충전 UI 개수가 다릅니다.");
        }

        // UI가 제공하는 슬롯보다 실제 장착 무기가 많으면
        // 일부 무기의 상태를 표시할 수 없으므로 설정 오류로 처리한다.
        if (equippedWeapons.Count >
            _equippedWeaponChargeViews.Length)
        {
            throw new InvalidOperationException(
                $"장착 무기 개수({equippedWeapons.Count})가 " +
                $"충전 UI 슬롯 개수({_equippedWeaponChargeViews.Length})보다 많습니다.");
        }

        // 현재 선택해서 사용하는 무기의 충전 상태를 표시한다.
        // 일반 무기거나 아직 무기가 없으면 내부에서 충전 UI를 숨긴다.
        _currentWeaponChargeView.Refresh(
            currentWeapon);

        // UI에 준비된 슬롯 3개를 모두 갱신한다.
        for (int slotIndex = 0;
             slotIndex < _equippedWeaponChargeViews.Length;
             slotIndex++)
        {
            WeaponChargeView chargeView =
                _equippedWeaponChargeViews[slotIndex];

            if (chargeView == null)
            {
                throw new InvalidOperationException(
                    $"WeaponUI의 {slotIndex + 1}번 슬롯 Charge View가 연결되지 않았습니다.");
            }

            // 무기 초기화 전이거나 빈 슬롯이면 null을 전달한다.
            // WeaponChargeView는 null을 받으면 해당 충전 표시를 숨긴다.
            WeaponRuntimeState slotWeapon =
                slotIndex < equippedWeapons.Count
                    ? equippedWeapons[slotIndex]
                    : null;

            chargeView.Refresh(
                slotWeapon);
        }
    }

    /// <summary>
    /// 지정한 Image에 무기 아이콘을 표시한다.
    /// 아이콘이 없다면 Image를 숨긴다.
    /// </summary>
    private void SetWeaponIcon(
        Image targetImage,
        Sprite weaponIcon)
    {
        // Inspector 연결이 빠졌는지 확인한다.
        if (targetImage == null)
        {
            throw new InvalidOperationException(
                "WeaponUI에 무기 Image가 연결되지 않았습니다.");
        }

        // 표시할 아이콘이 없다면 Image를 숨긴다.
        if (weaponIcon == null)
        {
            targetImage.sprite = null;
            targetImage.enabled = false;
            return;
        }

        // WeaponData에 설정된 아이콘을 표시한다.
        targetImage.sprite = weaponIcon;

        // 원본 이미지의 가로세로 비율을 유지한다.
        targetImage.preserveAspect = true;

        // Image를 활성화한다.
        targetImage.enabled = true;
    }
}
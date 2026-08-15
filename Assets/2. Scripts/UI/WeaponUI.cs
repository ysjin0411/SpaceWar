using System;
using UnityEngine;
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


    [Header("Tab 장착 무기 목록")]

    [Tooltip("JSON에 저장된 장착 슬롯 순서대로 연결한다.")]
    [SerializeField]
    private Image[] _equippedWeaponIcons = new Image[3];


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
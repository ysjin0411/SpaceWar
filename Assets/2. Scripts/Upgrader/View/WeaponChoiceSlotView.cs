using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 2차 무기 선택 슬롯 하나를 표시한다.
/// WeaponData를 전달받아 번호, 무기 아이콘, 설명을 갱신한다.
/// </summary>
public class WeaponChoiceSlotView : SelectableSlotView
{
    [Header("무기 표시")]

    // 화면에 표시할 1부터 시작하는 슬롯 번호
    [SerializeField] private TMP_Text _numberingText;

    // 장착된 무기의 아이콘
    [SerializeField] private Image _weaponIcon;

    // WeaponData에 등록된 증강 대상 선택 전용 설명
    [SerializeField] private TMP_Text _infoText;

    /// <summary>
    /// 장착 무기 데이터를 이 슬롯 UI에 표시한다.
    /// </summary>
    /// <param name="slotIndex">Weapon 내부의 장착 슬롯 번호</param>
    /// <param name="weaponData">표시할 장착 무기 데이터</param>
    public void Bind(
        int slotIndex,
        WeaponData weaponData,
        UpgradeType upgradeType)
    {
        if (weaponData == null)
        {
            throw new ArgumentNullException(nameof(weaponData));
        }

        ValidateReferences();

        // 플레이어에게는 장착 슬롯을 1, 2, 3으로 표시한다.
        _numberingText.text = (slotIndex + 1).ToString();

        // WeaponData에 등록된 무기 아이콘을 표시한다.
        _weaponIcon.sprite = weaponData.Icon;
        _weaponIcon.enabled = weaponData.Icon != null;
        _weaponIcon.preserveAspect = true;

        // 일반 무기 설명이 아니라 증강 대상 선택 전용 문구를 표시한다.
        _infoText.text =
            weaponData.GetUpgradeSelectionInfo(upgradeType);

        // 공통 Button 이벤트와 선택 표시를 초기화한다.
        InitializeSelectable(slotIndex);
    }

    /// <summary>
    /// 프리팹의 필수 UI 참조가 연결됐는지 확인한다.
    /// </summary>
    private void ValidateReferences()
    {
        if (_numberingText == null)
        {
            throw new InvalidOperationException(
                $"{name}에 NumberingText가 연결되지 않았습니다.");
        }

        if (_weaponIcon == null)
        {
            throw new InvalidOperationException(
                $"{name}에 WeaponIcon이 연결되지 않았습니다.");
        }

        if (_infoText == null)
        {
            throw new InvalidOperationException(
                $"{name}에 WeaponSelectInfo가 연결되지 않았습니다.");
        }
    }
}
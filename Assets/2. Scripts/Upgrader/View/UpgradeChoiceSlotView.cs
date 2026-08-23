using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 1차 증강 선택 슬롯 하나를 표시한다.
/// UpgradeData를 전달받아 번호, 아이콘, 이름, 설명을 갱신한다.
/// </summary>
public class UpgradeChoiceSlotView : SelectableSlotView
{
    [Header("증강 표시")]

    // 화면에 표시할 1부터 시작하는 슬롯 번호
    [SerializeField] private TMP_Text _numberingText;

    // UpgradeData에 등록된 증강 아이콘
    [SerializeField] private Image _upgradeIcon;

    // 이동속도 강화 등의 증강 이름
    [SerializeField] private TMP_Text _typeText;

    // 증강의 구체적인 효과 설명
    [SerializeField] private TMP_Text _infoText;

    /// <summary>
    /// 선택 후보 데이터를 이 슬롯 UI에 표시한다.
    /// </summary>
    /// <param name="slotIndex">0부터 시작하는 슬롯 위치</param>
    /// <param name="upgradeData">표시할 증강 데이터</param>
    public void Bind(
        int slotIndex,
        UpgradeData upgradeData)
    {
        if (upgradeData == null)
        {
            throw new ArgumentNullException(nameof(upgradeData));
        }

        ValidateReferences();

        // 플레이어에게는 배열 인덱스가 아닌 1, 2, 3으로 표시한다.
        _numberingText.text = (slotIndex + 1).ToString();

        // 증강 데이터에 등록된 이름과 설명을 표시한다.
        _typeText.text = upgradeData.UpgradeName;
        _infoText.text = upgradeData.UpgradeInfo;

        // 아이콘이 없는 데이터라면 빈 Image를 표시하지 않는다.
        _upgradeIcon.sprite = upgradeData.UpgradeIcon;
        _upgradeIcon.enabled = upgradeData.UpgradeIcon != null;
        _upgradeIcon.preserveAspect = true;

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

        if (_upgradeIcon == null)
        {
            throw new InvalidOperationException(
                $"{name}에 UpgradeIcon이 연결되지 않았습니다.");
        }

        if (_typeText == null)
        {
            throw new InvalidOperationException(
                $"{name}에 TypeText가 연결되지 않았습니다.");
        }

        if (_infoText == null)
        {
            throw new InvalidOperationException(
                $"{name}에 InfoText가 연결되지 않았습니다.");
        }
    }
}
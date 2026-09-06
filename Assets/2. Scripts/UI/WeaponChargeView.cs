using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 무기 아이콘 하나 위에 충전형 무기의
/// 현재 보유량, 다음 충전 시간, 방사형 충전 상태를 표시한다.
/// </summary>
public class WeaponChargeView : MonoBehaviour
{
    [Header("충전 표시 UI")]

    // 무기 아이콘 위를 어둡게 덮으며
    // 충전 진행에 따라 Radial 360 방식으로 줄어드는 이미지다.
    [SerializeField] Image _cooldownOverlay;

    // 현재 보유량과 최대 보유량을 표시하는 텍스트다.
    [SerializeField] TMP_Text _chargeCountText;

    // 다음 수류탄 한 개가 충전되기까지 남은 시간을 표시한다.
    [SerializeField] TMP_Text _remainingTimeText;

    // Awake가 실행되기 전에 Refresh가 호출되는 상황을 처리한다.
    bool _isInitialized;

    private void Awake()
    {
        // 연결만 검사한다. 표시 여부는 Refresh에서 결정한다.
        // 비활성 UI를 Refresh로 켜는 도중 Awake가 실행되어도
        // 다시 숨겨지는 일이 없도록 한다.
        Initialize();
    }

    // 무기 종류에 맞게 수류탄 충전 또는 실드 체력을 표시한다.
    public void Refresh(WeaponRuntimeState weapon)
    {
        Initialize();

        // 빈 슬롯은 모든 상태 표시를 숨긴다.
        if (weapon == null)
        {
            Hide();
            return;
        }

        // 실드는 별도의 체력 표시를 사용한다.
        if (weapon.Data.IsShield)
        {
            RefreshShield(weapon);
            return;
        }

        // 충전형 무기도 실드도 아니면 표시하지 않는다.
        if (!weapon.Data.UsesCharges)
        {
            Hide();
            return;
        }

        gameObject.SetActive(true);

        // 수류탄의 현재 보유량과 최대 보유량을 표시한다.
        _chargeCountText.text =
            $"{weapon.CurrentChargeCount}/{weapon.Data.MaximumChargeCount}";

        bool isRecovering =
            weapon.CurrentChargeCount <
            weapon.Data.MaximumChargeCount;

        // 충전 중에만 어두운 영역과 남은 시간을 표시한다.
        _cooldownOverlay.gameObject.SetActive(isRecovering);
        _remainingTimeText.gameObject.SetActive(isRecovering);

        if (!isRecovering)
        {
            return;
        }

        _cooldownOverlay.sprite = weapon.Data.Icon;
        _cooldownOverlay.preserveAspect = true;

        // 수류탄 충전이 완료될수록 어두운 영역이 줄어든다.
        _cooldownOverlay.fillAmount =
            1f - weapon.ChargeRecoveryProgress;

        _remainingTimeText.text =
            $"{weapon.ChargeRecoveryRemaining:0.0}s";
    }

    // 같은 아이콘 UI에 실드 체력과 회복 상태를 표시한다.
    private void RefreshShield(WeaponRuntimeState weapon)
    {
        gameObject.SetActive(true);

        // 정수는 100/100처럼 표시하고,
        // 소수점이 있으면 82.5/100처럼 한 자리까지 표시한다.
        _chargeCountText.text =
            $"{weapon.CurrentShieldHealth:0.#}/{weapon.MaximumShieldHealth:0.#}";

        // 실드는 수류탄처럼 다음 한 개의 충전 시간을 표시하지 않는다.
        _remainingTimeText.gameObject.SetActive(false);

        bool hasMissingHealth =
            weapon.CurrentShieldHealth < weapon.MaximumShieldHealth;

        // 체력이 가득 차면 어두운 덮개를 숨긴다.
        _cooldownOverlay.gameObject.SetActive(hasMissingHealth);

        if (!hasMissingHealth)
        {
            return;
        }

        // 현재 실드 아이콘의 모양을 그대로 사용한다.
        _cooldownOverlay.sprite = weapon.Data.Icon;
        _cooldownOverlay.preserveAspect = true;

        // 잃은 체력만큼 어둡게 덮는다.
        // 실제 체력이 소수점 단위로 회복되므로 게이지도 부드럽게 차오른다.
        _cooldownOverlay.fillAmount =
            1f - weapon.ShieldHealthRatio;
    }

    // 실드와 충전형 무기를 제외한 무기에서는 상태 표시를 숨긴다.
    public void Hide()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Inspector 참조가 빠졌는지 한 번만 검사한다.
    /// </summary>
    private void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        if (_cooldownOverlay == null)
        {
            throw new InvalidOperationException(
                $"{name}의 WeaponChargeView에 Cooldown Overlay가 연결되지 않았습니다.");
        }

        if (_chargeCountText == null)
        {
            throw new InvalidOperationException(
                $"{name}의 WeaponChargeView에 Charge Count Text가 연결되지 않았습니다.");
        }

        if (_remainingTimeText == null)
        {
            throw new InvalidOperationException(
                $"{name}의 WeaponChargeView에 Remaining Time Text가 연결되지 않았습니다.");
        }

        _isInitialized = true;
    }
}
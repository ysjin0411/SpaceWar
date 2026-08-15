using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 증강 게이지의 화면 연출만 담당한다.
/// 실제 충전량과 증강 실행은 GameManager가 관리한다.
/// </summary>
public class GaugeUIManager : MonoBehaviour
{
    [Header("게이지 이미지")]
    [Tooltip("증강 게이지를 fillAmount로 표시할 이미지")]
    [SerializeField] Image _gaugeBar;

    [Header("게이지 연출 설정")]
    [Tooltip("적 처치 보너스가 게이지에 반영되는 시간")]
    [Min(0.01f)]
    [SerializeField] float _bonusFillDuration = 0.2f;

    [Tooltip("게이지가 가득 찰 때 마무리되는 시간")]
    [Min(0.01f)]
    [SerializeField] float _completeDuration = 0.1f;

    // 현재 재생 중인 게이지 연출이다.
    Tween _gaugeTween;

    private void Awake()
    {
        if (_gaugeBar == null)
        {
            throw new InvalidOperationException(
                $"{name}의 GaugeUIManager에 Gauge Bar가 연결되지 않았습니다.");
        }
    }

    /// <summary>
    /// 현재 충전량부터 최대 충전량까지 자연스럽게 채운다.
    /// 기본 충전 속도는 실제 시간 1초당 충전량 1이다.
    /// </summary>
    public void StartCharging(
        float currentCharge,
        float maxCharge)
    {
        if (maxCharge <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxCharge),
                "최대 게이지는 0보다 커야 합니다.");
        }

        _gaugeTween?.Kill();

        currentCharge = Mathf.Clamp(
            currentCharge,
            0f,
            maxCharge);

        float currentRatio =
            currentCharge / maxCharge;

        // 충전 시작 위치는 즉시 맞춘다.
        _gaugeBar.fillAmount = currentRatio;

        float remainingDuration =
            maxCharge - currentCharge;

        if (remainingDuration <= 0f)
        {
            _gaugeBar.fillAmount = 1f;
            return;
        }

        // 남은 시간 동안 일정한 속도로 1까지 채운다.
        _gaugeTween = _gaugeBar
            .DOFillAmount(1f, remainingDuration)
            .SetEase(Ease.Linear);
    }

    /// <summary>
    /// 적 처치 보너스를 부드럽게 반영한 뒤 기본 충전을 계속한다.
    /// </summary>
    public void AnimateBonus(
        float currentCharge,
        float maxCharge)
    {
        if (maxCharge <= 0f)
        {
            return;
        }

        _gaugeTween?.Kill();

        currentCharge = Mathf.Clamp(
            currentCharge,
            0f,
            maxCharge);

        float targetRatio =
            currentCharge / maxCharge;

        // 보너스 연출 중에도 실제 시간은 흐르므로
        // 전체 완료 시간이 늦어지지 않도록 지속시간을 제한한다.
        float bonusDuration = Mathf.Min(
            _bonusFillDuration,
            maxCharge - currentCharge);

        float remainingDuration = Mathf.Max(
            0f,
            maxCharge - currentCharge - bonusDuration);

        Sequence bonusSequence = DOTween.Sequence();

        // 현재 표시 위치에서 보너스가 적용된 위치까지 빠르게 채운다.
        bonusSequence.Append(
            _gaugeBar
                .DOFillAmount(targetRatio, bonusDuration)
                .SetEase(Ease.OutQuad));

        // 이후 남은 시간 동안 일정한 속도로 충전을 계속한다.
        if (remainingDuration > 0f)
        {
            bonusSequence.Append(
                _gaugeBar
                    .DOFillAmount(1f, remainingDuration)
                    .SetEase(Ease.Linear));
        }

        _gaugeTween = bonusSequence;
    }

    /// <summary>
    /// 실제 게이지가 최대치에 도달했을 때 화면도 1로 맞춘다.
    /// </summary>
    public void CompleteGauge()
    {
        _gaugeTween?.Kill();

        _gaugeTween = _gaugeBar
            .DOFillAmount(1f, _completeDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
    }

    private void OnDisable()
    {
        _gaugeTween?.Kill();
        _gaugeTween = null;
    }
}
using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 현재 플레이어의 체력을 UI에 부드럽게 표시한다.
/// 실제 체력은 즉시 변경하고, 체력바만 DOTween으로 따라간다.
/// </summary>
public class HPUI : MonoBehaviour
{
    [Header("런타임 플레이어")]
    [Tooltip("StageManager가 생성한 현재 PlayerController")]
    [SerializeField] PlayerController _playerController;

    [Header("체력바 이미지")]
    [Tooltip("현재 체력을 fillAmount로 표시할 Image")]
    [SerializeField] Image _hpBar;

    [Header("체력바 연출 설정")]
    [Tooltip("현재 체력에서 변경된 체력까지 이동하는 시간")]
    [Min(0.01f)]
    [SerializeField] float _fillDuration = 0.25f;

    // 현재 재생 중인 체력바 Tween이다.
    Tween _hpTween;

    private void Awake()
    {
        if (_hpBar == null)
        {
            throw new InvalidOperationException(
                $"{name}의 HPUI에 HP Bar가 연결되지 않았습니다.");
        }
    }

    /// <summary>
    /// 생성된 플레이어를 연결하고 최초 체력을 즉시 표시한다.
    /// </summary>
    public void Initialize(PlayerController playerController)
    {
        if (playerController == null)
        {
            throw new ArgumentNullException(
                nameof(playerController));
        }

        // 이번 스테이지에서 생성된 플레이어를 저장한다.
        _playerController = playerController;

        // 이전 캐릭터의 체력바 연출이 남아 있다면 제거한다.
        _hpTween?.Kill();

        // 최초 체력은 연출하지 않고 즉시 표시한다.
        _hpBar.fillAmount = CalculateHpRatio();
    }

    /// <summary>
    /// 현재 플레이어의 체력 비율까지 부드럽게 변경한다.
    /// </summary>
    public void UpdateHpUI()
    {
        // 플레이어가 아직 연결되지 않았다면 체력바를 비운다.
        if (_playerController == null)
        {
            _hpTween?.Kill();
            _hpBar.fillAmount = 0f;
            return;
        }

        // 변경된 실제 체력 비율을 계산한다.
        float targetFillAmount = CalculateHpRatio();

        // 빠르게 연속으로 피해를 받았다면 이전 연출을 중단한다.
        // 현재 화면에 표시된 위치에서 새로운 체력까지 다시 이동한다.
        _hpTween?.Kill();

        // 현재 fillAmount에서 실제 체력 비율까지 부드럽게 이동한다.
        _hpTween = _hpBar
            .DOFillAmount(targetFillAmount, _fillDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
    }

    /// <summary>
    /// 현재 체력을 0~1 사이의 비율로 계산한다.
    /// </summary>
    private float CalculateHpRatio()
    {
        // 최대 체력이 0이면 정상적인 비율을 계산할 수 없다.
        if (_playerController == null ||
            _playerController.MaxHp <= 0)
        {
            return 0f;
        }

        float hpRatio =
            (float)_playerController.CurrentHp /
            _playerController.MaxHp;

        return Mathf.Clamp01(hpRatio);
    }

    private void OnDisable()
    {
        // 비활성화된 UI의 Tween이 계속 실행되지 않도록 제거한다.
        _hpTween?.Kill();
        _hpTween = null;
    }
}
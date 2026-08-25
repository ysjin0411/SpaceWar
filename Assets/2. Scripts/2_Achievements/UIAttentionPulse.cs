using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// CanvasGroup의 투명도를 반복해서 변경하여
/// 사용자에게 확인이 필요한 UI임을 알려준다.
/// </summary>
public class UIAttentionPulse : MonoBehaviour
{
    [Header("점멸 대상")]

    // 투명도를 조절할 테두리 오브젝트의 CanvasGroup이다.
    [SerializeField] CanvasGroup _targetCanvasGroup;

    [Header("점멸 설정")]

    // 테두리가 가장 흐려졌을 때의 투명도다.
    [Range(0f, 1f)]
    [SerializeField] float _minimumAlpha = 0.25f;

    // 테두리가 가장 선명할 때의 투명도다.
    [Range(0f, 1f)]
    [SerializeField] float _maximumAlpha = 1f;

    // 선명한 상태에서 흐린 상태까지 변하는 시간이다.
    [Min(0.01f)]
    [SerializeField] float _pulseDuration = 0.45f;

    // 현재 실행 중인 점멸 Tween이다.
    Tween _pulseTween;

    // 현재 알림 효과가 활성화되어 있는지 저장한다.
    bool _isAttentionActive;

    private void Awake()
    {
        ValidateReferences();

        // 게임 시작 시에는 알림 테두리가 보이지 않게 한다.
        _targetCanvasGroup.alpha = 0f;
        _targetCanvasGroup.interactable = false;
        _targetCanvasGroup.blocksRaycasts = false;
    }

    /// <summary>
    /// 알림이 필요한 상태에 따라 점멸 효과를 켜거나 끈다.
    /// </summary>
    public void SetAttention(bool shouldShow)
    {
        // 이미 같은 상태라면 Tween을 중복 생성하지 않는다.
        if (_isAttentionActive == shouldShow)
        {
            return;
        }

        _isAttentionActive = shouldShow;

        // 기존 점멸 효과가 있다면 먼저 정리한다.
        _pulseTween?.Kill();
        _pulseTween = null;

        if (!shouldShow)
        {
            // 알림이 필요하지 않으면 테두리를 완전히 숨긴다.
            _targetCanvasGroup.alpha = 0f;
            return;
        }

        // 선명한 상태에서 점멸을 시작한다.
        _targetCanvasGroup.alpha = _maximumAlpha;

        _pulseTween = _targetCanvasGroup
            .DOFade(_minimumAlpha, _pulseDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)

            // 메뉴에서 Time.timeScale이 0이어도 점멸하도록 한다.
            .SetUpdate(true);
    }

    private void ValidateReferences()
    {
        if (_targetCanvasGroup == null)
        {
            throw new InvalidOperationException(
                $"{name}에 Target CanvasGroup이 연결되지 않았습니다.");
        }
    }

    private void OnDisable()
    {
        // 부모 창이 닫힐 때 실행 중인 Tween을 정리한다.
        _pulseTween?.Kill();
        _pulseTween = null;
        _isAttentionActive = false;

        if (_targetCanvasGroup != null)
        {
            _targetCanvasGroup.alpha = 0f;
        }
    }
}
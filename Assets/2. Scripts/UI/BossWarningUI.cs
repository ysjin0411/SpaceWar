using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// 보스 경고 페이즈가 시작되면
/// 경고 문구를 깜빡인 후 보스 전투 페이즈로 넘긴다.
/// </summary>
public class BossWarningUI : MonoBehaviour
{
    [Header("스테이지 진행 정보")]
    [Tooltip("현재 스테이지의 진행 상태를 관리하는 StageFlowManager")]
    [SerializeField] StageFlowManager _stageFlowManager;

    [Header("경고 UI")]
    [Tooltip("보스 경고 UI의 투명도를 조절하는 CanvasGroup")]
    [SerializeField] CanvasGroup _warningCanvasGroup;

    [Tooltip("보스 종류에 맞는 경고 문구를 표시할 TMP 텍스트")]
    [SerializeField] TMP_Text _warningText;

    [Header("경고 연출 설정")]
    [Tooltip("한 번 밝아지거나 어두워지는 시간")]
    [Min(0.01f)]
    [SerializeField] float _blinkDuration = 0.25f;

    [Tooltip("경고 문구가 깜빡이는 횟수")]
    [Min(1)]
    [SerializeField] int _blinkCount = 4;

    [Tooltip("연출 시작 시 적용할 작은 크기")]
    [Range(0.1f, 1f)]
    [SerializeField] float _startScale = 0.7f;

    [Tooltip("문구가 원래 크기로 커지는 시간")]
    [Min(0.01f)]
    [SerializeField] float _scaleDuration = 0.3f;

    // 현재 실행 중인 보스 경고 연출
    Sequence _warningTween;

    private void Awake()
    {
        if (_stageFlowManager == null)
        {
            throw new InvalidOperationException(
                $"{name}의 BossWarningUI에 StageFlowManager가 연결되지 않았습니다.");
        }

        if (_warningCanvasGroup == null)
        {
            throw new InvalidOperationException(
                $"{name}의 BossWarningUI에 Warning CanvasGroup이 연결되지 않았습니다.");
        }

        if (_warningText == null)
        {
            throw new InvalidOperationException(
                $"{name}의 BossWarningUI에 Warning Text가 연결되지 않았습니다.");
        }

        // 스테이지 시작 시에는 보스 경고를 보이지 않게 한다.
        _warningCanvasGroup.alpha = 0f;
    }

    private void OnEnable()
    {
        // 스테이지 진행 상태 변경 이벤트를 구독한다.
        if (_stageFlowManager != null)
        {
            _stageFlowManager.PhaseChanged += HandlePhaseChanged;
        }
    }

    private void OnDisable()
    {
        // 오브젝트가 비활성화될 때 이벤트 구독을 해제한다.
        if (_stageFlowManager != null)
        {
            _stageFlowManager.PhaseChanged -= HandlePhaseChanged;
        }

        // 실행 중이던 경고 연출도 제거한다.
        _warningTween?.Kill();
        _warningTween = null;
    }

    /// <summary>
    /// 변경된 스테이지 페이즈가 보스 경고인지 확인한다.
    /// </summary>
    private void HandlePhaseChanged(StagePhase phase)
    {
        switch (phase)
        {
            case StagePhase.MidBossWarning:

                // 중간보스 경고 문구를 표시한다.
                PlayWarning("WARNING\nMID BOSS");
                break;

            case StagePhase.FinalBossWarning:

                // 최종보스 경고 문구를 표시한다.
                PlayWarning("WARNING\nFINAL BOSS");
                break;
        }
    }

    /// <summary>
    /// 전달받은 문구를 확대하면서 여러 번 깜빡인다.
    /// </summary>
    private void PlayWarning(string warningMessage)
    {
        // 이전 경고 연출이 남아 있다면 제거한다.
        _warningTween?.Kill();

        // 현재 보스 종류에 맞는 문구를 적용한다.
        _warningText.text = warningMessage;

        // 투명하고 작은 상태에서 시작한다.
        _warningCanvasGroup.alpha = 0f;
        _warningText.rectTransform.localScale =
            Vector3.one * _startScale;

        _warningTween = DOTween.Sequence();

        // 경고 문구를 원래 크기로 빠르게 확대한다.
        _warningTween.Append(
            _warningText.rectTransform
                .DOScale(1f, _scaleDuration)
                .SetEase(Ease.OutBack));

        // 경고 문구를 여러 번 깜빡인다.
        _warningTween.Append(
            _warningCanvasGroup
                .DOFade(1f, _blinkDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(
                    _blinkCount * 2,
                    LoopType.Yoyo));

        _warningTween.OnComplete(() =>
        {
            // 경고 연출이 끝나면 다시 투명하게 만든다.
            _warningCanvasGroup.alpha = 0f;
            _warningText.rectTransform.localScale = Vector3.one;

            _warningTween = null;

            // StageFlowManager에 경고 연출이 끝났다고 알린다.
            _stageFlowManager.NotifyBossWarningCompleted();
        });
    }

    private void OnDestroy()
    {
        _warningTween?.Kill();
        _warningTween = null;
    }
}
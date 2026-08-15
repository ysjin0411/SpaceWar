using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// StageFlowManager의 현재 진행 시간을 화면의 진행 바로 표현한다.
/// 1구간은 0%에서 50%까지,
/// 2구간은 50%에서 100%까지 진행된다.
/// </summary>
public class StageProgressUI : MonoBehaviour
{
    [Header("스테이지 진행 정보")]
    [Tooltip("현재 스테이지의 진행 상태와 시간을 관리하는 StageFlowManager")]
    [SerializeField] StageFlowManager _stageFlowManager;

    [Header("진행 바")]
    [Tooltip("Fill Amount를 변경할 StageProgressBar Image")]
    [SerializeField] Image _progressFillImage;

    [Header("러너")]
    [Tooltip("진행도에 따라 가로로 이동하는 RunnerAnchor")]
    [SerializeField] RectTransform _runnerAnchor;

    [Tooltip("제자리 달리기 연출을 적용할 RunnerVisual")]
    [SerializeField] RectTransform _runnerVisual;

    [Tooltip("러너가 위아래로 움직이는 거리")]
    [Min(0f)]
    [SerializeField] float _runnerBounceHeight = 8f;

    [Tooltip("러너가 한쪽 방향으로 움직이는 시간")]
    [Min(0.01f)]
    [SerializeField] float _runnerStepDuration = 0.18f;

    [Tooltip("러너가 좌우로 기울어지는 각도")]
    [Range(0f, 30f)]
    [SerializeField] float _runnerTiltAngle = 8f;

    // 러너의 제자리 달리기 연출을 보관한다.
    Sequence _runnerTween;

    // RunnerVisual의 원래 Y 위치다.
    float _runnerOriginalY;

    private void Awake()
    {
        if (_stageFlowManager == null)
        {
            throw new InvalidOperationException(
                $"{name}의 StageProgressUI에 StageFlowManager가 연결되지 않았습니다.");
        }

        if (_progressFillImage == null)
        {
            throw new InvalidOperationException(
                $"{name}의 StageProgressUI에 Progress Fill Image가 연결되지 않았습니다.");
        }

        if (_runnerAnchor == null)
        {
            throw new InvalidOperationException(
                $"{name}의 StageProgressUI에 RunnerAnchor가 연결되지 않았습니다.");
        }

        if (_runnerVisual == null)
        {
            throw new InvalidOperationException(
                $"{name}의 StageProgressUI에 RunnerVisual이 연결되지 않았습니다.");
        }

        // Fill Amount를 사용하려면 Image Type이 Filled여야 한다.
        if (_progressFillImage.type != Image.Type.Filled)
        {
            throw new InvalidOperationException(
                "StageProgressBar의 Image Type을 Filled로 설정해야 합니다.");
        }

        // 인스펙터에서 설정한 RunnerVisual의 원래 Y 위치를 기억한다.
        _runnerOriginalY = _runnerVisual.anchoredPosition.y;
    }

    private void Start()
    {
        // 스테이지 시작 시 진행 바와 러너를 왼쪽 끝에 배치한다.
        ApplyProgress(0f);

        // 러너가 제자리에서 달리는 것처럼 보이는 연출을 시작한다.
        PlayRunnerAnimation();
    }

    private void Update()
    {
        // StageFlowManager의 현재 시간으로 전체 진행도를 계산한다.
        float progress = CalculateProgress();

        // 계산된 진행도를 진행 바와 러너 위치에 적용한다.
        ApplyProgress(progress);
    }

    /// <summary>
    /// 현재 스테이지 페이즈와 구간 시간으로
    /// 0에서 1 사이의 전체 진행도를 계산한다.
    /// </summary>
    private float CalculateProgress()
    {
        switch (_stageFlowManager.CurrentPhase)
        {
            case StagePhase.Section1:
                {
                    // 1구간의 진행 비율을 0에서 1 사이로 계산한다.
                    float sectionProgress = Mathf.Clamp01(
                        _stageFlowManager.PhaseElapsedTime /
                        _stageFlowManager.Section1Duration);

                    // 1구간은 전체 진행 바의 앞쪽 절반을 사용한다.
                    return sectionProgress * 0.5f;
                }

            case StagePhase.MidBossWarning:
            case StagePhase.MidBossBattle:
            case StagePhase.SectionTransition:

                // 중간보스 경고부터 구간 전환이 끝날 때까지
                // 진행 바를 중간보스 위치에서 정지시킨다.
                return 0.5f;

            case StagePhase.Section2:
                {
                    // 2구간의 진행 비율을 0에서 1 사이로 계산한다.
                    float sectionProgress = Mathf.Clamp01(
                        _stageFlowManager.PhaseElapsedTime /
                        _stageFlowManager.Section2Duration);

                    // 2구간은 진행 바의 50%부터 100%까지 사용한다.
                    return 0.5f + sectionProgress * 0.5f;
                }

            case StagePhase.FinalBossWarning:
            case StagePhase.FinalBossBattle:
            case StagePhase.Cleared:

                // 최종보스 경고부터는 진행 바 끝에 고정한다.
                return 1f;

            default:
                return 0f;
        }
    }

    /// <summary>
    /// 진행 바의 채움 정도와 러너의 가로 위치를 함께 변경한다.
    /// </summary>
    private void ApplyProgress(float progress)
    {
        // 예상하지 못한 값이 들어와도 0에서 1 사이로 제한한다.
        progress = Mathf.Clamp01(progress);

        // 진행 바를 왼쪽에서 오른쪽으로 채운다.
        _progressFillImage.fillAmount = progress;

        // RunnerAnchor의 가로 Anchor를 진행도 위치로 이동시킨다.
        Vector2 anchorMin = _runnerAnchor.anchorMin;
        anchorMin.x = progress;
        _runnerAnchor.anchorMin = anchorMin;

        Vector2 anchorMax = _runnerAnchor.anchorMax;
        anchorMax.x = progress;
        _runnerAnchor.anchorMax = anchorMax;

        // Anchor를 기준으로 추가적인 가로 위치가 생기지 않게 한다.
        Vector2 anchoredPosition = _runnerAnchor.anchoredPosition;
        anchoredPosition.x = 0f;
        _runnerAnchor.anchoredPosition = anchoredPosition;
    }

    /// <summary>
    /// RunnerVisual을 위아래로 움직이고 좌우로 기울여
    /// 제자리에서 달리는 것처럼 표현한다.
    /// </summary>
    private void PlayRunnerAnimation()
    {
        // 기존 연출이 남아 있다면 제거한다.
        _runnerTween?.Kill();

        // 처음에는 몸을 왼쪽으로 살짝 기울인다.
        _runnerVisual.localRotation =
            Quaternion.Euler(0f, 0f, -_runnerTiltAngle);

        // 위쪽 이동과 몸 기울이기를 동시에 실행한다.
        _runnerTween = DOTween.Sequence();

        _runnerTween.Append(
            _runnerVisual
                .DOAnchorPosY(
                    _runnerOriginalY + _runnerBounceHeight,
                    _runnerStepDuration)
                .SetEase(Ease.InOutSine));

        _runnerTween.Join(
            _runnerVisual
                .DOLocalRotate(
                    new Vector3(0f, 0f, _runnerTiltAngle),
                    _runnerStepDuration)
                .SetEase(Ease.InOutSine));

        // 위아래 이동을 계속 반복한다.
        _runnerTween.SetLoops(-1, LoopType.Yoyo);
    }

    private void OnDestroy()
    {
        // 씬을 나갈 때 남아 있는 러너 연출을 제거한다.
        _runnerTween?.Kill();
        _runnerTween = null;
    }
}
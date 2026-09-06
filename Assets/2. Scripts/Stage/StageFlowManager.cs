using System;
using TMPro;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// 현재 스테이지의 진행 상태.
/// 이후 중간보스, 구간 전환, 최종보스 흐름을 이 상태로 구분한다.
/// </summary>
public enum StagePhase
{
    None,              // 아직 스테이지 흐름이 시작되지 않은 상태
    StageIntro,        // 플레이어 입장 연출 상태
    Section1,          // 스테이지 1-1 일반 전투
    MidBossWarning,    // 중간보스 출현 경고
    MidBossBattle,     // 중간보스 전투
    SectionTransition, // 첫 번째 맵에서 두 번째 맵으로 이동하는 상태
    Section2,          // 스테이지 1-2 일반 전투
    FinalBossWarning,  // 최종보스 출현 경고
    FinalBossBattle,   // 최종보스 전투
    Cleared,            // 스테이지 클리어
    Failed             // 플레이어 사망으로 스테이지 실패
}

/// <summary>
/// 전체 스테이지 진행과 클리어 시간을 관리한다.
/// </summary>
public class StageFlowManager : MonoBehaviour
{
    [Header("스테이지 시간 UI")]
    [Tooltip("현재 플레이 시간을 표시할 TextMeshPro 텍스트")]
    [SerializeField] TMP_Text _stageTimerText;

    [Header("스테이지 시작 연출")]
    [Tooltip("스테이지 번호를 표시할 텍스트")]
    [SerializeField] TMP_Text _stageIntroText;

    [Tooltip("스테이지 번호의 투명도를 조절할 CanvasGroup")]
    [SerializeField] CanvasGroup _stageIntroCanvasGroup;

    [Tooltip("한 번 밝아지거나 어두워지는 시간")]
    [Min(0.01f)]
    [SerializeField] float _stageIntroFadeDuration = 0.3f;

    [Tooltip("스테이지 문구가 깜빡일 횟수")]
    [Min(1)]
    [SerializeField] int _stageIntroBlinkCount = 3;

    // 현재 재생 중인 스테이지 시작 연출
    Tween _stageIntroTween;

    [Header("스테이지 구간 시간")]
    [Tooltip("스테이지 시작 후 중간보스 경고가 나타날 때까지의 시간")]
    [Min(0.1f)]
    [SerializeField] float _section1Duration = 60f;
    [Tooltip("2구간 시작 후 최종보스 경고가 나타날 때까지의 시간")]
    [Min(0.1f)]
    [SerializeField] float _section2Duration = 60f;

    [Header("스테이지 런타임 데이터")]
    [Tooltip("현재 스테이지의 진행 상태")]
    [SerializeField] StagePhase _currentPhase;

    [Tooltip("스테이지 시작부터 현재까지 흐른 전체 플레이 시간")]
    [SerializeField] float _stageElapsedTime;

    [Tooltip("현재 구간이 시작된 후 흐른 시간")]
    [SerializeField] float _phaseElapsedTime;

    [Tooltip("현재 스테이지 타이머가 작동 중인지 확인")]
    [SerializeField] bool _isTimerRunning;



    // 스테이지 진행 상태가 변경됐을 때 외부 시스템에 알려주는 이벤트
    public event Action<StagePhase> PhaseChanged;

    // 외부에서 현재 진행 상태를 확인할 때 사용한다.
    public StagePhase CurrentPhase => _currentPhase;

    // 최종 클리어 기록 저장에 사용할 전체 시간이다.
    public float StageElapsedTime => _stageElapsedTime;

    // 중간보스와 최종보스 출현 시간을 판단할 때 사용한다.
    public float PhaseElapsedTime => _phaseElapsedTime;

    // 1구간의 전체 진행 시간
    public float Section1Duration => _section1Duration;

    // 2구간의 전체 진행 시간
    public float Section2Duration => _section2Duration;

    private void Awake()
    {
        // 타이머를 표시할 UI가 연결되지 않았다면 정상적으로 실행할 수 없다.
        if (_stageTimerText == null)
        {
            throw new InvalidOperationException(
                $"{name}의 StageFlowManager에 Stage Timer Text가 연결되지 않았습니다.");
        }
        if (_stageIntroText == null)
        {
            throw new InvalidOperationException(
                $"{name}의 StageFlowManager에 Stage Intro Text가 연결되지 않았습니다.");
        }

        if (_stageIntroCanvasGroup == null)
        {
            throw new InvalidOperationException(
                $"{name}의 StageFlowManager에 Stage Intro CanvasGroup이 연결되지 않았습니다.");
        }
    }

    private void Start()
    {
        // 스테이지가 시작되면 1-1 일반 전투 상태로 시작한다.
        BeginStage();
    }

    private void Update()
    {
        // 타이머가 정지된 상태에서는 시간을 증가시키지 않는다.
        if (!_isTimerRunning)
        {
            return;
        }

        // 스테이지 전체 플레이 시간을 증가시킨다.
        _stageElapsedTime += Time.deltaTime;

        // 현재 진행 구간의 시간을 증가시킨다.
        _phaseElapsedTime += Time.deltaTime;

        // 증가한 시간을 화면에 표시한다.
        UpdateTimerText();

        // 현재 구간의 시간이 끝났는지 확인한다.
        UpdateStagePhase();
    }

    /// <summary>
    /// 새로운 스테이지의 시간과 최초 상태를 초기화한다.
    /// 플레이어 입장 연출이 끝나기 전에는 전투 타이머를 시작하지 않는다.
    /// </summary>
    private void BeginStage()
    {
        // 전체 플레이 시간을 초기화한다.
        _stageElapsedTime = 0f;

        // 현재 페이즈의 진행 시간을 초기화한다.
        _phaseElapsedTime = 0f;

        // 최초 상태를 아직 시작되지 않은 상태로 설정한다.
        _currentPhase = StagePhase.None;

        // 플레이어 입장 연출 중에는 클리어 타이머를 흐르게 하지 않는다.
        _isTimerRunning = false;

        // 화면에 초기 시간 00:00을 표시한다.
        UpdateTimerText();

        // 스테이지 번호 연출을 시작한다.
        PlayStageIntro();

        // 플레이어 입장 연출 상태를 시작한다.
        ChangePhase(StagePhase.StageIntro);
    }

    /// <summary>
    /// 현재 일반 전투 구간의 제한 시간이 끝났는지 확인하고
    /// 다음 보스 경고 상태로 변경한다.
    /// </summary>
    private void UpdateStagePhase()
    {
        switch (_currentPhase)
        {
            // 1구간 일반 전투 상태
            case StagePhase.Section1:

                // 설정한 1구간 시간이 끝나지 않았다면 유지한다.
                if (_phaseElapsedTime < _section1Duration)
                {
                    return;
                }

                // 1구간 시간이 끝나면 중간보스 경고 상태로 넘어간다.
                ChangePhase(StagePhase.MidBossWarning);
                break;

            // 2구간 일반 전투 상태
            case StagePhase.Section2:

                // 설정한 2구간 시간이 끝나지 않았다면 유지한다.
                if (_phaseElapsedTime < _section2Duration)
                {
                    return;
                }

                // 2구간 시간이 끝나면 최종보스 경고 상태로 넘어간다.
                ChangePhase(StagePhase.FinalBossWarning);
                break;
        }
    }

    /// <summary>
    /// 진행 상태를 변경하고 연결된 시스템에 알린다.
    /// 종료된 스테이지는 다른 상태로 다시 넘어가지 않는다.
    /// </summary>
    public void ChangePhase(StagePhase nextPhase)
    {
        // 같은 상태의 중복 실행을 막는다.
        if (_currentPhase == nextPhase)
        {
            return;
        }

        // 클리어 또는 실패가 확정되면 이후 보스·연출 알림을 무시한다.
        if (_currentPhase == StagePhase.Cleared ||
            _currentPhase == StagePhase.Failed)
        {
            return;
        }

        _currentPhase = nextPhase;
        _phaseElapsedTime = 0f;

        // 결과창이 시간을 읽기 전에 타이머부터 정지한다.
        if (_currentPhase == StagePhase.Cleared ||
            _currentPhase == StagePhase.Failed)
        {
            _isTimerRunning = false;
        }

        // 스포너와 결과창 등에 상태 변경을 전달한다.
        if (PhaseChanged != null)
        {
            PhaseChanged.Invoke(_currentPhase);
        }

        Debug.Log($"스테이지 진행 상태 변경: {_currentPhase}");
    }

    /// <summary>
    /// 플레이어 입장 연출이 끝났다는 알림을 받는다.
    /// 이 시점부터 전투 타이머와 Section1을 시작한다.
    /// </summary>
    public void NotifyStageIntroCompleted()
    {
        // 입장 상태가 아닌데 호출됐다면 잘못된 흐름이므로 처리하지 않는다.
        if (_currentPhase != StagePhase.StageIntro)
        {
            Debug.LogWarning(
                $"StageIntro 상태가 아니므로 입장 완료를 처리할 수 없습니다: {_currentPhase}");

            return;
        }

        // 플레이어 입장이 끝난 시점부터 클리어 시간을 측정한다.
        _isTimerRunning = true;

        // 첫 번째 일반 전투 구간을 시작한다.
        ChangePhase(StagePhase.Section1);
    }

    /// <summary>
    /// 플레이어 퇴장, 배경 교체, 재입장 연출이 모두 끝났다는 알림을 받는다.
    /// </summary>
    public void NotifySectionTransitionCompleted()
    {
        // 구간 전환 상태가 아니라면 잘못된 호출이므로 처리하지 않는다.
        if (_currentPhase != StagePhase.SectionTransition)
        {
            Debug.LogWarning(
                $"SectionTransition 상태가 아니므로 전환 완료를 처리할 수 없습니다: {_currentPhase}");

            return;
        }

        // 두 번째 일반 전투 구간을 시작한다.
        ChangePhase(StagePhase.Section2);
    }

    /// <summary>
    /// 보스 경고 UI 연출이 끝났을 때 호출한다.
    /// 현재 경고 종류에 맞는 보스 전투 상태로 변경한다.
    /// </summary>
    public void NotifyBossWarningCompleted()
    {
        switch (_currentPhase)
        {
            case StagePhase.MidBossWarning:

                // 중간보스 경고가 끝났으므로 중간보스 전투로 넘어간다.
                ChangePhase(StagePhase.MidBossBattle);
                break;

            case StagePhase.FinalBossWarning:

                // 최종보스 경고가 끝났으므로 최종보스 전투로 넘어간다.
                ChangePhase(StagePhase.FinalBossBattle);
                break;

            default:

                // 보스 경고 상태가 아닐 때 잘못 호출된 경우다.
                Debug.LogWarning(
                    $"현재 상태가 보스 경고가 아니므로 경고 종료를 처리할 수 없습니다: {_currentPhase}");
                break;
        }
    }

    /// <summary>
    /// BossEncounterManager로부터 현재 보스가 처치됐다는 알림을 받는다.
    /// 현재 보스 전투 단계에 따라 다음 스테이지 단계로 전환한다.
    /// </summary>
    public void NotifyBossDefeated()
    {
        switch (_currentPhase)
        {
            case StagePhase.MidBossBattle:

                // 미드보스를 처치했으므로 Section2 전환 구간을 시작한다.
                ChangePhase(StagePhase.SectionTransition);
                break;

            case StagePhase.FinalBossBattle:

                // 최종보스를 처치했으므로 스테이지를 클리어 처리한다.
                CompleteStage();
                break;

            default:

                // 보스 전투가 아닌 상태에서 호출됐다면 잘못된 흐름이므로 경고한다.
                Debug.LogWarning(
                    $"보스 전투 상태가 아니므로 보스 처치를 처리할 수 없습니다: {_currentPhase}");
                break;
        }
    }

    /// <summary>
    /// 최종보스 처치로 스테이지를 완료한다.
    /// </summary>
    public void CompleteStage()
    {
        // 이미 종료된 판은 다시 처리하지 않는다.
        if (_currentPhase == StagePhase.Cleared ||
            _currentPhase == StagePhase.Failed)
        {
            return;
        }

        ChangePhase(StagePhase.Cleared);
        UpdateTimerText();
    }

    /// <summary>
    /// 플레이어 사망으로 스테이지를 실패 처리한다.
    /// </summary>
    public void FailStage()
    {
        // 클리어와 실패 중 먼저 확정된 결과를 유지한다.
        if (_currentPhase == StagePhase.Cleared ||
            _currentPhase == StagePhase.Failed)
        {
            return;
        }

        ChangePhase(StagePhase.Failed);
        UpdateTimerText();
    }

    /// <summary>
    /// 전체 플레이 시간을 분:초 형식으로 표시한다.
    /// </summary>
    private void UpdateTimerText()
    {
        // 소수점 이하를 제외한 전체 초를 구한다.
        int totalSeconds = Mathf.FloorToInt(_stageElapsedTime);

        // 전체 초에서 분을 계산한다.
        int minutes = totalSeconds / 60;

        // 전체 초에서 60초 미만의 나머지를 계산한다.
        int seconds = totalSeconds % 60;

        // 예: 1분 5초를 01:05 형식으로 표시한다.
        _stageTimerText.text = $"{minutes:00}:{seconds:00}";
    }

    /// <summary>
    /// 선택한 스테이지 번호를 여러 번 깜빡인 후 숨긴다.
    /// </summary>
    private void PlayStageIntro()
    {
        // Bootstrap부터 정상적으로 진입했는지 확인한다.
        if (GameSessionManager.Instance == null)
        {
            throw new InvalidOperationException(
                "GameSessionManager가 없습니다. Bootstrap 씬부터 실행해야 합니다.");
        }

        int selectedStageIndex =
            GameSessionManager.Instance.SelectedStageIndex;

        // -1은 스테이지가 선택되지 않은 상태다.
        if (selectedStageIndex < 0)
        {
            throw new InvalidOperationException(
                "선택된 스테이지 인덱스가 없습니다.");
        }

        // 저장된 값은 0번부터 시작하므로 화면 번호는 1을 더한다.
        int stageNumber = selectedStageIndex + 1;

        // 예: 0번 인덱스는 STAGE 1로 표시한다.
        _stageIntroText.text = $"STAGE {stageNumber}";

        // 이전 시작 연출이 남아 있다면 제거한다.
        _stageIntroTween?.Kill();

        // 비활성화된 StageNumText를 화면에 표시한다.
        _stageIntroText.gameObject.SetActive(true);

        // 투명한 상태에서 깜빡임을 시작한다.
        _stageIntroCanvasGroup.alpha = 0f;

        // 0 → 1 → 0을 반복해 깜빡이는 연출을 만든다.
        _stageIntroTween = _stageIntroCanvasGroup
            .DOFade(1f, _stageIntroFadeDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(
                _stageIntroBlinkCount * 2,
                LoopType.Yoyo)
            .OnComplete(() =>
            {
                // 연출이 끝나면 다시 비활성화한다.
                _stageIntroCanvasGroup.alpha = 0f;
                _stageIntroText.gameObject.SetActive(false);
                _stageIntroTween = null;
            });
    }

    private void OnDestroy()
    {
        _stageIntroTween?.Kill();
        _stageIntroTween = null;
    }
}
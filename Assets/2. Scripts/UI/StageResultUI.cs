using System;
using TMPro;
using UnityEngine;
using DG.Tweening;
using UnityEngine.SceneManagement;

/// <summary>
/// 스테이지 클리어 결과를 화면에 표시하고 게임을 정지한다.
/// </summary>
public class StageResultUI : MonoBehaviour
{
    [Header("스테이지 시스템")]

    // 클리어 페이즈와 클리어 시간을 제공하는 관리자
    [SerializeField] StageFlowManager _stageFlowManager;

    // 이번 스테이지의 경험치와 골드 획득량을 제공하는 관리자
    [SerializeField] GameManager _gameManager;

    [Header("결과창")]

    // 클리어 전에는 비활성화하고 클리어 후 활성화할 패널
    [SerializeField] GameObject _resultPanel;

    // 어두운 배경을 제외하고 낙하 애니메이션을 적용할 실제 결과창
    [SerializeField] RectTransform _resultWindow;

    // 결과창이 원래 위치보다 위에서 시작하는 거리
    [Min(0f)]
    [SerializeField] float _dropStartOffsetY = 900f;

    // 결과창이 원래 위치까지 떨어지는 데 걸리는 시간
    [Min(0.01f)]
    [SerializeField] float _dropDuration = 0.65f;

    // 결과창이 최종적으로 도착해야 하는 원래 위치
    Vector2 _resultWindowRestPosition;

    // 현재 재생 중인 결과창 낙하 애니메이션
    Tween _resultWindowTween;

    // 버튼이 여러 번 눌려 씬 이동이 중복 실행되는 것을 방지한다.
    bool _isReturningToStageSelect;

    // 이번 스테이지에서 획득한 경험치를 표시한다.
    [SerializeField] TMP_Text _experienceValueText;

    // 이번 스테이지에서 획득한 골드를 표시한다.
    [SerializeField] TMP_Text _goldValueText;

    // 이번 스테이지의 클리어 시간을 표시한다.
    [SerializeField] TMP_Text _clearTimeValueText;

    // 결과창이 중복으로 열리는 것을 방지한다.
    bool _isResultShown;

    private void Awake()
    {
        if (_stageFlowManager == null)
        {
            throw new InvalidOperationException(
                $"{name}의 StageFlowManager가 연결되지 않았습니다.");
        }

        if (_gameManager == null)
        {
            throw new InvalidOperationException(
                $"{name}의 GameManager가 연결되지 않았습니다.");
        }

        if (_resultPanel == null)
        {
            throw new InvalidOperationException(
                $"{name}의 ResultPanel이 연결되지 않았습니다.");
        }

        if (_experienceValueText == null ||
            _goldValueText == null ||
            _clearTimeValueText == null)
        {
            throw new InvalidOperationException(
                $"{name}의 결과 텍스트가 모두 연결되지 않았습니다.");
        }

        if (_resultWindow == null)
        {
            throw new InvalidOperationException(
                $"{name}에 ResultWindow가 연결되지 않았습니다.");
        }

        // 인스펙터에서 배치한 결과창의 중앙 위치를 낙하 애니메이션 도착점으로 저장한다.
        _resultWindowRestPosition =
            _resultWindow.anchoredPosition;

        // 스테이지 시작 시에는 결과창을 숨긴다.
        _resultPanel.SetActive(false);
        _isResultShown = false;

        // 아직 StageSelect로 이동 중인 상태가 아니다.
        _isReturningToStageSelect = false;
    }

    private void OnEnable()
    {
        // 스테이지 페이즈가 변경될 때 결과창 표시 여부를 확인한다.
        _stageFlowManager.PhaseChanged += HandlePhaseChanged;
    }

    private void OnDisable()
    {
        // 파괴된 UI의 함수가 호출되지 않도록 이벤트 연결을 해제한다.
        if (_stageFlowManager != null)
        {
            _stageFlowManager.PhaseChanged -= HandlePhaseChanged;
        }
    }

    /// <summary>
    /// 스테이지가 Cleared 상태가 되면 결과창을 표시한다.
    /// </summary>
    private void HandlePhaseChanged(StagePhase phase)
    {
        if (phase != StagePhase.Cleared ||
            _isResultShown)
        {
            return;
        }

        ShowResult();
    }

    /// <summary>
    /// 획득량과 클리어 시간을 표시하고 게임을 일시정지한다.
    /// </summary>
    private void ShowResult()
    {
        _isResultShown = true;

        // 세이브 시스템이 준비되지 않았다면 기록을 저장할 수 없으므로 오류를 발생시킨다.
        if (SaveManager.Instance == null)
        {
            throw new InvalidOperationException(
                "SaveManager가 존재하지 않습니다.");
        }

        // 현재 플레이 중인 스테이지 번호를 가져오기 위해 세션 매니저를 확인한다.
        if (GameSessionManager.Instance == null)
        {
            throw new InvalidOperationException(
                "GameSessionManager가 존재하지 않습니다.");
        }

        // StageSelect에서 선택했던 현재 스테이지 인덱스를 가져온다.
        int selectedStageIndex =
            GameSessionManager.Instance.SelectedStageIndex;

        // 선택된 스테이지가 없는 상태라면 잘못된 게임 진입 흐름이다.
        if (selectedStageIndex < 0)
        {
            throw new InvalidOperationException(
                "선택된 스테이지 인덱스가 없습니다.");
        }

        // 결과 표시와 기록 저장에서 동일한 클리어 타임을 사용한다.
        float clearTime =
            _stageFlowManager.StageElapsedTime;

        // 기존 기록보다 짧거나 첫 클리어인 경우에만 최단 기록을 저장한다.
        SaveManager.Instance.TryUpdateBestClearTime(
            selectedStageIndex,
            clearTime);

        // 새로 저장된 최단 기록을 기준으로
        // 스테이지 클리어 시간 업적을 즉시 검사한다.
        if (AchievementManager.Instance == null)
        {
            throw new InvalidOperationException(
                "AchievementManager가 존재하지 않습니다.");
        }

        AchievementManager.Instance.EvaluateAllAchievements();

        // 이번 스테이지에서 획득한 경험치와 골드를 표시한다.
        _experienceValueText.text =
            _gameManager.StageEarnedExperience.ToString("N0");

        _goldValueText.text =
            _gameManager.StageEarnedGold.ToString("N0");

        // 위에서 저장에 사용한 것과 동일한 클리어 타임을 화면 표시용 초로 변환한다.
        int totalSeconds =
            Mathf.FloorToInt(clearTime);

        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        _clearTimeValueText.text =
            $"{minutes:00}:{seconds:00}";

        // 이전에 재생 중이던 결과창 애니메이션이 있다면 종료한다.
        _resultWindowTween?.Kill();

        // 화면이 켜지기 전에 결과창을 위쪽 시작 위치로 옮긴다.
        // ResultPanel 전체가 아니라 ResultWindow만 이동하므로 어두운 배경은 고정된다.
        _resultWindow.anchoredPosition =
            _resultWindowRestPosition +
            Vector2.up * _dropStartOffsetY;

        // 어두운 배경과 위쪽에 배치된 결과창을 활성화한다.
        _resultPanel.SetActive(true);

        // 결과 화면이 열린 동안 실제 게임 진행을 정지한다.
        Time.timeScale = 0f;

        // 결과창을 위쪽에서 원래 위치까지 떨어뜨린다.
        _resultWindowTween = _resultWindow
            .DOAnchorPos(
                _resultWindowRestPosition,
                _dropDuration)
            // 도착 지점에서 약간 튕기며 착지하는 느낌을 만든다.
            .SetEase(Ease.OutBounce)
            // Time.timeScale이 0이어도 UI 애니메이션이 재생되도록 한다.
            .SetUpdate(true)
            .OnComplete(() =>
            {
                // 종료된 Tween 참조를 정리한다.
                _resultWindowTween = null;
            });
    }

    /// <summary>
    /// 결과창에서 StageSelect 씬으로 돌아간다.
    /// BackToStageSelectButton의 OnClick에 연결한다.
    /// </summary>
    public void BackToStageSelect()
    {
        // 빠른 연속 클릭으로 씬 이동이 중복 실행되지 않도록 한다.
        if (_isReturningToStageSelect)
        {
            return;
        }

        _isReturningToStageSelect = true;

        // 씬을 이동하기 전에 진행 중인 결과창 애니메이션을 종료한다.
        _resultWindowTween?.Kill();
        _resultWindowTween = null;

        // 다음 씬까지 정지 상태가 이어지지 않도록 게임 시간을 복구한다.
        Time.timeScale = 1f;

        // 현재 스테이지와 캐릭터 선택 정보를 초기화한다.
        // 최단 기록은 SaveManager에 저장되어 있으므로 삭제되지 않는다.
        if (GameSessionManager.Instance != null)
        {
            GameSessionManager.Instance.ResetSelection();
        }

        // 스테이지 선택 화면으로 돌아간다.
        SceneManager.LoadScene("StageSelect");
    }

    private void OnDestroy()
    {
        // 씬이 종료될 때 살아 있는 Tween이 남지 않도록 정리한다.
        _resultWindowTween?.Kill();
        _resultWindowTween = null;
    }
}
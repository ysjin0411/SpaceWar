using System;
using TMPro;
using UnityEngine;
using DG.Tweening;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

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
    bool _isChangingScene;

    // 이번 스테이지에서 획득한 경험치를 표시한다.
    [SerializeField] TMP_Text _experienceValueText;

    // 이번 스테이지에서 획득한 골드를 표시한다.
    [SerializeField] TMP_Text _goldValueText;

    // 이번 스테이지의 클리어 시간을 표시한다.
    [SerializeField] TMP_Text _clearTimeValueText;

    // 성공·실패에 맞춰 문구를 바꿀 결과창 상단 텍스트
    [SerializeField] TMP_Text _resultTitleText;

    // 실패 시 숨길 Clear Time 설명 텍스트
    [SerializeField] TMP_Text _clearTimeLabelText;

    // 실패했을 때만 표시할 Restart 버튼 오브젝트
    [SerializeField] GameObject _restartButton;

    // 결과창에서 첫 방향키 입력으로 선택할 StageSelect 버튼
    [SerializeField] Button _stageSelectButton;

    // 결과창이 중복으로 열리는 것을 방지한다.
    bool _isResultShown;

    private void Awake()
    {
        // 키보드로 처음 선택할 버튼이 연결되어 있어야 한다.
        if (_stageSelectButton == null)
        {
            throw new InvalidOperationException(
                $"{name}에 StageSelect Button이 연결되지 않았습니다.");
        }

        // 성공·실패에 따라 변경할 UI 연결을 확인한다.
        if (_resultTitleText == null ||
            _clearTimeLabelText == null ||
            _restartButton == null)
        {
            throw new InvalidOperationException(
                $"{name}의 결과 제목, 시간 라벨, Restart 버튼 연결을 확인해주세요.");
        }

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
        _isChangingScene = false;
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
    /// 결과창에서 선택된 버튼이 없으면 첫 방향키 입력으로
    /// StageSelect 버튼을 선택한다.
    /// 이후 이동과 확정은 EventSystem에 맡긴다.
    /// </summary>
    private void LateUpdate()
    {
        // 결과창이 열려 있고 씬 이동을 시작하지 않은 상태에서만 처리한다.
        if (!_isResultShown ||
            _isChangingScene ||
            !_resultPanel.activeInHierarchy ||
            EventSystem.current == null)
        {
            return;
        }

        GameObject selected =
            EventSystem.current.currentSelectedGameObject;

        // 결과창 내부의 유효한 버튼이 이미 선택되어 있으면 유지한다.
        // 따라서 매번 StageSelect 버튼으로 선택이 되돌아가지 않는다.
        if (selected != null &&
            selected.transform.IsChildOf(_resultPanel.transform))
        {
            Button selectedButton = selected.GetComponent<Button>();

            if (selectedButton != null &&
                selectedButton.IsActive() &&
                selectedButton.IsInteractable())
            {
                return;
            }
        }

        // 위·아래·왼쪽·오른쪽 중 하나를 처음 누르면 기본 버튼을 선택한다.
        bool arrowPressed =
            Input.GetKeyDown(KeyCode.UpArrow) ||
            Input.GetKeyDown(KeyCode.DownArrow) ||
            Input.GetKeyDown(KeyCode.LeftArrow) ||
            Input.GetKeyDown(KeyCode.RightArrow);

        if (!arrowPressed)
        {
            return;
        }

        // 클릭할 수 있는 상태의 버튼만 선택한다.
        if (!_stageSelectButton.IsActive() ||
            !_stageSelectButton.IsInteractable())
        {
            return;
        }

        // EventSystem의 방향키 처리가 끝난 뒤 기본 선택을 설정한다.
        // 첫 입력에는 이 버튼을 선택하고, 다음 입력부터 Navigation을 따른다.
        _stageSelectButton.Select();
    }

    /// <summary>
    /// 클리어 또는 실패가 확정되면 결과창을 한 번 표시한다.
    /// </summary>
    private void HandlePhaseChanged(StagePhase phase)
    {
        if (_isResultShown)
        {
            return;
        }

        if (phase != StagePhase.Cleared &&
            phase != StagePhase.Failed)
        {
            return;
        }

        ShowResult(phase == StagePhase.Cleared);
    }

    /// <summary>
    /// 결과 종류에 맞는 문구와 획득량을 표시한다.
    /// 클리어 기록은 성공했을 때만 저장한다.
    /// </summary>
    private void ShowResult(bool isCleared)
    {
        _isResultShown = true;

        // 결과창이 열린 동안 전투를 정지한다.
        Time.timeScale = 0f;

        float clearTime = _stageFlowManager.StageElapsedTime;

        // 실패한 판의 시간은 클리어 기록에 저장하지 않는다.
        if (isCleared)
        {
            if (SaveManager.Instance == null ||
                GameSessionManager.Instance == null ||
                AchievementManager.Instance == null)
            {
                throw new InvalidOperationException(
                    "클리어 기록 저장에 필요한 매니저가 없습니다.");
            }

            int selectedStageIndex =
                GameSessionManager.Instance.SelectedStageIndex;

            if (selectedStageIndex < 0)
            {
                throw new InvalidOperationException(
                    "선택된 스테이지 인덱스가 없습니다.");
            }

            // 기존 최단 클리어 기록 저장을 유지한다.
            SaveManager.Instance.TryUpdateBestClearTime(
                selectedStageIndex,
                clearTime);

            // 갱신된 클리어 기록에 따른 업적을 검사한다.
            AchievementManager.Instance.EvaluateAllAchievements();
        }

        // 같은 결과창에서 제목만 변경한다.
        _resultTitleText.text =
            isCleared ? "Stage Clear" : "Stage Failed";

        // 실제로 지급된 이번 판의 획득량만 표시한다.
        // 여기서 경험치나 골드를 다시 지급하지 않는다.
        _experienceValueText.text =
            _gameManager.StageEarnedExperience.ToString("N0");

        _goldValueText.text =
            _gameManager.StageEarnedGold.ToString("N0");

        // 실패한 판에서는 클리어 시간 항목을 숨긴다.
        _clearTimeLabelText.gameObject.SetActive(isCleared);
        _clearTimeValueText.gameObject.SetActive(isCleared);

        if (isCleared)
        {
            int totalSeconds = Mathf.FloorToInt(clearTime);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;

            _clearTimeValueText.text =
                $"{minutes:00}:{seconds:00}";
        }

        // Restart는 실패 결과에서만 표시한다.
        _restartButton.SetActive(!isCleared);

        // 이전 결과창 연출이 남아 있다면 정리한다.
        if (_resultWindowTween != null)
        {
            _resultWindowTween.Kill();
            _resultWindowTween = null;
        }

        // 기존처럼 화면 위에서 결과창이 내려오게 한다.
        _resultWindow.anchoredPosition =
            _resultWindowRestPosition +
            Vector2.up * _dropStartOffsetY;

        _resultPanel.SetActive(true);

        // 같은 Canvas의 일반 HUD보다 앞에 표시한다.
        _resultPanel.transform.SetAsLastSibling();

        // 이전에 선택했던 HUD 버튼의 선택을 해제한다.
        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            UnityEngine.EventSystems.EventSystem.current
                .SetSelectedGameObject(null);
        }

        _resultWindowTween = _resultWindow
            .DOAnchorPos(
                _resultWindowRestPosition,
                _dropDuration)
            .SetEase(Ease.OutBounce)
            // 게임이 정지해도 결과창 연출은 진행한다.
            .SetUpdate(true)
            .OnComplete(() =>
            {
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
        if (_isChangingScene)
        {
            return;
        }

        _isChangingScene = true;

        // 남아 있는 결과창 연출을 정리한다.
        if (_resultWindowTween != null)
        {
            _resultWindowTween.Kill();
        }
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

    /// <summary>
    /// 캐릭터와 스테이지 선택을 유지한 채 현재 판을 다시 시작한다.
    /// </summary>
    public void RestartStage()
    {
        // 결과가 표시되지 않았거나 이미 이동 중이면 무시한다.
        if (!_isResultShown || _isChangingScene)
        {
            return;
        }

        _isChangingScene = true;

        // 현재 결과창 연출을 정리한다.
        if (_resultWindowTween != null)
        {
            _resultWindowTween.Kill();
            _resultWindowTween = null;
        }

        // 다음 판이 정지 상태로 시작하지 않게 한다.
        Time.timeScale = 1f;

        // ResetSelection은 호출하지 않는다.
        // 현재 선택한 캐릭터와 스테이지 정보가 재시작에 필요하다.
        string scenePath = gameObject.scene.path;
        SceneManager.LoadScene(scenePath);
    }

    private void OnDestroy()
    {
        // 남아 있는 결과창 연출을 정리한다.
        if (_resultWindowTween != null)
        {
            _resultWindowTween.Kill();
        }
        _resultWindowTween = null;
    }
}
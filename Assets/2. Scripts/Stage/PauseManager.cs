using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Stage에서 Esc 일시정지와 PausePanel 버튼 입력을 관리한다.
/// </summary>
public class PauseManager : MonoBehaviour
{
    [Header("일시정지 UI")]

    [Tooltip("Esc를 눌렀을 때 열고 닫을 PausePanel")]
    [SerializeField]
    private GameObject _pausePanel;

    // Stage의 환경설정 상태를 확인하고 표시 변경 이벤트를 구독한다.
    [SerializeField]
    private AudioSettingsUI _audioSettingsUI;


    [Header("일시정지 버튼")]

    [Tooltip("게임으로 돌아가는 맨 위 버튼")]
    [SerializeField]
    private Button _backGameButton;

    [Tooltip("현재 스테이지를 처음부터 다시 시작하는 버튼")]
    [SerializeField]
    private Button _restartStageButton;

    [Tooltip("StageSelect 씬으로 돌아가는 버튼")]
    [SerializeField]
    private Button _backToStageSelectButton;


    // 이 관리자가 게임을 일시정지했는지 나타낸다.
    // 환경설정을 표시하는 동안에도 true를 유지한다.
    private bool _isPauseOpen;


    /// <summary>
    /// 필수 연결을 검사하고 환경설정 이벤트와 초기 상태를 준비한다.
    /// </summary>
    private void Awake()
    {
        // Inspector 연결을 확인한다.
        if (_pausePanel == null)
        {
            throw new InvalidOperationException(
                "PauseManager에 PausePanel이 연결되지 않았습니다.");
        }

        if (_backGameButton == null)
        {
            throw new InvalidOperationException(
                "PauseManager에 BackGameButton이 연결되지 않았습니다.");
        }

        if (_restartStageButton == null)
        {
            throw new InvalidOperationException(
                "PauseManager에 RestartStageButton이 연결되지 않았습니다.");
        }

        if (_backToStageSelectButton == null)
        {
            throw new InvalidOperationException(
                "PauseManager에 BackToStageSelectButton이 연결되지 않았습니다.");
        }

        // 환경설정 연결 누락을 시작 시점에 확인한다.
        if (_audioSettingsUI == null)
        {
            throw new InvalidOperationException(
                "PauseManager에 AudioSettingsUI가 연결되지 않았습니다.");
        }

        // 환경설정 표시 상태에 맞춰 PausePanel을 전환한다.
        _audioSettingsUI.VisibilityChanged += HandleSettingsVisibilityChanged;

        // 스테이지 시작 시 일시정지 패널을 닫아둔다.
        _isPauseOpen = false;
        _pausePanel.SetActive(false);
    }


    /// <summary>
    /// 환경설정 닫기를 우선 처리하고 Pause 전환과 버튼 선택을 처리한다.
    /// </summary>
    private void Update()
    {
        // 환경설정이 열려 있으면 해당 화면의 닫기 입력만 처리한다.
        if (_audioSettingsUI.IsOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                // 닫힘 이벤트가 PausePanel을 복원한다.
                _audioSettingsUI.CloseFromButton();
            }

            // 같은 ESC가 게임 재개까지 이어지거나 숨긴 버튼이 선택되지 않게 한다.
            return;
        }

        // Esc를 누르면 일시정지 상태를 전환한다.
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (_isPauseOpen)
            {
                ResumeGame();
            }
            else
            {
                OpenPause();
            }
        }

        // PausePanel이 닫혀 있으면 버튼 선택 입력을 처리하지 않는다.
        if (!_isPauseOpen)
        {
            return;
        }

        // EventSystem이 없다면 UI 버튼을 선택할 수 없다.
        if (EventSystem.current == null)
        {
            return;
        }

        // 이미 선택된 버튼이 있다면 기본 선택을 다시 하지 않는다.
        if (EventSystem.current.currentSelectedGameObject != null)
        {
            return;
        }

        // 선택된 버튼이 없는 상태에서 첫 방향키 입력을 받으면
        // 가장 위에 있는 BackGameButton을 선택한다.
        if (IsAnyArrowKeyDown())
        {
            _backGameButton.Select();
        }
    }


    /// <summary>
    /// 게임 정지를 유지하며 환경설정 상태에 맞춰 PausePanel을 전환한다.
    /// </summary>
    private void HandleSettingsVisibilityChanged(bool isOpen)
    {
        // 이 관리자가 정지한 상태에서만 PausePanel을 관리한다.
        if (!_isPauseOpen)
        {
            return;
        }

        // 이전 화면에 남아 있는 버튼 선택을 해제한다.
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        // 시간 배율은 유지하고 패널의 표시만 바꾼다.
        _pausePanel.SetActive(!isOpen);
    }

    /// <summary>
    /// 객체가 제거될 때 환경설정 이벤트 구독을 해제한다.
    /// </summary>
    private void OnDestroy()
    {
        // 환경설정 UI가 남아 있으면 등록했던 콜백을 제거한다.
        if (_audioSettingsUI != null)
        {
            _audioSettingsUI.VisibilityChanged -= HandleSettingsVisibilityChanged;
        }
    }

    /// <summary>
    /// 방향키 중 하나가 이번 프레임에 눌렸는지 확인한다.
    /// </summary>
    private bool IsAnyArrowKeyDown()
    {
        return Input.GetKeyDown(KeyCode.UpArrow)
            || Input.GetKeyDown(KeyCode.DownArrow)
            || Input.GetKeyDown(KeyCode.LeftArrow)
            || Input.GetKeyDown(KeyCode.RightArrow);
    }


    /// <summary>
    /// 일시정지 패널을 연다.
    /// </summary>
    private void OpenPause()
    {
        // 증강 선택이나 사망 등 다른 시스템이 이미 게임을
        // 정지한 상태라면 PausePanel을 열지 않는다.
        if (Time.timeScale <= 0f)
        {
            return;
        }

        _isPauseOpen = true;

        // PausePanel을 화면에 표시한다.
        _pausePanel.SetActive(true);

        // 게임 시간을 멈춘다.
        Time.timeScale = 0f;
        GameAudio.Play(GameSound.PauseOpen);

        // 처음부터 버튼을 선택하지 않고,
        // 방향키를 처음 눌렀을 때 맨 위 버튼을 선택하게 한다.
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }


    /// <summary>
    /// 일시정지를 해제하고 게임으로 돌아간다.
    /// BackGameButton의 OnClick에도 연결한다.
    /// </summary>
    public void ResumeGame()
    {
        if (!_isPauseOpen)
        {
            return;
        }

        _isPauseOpen = false;
        GameAudio.Play(GameSound.PauseResume);

        // PausePanel을 닫는다.
        _pausePanel.SetActive(false);

        // 게임 시간을 정상화한다.
        Time.timeScale = 1f;

        // PausePanel에서 선택했던 버튼 선택을 해제한다.
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }


    /// <summary>
    /// 현재 플레이 중인 Stage를 처음부터 다시 불러온다.
    /// RestartStageButton의 OnClick에 연결한다.
    /// </summary>
    public void RestartStage()
    {
        GameAudio.Play(GameSound.UiConfirm);
        // 씬을 다시 불러와도 정지 상태가 남지 않도록 먼저 정상화한다.
        Time.timeScale = 1f;

        // 현재 활성화된 Stage 씬 이름을 가져온다.
        string currentSceneName =
            SceneManager.GetActiveScene().name;

        // 현재 Stage를 다시 불러온다.
        SceneManager.LoadScene(currentSceneName);
    }


    /// <summary>
    /// 현재 전투를 종료하고 StageSelect 씬으로 돌아간다.
    /// BackToStageSelectButton의 OnClick에 연결한다.
    /// </summary>
    public void BackToTown()
    {
        GameAudio.Play(GameSound.UiBack);
        // 다음 씬에서도 시간이 멈춰 있지 않도록 정상화한다.
        Time.timeScale = 1f;

        // 현재 전투에서 사용한 임시 선택 정보를 초기화한다.
        if (GameSessionManager.Instance != null)
        {
            GameSessionManager.Instance.ResetSelection();
        }

        // 전투를 종료하고 NPC 서비스를 이용하는 Town으로 돌아간다.
        SceneManager.LoadScene("Town");
    }
}

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


    // 현재 PausePanel이 열려 있는지 나타낸다.
    private bool _isPauseOpen;


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

        // 스테이지 시작 시 일시정지 패널을 닫아둔다.
        _isPauseOpen = false;
        _pausePanel.SetActive(false);
    }


    private void Update()
    {
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
    public void BackToStageSelect()
    {
        // 다음 씬에서도 시간이 멈춰 있지 않도록 정상화한다.
        Time.timeScale = 1f;

        // 현재 전투에서 사용한 임시 선택 정보를 초기화한다.
        if (GameSessionManager.Instance != null)
        {
            GameSessionManager.Instance.ResetSelection();
        }

        // 스테이지 선택 화면으로 돌아간다.
        SceneManager.LoadScene("StageSelect");
    }
}
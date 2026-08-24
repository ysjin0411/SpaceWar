using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

public class StageSelectManager : MonoBehaviour
{
    [Header("셀렉")]
    [SerializeField] GameObject[] _selectImage;     // 선택됐을때 보여질 화면
    [SerializeField] GameObject[] _selectButton;    // 선택됐을때 보여질 버튼
    [Header("스테이지 클리어 기록 UI")]
    // 배열 순서가 스테이지 인덱스와 일치하는 클리어 타임 텍스트 목록
    [SerializeField] TMP_Text[] _clearTimeTexts;
    [SerializeField] TMP_Text[] _selectedClearTimeTexts;  // Select 상태의 클리어 타임

    private int _currentStageIndex = -1;            // 현재 선택중인 스테이지 인덱스

    private void Start()
    {
        // Bootstrap에서 SaveManager가 정상적으로 생성됐는지 확인한다.
        if (SaveManager.Instance == null)
        {
            throw new InvalidOperationException(
                "SaveManager가 존재하지 않습니다.");
        }

        // 스테이지별 최단 클리어 기록을 화면에 표시한다.
        RefreshClearTimeTexts();
    }

    private void LateUpdate()
    {
        // 현재 선택된 UI가 없을 때만 방향키 입력을 검사한다.
        if (EventSystem.current.currentSelectedGameObject == null)
        {
            if (IsAnyArrowKeyDown())    // 방향키 입력받아오면 bool값이 켜짐.
            {
                // 아무키 입력시 stage1 활성화
                SelectStage1();
            }
        }
    }

    /// <summary>
    /// 스테이지1번 선택중
    /// </summary>
    private void SelectStage1()
    {
        // 현재 선택중인거 받아오기
        EventSystem.current.SetSelectedGameObject(_selectButton[0]);

        // 0번 스테이지 활성화
        SelectingStage(0);
    }

    /// <summary>
	/// 아무키 입력시 활성화
	/// </summary>
	/// <returns></returns>
	private bool IsAnyArrowKeyDown()
    {
        return Input.GetKeyDown(KeyCode.UpArrow)
            || Input.GetKeyDown(KeyCode.DownArrow)
            || Input.GetKeyDown(KeyCode.LeftArrow)
            || Input.GetKeyDown(KeyCode.RightArrow);
    }

    /// <summary>
    /// 현재 스테이지를 고르는중일때
    /// </summary>
    /// <param name="index">고르고 있는 스테이지 번호</param>
    public void SelectingStage(int index)
    {
        // 인덱스가 정상범위 아닐때 함수종료
        if (index < 0 || index >= _selectImage.Length) return;

        // 이미 선택 중인 스테이지면 다시 처리하지 않는다.
        if (_currentStageIndex == index) return;

        // 이전 선택 끄기(아직 이전 인덱스 적용중)
        UnSelectedStage(_currentStageIndex);

        // 새로운 선택 표시 켜기(새로운 인덱스 적용)
        _currentStageIndex = index;

        // 새로운 인덱스의 이미지 켜기
        _selectImage[_currentStageIndex].SetActive(true);
    }

    /// <summary>
    /// 이전 선택 끄기 
    /// </summary>
    /// <param name="index"></param>
    public void UnSelectedStage(int index)
    {
        // 최초 선택 때는 index가 -1이므로 검사 필요 + 인덱스 범위 벗어날시 리턴
        if (index < 0 || index >= _selectImage.Length) return;

        // 이전 선택 이미지 끄기
        _selectImage[index].SetActive(false);
    }

    public void LoadPlayCharacterSelect()
    {
        // 아직 스테이지를 선택하지 않았다면 이동하지 않는다.
        if (_currentStageIndex < 0)
        {
            return;
        }

        // 
        GameSessionManager session = GameSessionManager.Instance;

        // 선택한 스테이지 인덱스를 세션에 저장한다.
        session.SelectStage(_currentStageIndex);

        // 전투에 입장할 캐릭터를 고르는 과정임을 세션에 저장한다.
        session.BeginCharacterSelection(CharacterSelectPurpose.Battle);

        // 캐릭터 선택 화면으로 이동한다.
        SceneManager.LoadScene("CharacterSelect_play");
    }

    /// <summary>
    /// 스테이지 선택을 취소하고 메인 메뉴로 돌아간다.
    /// </summary>
    public void BackToMainMenu()
    {
        // Bootstrap을 거치지 않았다면 세션 상태를 정리할 수 없다.
        if (GameSessionManager.Instance == null)
        {
            throw new InvalidOperationException(
                "GameSessionManager가 존재하지 않습니다.");
        }

        // 전투 진입 과정에서 선택했던 스테이지와 캐릭터 정보를 초기화한다.
        GameSessionManager.Instance.ResetSelection();

        // 메인 메뉴로 돌아간다.
        SceneManager.LoadScene("1.MainMenu");
    }

    /// <summary>
    /// 저장된 최고 클리어 타임을 Unselect/Select 상태 양쪽에 표시한다.
    /// 비활성화된 SelectImage의 TMP_Text도 값 설정은 가능하다.
    /// </summary>
    private void RefreshClearTimeTexts()
    {
        // 두 배열은 스테이지 개수만큼 동일하게 구성되어야 한다.
        if (_clearTimeTexts == null ||
            _selectedClearTimeTexts == null ||
            _clearTimeTexts.Length == 0 ||
            _clearTimeTexts.Length != _selectedClearTimeTexts.Length)
        {
            throw new InvalidOperationException(
                "Unselect/Select Clear Time Text 배열의 개수가 올바르지 않습니다.");
        }

        for (int stageIndex = 0;
             stageIndex < _clearTimeTexts.Length;
             stageIndex++)
        {
            // 어느 한쪽이라도 빠졌으면 잘못 연결된 스테이지를 바로 확인한다.
            if (_clearTimeTexts[stageIndex] == null ||
                _selectedClearTimeTexts[stageIndex] == null)
            {
                throw new InvalidOperationException(
                    $"Stage {stageIndex + 1}의 Clear Time Text가 연결되지 않았습니다.");
            }

            float bestClearTime =
                SaveManager.Instance.GetBestClearTime(stageIndex);

            // 같은 저장값을 두 UI 상태에 동시에 적용한다.
            string formattedClearTime =
                FormatBestClearTime(bestClearTime);

            _clearTimeTexts[stageIndex].text =
                formattedClearTime;

            _selectedClearTimeTexts[stageIndex].text =
                formattedClearTime;
        }
    }

    /// <summary>
    /// 저장된 초 단위 기록을 스테이지 선택 화면용 문자열로 변환한다.
    /// </summary>
    private string FormatBestClearTime(float bestClearTime)
    {
        // 0 이하의 값은 아직 클리어 기록이 없다는 의미다.
        if (bestClearTime <= 0f)
        {
            return "ClearTime:-- : --";
        }

        // 소수점 이하는 버리고 전체 초로 변환한다.
        int totalSeconds =
            Mathf.FloorToInt(bestClearTime);

        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        // 예: 1분 5초를 ClearTime:01 : 05로 표시한다.
        return $"ClearTime:{minutes:00} : {seconds:00}";
    }
}

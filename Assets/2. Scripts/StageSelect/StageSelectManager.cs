using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class StageSelectManager : MonoBehaviour
{
    [Header("셀렉")]
    [SerializeField] GameObject[] _selectImage;     // 선택됐을때 보여질 화면
    [SerializeField] GameObject[] _selectButton;    // 선택됐을때 보여질 버튼

    [Header("입력")]
    [SerializeField] private InputActionReference _submitAction;

    private int _currentStageIndex = -1;            // 현재 선택중인 스테이지 인덱스

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

    /// <summary>
    /// 캐릭터 선택 씬 로드
    /// </summary>
    public void LoadCharacterSelect()
    {
        // 이상한 stage index 쳐내기
        if (_currentStageIndex < 0) return;

        // 세션매니저 자체에서 선택된 스테이지 인덱스 저장
        GameSessionManager.Instance.SelectStage(_currentStageIndex);

        SceneManager.LoadScene($"CharacterSelect");
    }

}

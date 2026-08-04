using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CharacterSelectManager : MonoBehaviour
{
    [Header("버튼")]
    [SerializeField] Button _soldierButton;      // 솔져 버튼
    [SerializeField] Button _vampireButton;     // 뱀파이어 버튼

    [Header("셀렉")]
    [SerializeField] GameObject _soldierSelectImage;     // 캐릭터1 선택됐을때 보여질 화면
    [SerializeField] GameObject _vampireSelectImage;    // 캐릭터2 선택됐을때 보여질 화면



    private int _currentCharacterIndex = -1;        // 지금 현재 캐릭터 번호

    private void LateUpdate()
    {
        // 현재 선택된 UI가 없을 때만 방향키 입력을 검사한다.
        if (EventSystem.current.currentSelectedGameObject == null)
        {
            if (IsAnyArrowKeyDown())    // 방향키 입력받아오면 bool값이 켜짐.
            {
                // 아무키 입력시 첫번째 캐릭터 활성화
                SelectCharacter1();
            }
        }

        UpdateSelectEffects();
    }

    /// <summary>
    /// 1번 캐릭터 고르기
    /// </summary>
    private void SelectCharacter1()
    {
        _soldierButton.Select();
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
    /// 현재 캐릭터 번호 저장
    /// </summary>
    /// <param name="index"></param>
    public void SelectingCharacter(int index)
    {
        _currentCharacterIndex = index;
    }

    /// <summary>
    /// 셀렉이펙트 관리
    /// </summary>
    private void UpdateSelectEffects()
    {
        // selectedObject에 현재 선택중인 게임오브젝트를 저장
        GameObject selectedObject = EventSystem.current.currentSelectedGameObject;

        // 해당 이펙트는 현재선택중인 오브젝트일때 SetActive
        _soldierSelectImage.gameObject.SetActive(selectedObject == _soldierButton.gameObject);
        _vampireSelectImage.gameObject.SetActive(selectedObject == _vampireButton.gameObject);
    }

    public void LoadPlay()
    {
        // 이상한 캐릭터 번호 쳐내기
        if (_currentCharacterIndex < 0) return;

        // 세션매니저에 선택한 캐릭터 번호 저장
        GameSessionManager.Instance.SelectCharacter(_currentCharacterIndex);

        SceneManager.LoadScene("Play");
    }
}
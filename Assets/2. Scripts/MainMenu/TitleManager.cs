using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleManager : MonoBehaviour
{
    [Header("버튼")]

    // 아무 UI도 선택하지 않은 상태에서 방향키를 누르면 선택할 버튼.
    [SerializeField] Button _playButton;

    private void LateUpdate()
    {
        // 선택 연출은 각 버튼의 UIButtonSelectionEffect가 처리한다.
        if (EventSystem.current == null)
        {
            return;
        }

        // 아직 선택한 UI가 없을 때 방향키로 플레이 버튼을 선택한다.
        if (EventSystem.current.currentSelectedGameObject == null &&
            IsAnyArrowKeyDown() &&
            _playButton.IsActive() &&
            _playButton.IsInteractable())
        {
            SelectPlay();
        }
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
	/// 플레이버튼 선택중
	/// </summary>
	private void SelectPlay()
	{
		_playButton.Select();
	}


    // 로그인한 사용자가 플레이를 누르면 마을로 이동한다.
    public void LoadTown()
    {
        // UI 외부에서 호출되더라도 로그인 전에는 이동하지 않는다.
        if (!SaveManager.Instance.IsLoggedIn)
        {
            return;
        }

        // 마을 씬을 아직 준비하지 않았다면 이동하지 않고 이유를 표시한다.
        if (!Application.CanStreamedLevelBeLoaded("Town"))
        {
            Debug.LogError(
                "Town 씬을 만들고 빌드 씬 목록에 등록해주세요.");
            return;
        }

        SceneManager.LoadScene("Town");
    }

    public void LoadPlayCharacterSelect()
	{
		SceneManager.LoadScene("CharacterSelect_play");
    }

    /// <summary>
    /// 장비를 수정할 캐릭터를 선택하기 위해 장비 편집 목적을 기록하고 CharacterSelect 씬으로 이동한다.
    /// </summary>
    public void LoadEquipmentCharacterSelect()
    {
        // 이번 캐릭터 선택은 전투 출전이 아니라 캐릭터의 장착 무기를 수정하기 위한 선택이라고 기록한다.
        GameSessionManager.Instance.BeginCharacterSelection(CharacterSelectPurpose.LoadoutEdit);

        // 장비를 수정할 캐릭터를 고르기 위해 CharacterSelect 씬으로 이동한다.
        SceneManager.LoadScene("CharacterSelect_equip");
    }

    public void LoadAbility()
	{
		
	}

	public void LoadSetting()
	{
		SceneManager.LoadScene("Setting");
	}
}

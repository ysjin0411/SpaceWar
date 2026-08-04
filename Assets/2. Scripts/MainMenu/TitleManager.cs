using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class TitleManager : MonoBehaviour
{
    [Header("버튼")]
    [SerializeField] Button _playButton;
	[SerializeField] Button _skillButton;
	[SerializeField] Button _abilityButton;
	[SerializeField] Button _settingButton;

    [Header("셀렉이펙트")]
    [SerializeField] Image _playSelectEffect;
	[SerializeField] Image _skillSelectEffect;
	[SerializeField] Image _abilitySelectEffect;
	[SerializeField] Image _settingSelectEffect;


	private void Start()
    {
		MainMenuBaseState();
	}

	private void LateUpdate()
	{
		// 현재 선택된 UI가 없을 때만 방향키 입력을 검사한다.
		if (EventSystem.current.currentSelectedGameObject == null)
		{
			if (IsAnyArrowKeyDown())	// 방향키 입력받아오면 bool값이 켜짐.
			{
				// 아무키 입력시 맨위 버튼인 play활성화
				SelectPlay();
			}
		}

		UpdateSelectEffects();
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
	/// 셀렉이펙트 관리
	/// </summary>
	private void UpdateSelectEffects()
	{
		// selectedObject에 현재 선택중인 게임오브젝트를 저장
		GameObject selectedObject = EventSystem.current.currentSelectedGameObject;

		// 해당 이펙트는 현재선택중인 오브젝트일때 SetActive
		_playSelectEffect.gameObject.SetActive(selectedObject == _playButton.gameObject);
		_skillSelectEffect.gameObject.SetActive(selectedObject == _skillButton.gameObject);
		_abilitySelectEffect.gameObject.SetActive(selectedObject == _abilityButton.gameObject);
		_settingSelectEffect.gameObject.SetActive(selectedObject == _settingButton.gameObject);
	}

	/// <summary>
	/// 플레이버튼 선택중
	/// </summary>
	private void SelectPlay()
	{
		_playButton.Select();
	}

	private void MainMenuBaseState()
    {
        _playSelectEffect.gameObject.SetActive(false);
		_skillSelectEffect.gameObject.SetActive(false);
		_abilitySelectEffect.gameObject.SetActive(false);
		_settingSelectEffect.gameObject.SetActive(false);
	}

	public void LoadStageSelect()
	{
		SceneManager.LoadScene("StageSelect");
	}

	public void LoadSkill()
	{
		SceneManager.LoadScene("SkillManage");
	}

	public void LoadAbility()
	{
		SceneManager.LoadScene("AbilityManage");
	}

	public void LoadSetting()
	{
		SceneManager.LoadScene("Setting");
	}
}

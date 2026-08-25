using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleManager : MonoBehaviour
{
    [Header("버튼")]
    [SerializeField] Button _playButton;
	[SerializeField] Button _skillButton;
    [SerializeField] Button _achievementButton;
    [SerializeField] Button _settingButton;

    [Header("셀렉이펙트")]
    [SerializeField] Image _playSelectEffect;
	[SerializeField] Image _skillSelectEffect;
    [SerializeField] Image _achievementSelectEffect;
    [SerializeField] Image _settingSelectEffect;

    [Header("업적 보상 알림")]

    // 받을 수 있는 업적 보상이 있을 때 점멸할 테두리다.
    [SerializeField] UIAttentionPulse _achievementRewardAlert;


    private void Start()
    {
		MainMenuBaseState();

        if (AchievementManager.Instance == null)
        {
            throw new InvalidOperationException(
                "AchievementManager가 존재하지 않습니다.");
        }

        // 업적 달성이나 보상 수령으로 상태가 바뀌면
        // 메인 메뉴의 업적 알림도 즉시 갱신한다.
        AchievementManager.Instance.AchievementStateChanged +=
            HandleAchievementStateChanged;

        // 메인 메뉴에 처음 진입한 시점의 저장 데이터를 확인한다.
        RefreshAchievementRewardAlert();
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

    private void OnDestroy()
    {
        // 씬이 종료될 때 이벤트 연결을 해제한다.
        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.AchievementStateChanged -=
                HandleAchievementStateChanged;
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
	/// 셀렉이펙트 관리
	/// </summary>
	private void UpdateSelectEffects()
	{
		// selectedObject에 현재 선택중인 게임오브젝트를 저장
		GameObject selectedObject = EventSystem.current.currentSelectedGameObject;

		// 해당 이펙트는 현재선택중인 오브젝트일때 SetActive
		_playSelectEffect.gameObject.SetActive(selectedObject == _playButton.gameObject);
		_skillSelectEffect.gameObject.SetActive(selectedObject == _skillButton.gameObject);
        _achievementSelectEffect.gameObject.SetActive(selectedObject == _achievementButton.gameObject);
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
        _achievementSelectEffect.gameObject.SetActive(false);
		_settingSelectEffect.gameObject.SetActive(false);
	}

	public void LoadStageSelect()
	{
		SceneManager.LoadScene("StageSelect");
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

    /// <summary>
    /// 받을 수 있는 업적 보상이 하나라도 있는지 확인하여
    /// 메인 메뉴 업적 버튼의 점멸 상태를 갱신한다.
    /// </summary>
    private void RefreshAchievementRewardAlert()
    {
        bool hasClaimableAchievement =
            AchievementManager.Instance.HasClaimableAchievement();

        _achievementRewardAlert.SetAttention(
            hasClaimableAchievement);
    }

    /// <summary>
    /// 업적 상태가 변경되었을 때 호출된다.
    /// </summary>
    private void HandleAchievementStateChanged()
    {
        RefreshAchievementRewardAlert();
    }
}

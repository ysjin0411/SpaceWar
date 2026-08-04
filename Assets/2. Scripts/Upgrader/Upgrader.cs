using TMPro;
using UnityEngine;

public class Upgrader : MonoBehaviour
{
	[Header("컴포넌트")]
	[SerializeField] GameManager _gameManager;
	[SerializeField] PlayerController _playerController;
	[SerializeField] Weapon _weapon;


	[Header("증강패널")]
	[SerializeField] GameObject _firstSelectPanel;               // 1차 증강 목록 패널
	[SerializeField] GameObject _weaponSelectPanel;         // 2차 무기 증강 목록 패널


	[Header("증강데이터")]
	[SerializeField] UpgradeData[] _selectedUpgrades = new UpgradeData[3];

	[Header("1차 증강Info")]
	[SerializeField] TMP_Text[] _firstUpgradeInfoTexts;

	[Header("2차 무기 선택 Info")]
	[SerializeField] TMP_Text[] _weaponSelectInfoTexts;


	UpgradeData _pendingUpgrade;		// 1차 증강 선택 임시 데이터 보관용





	/// <summary>
	/// 정지 > 증강패널 열기 >  증강 데이터 띄우기
	/// </summary>
	public void UpgradeSelectFirst()
	{
		// 일시정지
		Time.timeScale = 0f;

		// 1차 증강 패널 열기
		_firstSelectPanel.SetActive(true);

		// 1차 증강 데이터 띄우기
		UpdateDataToUpgradePanel();
		//_isWeaponSelectPanel = true;
	}

	/// <summary>
	/// 증강에 데이터 띄우기
	/// </summary>
	private void UpdateDataToUpgradePanel()
	{
		for (int i = 0; i < _selectedUpgrades.Length; i++)
		{
			_firstUpgradeInfoTexts[i].text = _selectedUpgrades[i].UpgradeInfo;
		}
	}

	/// <summary>
	/// 번호를 받고 1차적 판단
	/// </summary>
	/// <param name="index">선택증강번호</param>
	public void FirstSelectUpgrade(int index)
	{
		// selectedData에 증강목록[인덱스]를 가져옴.
		UpgradeData selectedData = _selectedUpgrades[index];
		// 0번 타입일때
		if (index == 0)
		{
			// 이속증가함수 호출, 가져온 증강목록에서 UpgradeSpeedValue만큼 AddMoveSpeed해줌
			_playerController.AddMoveSpeed(selectedData.UpgradeSpeedValue);

			// 선택 종료
			_gameManager.ExitUpgrade();
		}
		// 1번 타입이 아닐때
		else
		{
			// 1차 패널 닫기
			_firstSelectPanel.SetActive(false);

			// 2차 선택으로 넘기기
			OpenWeaponSelect(selectedData);
		}
	}

	/// <summary>
	/// 2차 선택 시작
	/// </summary>
	/// <param name="data"></param>
	private void OpenWeaponSelect(UpgradeData data)
	{
		// 1차증강 선택 데이터를 임시 보관소 펜딩업그레이드에 적용
		_pendingUpgrade = data;

		// 2차 무기 선택 설명 표시
		UpdateWeaponSelectInfo();

		// 2차 무기 증강 목록 펼치기
		_weaponSelectPanel.SetActive(true);

		// 2차 증강중이라고 스테이트 변경
		_gameManager.SetWeaponSelectPanelState(true);
	}

	public void ChooseWeapon(int index)
	{
		// 만약 1차선택이 연사였을때
		if (_pendingUpgrade.Type == UpgradeType.FireRate)
		{
			// value에 1차선택에서의 연사값 저장
			float value = _pendingUpgrade.UpgradeFireRateValue;

			// 웨폰에 연사강화 함수 호출
			_weapon.AddFireRate(index, value);
		}
		// 만약 1차선택이 공격력이였을때
		else if (_pendingUpgrade.Type == UpgradeType.Damage)
		{
			// value에 1차선택에서의 공격력 저장 + 그 값을 반올림처리
			int value = Mathf.RoundToInt(_pendingUpgrade.UpgradeDamageValue);

			// 웨폰에 공격력강화 함수 호출
			_weapon.AddDamage(index, value);
		}

		// 사용이 끝난 임시 데이터 비우기
		_pendingUpgrade = null;

		// 패널 닫기 및 게임 재개
		_gameManager.ExitUpgrade();
	}

	/// <summary>
	/// 2차 증강 텍스트 띄우기
	/// </summary>
	private void UpdateWeaponSelectInfo()
	{
		_weaponSelectInfoTexts[0].text = "1. Bullet을 강화한다.";

		_weaponSelectInfoTexts[1].text = "2. Lazer를 강화한다.";
	}
}

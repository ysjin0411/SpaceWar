using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Upgrader : MonoBehaviour
{
	[Header("컴포넌트")]
	[SerializeField] GameManager _gameManager;
	[SerializeField] PlayerController _playerController;
	[SerializeField] Weapon _weapon;
	[SerializeField] GameObject _upgradeUI;			// UpgradeUI 게임 오브젝트

	[Header("증강패널")]
	[SerializeField] GameObject _firstSelectPanel;               // 1차 증강 목록 패널
	[SerializeField] GameObject _weaponSelectPanel;         // 2차 무기 증강 목록 패널

	[Header("증강데이터")]
	[SerializeField] UpgradeData[] _selectedUpgrades = new UpgradeData[3];

	[Header("1차 증강Info")]
	[SerializeField] TMP_Text[] _firstUpgradeInfoTexts;

    [Header("2차 무기 선택 슬롯")]
    [SerializeField] GameObject[] _weaponSelectSlots;

    [Header("2차 무기 선택 아이콘")]
    [SerializeField] Image[] _weaponSelectIcons;

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
        _upgradeUI.SetActive(true);
        _firstSelectPanel.SetActive(true);
        _weaponSelectPanel.SetActive(false);

        // 1차 증강 데이터 띄우기
        UpdateDataToUpgradePanel();
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

    /// <summary>
    /// 선택한 장착 슬롯의 무기에 대기 중인 증강을 적용한다.
    /// </summary>
    /// <param name="slotIndex">강화할 장착 무기 슬롯 번호</param>
    public void ChooseWeapon(int slotIndex)
    {
        // 적용할 증강 데이터가 없다면 함수를 종료한다.
        if (_pendingUpgrade == null)
        {
            return;
        }

        // 선택한 슬롯에 실제로 장착된 무기가 있는지 확인한다.
        WeaponData weaponData = _weapon.GetWeaponDataAtSlot(slotIndex);

        // 해당 슬롯에 무기가 없다면 증강을 적용하지 않는다.
        if (weaponData == null)
        {
            return;
        }

        // 선택한 증강이 연사력 강화인 경우
        if (_pendingUpgrade.Type == UpgradeType.FireRate)
        {
            // 실제 연사 강화량은 해당 슬롯 무기의 WeaponData에서 가져와 적용한다.
            _weapon.AddFireRate(slotIndex);
        }

        // 선택한 증강이 공격력 강화인 경우
        else if (_pendingUpgrade.Type == UpgradeType.Damage)
        {
            // UpgradeData의 공격력 증가값을 정수로 변환한다.
            int value = Mathf.RoundToInt(_pendingUpgrade.UpgradeDamageValue);

            // 선택한 슬롯 무기의 현재 공격력을 증가시킨다.
            _weapon.AddDamage(slotIndex, value);
        }

        // 증강 적용이 끝났으므로 대기 데이터를 비운다.
        _pendingUpgrade = null;

        // 증강 UI를 닫고 게임을 다시 진행한다.
        _gameManager.ExitUpgrade();
    }

    /// <summary>
    /// 현재 장착된 무기만 증강 대상 화면에 표시한다.
    /// </summary>
    private void UpdateWeaponSelectInfo()
    {
        // 증강 선택 카드의 개수만큼 반복한다.
        for (int slotIndex = 0;slotIndex < _weaponSelectSlots.Length;slotIndex++)
        {
            // 해당 장착 슬롯의 무기 원본 데이터를 가져온다.
            WeaponData weaponData = _weapon.GetWeaponDataAtSlot(slotIndex);

            // 해당 슬롯에 무기가 장착되어 있는지 확인한다.
            bool hasWeapon = weaponData != null;

            // 무기가 있으면 카드 전체를 켜고, 무기가 없으면 번호와 배경을 포함한 카드 전체를 끈다.
            _weaponSelectSlots[slotIndex].SetActive(hasWeapon);

            // 무기가 없는 슬롯은 텍스트를 설정할 필요가 없다.
            if (!hasWeapon)
            {
                continue;
            }

            // 해당 슬롯의 아이콘 Image를 가져온다.
            Image weaponIconImage = _weaponSelectIcons[slotIndex];

            // Image가 Inspector에 정상적으로 연결된 경우에만 WeaponData의 아이콘을 표시한다.
            if (weaponIconImage != null)
            {
                weaponIconImage.sprite = weaponData.Icon;

                // WeaponData에 아이콘이 있을 때만 Image 컴포넌트를 활성화한다.
                weaponIconImage.enabled = weaponData.Icon != null;
            }

            // 카드 인포 설명
            _weaponSelectInfoTexts[slotIndex].text = "강화 대상: " + weaponData.WeaponName;
        }
    }
}

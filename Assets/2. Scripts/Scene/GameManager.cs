using System;
using TMPro;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    [Header("컴포넌트")]
    [SerializeField] PlayerController _playerController;
    [SerializeField] UpgradeMap _upgradeDataMap;
    [SerializeField] Weapon _weapon;

    [Header("게이지관련")]
    [SerializeField] Image _gaugeBar;
    [SerializeField] float _maxGaugeBar = 100f;
    [SerializeField] float _timeCharge;
    [SerializeField] float _bonusCharge;

    [Header("증강패널")]
    [SerializeField] GameObject _selectPanel;               // 1차 증강 목록 패널
    [SerializeField] GameObject _weaponSelectPanel;         // 2차 무기 증강 목록 패널

    [Header("프리팹")]
    [SerializeField] GameObject _fastEnemyPrefab;

    [Header("증강")]
    [SerializeField] UpgradeData[] _selectedUpgrades = new UpgradeData[3];
    [SerializeField] TMP_Text[] _upgradeInfoTexts;
    int _selectNum;
    bool _isWeaponSelectPanel;

    public void UpdateGaugeBar()
    {
        
        _gaugeBar.fillAmount = _timeCharge / _maxGaugeBar;
    }

    public void BonusGauge()
    {
        _timeCharge += _bonusCharge;
    }


    private void Update()
    {
        _timeCharge += Time.deltaTime;

        // 게이지완 후 증강패널오픈함수 호출
        if(_timeCharge >= _maxGaugeBar)
        {
            // 증강 오픈
            UpgradeSelect();
            // 일시 정지
            Time.timeScale = 0f;
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                _selectNum = 0;
				SelectUpgrade(_selectNum);
			}
            else if(Input.GetKeyDown(KeyCode.Alpha2))
            {
				_selectNum = 1;
				SelectUpgrade(_selectNum);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
				_selectNum = 2;
				SelectUpgrade(_selectNum);
            }
            else
            {
                return;
            }
        }

        if (_isWeaponSelectPanel)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                // 1번 무기 선택

                // 1차 선택때 공격력 or 연사속도 중 무얼 선택했는지에 따라 업그레이드함수(공격력or연사)

            }
        }
    }

    /// <summary>
    /// 증강패널 열고 데이터 띄우기
    /// </summary>
    public void UpgradeSelect()
    {
        // 증강 데이터 표시
        UpdateUpgradePanel();
        // 증강 패널 열기
        _selectPanel.SetActive(true);
        _isWeaponSelectPanel = true;
	}

    /// <summary>
    /// 증강패널에 데이터 띄우기
    /// </summary>
    private void UpdateUpgradePanel()
    {
        for (int i = 0; i < _selectedUpgrades.Length; i++)
        {
            _upgradeInfoTexts[i].text =
                _selectedUpgrades[i].UpgradeInfo;
        }
    }

    /// <summary>
    /// 번호를 받고 패널 띄우기
    /// </summary>
    /// <param name="index"></param>
    public void SelectUpgrade(int index)
    {
        // 번호 받기
        index = _selectNum;

        if(index == 0)
        {
            ApplyUpgrade(index);
		}
        else if(index >= 1)
        {
            // 1차패널 닫기
            _selectPanel.SetActive(false);
            // 2차 패널 열기
            _weaponSelectPanel.SetActive(true);
        }
    }

    

    /// <summary>
    /// 증강 이동속도 업그레이드를 골랐을때, 업그레이드한테 이속올리라고 전달.
    /// </summary>
    /// <param name="data"></param>
    private void ApplyPlayerUpgrade(UpgradeData data)
    {
        switch (data.Type)
        {
            case UpgradeType.MoveSpeed:
                _playerController.AddMoveSpeed(data.UpgradeSpeedValue);
                break;
        }
    }

    /// <summary>
    /// 증강 무기 업그레이드
    /// </summary>
    /// <param name="data"></param>
    private void ApplyWeaponUpgrade(UpgradeData data)
    {
        switch (data.Type)
        {
            case UpgradeType.FireRate:
                _weapon.ReduceCurrentWeaponCooltime(data.UpgradeDamageValue);
                break;
        }
    }

	/// <summary>
	/// 무기 강화 선택했을때 무기 증강 펼침
	/// </summary>
	/// <param name="data"></param>
	private void OpenWeaponSelect(UpgradeData data)
	{
        // 1차 증강 종류 목록 닫기
        _selectPanel.SetActive(false);

        // 2차 무기 증강 목록 펼치기
		_weaponSelectPanel.SetActive(true);
        
	}

	/// <summary>
	/// 증강 선택 종료 후 패널 닫기
	/// </summary>
	public void CloseUpgradePanel()
    {
        _timeCharge = 0f;
        _selectPanel.SetActive(false);
        Time.timeScale = 1f;
    }
}
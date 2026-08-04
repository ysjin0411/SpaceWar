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
    [SerializeField] Upgrader _upgrader;

	[Header("증강패널")]
	[SerializeField] GameObject _firstSelectPanel;               // 1차 증강 목록 패널
	[SerializeField] GameObject _weaponSelectPanel;         // 2차 무기 증강 목록 패널

	[Header("게이지관련")]
    [SerializeField] Image _gaugeBar;
    [SerializeField] float _maxGaugeBar = 100f;
    [SerializeField] float _timeCharge;
    [SerializeField] float _bonusCharge;

    [Header("프리팹")]
    [SerializeField] GameObject _fastEnemyPrefab;

	bool _isWeaponSelectPanel;          // 2차증강 열려있는지 여부



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
        
        // 2차선택 화면이 켜져있을때
		if (_isWeaponSelectPanel)         
		{
			if (Input.GetKeyDown(KeyCode.Alpha1))
			{
                // 1번 웨폰 선택
				_upgrader.ChooseWeapon(0);
			}
			else if (Input.GetKeyDown(KeyCode.Alpha2))
			{
                // 2번 웨폰 선택
				_upgrader.ChooseWeapon(1);
			}
			else {return;}
		}

		// 게이지완 후 증강패널오픈함수 호출
		if (_timeCharge >= _maxGaugeBar)
        {
            // 증강 오픈
            _upgrader.UpgradeSelectFirst();
            // 일시 정지
            Time.timeScale = 0f;
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                // 이속 선택
                _upgrader.FirstSelectUpgrade(0);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                // 연사 강화 선택
                _upgrader.FirstSelectUpgrade(1);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                // 공격력 강화 선택
                _upgrader.FirstSelectUpgrade(2); 
            }
        }
    }
    

	/// <summary>
	/// 패널 닫고 게임 재생
	/// </summary>
	public void ExitUpgrade()
	{
        // 웨폰창 닫기
		_isWeaponSelectPanel = false;       

        // 게이지 초기화
		_timeCharge = 0f;

        // 증강들 다 닫기
		_firstSelectPanel.SetActive(false);
		_weaponSelectPanel.SetActive(false);

        // 게임 재생
		Time.timeScale = 1f;
	}

	/// <summary>
	/// 2차 무기 증강 선택 상태를 변경
	/// </summary>
	public void SetWeaponSelectPanelState(bool isOpen)
	{
		_isWeaponSelectPanel = isOpen;
	}


}
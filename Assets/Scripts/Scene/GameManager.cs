using System;
using TMPro;
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
    [SerializeField] GameObject _selectPanel;

    [Header("프리팹")]
    [SerializeField] GameObject _fastEnemyPrefab;

    [Header("증강")]
    [SerializeField] UpgradeData[] _selectedUpgrades = new UpgradeData[3];
    [SerializeField] TMP_Text[] _upgradeInfoTexts;

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
                SelectUpgrade(0);
            }
            else if(Input.GetKeyDown(KeyCode.Alpha2))
            {
                SelectUpgrade(1);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                SelectUpgrade(2);
            }
            else
            {
                return;
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
    /// 능력치 적용 후 패널 닫기
    /// </summary>
    /// <param name="index"></param>
    public void SelectUpgrade(int index)
    {
        ApplyUpgrade(_selectedUpgrades[index]);
        CloseUpgradePanel();
    }

    /// <summary>
    /// 증강 업그레이드 준비
    /// </summary>
    /// <param name="data"></param>
    private void ApplyUpgrade(UpgradeData data)
    {
        switch (data.Target)
        {
            case UpgradeTarget.Player:
                ApplyPlayerUpgrade(data);
                break;

            case UpgradeTarget.Weapon:
                ApplyWeaponUpgrade(data);
                break;
        }
    }

    /// <summary>
    /// 증강 이동속도 업그레이드
    /// </summary>
    /// <param name="data"></param>
    private void ApplyPlayerUpgrade(UpgradeData data)
    {
        switch (data.Type)
        {
            case UpgradeType.MoveSpeed:
                _playerController.AddMoveSpeed(data.Value);
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
                _weapon.ReduceCurrentWeaponCooltime(data.Value);
                break;
        }
    }

    /// <summary>
    /// 증강패널 닫기
    /// </summary>
    private void CloseUpgradePanel()
    {
        _timeCharge = 0f;
        _selectPanel.SetActive(false);
        Time.timeScale = 1f;
    }
}
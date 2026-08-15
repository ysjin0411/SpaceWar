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
    [SerializeField] GoldUI _goldUI; // 몬스터가 드롭한 골드를 화면에 생성할 GoldUI

    [Header("증강패널")]
	[SerializeField] GameObject _firstSelectPanel;               // 1차 증강 목록 패널
	[SerializeField] GameObject _weaponSelectPanel;         // 2차 무기 증강 목록 패널

	[Header("게이지관련")]
    [Tooltip("증강 게이지의 화면 연출을 담당하는 UI")]
    [SerializeField] GaugeUIManager _gaugeUIManager;
    [SerializeField] float _maxGaugeBar = 100f;
    [SerializeField] float _timeCharge;

    [Header("프리팹")]
    [SerializeField] GameObject _fastEnemyPrefab;

    // 같은 오브젝트에 있는 캐릭터 성장 시스템
    CharacterProgression _characterProgression;

    bool _isWeaponSelectPanel;          // 2차증강 열려있는지 여부

    // 현재 증강 선택 화면이 열려 있는지 확인한다.
    bool _isUpgradeOpen;

    private void Awake()
    {
        // StageManager와 같은 오브젝트에 있는 성장 시스템을 가져온다.
        _characterProgression = GetComponent<CharacterProgression>();

        // 성장 시스템이 없으면 적 처치 경험치를 지급할 수 없다.
        if (_characterProgression == null)
        {
            throw new MissingComponentException(
                $"{name}에 CharacterProgression 컴포넌트가 없습니다.");
        }

        if (_gaugeUIManager == null)
        {
            throw new InvalidOperationException(
                $"{name}의 GameManager에 GaugeUIManager가 연결되지 않았습니다.");
        }

        if (_maxGaugeBar <= 0f)
        {
            throw new InvalidOperationException(
                $"{name}의 Max Gauge Bar는 0보다 커야 합니다.");
        }
    }

    private void Start()
    {
        // 새로운 스테이지에서는 빈 게이지로 시작한다.
        _timeCharge = 0f;
        _isUpgradeOpen = false;

        // 현재 충전량부터 최대치까지 DOTween 충전을 시작한다.
        _gaugeUIManager.StartCharging(
            _timeCharge,
            _maxGaugeBar);
    }

    /// <summary>
    /// 적을 처치했을 때 현재 캐릭터에게 경험치를 지급한다.
    /// </summary>
    public void AddExperience(int amount)
    {
        // 경험치가 0 이하라면 지급하지 않는다.
        if (amount <= 0)
        {
            return;
        }

        // 실제 레벨과 경험치를 관리하는 성장 시스템에 전달한다.
        _characterProgression.AddExp(amount);
    }

    /// <summary>
    /// 적이 전달한 보상량만큼 현재 증강 게이지를 증가시킨다.
    /// </summary>
    public void BonusGauge(float amount)
    {
        // 보상량이 0 이하라면 게이지를 증가시키지 않는다.
        if (amount <= 0f)
        {
            return;
        }

        // 이미 증강 선택 중이라면 추가 게이지를 받지 않는다.
        if (_isUpgradeOpen)
        {
            return;
        }

        // 적이 전달한 보상량을 실제 게이지에 반영한다.
        _timeCharge = Mathf.Min(
            _maxGaugeBar,
            _timeCharge + amount);

        // 화면 게이지는 변경된 위치까지 부드럽게 따라간다.
        _gaugeUIManager.AnimateBonus(
            _timeCharge,
            _maxGaugeBar);

        // 적 처치 보상으로 최대치에 도달했다면 증강을 시작한다.
        if (_timeCharge >= _maxGaugeBar)
        {
            OpenUpgrade();
        }
    }


    private void Update()
    {
        // 증강 선택 중이 아닐 때만 실제 게이지 시간을 증가시킨다.
        if (!_isUpgradeOpen)
        {
            _timeCharge = Mathf.Min(
                _maxGaugeBar,
                _timeCharge + Time.deltaTime);

            // 시간 충전으로 최대치에 도달했다.
            if (_timeCharge >= _maxGaugeBar)
            {
                OpenUpgrade();
            }

            return;
        }

        // 무기 증강 대상을 선택하는 단계다.
        if (_isWeaponSelectPanel)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                _upgrader.ChooseWeapon(0);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                _upgrader.ChooseWeapon(1);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                _upgrader.ChooseWeapon(2);
            }

            return;
        }

        // 첫 번째 증강 종류를 선택하는 단계다.
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            _upgrader.FirstSelectUpgrade(0);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            _upgrader.FirstSelectUpgrade(1);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            _upgrader.FirstSelectUpgrade(2);
        }
    }


    public void ExitUpgrade()
    {
        _isWeaponSelectPanel = false;
        _isUpgradeOpen = false;
        _timeCharge = 0f;

        _firstSelectPanel.SetActive(false);
        _weaponSelectPanel.SetActive(false);

        // 먼저 게임 시간을 정상화한다.
        Time.timeScale = 1f;

        // 빈 상태부터 다음 증강 게이지 충전을 시작한다.
        _gaugeUIManager.StartCharging(
            _timeCharge,
            _maxGaugeBar);
    }

    /// <summary>
    /// 2차 무기 증강 선택 상태를 변경
    /// </summary>
    public void SetWeaponSelectPanelState(bool isOpen)
	{
		_isWeaponSelectPanel = isOpen;
	}

    public void SpawnGold(
    Gold goldPrefab,
    Vector3 worldPosition,
    int dropCount)
    {
        if (_goldUI == null)
        {
            throw new InvalidOperationException(
                $"{name}의 GameManager에 GoldUI가 연결되지 않았습니다.");
        }

        _goldUI.SpawnGold(
            goldPrefab,
            worldPosition,
            dropCount);
    }

    /// <summary>
    /// 증강 게이지가 최대치에 도달했을 때 선택 화면을 연다.
    /// </summary>
    private void OpenUpgrade()
    {
        // 이미 열려 있다면 중복 실행하지 않는다.
        if (_isUpgradeOpen)
        {
            return;
        }

        _isUpgradeOpen = true;

        // 화면 게이지를 정확히 1까지 마무리한다.
        _gaugeUIManager.CompleteGauge();

        // 첫 번째 증강 선택 화면을 연다.
        _upgrader.UpgradeSelectFirst();

        // 증강을 선택하는 동안 게임을 정지한다.
        Time.timeScale = 0f;
    }
}
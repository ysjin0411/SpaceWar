using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class BossTowerShield : MonoBehaviour
{
    // 이 보스의 실드 체력으로 합산할 타워들을 연결한다.
    [SerializeField] private Tower[] _towers;

    // 최초 최대 체력 합계를 유지한다.
    private int _maxShield;

    // 현재 타워 체력 합계다.
    private int _currentShield;

    // 초기화 중복을 방지한다.
    private bool _isInitialized;

    // 합산 체력이 바뀌면 현재 값과 최대 값을 UI에 전달한다.
    public event Action<int, int> ShieldChanged;

    // UI가 이벤트 구독 직후 현재 상태를 읽을 수 있도록 공개한다.
    public int MaxShield
    {
        get
        {
            InitializeShield();
            return _maxShield;
        }
    }

    public int CurrentShield
    {
        get
        {
            InitializeShield();
            return _currentShield;
        }
    }

    private void Awake()
    {
        InitializeShield();
    }

    // 타워 목록을 검증하고 최대 체력 합계를 한 번만 계산한다.
    private void InitializeShield()
    {
        if (_isInitialized)
            return;

        if (_towers == null || _towers.Length == 0)
            throw new InvalidOperationException(
                $"{name}: 실드로 사용할 타워를 연결하세요.");

        var registeredTowers = new HashSet<Tower>();

        foreach (Tower tower in _towers)
        {
            // 같은 타워가 두 번 들어가면 체력이 중복 합산되므로 막는다.
            if (tower == null || !registeredTowers.Add(tower))
                throw new InvalidOperationException(
                    $"{name}: 타워의 누락 또는 중복 연결을 확인하세요.");

            tower.InitializeHealth();

            _maxShield += tower.MaxHp;
            _currentShield += tower.CurrentHp;
        }

        _isInitialized = true;
    }

    private void OnEnable()
    {
        InitializeShield();

        // 타워마다 체력 변화 이벤트를 구독한다.
        foreach (Tower tower in _towers)
            tower.HealthChanged += RefreshShield;

        // 비활성화 중 값이 바뀌었더라도 다시 합산한다.
        RefreshShield();
    }

    private void OnDisable()
    {
        if (!_isInitialized)
            return;

        // 이벤트 구독을 해제해 중복 호출을 방지한다.
        foreach (Tower tower in _towers)
        {
            if (tower != null)
                tower.HealthChanged -= RefreshShield;
        }
    }

    // 피해가 발생했을 때만 현재 체력을 다시 합산한다.
    private void RefreshShield()
    {
        _currentShield = 0;

        foreach (Tower tower in _towers)
        {
            if (tower != null)
                _currentShield += tower.CurrentHp;
        }

        // 최대 합계는 타워가 파괴되어도 변경하지 않는다.
        ShieldChanged?.Invoke(_currentShield, _maxShield);
    }
}
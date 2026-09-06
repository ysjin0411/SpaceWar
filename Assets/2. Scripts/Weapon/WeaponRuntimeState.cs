using System;
using UnityEngine;

/// <summary>
/// Stage가 진행되는 동안 무기마다 달라지는 현재 상태를 보관한다.
/// 공격력, 공격 쿨타임, 충전형 무기의 보유량과 충전 시간을 관리한다.
/// </summary>
[System.Serializable]
public class WeaponRuntimeState
{
    [Header("런타임 데이터")]

    [SerializeField] WeaponData _data;           // 이 런타임 상태가 사용하는 원본 무기 데이터
    [SerializeField] int _currentDamage;         // 증강이 적용된 현재 공격력
    [SerializeField] float _currentCooltime;     // 증강이 적용된 현재 공격 쿨타임

    [Header("충전형 무기 런타임 데이터")]

    [SerializeField] int _currentChargeCount;    // 현재 보유하고 있는 수류탄 개수
    [SerializeField] float _chargeRecoveryTimer; // 다음 수류탄 한 개를 충전하며 누적한 시간
    [SerializeField] float _currentChargeRecoveryTime; // 강화가 적용된 현재 1개 충전 시간

    [Header("실드 런타임 상태")]

    // 전투 중 남아 있는 실드 체력.
    // 자연회복을 부드럽게 계산하기 위해 소수점까지 보관한다.
    [SerializeField] float _currentShieldHealth;

    // 연사 증강이 반영된 초당 자연회복량.
    [SerializeField] float _currentShieldRecoveryPerSecond;

    // 현재 실드 체력.
    public float CurrentShieldHealth => _currentShieldHealth;

    // 최대 체력은 원본 WeaponData에서 가져온다.
    public float MaximumShieldHealth =>
        _data.IsShield ? _data.ShieldMaxHealth : 0f;

    // 증강이 반영된 초당 자연회복량.
    public float CurrentShieldRecoveryPerSecond =>
        _currentShieldRecoveryPerSecond;

    // 실드가 한 번이라도 공격을 막을 수 있는 상태인지 확인한다.
    public bool HasShieldHealth =>
        _data.IsShield && _currentShieldHealth > 0f;

    // UI 게이지에 사용할 0~1 범위의 남은 체력 비율.
    public float ShieldHealthRatio =>
        MaximumShieldHealth > 0f
            ? Mathf.Clamp01(_currentShieldHealth / MaximumShieldHealth)
            : 0f;

    public WeaponData Data => _data;
    public int CurrentDamage => _currentDamage;
    public float CurrentCooltime => _currentCooltime;

    // 강화가 적용된 현재 수류탄 1개 충전 시간이다.
    public float CurrentChargeRecoveryTime =>
        _currentChargeRecoveryTime;

    // 현재 보유하고 있는 충전 개수다.
    public int CurrentChargeCount => _currentChargeCount;

    // 현재 수류탄을 한 개 이상 사용할 수 있는지 확인한다.
    public bool HasAvailableCharge =>
        !_data.UsesCharges || _currentChargeCount > 0;

    // 다음 수류탄 한 개가 충전될 때까지 남은 시간이다.
    // 최대 소지량에 도달한 동안에는 0을 반환한다.
    public float ChargeRecoveryRemaining
    {
        get
        {
            if (!_data.UsesCharges ||
                _currentChargeCount >= _data.MaximumChargeCount)
            {
                return 0f;
            }

            return Mathf.Max(
                0f,
                _currentChargeRecoveryTime -
                _chargeRecoveryTimer);
        }
    }

    // 충전 진행도를 0부터 1 사이의 값으로 반환한다.
    // 나중에 UI의 Radial 360 Image에 사용한다.
    public float ChargeRecoveryProgress
    {
        get
        {
            if (!_data.UsesCharges ||
                _currentChargeCount >= _data.MaximumChargeCount)
            {
                return 1f;
            }

            return Mathf.Clamp01(
                _chargeRecoveryTimer /
                _currentChargeRecoveryTime);
        }
    }

    public WeaponRuntimeState(WeaponData data)
    {
        // 런타임 상태는 반드시 원본 WeaponData를 가지고 시작해야 한다.
        if (data == null)
        {
            throw new ArgumentNullException(
                nameof(data),
                "WeaponRuntimeState를 생성하려면 WeaponData가 필요합니다.");
        }

        // 원본 무기 데이터를 저장한다.
        _data = data;

        // 전투 시작 시 기본 공격력과 기본 공격 쿨타임을 사용한다.
        _currentDamage = data.BaseDamage;
        _currentCooltime = data.BaseCooltime;

        // Stage 시작 시 WeaponData에 설정된 기본 충전 시간을 사용한다.
        // 이후 Grenade 연사 강화를 받으면 이 값이 감소한다.
        _currentChargeRecoveryTime =
            data.ChargeRecoveryTime;

        // 충전형 무기는 Stage 시작 시 최대 소지량을 가지고 시작한다.
        // 일반 무기는 MaximumChargeCount가 0이므로 현재 충전량도 0이다.
        _currentChargeCount = data.UsesCharges
            ? data.MaximumChargeCount
            : 0;

        // 최대 소지량으로 시작하므로 충전 중인 시간은 없다.
        _chargeRecoveryTimer = 0f;

        // 실드는 전투 시작 시 최대 체력으로 시작한다.
        // 다른 무기는 실드 체력을 사용하지 않는다.
        _currentShieldHealth = data.IsShield
            ? data.ShieldMaxHealth
            : 0f;

        // 기본 자연회복량을 전투용 값으로 가져온다.
        _currentShieldRecoveryPerSecond = data.IsShield
            ? data.ShieldRecoveryPerSecond
            : 0f;
    }

    /// <summary>
    /// 장착 중인 충전형 무기의 충전 시간을 진행한다.
    /// 현재 선택한 무기가 아니어도 Weapon이 이 메서드를 호출한다.
    /// </summary>
    public void UpdateChargeRecovery(float deltaTime)
    {
        // 일반 무기는 충전량을 사용하지 않는다.
        if (!_data.UsesCharges)
        {
            return;
        }

        // 최대 소지량이면 충전 타이머를 진행하지 않는다.
        if (_currentChargeCount >= _data.MaximumChargeCount)
        {
            _currentChargeCount = _data.MaximumChargeCount;
            _chargeRecoveryTimer = 0f;
            return;
        }

        // Stage의 Time.deltaTime을 충전 시간에 누적한다.
        _chargeRecoveryTimer += deltaTime;

        // 프레임 지연 등으로 충전 시간이 여러 번 지났을 경우도 처리한다.
        while (_chargeRecoveryTimer >= _currentChargeRecoveryTime &&
               _currentChargeCount < _data.MaximumChargeCount)
        {
            // 현재 강화 상태의 충전 시간만큼 사용한다.
            _chargeRecoveryTimer -=
                _currentChargeRecoveryTime;

            _currentChargeCount++;
        }

        // 마지막 한 개가 충전되어 최대 소지량에 도달하면
        // 더 이상 다음 충전을 계산하지 않도록 타이머를 비운다.
        if (_currentChargeCount >= _data.MaximumChargeCount)
        {
            _currentChargeCount = _data.MaximumChargeCount;
            _chargeRecoveryTimer = 0f;
        }
    }

    /// <summary>
    /// 충전형 무기의 보유량을 한 개 소비한다.
    /// 보유량이 없으면 false를 반환하여 공격을 막는다.
    /// </summary>
    public bool TryConsumeCharge()
    {
        // 일반 무기는 충전량 검사를 통과시킨다.
        if (!_data.UsesCharges)
        {
            return true;
        }

        // 현재 사용할 수 있는 수류탄이 없다.
        if (_currentChargeCount <= 0)
        {
            return false;
        }

        // 수류탄 한 개를 사용한다.
        _currentChargeCount--;

        // 최대 소지 상태에서 처음 사용한 경우
        // 0초부터 다음 수류탄 충전이 시작된다.
        if (_currentChargeCount == _data.MaximumChargeCount - 1)
        {
            _chargeRecoveryTimer = 0f;
        }

        return true;
    }

    // 장착된 실드의 자연회복을 진행한다.
    public void UpdateShieldRecovery(float deltaTime)
    {
        // 실드가 아니거나 시간이 흐르지 않으면 처리하지 않는다.
        if (!_data.IsShield ||
            deltaTime <= 0f ||
            _currentShieldRecoveryPerSecond <= 0f)
        {
            return;
        }

        // 초당 회복량에 이번 프레임의 경과 시간을 곱한다.
        RestoreShieldHealth(
            _currentShieldRecoveryPerSecond * deltaTime);
    }

    // 자연회복과 증강 회복이 함께 사용하는 체력 회복 함수.
    public void RestoreShieldHealth(float amount)
    {
        if (!_data.IsShield || amount <= 0f)
        {
            return;
        }

        // 회복해도 최대 체력을 넘지 않게 제한한다.
        _currentShieldHealth = Mathf.Clamp(
            _currentShieldHealth + amount,
            0f,
            MaximumShieldHealth);
    }

    // 실드가 공격을 막았을 때 체력을 소모한다.
    // true는 이번 공격을 실드가 받아냈다는 뜻이다.
    public bool TryAbsorbShieldDamage(float damage)
    {
        // 이미 체력이 없거나 잘못된 피해량이면 막지 않는다.
        if (!HasShieldHealth || damage < 0f)
        {
            return false;
        }

        // 남은 체력보다 큰 공격을 막으면 체력은 0이 된다.
        // 이번에 막아낸 공격의 초과 피해는 플레이어에게 넘기지 않는다.
        _currentShieldHealth = Mathf.Max(
            0f,
            _currentShieldHealth - damage);

        return true;
    }

    // 공격력 증강을 무기 종류에 맞게 적용한다.
    public void AddDamage(int value)
    {
        if (_data.IsShield)
        {
            // 실드는 공격력을 올리는 대신 설정된 양만큼 체력을 회복한다.
            // 최대 체력 자체는 증가하지 않는다.
            RestoreShieldHealth(_data.ShieldRepairAmount);
            return;
        }

        // 다른 무기는 기존처럼 전달받은 공격력 증가량을 적용한다.
        if (value > 0)
        {
            _currentDamage += value;
        }
    }

    /// <summary>
    /// 무기 종류에 맞는 연사 강화를 적용한다.
    /// Grenade는 충전 속도가 빨라지고,
    /// 다른 무기는 기존 공격 쿨타임이 감소한다.
    /// </summary>
    public void AddFireRate()
    {
        // 실드는 공격 쿨타임 대신 초당 자연회복량을 증가시킨다.
        if (_data.IsShield)
        {
            _currentShieldRecoveryPerSecond +=
                _data.ShieldRecoveryUpgradeValue;

            return;
        }

        // Grenade의 연사 강화는 투척 간격 대신
        // 수류탄 한 개가 회복되는 시간을 감소시킨다.
        if (_data.Type == WeaponType.Grenade)
        {
            _currentChargeRecoveryTime -=
                _data.ChargeRecoveryUpgradeValue;

            // 여러 번 강화해도 Grenade에 설정된
            // 최소 충전 시간보다 짧아지지 않게 제한한다.
            _currentChargeRecoveryTime = Mathf.Max(
                _currentChargeRecoveryTime,
                _data.MinimumChargeRecoveryTime);

            return;
        }

        // Grenade 이외의 무기는 기존처럼 공격 쿨타임을 감소시킨다.
        _currentCooltime -=
            _data.FireRateUpgradeValue;

        // 일반 무기의 최소 공격 쿨타임보다 작아지지 않게 제한한다.
        _currentCooltime = Mathf.Max(
            _currentCooltime,
            _data.MinimumCooltime);
    }
}
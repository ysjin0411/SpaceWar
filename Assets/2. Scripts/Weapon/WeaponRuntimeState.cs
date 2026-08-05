using UnityEngine;

/// <summary>
/// 전투 중 증강으로 변하는 현재 스탯
/// </summary>
[System.Serializable]
public class WeaponRuntimeState
{
    [Header("런타임 데이터")]
    [SerializeField] WeaponData _data;                  // 무기 데이터
    [SerializeField] int _currentDamage;                // 현재 데미지
    [SerializeField] float _currentCooltime;            // 현재 쿨타임

    public WeaponData Data => _data;
    public int CurrentDamage => _currentDamage;
    public float CurrentCooltime => _currentCooltime;

    public WeaponRuntimeState(WeaponData data)
    {
        // 데이터 받기
        _data = data; 
        
        // 현재 데미지 = 받아온 데이터의 기본데미지
        _currentDamage = data.BaseDamage;

        // 현재 쿨타임 = 받아온 데이터의 기본쿨타임
        _currentCooltime = data.BaseCooltime;
    }

    /// <summary>
    /// 공격력 증가
    /// </summary>
    /// <param name="value">증가량</param>
    public void AddDamage(int value)
    {
        _currentDamage += value;
    }

    /// <summary>
    /// 연사력 강화
    /// </summary>
    public void AddFireRate()
    {
        // 현재 쿨타임에서 데이터의 연사력만큼 감소(레이저용)
        _currentCooltime -= _data.FireRateUpgradeValue;

        // 현재 쿨타임은 현재 쿨타임과 데이터의 최소쿨타임 중 큰 값으로 보정
        _currentCooltime = Mathf.Max(_currentCooltime, _data.MinimumCooltime);
    }
}
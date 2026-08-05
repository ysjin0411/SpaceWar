using System;
using System.Collections.Generic;
using UnityEngine;

public enum WeaponType
{
    Bullet,
    Lazer
}

/// <summary>
/// 장착 무기 관리, 무기 교체, 무기 발사를 담당한다.
/// </summary>
public class Weapon : MonoBehaviour
{
    [Header("컴포넌트")]
    [SerializeField] WeaponUI _weaponUI;    // 웨폰UI 컴포넌트

    [Header("테스트용 장착 무기 원본")]
    [Tooltip("세이브 시스템이 완성되기 전 테스트용으로 사용할 WeaponData")]
    [SerializeField] WeaponData[] _testEquippedWeaponDatas;

    [Header("장착 무기 런타임 데이터")]
    [Tooltip("WeaponData를 기반으로 전투 시작 시 생성되는 현재 무기 상태")]
    [SerializeField] List<WeaponRuntimeState> _equippedWeapons = new();     // 장착 무기 리스트 런타임 데이터 

    [Header("무기 슬롯")]
    [SerializeField] int _currentSlotIndex = -1;    // 현재 선택한 슬롯번호, -1이면 장착 무기 없는 상태

    [Header("발사 위치")]
    [SerializeField] Transform _firePoint;

    [Header("무기 패널")]
    [SerializeField] bool _isWeaponSlotPanel;
    [SerializeField] GameObject _weaponSlotPanel;

    // 마지막 발사 이후 흐른 시간을 저장
    float _timer;

    /// <summary>
    /// 현재 선택한 슬롯의 무기 런타임 데이터를 반환한다.
    /// 유효한 무기가 없으면 null을 반환한다.
    /// </summary>
    public WeaponRuntimeState CurrentWeapon
    {
        get
        {
            // 현재 슬롯이 장착 무기 범위를 벗어나면 무기가 없는 상태
            if (_currentSlotIndex < 0 || _currentSlotIndex >= _equippedWeapons.Count)
            {
                return null;
            }
            // 현재 슬롯의 무기 런타임 상태를 반환한다.
            return _equippedWeapons[_currentSlotIndex];
        }
    }

    private void Awake()
    {
        // 세이브 시스템 연결 전까지는 Inspector에 넣어둔 테스트용 무기로 전투 데이터를 초기화한다.
        InitializeWeapons(_testEquippedWeaponDatas);
    }

    /// <summary>
    /// 장착된 WeaponData를 이용해 전투용 런타임 무기 상태를 만든다.
    /// 현재는 테스트 데이터가 들어오고, 나중에는 세이브 데이터를 변환한 WeaponData가 들어온다.
    /// </summary>
    /// <param name="equippedWeaponDatas">슬롯 순서대로 정리된 장착 무기 원본 데이터</param>
    public void InitializeWeapons(WeaponData[] equippedWeaponDatas)
    {
        // 배열 자체가 null인 것은 정상적인 빈 슬롯이 아니라 초기화 데이터를 전달하지 않은 코드 오류다.
        if (equippedWeaponDatas == null)
        {
            throw new ArgumentNullException(nameof(equippedWeaponDatas),"장착 무기 데이터 배열이 전달되지 않았습니다.");
        }

        // 이전 전투에서 사용하던 런타임 무기 데이터를 제거한다.
        _equippedWeapons.Clear();

        // 아직 선택된 무기가 없는 상태로 초기화한다.
        _currentSlotIndex = -1;

        // 전달받은 장착 슬롯을 순서대로 확인한다.
        for (int slotIndex = 0; slotIndex < equippedWeaponDatas.Length; slotIndex++)
        {
            // 현재 슬롯에 장착된 WeaponData를 가져온다.
            WeaponData weaponData = equippedWeaponDatas[slotIndex];

            // 빈 슬롯도 리스트에 null로 추가해 슬롯 위치를 보존한다./ Bullet null Lazer 이런식으로
            if (weaponData == null)
            {
                _equippedWeapons.Add(null);
                continue;
            }

            // WeaponData의 기본 스탯을 복사해 이번 전투에서 사용할 런타임 상태를 만든다.
            WeaponRuntimeState runtimeState = new WeaponRuntimeState(weaponData);

            // 원래 장착 슬롯 위치에 런타임 무기를 추가한다.
            _equippedWeapons.Add(runtimeState);

            // 아직 선택된 무기가 없다면 처음 발견한 장착 무기를 현재 무기로 선택한다.
            if (_currentSlotIndex == -1)
            {
                _currentSlotIndex = slotIndex;
            }
        }

        // 초기화된 현재 무기를 UI에 표시한다.
        UpdateWeaponUI();
    }

    private void Update()
    {
        // 발사 후 흐른 시간을 누적한다.
        _timer += Time.deltaTime;

        // Z키를 누르는 동안 현재 무기로 공격한다.
        if (Input.GetKey(KeyCode.Z))
        {
            Fire();
        }

        // Tab키로 무기 선택 패널을 열고 닫는다.
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            // bool 값을 반전시키는 연산자.
            _isWeaponSlotPanel = !_isWeaponSlotPanel;
            _weaponSlotPanel.SetActive(_isWeaponSlotPanel);
        }

        // 무기 패널이 닫혀 있으면 슬롯 선택 입력을 처리하지 않는다.
        if (!_isWeaponSlotPanel)
        {
            return;
        }

        // 첫 번째 장착 무기를 선택
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SelectWeaponSlot(0);
        }
        // 두 번째 장착 무기를 선택
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SelectWeaponSlot(1);
        }
        // 세 번째 장착 무기를 선택
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            SelectWeaponSlot(2);
        }
    }

    public void SelectWeaponSlot(int slotIndex)
    {
        // 지정한 슬롯의 런타임 무기를 가져온다.
        WeaponRuntimeState weapon = GetWeaponAtSlot(slotIndex);

        // 존재하지 않는 슬롯이면 변경하지 않는다.
        if (weapon == null)
        {
            return;
        }

        // 현재 사용 중인 장착 슬롯을 변경한다.
        _currentSlotIndex = slotIndex;

        // 변경된 현재 무기의 아이콘을 UI에 표시한다.
        UpdateWeaponUI();

        // 무기 선택이 끝났으므로 패널을 닫는다.
        _isWeaponSlotPanel = false;
        _weaponSlotPanel.SetActive(false);
    }

    /// <summary>
    /// 현재 장착한 무기를 발사한다.
    /// </summary>
    public void Fire()
    {
        // 현재 슬롯의 런타임 무기 상태를 가져온다.
        WeaponRuntimeState weapon = CurrentWeapon;

        // 장착된 무기가 없다면 발사하지 않는다.
        if (weapon == null)
        {
            return;
        }

        // 현재 무기의 쿨타임이 지나지 않았다면 발사하지 않는다.
        if (_timer < weapon.CurrentCooltime)
        {
            return;
        }

        // WeaponData에 등록된 투사체 프리팹을 생성한다.
        GameObject projectileObject = Instantiate(weapon.Data.ProjectilePrefab, _firePoint.position, Quaternion.identity);

        // 생성한 투사체가 Bullet이라면 현재 공격력을 전달한다.
        if (projectileObject.TryGetComponent(out Bullet bullet))
        {
            bullet.SetDamage(weapon.CurrentDamage);
        }
        // 생성한 투사체가 Lazer라면 현재 공격력을 전달한다.
        else if (projectileObject.TryGetComponent(out Lazer lazer))
        {
            lazer.SetDamage(weapon.CurrentDamage);
        }

        // 발사했으므로 발사 타이머를 초기화한다.
        _timer = 0f;
    }

    /// <summary>
    /// 지정한 장착 슬롯 무기의 공격력을 강화한다.
    /// </summary>
    /// <param name="slotIndex">강화할 장착 슬롯 번호</param>
    /// <param name="value">증가시킬 공격력</param>
    public void AddDamage(int slotIndex, int value)
    {
        // 지정한 슬롯의 런타임 무기 상태를 가져와 weapon에 저장
        WeaponRuntimeState weapon = GetWeaponAtSlot(slotIndex);

        // 해당 슬롯에 무기가 없다면 강화하지 않는다.
        if (weapon == null)
        {
            return;
        }

        // WeaponRuntimeState의 현재 공격력만 증가시킨다.
        weapon.AddDamage(value);
    }

    /// <summary>
    /// 지정한 장착 슬롯 무기의 연사력을 강화한다.
    /// 실제 강화량은 해당 무기의 WeaponData가 결정한다.
    /// </summary>
    /// <param name="slotIndex">강화할 장착 슬롯 번호</param>
    public void AddFireRate(int slotIndex)
    {
        // 지정한 슬롯의 런타임 무기 상태를 가져와 weapon에 저장
        WeaponRuntimeState weapon = GetWeaponAtSlot(slotIndex);

        // 해당 슬롯에 무기가 없다면 강화하지 않는다.
        if (weapon == null)
        {
            return;
        }

        // 각 무기의 WeaponData에 설정된 강화량을 적용한다.
        weapon.AddFireRate();
    }

    /// <summary>
    /// 지정한 슬롯의 런타임 무기를 안전하게 가져온다.
    /// </summary>
    private WeaponRuntimeState GetWeaponAtSlot(int slotIndex)
    {
        // 슬롯 번호가 범위를 벗어나면 null을 반환한다.
        if (slotIndex < 0 || slotIndex >= _equippedWeapons.Count)
        {
            return null;
        }

        return _equippedWeapons[slotIndex];
    }

    /// <summary>
    /// 지정한 장착 슬롯의 무기 원본 데이터를 반환 하고 슬롯에 무기가 없으면 null을 반환한다.
    /// </summary>
    /// <param name="slotIndex">확인할 장착 슬롯 번호</param>
    public WeaponData GetWeaponDataAtSlot(int slotIndex)
    {
        // 현재 무기 가져와서 weapon에 저장
        WeaponRuntimeState weapon = GetWeaponAtSlot(slotIndex);

        // 해당 슬롯에 장착된 무기가 없으면 원본 데이터도 없다.
        if (weapon == null)
        {
            return null;
        }

        // 런타임 무기가 참조하고 있는 WeaponData를 반환한다.
        return weapon.Data;
    }

    /// <summary>
    /// 현재 선택된 무기의 아이콘을 WeaponUI에 전달한다.
    /// </summary>
    private void UpdateWeaponUI()
    {
        // 없으면 돌아가.
        if (_weaponUI == null) return;

        // 현재 선택된 슬롯의 런타임 무기를 가져온다.
        WeaponRuntimeState weapon = CurrentWeapon;

        // 현재 무기가 없다면 null을 전달해 UI 이미지를 숨긴다.
        if (weapon == null)
        {
            _weaponUI.UpdateCurrentWeapon(null);
            return;
        }

        // UI한테 현재 무기 아이콘 준다.
        _weaponUI.UpdateCurrentWeapon(weapon.Data.Icon);
    }


}

//private void Awake()
//{
//    // 씬에 남아 있을 수 있는 이전 런타임 데이터를 제거
//    _equippedWeapons.Clear();

//    // 테스트용으로 연결한 WeaponData를 런타임 상태로 변환한다.
//    foreach (WeaponData weaponData in _testEquippedWeaponDatas)
//    {
//        // 비어 있는 슬롯은 건너뛴다.
//        if (weaponData == null)
//        {
//            continue;
//        }

//        // WeaponData의 기본 스탯을 복사해 전투용 런타임 상태를 만든다.
//        WeaponRuntimeState runtimeState = new WeaponRuntimeState(weaponData);

//        // 런타임 상태를 장착 무기 리스트에 추가한다.
//        _equippedWeapons.Add(runtimeState);
//    }

//    // 장착된 무기가 하나 이상 있다면 첫 번째 슬롯을 선택한다.
//    if (_equippedWeapons.Count > 0)
//    {
//        _currentSlotIndex = 0;
//    }
//    else
//    {
//        // 장착 무기가 전혀 없다면 현재 슬롯도 없는 상태로 만든다.
//        _currentSlotIndex = -1;
//    }

//    // 전투 시작 시 UI업데이트해서 기본으로 껴져있는 첫 번째 장착 무기의 아이콘을 표시한다.
//    UpdateWeaponUI();
//}
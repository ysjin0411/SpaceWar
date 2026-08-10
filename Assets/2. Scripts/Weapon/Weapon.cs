using System;
using System.Collections.Generic;
using UnityEngine;

public enum WeaponType
{
    Bullet,
    Lazer
}

/// <summary>
/// 현재 캐릭터의 장착 무기, 무기 변경, 발사를 관리한다.
/// WeaponUI와 WeaponSlotPanel은 Stage 씬에 있으므로 StageManager가 InitializeUI()를 통해 전달한다.
/// </summary>
public class Weapon : MonoBehaviour
{
    [Header("테스트용 장착 무기")]
    [Tooltip("세이브 연결 전 테스트할 때만 사용한다.")]
    [SerializeField] WeaponData[] _testEquippedWeaponDatas;

    [Header("장착 무기 런타임 데이터")]
    [SerializeField] List<WeaponRuntimeState> _equippedWeapons = new();

    [Header("현재 무기 슬롯")]
    [Tooltip("-1이면 현재 사용할 수 있는 무기가 없는 상태")]
    [SerializeField] int _currentSlotIndex = -1;

    [Header("캐릭터 프리팹 내부 참조")]
    [Tooltip("투사체가 생성될 캐릭터 프리팹 내부 위치")]
    [SerializeField] Transform _firePoint;

    // StageManager가 런타임에 전달하는 Stage UI
    WeaponUI _weaponUI;
    GameObject _weaponSlotPanel;

    // 무기 선택 패널이 현재 열려 있는지 저장한다.
    bool _isWeaponSlotPanelOpen;

    // 마지막 발사 이후 흐른 시간
    float _timer;

    /// <summary>
    /// 현재 선택한 슬롯의 무기 상태를 반환한다. 장착 무기가 없다면 null을 반환한다.
    /// </summary>
    public WeaponRuntimeState CurrentWeapon
    {
        get
        {
            if (_currentSlotIndex < 0 || _currentSlotIndex >= _equippedWeapons.Count)
            {
                return null;
            }

            return _equippedWeapons[_currentSlotIndex];
        }
    }

    private void Awake()
    {
        // FirePoint는 캐릭터 프리팹 내부 참조이므로 프리팹에 반드시 연결되어 있어야 한다.
        if (_firePoint == null)
        {
            throw new InvalidOperationException($"{name}의 Weapon에 FirePoint가 연결되지 않았습니다.");
        }

        // 세이브 시스템 연결 전 테스트 데이터가 있다면 사용한다.
        if (_testEquippedWeaponDatas != null && _testEquippedWeaponDatas.Length > 0)
        {
            InitializeWeapons(_testEquippedWeaponDatas);
        }
        else
        {
            // 테스트 무기가 없다면 빈 무기 상태로 시작한다.
            _equippedWeapons.Clear();
            _currentSlotIndex = -1;
        }
    }

    /// <summary>
    /// StageManager가 Stage의 무기 UI를 전달한다.
    /// </summary>
    /// <param name="weaponUI">현재 무기 아이콘을 표시할 WeaponUI</param>
    /// <param name="weaponSlotPanel">Tab 키로 열고 닫을 무기 슬롯 패널</param>
    public void InitializeUI(WeaponUI weaponUI, GameObject weaponSlotPanel)
    {
        if (weaponUI == null)
        {
            throw new ArgumentNullException(nameof(weaponUI));
        }

        if (weaponSlotPanel == null)
        {
            throw new ArgumentNullException(nameof(weaponSlotPanel));
        }

        _weaponUI = weaponUI;
        _weaponSlotPanel = weaponSlotPanel;

        // Stage 시작 시 무기 선택 패널을 닫는다.
        _isWeaponSlotPanelOpen = false;
        _weaponSlotPanel.SetActive(false);

        // 현재 장착 무기 상태를 UI에 반영한다.
        UpdateWeaponUI();
    }

    /// <summary>
    /// 슬롯 순서대로 전달받은 WeaponData를
    /// 전투용 WeaponRuntimeState로 변환한다.
    /// </summary>
    public void InitializeWeapons(WeaponData[] equippedWeaponDatas)
    {
        if (equippedWeaponDatas == null)
        {
            throw new ArgumentNullException(nameof(equippedWeaponDatas), "장착 무기 데이터 배열이 전달되지 않았습니다.");
        }

        // 이전 전투에서 사용하던 런타임 무기를 제거한다.
        _equippedWeapons.Clear();

        _currentSlotIndex = -1;

        for (int slotIndex = 0; slotIndex < equippedWeaponDatas.Length; slotIndex++)
        {
            WeaponData weaponData = equippedWeaponDatas[slotIndex];

            // 빈 슬롯도 null로 추가하여 슬롯 위치를 보존한다.
            if (weaponData == null)
            {
                _equippedWeapons.Add(null);
                continue;
            }

            WeaponRuntimeState runtimeState = new WeaponRuntimeState(weaponData);

            _equippedWeapons.Add(runtimeState);

            // 처음 발견한 장착 무기를 현재 무기로 선택한다.
            if (_currentSlotIndex == -1)
            {
                _currentSlotIndex = slotIndex;
            }
        }

        _timer = 0f;

        UpdateWeaponUI();
    }

    private void Update()
    {
        _timer += Time.deltaTime;

        // Z키를 누르는 동안 현재 무기를 발사한다.
        if (Input.GetKey(KeyCode.Z))
        {
            Fire();
        }

        // Tab 입력 시 무기 선택 패널을 연다.
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleWeaponSlotPanel();
        }

        // 패널이 닫혀 있으면 슬롯 선택 입력을 처리하지 않는다.
        if (!_isWeaponSlotPanelOpen)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SelectWeaponSlot(0);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SelectWeaponSlot(1);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            SelectWeaponSlot(2);
        }
    }

    /// <summary>
    /// Stage의 무기 슬롯 패널을 열거나 닫는다.
    /// </summary>
    private void ToggleWeaponSlotPanel()
    {
        // StageManager가 아직 UI를 전달하지 않았다면 패널 입력을 처리하지 않는다.
        if (_weaponSlotPanel == null)
        {
            return;
        }

        _isWeaponSlotPanelOpen = !_isWeaponSlotPanelOpen;

        _weaponSlotPanel.SetActive(_isWeaponSlotPanelOpen);
    }

    /// <summary>
    /// 지정한 장착 슬롯의 무기를 현재 무기로 선택한다.
    /// </summary>
    public void SelectWeaponSlot(int slotIndex)
    {
        WeaponRuntimeState weapon = GetWeaponAtSlot(slotIndex);

        // 빈 슬롯이거나 잘못된 슬롯이면 변경하지 않는다.
        if (weapon == null)
        {
            return;
        }

        _currentSlotIndex = slotIndex;

        UpdateWeaponUI();

        // 무기 선택을 완료했으므로 패널을 닫는다.
        _isWeaponSlotPanelOpen = false;

        if (_weaponSlotPanel != null)
        {
            _weaponSlotPanel.SetActive(false);
        }
    }

    /// <summary>
    /// 현재 선택한 무기를 발사한다.
    /// </summary>
    public void Fire()
    {
        WeaponRuntimeState weapon = CurrentWeapon;

        if (weapon == null)
        {
            return;
        }

        if (_timer < weapon.CurrentCooltime)
        {
            return;
        }

        if (weapon.Data.ProjectilePrefab == null)
        {
            throw new InvalidOperationException($"무기 '{weapon.Data.WeaponName}'에 ProjectilePrefab이 없습니다.");
        }

        GameObject projectileObject = Instantiate(weapon.Data.ProjectilePrefab, _firePoint.position, _firePoint.rotation);

        // 현재 공격력을 생성된 투사체에 전달한다.
        if (projectileObject.TryGetComponent(out Bullet bullet))
        {
            bullet.SetDamage(weapon.CurrentDamage);
        }
        else if (projectileObject.TryGetComponent(out Lazer lazer))
        {
            lazer.SetDamage(weapon.CurrentDamage);
        }

        _timer = 0f;
    }

    /// <summary>
    /// 지정한 슬롯 무기의 공격력을 증가시킨다.
    /// </summary>
    public void AddDamage(int slotIndex, int value)
    {
        WeaponRuntimeState weapon = GetWeaponAtSlot(slotIndex);

        if (weapon == null || value <= 0)
        {
            return;
        }

        weapon.AddDamage(value);
    }

    /// <summary>
    /// 지정한 슬롯 무기의 연사력을 증가시킨다.
    /// </summary>
    public void AddFireRate(int slotIndex)
    {
        WeaponRuntimeState weapon = GetWeaponAtSlot(slotIndex);

        if (weapon == null)
        {
            return;
        }

        weapon.AddFireRate();
    }

    /// <summary>
    /// 지정한 슬롯의 런타임 무기를 안전하게 반환한다.
    /// </summary>
    private WeaponRuntimeState GetWeaponAtSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _equippedWeapons.Count)
        {
            return null;
        }

        return _equippedWeapons[slotIndex];
    }

    /// <summary>
    /// 지정한 슬롯에 장착된 WeaponData를 반환한다.
    /// Upgrader가 무기 증강 대상을 확인할 때 사용한다.
    /// </summary>
    public WeaponData GetWeaponDataAtSlot(int slotIndex)
    {
        WeaponRuntimeState weapon = GetWeaponAtSlot(slotIndex);

        return weapon?.Data;
    }

    /// <summary>
    /// 현재 무기 아이콘을 Stage의 WeaponUI에 표시한다.
    /// </summary>
    private void UpdateWeaponUI()
    {
        // StageManager가 아직 WeaponUI를 전달하지 않았다면
        // 런타임 무기 데이터만 갱신하고 UI는 건너뛴다.
        if (_weaponUI == null)
        {
            return;
        }

        WeaponRuntimeState weapon = CurrentWeapon;

        Sprite currentIcon = weapon != null ? weapon.Data.Icon : null;

        _weaponUI.UpdateCurrentWeapon(currentIcon);
    }
}
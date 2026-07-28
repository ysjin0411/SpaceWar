using UnityEngine;

// 1. 무기 분리 작업
// 2. 이동 속력/공격력 업그레이드

public enum WeaponType
{
    Bullet,
    Lazer
}

/// <summary>
/// 무기 교체, 공격
/// </summary>
public class Weapon : MonoBehaviour
{
    [Header("컴포넌트")]
    [SerializeField] WeaponUI _weaponUI;

    [Header("총알프리팹")]
    [SerializeField] GameObject[] _weaponPrefabs;

    [Header("무기슬롯총알아이콘")]
    [SerializeField] GameObject _bulletIcon;
    [SerializeField] GameObject _lazerIcon;
    int _currentWeaponIndex = 0;

    [Header("발사 위치")]
    [SerializeField] Transform _firePoint;
    Vector2 _dir;

    [Header("무기 패널")]
    [SerializeField] bool _isWeaponSlotPanel;
    [SerializeField] GameObject _weaponSlotPanel;

    [Header("무기 쿨타임")]
    [SerializeField] float[] _weaponCooltimes;
    [SerializeField] float _timer;

    public int CurrentWeaponIndex => _currentWeaponIndex;


    private void Awake()
    {
        _currentWeaponIndex = 0;
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        // 공격
        if (Input.GetKey(KeyCode.Z))
        {
                Fire();
        }

        // 무기 슬롯 열고 닫기
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (_isWeaponSlotPanel == true)
            {
                _isWeaponSlotPanel = false;
                _weaponSlotPanel.SetActive(false);
            }
            else
            {
                _isWeaponSlotPanel = true;
                _weaponSlotPanel.SetActive(true);
            }
        }

        // 1번 무기 선택
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            if (_isWeaponSlotPanel == true)
            {
                // 무기 변경
                _currentWeaponIndex = 0;
                // 현재무기아이콘갱신
                _weaponUI.UpdateCurrentWeapon(_currentWeaponIndex);
                // 창닫기
                _weaponSlotPanel.SetActive(false);
                _isWeaponSlotPanel = false;
                _bulletIcon.SetActive(true);
                _lazerIcon.SetActive(false);
            }
        }

        // 2번 무기 선택
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            if (_isWeaponSlotPanel == true)
            {
                // 무기변경
                _currentWeaponIndex = 1;
                // 현재무기아이콘갱신
                _weaponUI.UpdateCurrentWeapon(_currentWeaponIndex);
                // 창닫기
                _weaponSlotPanel.SetActive(false);
                _isWeaponSlotPanel = false;
                _bulletIcon.SetActive(false);
                _lazerIcon.SetActive(true);
            }
        }
    }

    /// <summary>
    /// 공격
    /// </summary>
    public void Fire()
    {
        // 지금 내 무기의 쿨타임
        float currentCooltime = _weaponCooltimes[_currentWeaponIndex];
        // 쿨타임 다 돌았는지 체크
        if (_timer >= currentCooltime)
        {
        // 총알 게임오브젝트 복제 생성
        Instantiate(_weaponPrefabs[_currentWeaponIndex], _firePoint.position, Quaternion.identity);
        // 발사후 쿨초
        _timer = 0f;
        }
    }

   

}

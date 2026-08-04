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
	[Header("무기 런타임 데이터")]
	[SerializeField] int[] _weaponDamages;
	[SerializeField] float[] _weaponCooltimes;

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

    float _timer;      // 무기 타이머

    public int CurrentWeaponIndex => _currentWeaponIndex;


    private void Awake()
    {
        // 현재 무기 인덱스 읽기
        _currentWeaponIndex = 0;

		// _weaponPrefabs에 무기가 들어 있는만큼 공격력 저장공간도 그만큼 만들기
		_weaponDamages = new int[_weaponPrefabs.Length];

        // 0번무기 데미지 받아오기
		_weaponDamages[0] = _weaponPrefabs[0].GetComponent<Bullet>().BulletDamage;

        // 1번무기 데미지 받아오기
		_weaponDamages[1] = _weaponPrefabs[1].GetComponent<Lazer>().LazerDamage;
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

        // 아직 쿨이 안됐다면 함수종료
        if (_timer < currentCooltime) return;

        // 오브젝트를 생성하고 생성한 오브젝트를 projectileObject에 저장
        GameObject projectileObject = Instantiate(_weaponPrefabs[_currentWeaponIndex], _firePoint.position, Quaternion.identity);

        // 내 무기가 0번 무기일때
		if (_currentWeaponIndex == 0)
		{
            // 방금 생성한 오브젝트변수에 Bullet컴포넌트 적용해서 bullet에 초기값 적용
			Bullet bullet = projectileObject.GetComponent<Bullet>();

            // Bullet컴포넌트 적용된 불렛에 적용중인 현재 공격력 새로 입히기
			bullet.SetDamage(_weaponDamages[0]);
		}
        // 내 무기가 1번 무기일때
		else if (_currentWeaponIndex == 1)  
		{
			// 방금 생성한 오브젝트변수에 Lazer컴포넌트 적용해서 lazer에 초기값 적용
			Lazer lazer = projectileObject.GetComponent<Lazer>();

			// Lazer컴포넌트 적용된 레이저에 적용중인 현재 공격력 새로 입히기
			lazer.SetDamage(_weaponDamages[1]);
		}
        // 발사후 쿨초
        _timer = 0f;
    }

    /// <summary>
    /// 공격력 증가
    /// </summary>
    /// <param name="weaponIndex">무기번호</param>
    /// <param name="value">증가량</param>
	public void AddDamage(int weaponIndex, int value)
	{
        // _weaponDamages[N] 에 해당하는 애한테 함수 호출될때마다 value만큼 더해줌.
		_weaponDamages[weaponIndex] += value;
	}

    /// <summary>
    /// 연사력 증가
    /// </summary>
    /// <param name="weaponIndex">무기번호</param>
    /// <param name="value">강화정도</param>
	public void AddFireRate(int weaponIndex, float value)
	{
        // _weaponCooltimes[N]에 해당하는 애한테 함수 호출될때마다 cooltime을 value만큼 줄여줌.
		_weaponCooltimes[weaponIndex] -= value;

        // 그치만 cooltimes의 최소값은 0.05임 > 쿨타임이 0이나 음수가 되는걸 방지
		_weaponCooltimes[weaponIndex] = Mathf.Max(_weaponCooltimes[weaponIndex], 0.05f);
	}

}

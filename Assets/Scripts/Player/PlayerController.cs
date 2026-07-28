using UnityEngine;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour
{
    [Header("컴포넌트")]
    [SerializeField] HPUI _hpUI;                // 체력UI 컴포넌트
    [SerializeField] Weapon _weapon;            // 무기 컴포넌트

    [Header("프리팹")]
    [SerializeField] GameObject _bulletPrefab;  // 불렛 프리팹

    [Header("이동")]
    [SerializeField] float _moveSpeed = 5f;     // 이동속도

    [Header("플레이어 체력")]
    [SerializeField] int _maxHp;                // 최대 체력
    [SerializeField] int _currentHp;            // 현재 체력
    public int MaxHp => _maxHp;                 // 최대 체력 프로퍼티
    public int CurrentHp => _currentHp;         // 현재 체력 프로퍼티

    [Header("이동 제한")]                       // 화면 이동제한 수치
    [SerializeField] float _minX = -8f;
    [SerializeField] float _maxX = 8f;
    [SerializeField] float _minY = -4f;
    [SerializeField] float _maxY = 4f;

    Vector2 _dir;                            // 방향 벡터



    Rigidbody2D _rigid;                      // 리지드바디 컴포넌트

    private void Start()
    {
        _currentHp = _maxHp;
    }

    private void Awake()
    {
        _rigid = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        float h = Input.GetAxisRaw("Horizontal");       // float h에 getaxisraw(음수)값으로 입력받는다.
        float v = Input.GetAxisRaw("Vertical");         // float v에 getaxisraw(음수)값으로 입력받는다.

        _dir = new Vector2(h, v).normalized;            // _dir은 새로운 벡터 h와 v를 입력받아 저장시킨다.normailized는, 벡터의 크기를 1로 보정시키는 용
        Vector2 pos = _rigid.position;                  // 벡터2 pos에 리지드에서 가져온 나의 위치를 저장
        pos.x = Mathf.Clamp(pos.x, _minX, _maxX);       // pos.x는 (_minX와 _maxX의 사이로만 한정되게 국한시킨다)
        pos.y = Mathf.Clamp(pos.y, _minY, _maxY);       // pos.y는 (_minY와 _maxY의 사이로만 한정되게 국한시킨다)

        _rigid.position = pos;                          // 리지드바디 포지션(나의 위치) 를 방금 받아온 pos로 바꾼다.
    }

    private void FixedUpdate()
    {
        _rigid.linearVelocity = _dir * _moveSpeed;
    }

    /// <summary>
    /// 피격
    /// </summary>
    /// <param name="damage">피격량</param>
    public void TakeHit(int damage)
    {
        _currentHp -= damage;
        if (_currentHp < 0)
        {
            Die();
            return;
        }
        _hpUI.UpdateHpUI();
        Debug.Log($"입은 데미지: {damage}");
    }

    /// <summary>
    /// 사망
    /// </summary>
    private void Die()
    {
        Debug.Log("사망");
        Time.timeScale = 0f;
    }

    /// <summary>
    /// 이동속도 증가
    /// </summary>
    /// <param name="value">이동속도 증가량</param>
    public void AddMoveSpeed(float value)
    {
        _moveSpeed += value;
    }
}


/*
 using UnityEngine;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour
{
    [Header("컴포넌트")]
    [SerializeField] HPUI _hpUI;
    [SerializeField] GameObject _weaponSlotPanel;
    [SerializeField] bool _isWeaponSlotPanel;

    [Header("무기")]
    [SerializeField] Weapon _weapon;

    [Header("총알프리팹")]
    [SerializeField] GameObject[] _weaponPrefabs;
    [SerializeField] GameObject _bulletIcon;
    [SerializeField] GameObject _lazerIcon;
    int _currentWeaponIndex = 0;

    [Header("이동")]
    [SerializeField] float _moveSpeed = 5f;

    [Header("이동 제한")]
    [SerializeField] float _minX = -8f;
    [SerializeField] float _maxX = 8f;
    [SerializeField] float _minY = -4f;
    [SerializeField] float _maxY = 4f;

    [Header("플레이어 체력")]
    [SerializeField] int _maxHp;
    [SerializeField] int _currentHp;

    [Header("현재 총")]
    [SerializeField] GameObject _bulletPrefab;
    [SerializeField] float _cooltime;
    [SerializeField] float _timer;

    [Header("발사 위치")]
    [SerializeField] Transform _firePoint;
    Vector2 _dir;



    Rigidbody2D _rigid;

    private void Start()
    {
        _currentHp = _maxHp;
    }

    private void Awake()
    {
        _rigid = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        _dir = new Vector2(h, v).normalized;



        Vector2 pos = _rigid.position;
        pos.x = Mathf.Clamp(pos.x, _minX, _maxX);
        pos.y = Mathf.Clamp(pos.y, _minY, _maxY);

        _rigid.position = pos;

        // 공격
        if (Input.GetKey(KeyCode.Z))
        {
            if (_timer > _cooltime)
            {
                _weapon.Fire();
                _timer = 0;
            }
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
            if(_isWeaponSlotPanel == true)
            {
                _currentWeaponIndex = 0;
                _weaponSlotPanel.SetActive(false);
                _isWeaponSlotPanel = false;
                _cooltime = 0.1f;
                _bulletIcon.SetActive(true);
                _lazerIcon.SetActive(false);
            }
        }

        // 2번 무기 선택
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            if (_isWeaponSlotPanel == true)
            {
                _cooltime = -0.2f;
                _currentWeaponIndex = 1;
                _weaponSlotPanel.SetActive(false);
                _isWeaponSlotPanel = false;
                _bulletIcon.SetActive(false);
                _lazerIcon.SetActive(true);
            }
        }
    }

    private void FixedUpdate()
    {
        _rigid.linearVelocity = _dir * _moveSpeed;
    }

    public void TakeHit(int damage)
    {
        _currentHp -= damage;
        if (_currentHp < 0)
        {
            Die();
            return;
        }
        _hpUI.UpdateHpUI();
        Debug.Log($"입은 데미지: {damage}");
    }

    private void Die()
    {
        Debug.Log("사망");
        Time.timeScale = 0f;
    }

    public void Fire()
    {
        Instantiate(_weaponPrefabs[_currentWeaponIndex],_firePoint.position,Quaternion.identity);
    }
}

 
 
 */
using UnityEngine;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour
{
    [Header("컴포넌트")]
    [SerializeField] HPUI _hpUI;
    [SerializeField] GameObject _weaponSlotPanel;
    [SerializeField] bool _isWeaponSlotPanel;

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
                Fire();
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

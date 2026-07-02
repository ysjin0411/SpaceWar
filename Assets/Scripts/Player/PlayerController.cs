using UnityEngine;

public class PlayerController : MonoBehaviour
{
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

    [Header("총알")]
    [SerializeField] GameObject _bulletPrefab;
    [SerializeField] float _cooltime;
    [SerializeField] float _timer;

    [Header("발사 위치")]
    [SerializeField] Transform _firePoint;

    Rigidbody2D _rigid;

    private void Awake()
    {
        _rigid = GetComponent<Rigidbody2D>();
    }

    private void FixedUpdate()
    {
        _timer += Time.deltaTime;
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector2 dir = new Vector2(h, v).normalized;

        _rigid.linearVelocity = dir * _moveSpeed;

        Vector2 pos = _rigid.position;
        pos.x = Mathf.Clamp(pos.x, _minX, _maxX);
        pos.y = Mathf.Clamp(pos.y, _minY, _maxY);

        _rigid.position = pos;

        if (Input.GetKey(KeyCode.Z))
        {
            if (_timer > _cooltime)
            {
                Fire();
                _timer = 0;
            }
        }
    }

    public void TakeHit(int damage)
    {
        _currentHp -= damage;
        Debug.Log($"입은 데미지: {damage}");
        if (_currentHp < 0)
        {
            Die();
        }
    }

    public void Die()
    {
        Debug.Log("사망");
        Time.timeScale = 0f;
    }

    public void Fire()
    {
        Instantiate(_bulletPrefab,_firePoint.position,Quaternion.identity);
    }
}

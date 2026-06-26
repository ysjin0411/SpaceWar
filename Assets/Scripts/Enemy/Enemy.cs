using UnityEngine;

public class Enemy : MonoBehaviour
{
    [Header("체력")]
    [SerializeField] int _maxHp = 10;
    int _currentHp;

    [Header("이동")]
    [SerializeField] float _moveSpeed = 2f;
    [SerializeField] float _chaseDistance = 6f;
    [SerializeField] float _stopDistance = 3f;

    [Header("공격")]
    [SerializeField] GameObject _enemyBulletPrefab;
    [SerializeField] Transform _firePoint;
    [SerializeField] float _fireInterval = 1.5f;

    Transform _player;
    float _fireTimer;

    private void Awake()
    {
        _currentHp = _maxHp;
    }

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");

        if (playerObj != null)
        {
            _player = playerObj.transform;
        }
    }

    private void Update()
    {
        if (_player == null) return;

        Move();
        Attack();
    }

    void Move()
    {
        float dist = Vector2.Distance(transform.position, _player.position);

        // 플레이어와 멀면 그냥 왼쪽으로 이동
        if (dist > _chaseDistance)
        {
            transform.Translate(Vector2.left * _moveSpeed * Time.deltaTime);
        }
        // 가까워지면 플레이어 추적
        else if (dist > _stopDistance)
        {
            Vector2 dir = (_player.position - transform.position).normalized;
            transform.Translate(dir * _moveSpeed * Time.deltaTime);
        }
        // 너무 가까우면 멈추고 공격
        else
        {
            // 정지
        }

        if (transform.position.x < -12f)
        {
            Destroy(gameObject);
        }
    }

    void Attack()
    {
        _fireTimer += Time.deltaTime;

        if (_fireTimer >= _fireInterval)
        {
            Fire();
            _fireTimer = 0f;
        }
    }

    void Fire()
    {
        if (_enemyBulletPrefab == null || _firePoint == null) return;

        Instantiate(_enemyBulletPrefab, _firePoint.position, Quaternion.identity);
    }

    public void TakeHit(int damage)
    {
        _currentHp -= damage;

        if (_currentHp <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Destroy(gameObject);
    }
}
using UnityEngine;

public enum MoveType
{
    DownToUp,
    RightToLeft
}

public class FastEnemy : MonoBehaviour
{
    [Header("컴포넌트")]
    [SerializeField] GameManager _gameManager;

    [Header("프리팹")]
    [SerializeField] GameObject _goldPrefab;
 
    [Header("체력")]
    [SerializeField] int _maxHp;
    [SerializeField] int _currentHp;

    [Header("이동")]
    [SerializeField] float _moveSpeed = 2f;

    [Header("공격")]
    [SerializeField] GameObject _enemyBulletPrefab;
    [SerializeField] Transform _firePoint;
    [SerializeField] float _coolTime;
    [SerializeField] float _fireDist;

    [Header("몸박데미지")]
    [SerializeField] int _crashDamage;

    [Header("보상")]
    [SerializeField] float _getExp;
    [SerializeField] int _goldDropCount;            // 골드를 떨어뜨릴 갯수

    // 의존성들
    PlayerController _playerController;



    [SerializeField] MoveType _moveType;
    Transform _player;
    public bool _isFired;

    public void InitPlayerController(PlayerController playerController)
    {
        _playerController = playerController;
    }
    public void InitGameManager(GameManager gameManager)
    {
        _gameManager = gameManager;
    }
    public void SetMoveType(MoveType moveType)
    {
        _moveType = moveType;
    }

    private void Start()
    {
        _currentHp = _maxHp;
        _isFired = false;
    }

    private void Update()
    {
        switch (_moveType)
        {
            case MoveType.RightToLeft:
                RightToLeft();
                break;
            case MoveType.DownToUp:
                DownToUp();
                break;
        }
    }


    private void Fire()
    {
        GameObject enemyBullet = Instantiate(_enemyBulletPrefab, _firePoint.position, Quaternion.identity);

        EnemyBullet bullet = enemyBullet.GetComponent<EnemyBullet>();

        bullet.Init(_playerController);
    }

    public void TakeHit(int damage)
    {
        _currentHp -= damage;

        if (_currentHp <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        _gameManager.BonusGauge();

        // 드랍할 갯수만큼 생성반복
        for (int i = 0; i < _goldDropCount; i++)
        {
            // 몬스터의 현재 위치에 골드 프리팹을 생성합니다.
            Instantiate(_goldPrefab, transform.position, Quaternion.identity);
        }
        Destroy(gameObject);
    }

    public void OnTriggerEnter2D(Collider2D collision)
    {
        PlayerController player = collision.GetComponent<PlayerController>();
        if (player != null)
        {
            player.TakeHit(_crashDamage);
            Destroy(gameObject);
        }

    }

    public void RightToLeft()
    {
        gameObject.transform.Translate(Vector2.left * _moveSpeed * Time.deltaTime, Space.World);

        if (gameObject.transform.position.x <= 0f && !_isFired)
        {
            Fire();
            _isFired = true;
        }

        if (transform.position.x < -12f)
        {
            Destroy(gameObject);
        }
    }

    public void DownToUp()
    {
        gameObject.transform.Translate(Vector2.up * _moveSpeed * Time.deltaTime, Space.World);

        if (gameObject.transform.position.y >= 0f && !_isFired)
        {
            Fire();
            _isFired = true;
        }

        if (transform.position.y > 6f)
        {
            Destroy(gameObject);
        }
    }
}
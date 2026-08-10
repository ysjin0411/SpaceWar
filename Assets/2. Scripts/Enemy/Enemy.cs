using UnityEngine;

public abstract class Enemy : MonoBehaviour
{
    [Header("체력")]
    [SerializeField] int _maxHp;

    [Header("충돌")]
    [SerializeField] int _crashDamage;

    [Header("골드 보상")]
    [SerializeField] GameObject _goldPrefab;
    [SerializeField] int _goldDropCount;        // 골드 뿌리는 갯수

    protected PlayerController PlayerController { get; private set; }
    protected GameManager GameManager { get; private set; }

    int _currentHp;
    bool _isDead;

    protected virtual void Start()
    {
        _currentHp = _maxHp;
    }

    public void Init(
        PlayerController playerController,
        GameManager gameManager)
    {
        PlayerController = playerController;
        GameManager = gameManager;
    }

    public void TakeHit(int damage)
    {
        if (_isDead || damage <= 0)
        {
            return;
        }

        _currentHp -= damage;

        if (_currentHp <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        if (_isDead)
        {
            return;
        }

        _isDead = true;

        GameManager?.BonusGauge();

        DropGold();
        OnDied();

        Destroy(gameObject);
    }

    /// <summary>
    /// 골드 프리팹을 DropCount만큼 생성한다.
    /// </summary>
    private void DropGold()
    {
        for (int i = 0; i < _goldDropCount; i++)
        {
            Instantiate(_goldPrefab,transform.position,Quaternion.identity);
        }
    }

    protected virtual void OnDied()
    {

    }

    protected virtual void OnTriggerEnter2D(Collider2D collision)
    {
        PlayerController player = collision.GetComponent<PlayerController>();

        if (player == null)
        {
            return;
        }

        player.TakeHit(_crashDamage);

        // 플레이어와 충돌하여 사라진 적은
        // 처치한 것이 아니므로 골드를 주지 않는다.
        Destroy(gameObject);
    }
}
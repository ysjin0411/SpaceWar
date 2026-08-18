using System;
using UnityEngine;

/// <summary>
/// 모든 보스가 상속받는 공통 부모 클래스.
/// 일반 적 Enemy와는 별개의 생명주기를 가지며,
/// 체력, 피격, 플레이어 충돌, 사망 이벤트를 담당한다.
/// </summary>
public abstract class BossEnemy : MonoBehaviour, IDamageable
{
    [Header("보스 체력 설정")]
    [Tooltip("보스의 최대 체력")]
    [Min(1)]
    [SerializeField] int _maxHp = 100;

    [Header("플레이어 충돌 설정")]
    [Tooltip("보스와 플레이어가 충돌했을 때 주는 피해")]
    [Min(0)]
    [SerializeField] int _crashDamage;

    //[Header("사망 처리 설정")]
    //[Tooltip("사망 처리 후 보스 오브젝트를 제거하기까지의 시간")]
    //[Min(0f)]
    //[SerializeField] float _destroyDelay;

    [Header("보스 런타임 데이터")]
    [Tooltip("현재 보스 체력")]
    [SerializeField] int _currentHp;

    [Tooltip("현재 보스가 사망 처리됐는지 확인")]
    [SerializeField] bool _isDead;

    /// <summary>
    /// 보스 체력이 변경되면 현재 체력과 최대 체력을 전달한다.
    /// BossHPUI가 이 이벤트를 구독한다.
    /// </summary>
    public event Action<int, int> HealthChanged;

    /// <summary>
    /// 보스가 처치됐을 때 처치된 보스 자신을 전달한다.
    /// BossEncounterManager가 이 이벤트를 구독한다.
    /// </summary>
    public event Action<BossEnemy> Defeated;

    // 보스별 이동 및 공격 패턴에서 현재 플레이어를 사용한다.
    protected PlayerController TargetPlayer { get; private set; }

    // 보스별 보상 처리가 필요한 경우 현재 GameManager를 사용한다.
    protected GameManager GameManager { get; private set; }

    // 외부에서 최대 체력을 읽을 수 있게 제공한다.
    public int MaxHp => _maxHp;

    // 외부에서 현재 체력을 읽을 수 있게 제공한다.
    public int CurrentHp => _currentHp;

    // 외부에서 사망 여부를 읽을 수 있게 제공한다.
    public bool IsDead => _isDead;

    // 유도 투사체가 보스를 추적할 때 사용할 Transform이다.
    public Transform TargetTransform => transform;

    protected virtual void Awake()
    {
        // 보스가 생성되면 최대 체력으로 시작한다.
        _currentHp = _maxHp;

        // 새로 생성된 보스는 살아 있는 상태다.
        _isDead = false;
    }

    /// <summary>
    /// BossEncounterManager가 보스를 생성한 직후 호출한다.
    /// 보스 패턴에서 사용할 현재 플레이어와 GameManager를 전달한다.
    /// </summary>
    public void Initialize(
        PlayerController playerController,
        GameManager gameManager)
    {
        if (playerController == null)
        {
            throw new ArgumentNullException(nameof(playerController));
        }

        if (gameManager == null)
        {
            throw new ArgumentNullException(nameof(gameManager));
        }

        TargetPlayer = playerController;
        GameManager = gameManager;

        // 이벤트 연결 이후 최초 체력을 UI에 전달한다.
        HealthChanged?.Invoke(_currentHp, _maxHp);

        // 보스별 초기화 작업을 실행한다.
        OnBossInitialized();
    }

    /// <summary>
    /// 플레이어 공격으로 전달받은 피해를 적용한다.
    /// </summary>
    public void TakeHit(int damage)
    {
        // 사망한 보스는 추가 피해를 받지 않는다.
        if (_isDead)
        {
            return;
        }

        // 0 이하의 피해는 처리하지 않는다.
        if (damage <= 0)
        {
            return;
        }

        _currentHp = Mathf.Max(0, _currentHp - damage);

        // 변경된 체력을 보스 체력 UI에 전달한다.
        HealthChanged?.Invoke(_currentHp, _maxHp);

        // 보스별 피격 연출을 실행한다.
        OnBossDamaged(damage);

        if (_currentHp <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// 보스 사망을 한 번만 처리한다.
    /// </summary>
    private void Die()
    {
        if (_isDead)
        {
            return;
        }

        _isDead = true;

        // 사망 후 플레이어 공격과 충돌하지 않도록 Collider를 비활성화한다.
        SetCollidersEnabled(false);

        // Rigidbody2D를 사용 중이라면 이동을 정지시킨다.
        if (TryGetComponent(out Rigidbody2D body))
        {
            body.linearVelocity = Vector2.zero;
        }

        // 보스별 폭발, 애니메이션, 사운드 처리를 실행한다.
        OnBossDefeated();

        // BossEncounterManager에 보스 처치를 알린다.
        Defeated?.Invoke(this);

        // 사망 연출 시간을 확보한 뒤 보스 오브젝트를 제거한다.
        Destroy(gameObject);
    }

    /// <summary>
    /// 보스와 플레이어가 몸으로 충돌했을 때 피해를 준다.
    /// 일반 적과 달리 보스 자신은 충돌 후 제거되지 않는다.
    /// </summary>
    protected virtual void OnTriggerEnter2D(Collider2D collision)
    {
        if (_isDead)
        {
            return;
        }

        PlayerController player =
            collision.GetComponentInParent<PlayerController>();

        if (player == null)
        {
            return;
        }

        player.TakeHit(_crashDamage);
    }

    /// <summary>
    /// 보스 자신과 자식 오브젝트에 있는 Collider를 변경한다.
    /// </summary>
    private void SetCollidersEnabled(bool enabled)
    {
        Collider2D[] colliders =
            GetComponentsInChildren<Collider2D>();

        foreach (Collider2D bossCollider in colliders)
        {
            bossCollider.enabled = enabled;
        }
    }

    /// <summary>
    /// 보스별 초기화가 필요하면 자식 클래스에서 재정의한다.
    /// </summary>
    protected virtual void OnBossInitialized()
    {
    }

    /// <summary>
    /// 보스별 피격 연출이 필요하면 자식 클래스에서 재정의한다.
    /// </summary>
    protected virtual void OnBossDamaged(int damage)
    {
    }

    /// <summary>
    /// 보스별 사망 연출이 필요하면 자식 클래스에서 재정의한다.
    /// </summary>
    protected virtual void OnBossDefeated()
    {
    }
}
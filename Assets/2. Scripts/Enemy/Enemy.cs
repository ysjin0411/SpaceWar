using System;
using UnityEngine;

/// <summary>
/// 모든 적이 공통으로 상속받는 부모 클래스.
/// 체력, 피해, 사망, 골드 보상, 플레이어 충돌을 담당한다.
/// </summary>
public abstract class Enemy : MonoBehaviour, IDamageable
{
    [Header("체력 설정")]
    [Tooltip("적이 생성됐을 때 가지는 최대 체력")]
    [Min(1)]
    [SerializeField] float _maxHp = 1;

    [Header("플레이어 충돌 설정")]
    [Tooltip("플레이어와 몸으로 충돌했을 때 주는 피해")]
    [Min(0)]
    [SerializeField] float _crashDamage;

    [Header("경험치 보상 설정")]
    [Tooltip("이 적을 처치했을 때 현재 캐릭터가 획득할 경험치")]
    [Min(0)]
    [SerializeField] int _expReward;
    // 이 몬스터를 처치했을 때 사용자 계정에 지급할 경험치.
    // 각 몬스터 프리팹의 Inspector에서 설정한다.
    [SerializeField, Min(0)] private int _userExpReward;

    [Header("증강 게이지 보상 설정")]
    [Tooltip("이 적을 처치했을 때 증가할 증강 게이지")]
    [Min(0f)]
    [SerializeField] float _upgradeGaugeReward;

    [Header("적 런타임 데이터")]
    [Tooltip("현재 남아 있는 체력")]
    [SerializeField] float _currentHp;

    [Tooltip("현재 적이 사망 처리됐는지 확인")]
    [SerializeField] bool _isDead;

    // 잡몹이 죽었을 때 한 번 재생할 사망 이펙트다.
    [SerializeField] private SpriteOneShotEffect _deathEffectPrefab;

    // 적 루트와 다른 위치에서 터뜨리고 싶을 때 사용할 로컬 위치다.
    [SerializeField] private Vector3 _deathEffectOffset;

    // 같은 오브젝트에 붙은 드롭 모듈이다.
    // 없는 적은 프리팹 드롭 보상을 생성하지 않는다.
    private Drop _drop;

    // 적이 제거돼도 끝까지 재생되는 독립 사망 이펙트를 생성한다.
    /// <summary>
    /// 적이 제거돼도 끝까지 재생되는 독립 사망 이펙트를 생성한다.
    /// </summary>
    private void SpawnDeathEffect()
    {
        GameAudio.Play(GameSound.EnemyDeath); // 사망 및 충돌 소멸 시 한 번.
        Vector3 worldPosition =
            transform.TransformPoint(_deathEffectOffset);

        Instantiate(
            _deathEffectPrefab,
            worldPosition,
            Quaternion.identity);
    }

    public event Action<float> Damaged;

    // 자식 적이 현재 플레이어를 사용할 수 있도록 제공한다.
    protected PlayerController PlayerController { get; private set; }

    // 자식 적이 현재 게임 매니저를 사용할 수 있도록 제공한다.
    protected GameManager GameManager { get; private set; }

    // 외부에서 최대 체력을 읽을 수 있게 제공한다.
    public float MaxHp => _maxHp;

    // 외부에서 현재 체력을 읽을 수 있게 제공한다.
    public float CurrentHp => _currentHp;

    // 외부에서 사망 여부를 읽을 수 있게 제공한다.
    public bool IsDead => _isDead;

    // 유도 투사체가 현재 적의 위치를 추적할 때 사용한다.
    public Transform TargetTransform => transform;

    /// <summary>
    /// 적 오브젝트가 생성될 때 공통 런타임 데이터를 초기화한다.
    /// </summary>
    protected virtual void Awake()
    {
        // 현재 체력을 최대 체력으로 초기화한다.
        _currentHp = _maxHp;

        // 새로 생성된 적은 살아 있는 상태로 시작한다.
        _isDead = false;
    }

    /// <summary>
    /// EnemySpawner가 현재 플레이어와 GameManager를 전달한다.
    /// FastEnemy, BossEnemy 등 모든 적이 공통으로 사용한다.
    /// </summary>
    public void Init(PlayerController playerController, GameManager gameManager)
    {
        // 플레이어가 전달되지 않았다면 적의 공격과 추적이 불가능하다.
        if (playerController == null)
        {
            throw new ArgumentNullException(nameof(playerController));
        }

        // GameManager가 전달되지 않으면 처치 보상을 처리할 수 없다.
        if (gameManager == null)
        {
            throw new ArgumentNullException(nameof(gameManager));
        }

        // 다시 등장할 때 체력을 최대 체력으로 복구한다.
        _currentHp = _maxHp;

        // 이전 사용에서 죽었던 상태를 해제한다.
        _isDead = false;

        // 현재 스테이지에서 생성된 플레이어를 저장한다.
        PlayerController = playerController;

        // 현재 스테이지의 GameManager를 저장한다.
        GameManager = gameManager;

        // 드롭 모듈이 있는 적만 보상 설정을 초기화한다.
        _drop = GetComponent<Drop>();

        if (_drop != null)
        {
            _drop.Initialize(gameManager);
        }
    }

    /// <summary>
    /// 플레이어 투사체로부터 피해를 받는다.
    /// </summary>
    public void TakeHit(float damage)
    {
        // 이미 죽은 적은 추가 피해를 받지 않는다.
        if (_isDead)
        {
            return;
        }

        // 0 이하의 피해는 정상적인 공격 피해가 아니다.
        if (damage <= 0)
        {
            return;
        }

        // 현재 체력에서 전달받은 피해량을 차감한다.
        _currentHp = Mathf.Max(0, _currentHp - damage);

        // 살아남은 일반 적에게만 피격 애니메이션을 재생한다.
        // 체력이 0인 적은 같은 프레임에 제거되므로 점멸시킬 필요가 없다.
        if (_currentHp > 0)
        {
            GameAudio.Play((GameSound)((int)GameSound.EnemyHit01 + UnityEngine.Random.Range(0, 3)));
            Damaged?.Invoke(damage);
        }

        // 피격 효과가 필요한 자식 클래스가 처리할 수 있게 호출한다.
        OnDamaged(damage);

        // 체력이 남아 있다면 사망 처리하지 않는다.
        if (_currentHp > 0)
        {
            return;
        }

        // 체력이 0이 됐으므로 사망 처리한다.
        Die();
    }

    /// <summary>
    /// 적의 공통 사망 처리를 실행한다.
    /// 중복 보상과 중복 제거를 방지하기 위해 부모만 관리한다.
    /// </summary>
    private void Die()
    {
        // 이미 사망 처리된 적이라면 다시 처리하지 않는다.
        if (_isDead)
        {
            return;
        }

        // 이후 추가 충돌이나 피해를 처리하지 않도록 사망 상태로 변경한다.
        _isDead = true;

        // 처치 보상 처리와 별개로 사망 위치에 연출을 남긴다.
        SpawnDeathEffect();

        // 이 적에게 설정된 수치만큼 증강 게이지를 지급한다.
        GameManager?.BonusGauge(_upgradeGaugeReward);

        // 현재 캐릭터에게 이 적의 처치 경험치를 지급한다.
        GameManager?.AddExperience(_expReward);

        // 이 몬스터의 User 경험치를 즉시 지급하고 이번 판 집계에 반영한다.
        GameManager.AddUserExperience(_userExpReward);

        // 설정된 모든 드롭을 실행한다.
        // 재화는 UI 아래에 생성되므로 적이 제거돼도 회수가 계속된다.
        if (_drop != null)
        {
            _drop.Execute();
        }

        // 자식 클래스의 추가 사망 효과를 실행한다.
        OnDied();

        // 사망 또는 충돌 처리가 끝난 적을 퇴장시킨다.
        Despawn();
    }


    /// <summary>
    /// 피해를 받은 직후 자식 클래스가 추가 효과를 처리할 수 있다.
    /// 일반 적은 구현하지 않아도 된다.
    /// </summary>
    protected virtual void OnDamaged(float damage)
    {
        // 전달받은 피해에 따른 후속 반응을 처리한다.
    }

    /// <summary>
    /// 사망 직전 자식 클래스가 추가 효과를 처리할 수 있다.
    /// BossEnemy의 사망 연출 등에 사용할 수 있다.
    /// </summary>
    protected virtual void OnDied()
    {
    }

    /// <summary>
    /// 플레이어와 몸으로 충돌했을 때 피해를 준다.
    /// 몸박으로 사라지는 적은 처치 보상을 지급하지 않는다.
    /// </summary>
    protected virtual void OnTriggerEnter2D(Collider2D collision)
    {
        // 이미 죽은 적이라면 충돌을 처리하지 않는다.
        if (_isDead)
        {
            return;
        }

        // 플레이어의 자식 Collider에도 대응하도록 부모까지 탐색한다.
        PlayerController player =
            collision.GetComponentInParent<PlayerController>();

        // 충돌 대상이 플레이어가 아니면 아무것도 하지 않는다.
        if (player == null)
        {
            return;
        }

        // 같은 프레임에 추가 피해나 충돌이 처리되지 않도록 표시한다.
        _isDead = true;

        // 플레이어에게 몸박 피해를 적용한다.
        player.TakeHit(_crashDamage);

        // 몸박으로 제거될 때도 연출은 재생하되 처치 보상은 지급하지 않는다.
        SpawnDeathEffect();

        // 사망 또는 충돌 처리가 끝난 적을 퇴장시킨다.
        Despawn();
    }

    /// <summary>
    /// 공격 애니메이션의 실제 공격 시점에 호출된다.
    /// 공격 내용은 자식 적이 구현한다.
    /// </summary>
    public virtual void FireFromAnimation()
    {
    }

    /// <summary>
    /// 공격 애니메이션을 한 번 재생한 뒤 호출된다.
    /// 필요한 종료 처리는 자식 적이 구현한다.
    /// </summary>
    public virtual void FinishAttackFromAnimation()
    {
    }

    /// <summary>
    /// 보스 사망 연출 종료 시 보상 없이 폭발 연출을 남기고 퇴장한다.
    /// </summary>
    public void DefeatAfterBossDeath()
    {
        // 이미 죽었거나 풀에 보관 중인 적은 중복 처리하지 않는다.
        if (_isDead || !gameObject.activeInHierarchy)
        {
            return;
        }

        // 추가 공격과 충돌을 막고 체력도 사망 상태로 맞춘다.
        _isDead = true;
        _currentHp = 0f;

        // 본체가 반환된 뒤에도 독립 이펙트는 끝까지 재생한다.
        GameAudio.Play(GameSound.EnemyDeath);
        SpriteOneShotEffect effect = Instantiate(
            _deathEffectPrefab,
            transform.TransformPoint(_deathEffectOffset),
            Quaternion.identity);

        // 결과창이 시간을 멈추더라도 이번 정리 연출은 완료한다.
        effect.UseUnscaledTime();
        Despawn();
    }

    // 사용이 끝난 적을 풀에 반납하거나, 풀링 대상이 아니면 제거한다.
    // 자식 클래스에서도 화면 이탈이나 자폭 후 호출할 수 있다.
    protected void Despawn()
    {
        // 퇴장 처리 중 추가 피해와 충돌 처리를 막는다.
        _isDead = true;

        // 이전 사용에서 예약한 작업이 남지 않도록 정리한다.
        CancelInvoke();
        StopAllCoroutines();

        // 풀링 대상인 적은 원래 풀로 반납한다.
        if (TryGetComponent(out PoolObject poolObject))
        {
            poolObject.ReturnToPool();
            return;
        }

        // PoolObject가 없는 적은 기존 방식으로 제거한다.
        Destroy(gameObject);
    }
}

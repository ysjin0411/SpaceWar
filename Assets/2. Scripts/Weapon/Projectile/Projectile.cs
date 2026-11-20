using System;
using UnityEngine;

/// <summary>
/// 모든 플레이어 투사체가 공통으로 사용하는 부모 클래스.
/// 공격력 초기화, 이동 방향, 수명, 적 충돌 처리
/// 실제 이동 방식은 자식 클래스가 구현한다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public abstract class Projectile : MonoBehaviour
{
    [Header("투사체 공통 런타임 데이터")]
    [Tooltip("현재 투사체의 이동속도")]
    [SerializeField] float _moveSpeed = 10f;

    [Tooltip("생성된 투사체가 자동으로 제거되기까지의 시간")]
    [Min(0.1f)]
    [SerializeField] float _lifeTime = 5f;

    [Tooltip("Weapon에서 전달받은 현재 공격력")]
    [SerializeField] float _damage;

    // 직접 명중할 때 생성할 이펙트다.
    // Bullet, Slash, Sniper 프리팹에 공통 HitEffect를 연결한다.
    // 자체 폭발을 사용하는 Missile, Grenade는 비워 둔다.
    [SerializeField] private SpriteOneShotEffect _hitEffectPrefab;
    // 직접 명중할 때 재생할 소리. 범위 폭발 투사체는 None으로 둔다.
    [SerializeField] private GameSound _impactSound;

    // 이 투사체를 원래 풀로 반납하는 컴포넌트.
    private PoolObject _poolObject;

    // 이번 발사에서 남은 생존 시간.
    private float _remainingLife;

    // 수류탄 등이 변경한 중력을 다음 발사 때 복원하기 위한 값.
    private float _defaultGravityScale;

    // 명중 위치 계산에 사용할 투사체 콜라이더다.
    protected Collider2D HitCollider { get; private set; }

    // 이번 명중의 월드 위치다.
    protected Vector2 HitPoint { get; set; }

    // 제거 예약 후 같은 프레임에 중복 명중하는 것을 방지한다.
    private bool _isHitConsumed;

    // 자식 클래스가 현재 Rigidbody2D를 사용할 수 있게 제공한다.
    protected Rigidbody2D Body { get; private set; }

    // 자식 클래스가 현재 이동속도를 읽을 수 있게 제공한다.
    protected float MoveSpeed => _moveSpeed;

    // 자식 클래스가 현재 공격력을 읽을 수 있게 제공한다.
    protected float Damage => _damage;

    // 자식 클래스가 현재 진행 방향을 읽거나 변경할 수 있게 제공한다.
    protected Vector2 Direction { get; set; }

    // Weapon에서 정상적으로 초기화됐는지 저장한다.
    bool _isInitialized;

    /// <summary>
    /// 컴포넌트 참조와 초기 실행 상태를 준비한다.
    /// </summary>
    protected virtual void Awake()
    {
        // 같은 오브젝트에 있는 Rigidbody2D를 가져온다.
        Body = GetComponent<Rigidbody2D>();

        // 풀 반납에 사용할 컴포넌트를 최초 한 번 가져온다.
        _poolObject = GetComponent<PoolObject>();

        // 최초 프리팹의 중력을 저장한다.
        _defaultGravityScale = Body.gravityScale;

        // Collider는 충돌 판정만 하므로 Trigger 상태여야 한다.
        Collider2D projectileCollider = GetComponent<Collider2D>();

        if (!projectileCollider.isTrigger)
        {
            throw new InvalidOperationException(
                $"{name}의 Collider2D는 Is Trigger가 체크되어야 합니다.");
        }

        // 자식 투사체도 같은 콜라이더를 이용해 명중 위치를 계산한다.
        HitCollider = GetComponent<Collider2D>();
    }

    /// <summary>
    /// Weapon이 투사체를 생성한 직후 호출한다.
    /// </summary>
    public void Initialize(float damage, Vector2 direction)
    {
        // 0 이하 공격력은 정상적인 투사체 공격력이 아니다.
        if (damage <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(damage),
                damage,
                "투사체 공격력은 0보다 커야 합니다.");
        }

        // 방향이 없는 경우 FirePoint의 오른쪽 방향을 사용한다.
        if (direction.sqrMagnitude <= Mathf.Epsilon)
        {
            direction = transform.right;
        }

        // WeaponRuntimeState의 현재 공격력을 저장한다.
        _damage = damage;

        // 속도가 달라지지 않도록 방향 벡터를 정규화한다.
        Direction = direction.normalized;

        // 자식 클래스가 초기화된 이후에만 움직이도록 표시한다.
        _isInitialized = true;

        // 발사할 때마다 수명을 새로 시작한다.
        _remainingLife = _lifeTime;

        // Missile의 타깃 탐색처럼 자식 전용 초기화 작업을 실행한다.
        OnInitialized();
    }

    /// <summary>
    /// 물리 갱신 주기에 맞춰 이동과 충돌 처리를 수행한다.
    /// </summary>
    protected virtual void FixedUpdate()
    {
        // Weapon이 초기화하기 전에는 움직이지 않는다.
        if (!_isInitialized)
        {
            Body.linearVelocity = Vector2.zero;
            return;
        }

        // 실제 이동 방식은 각 자식 클래스가 구현한다.
        MoveProjectile();
    }

    /// <summary>
    /// 투사체 종류별 이동 방식을 자식 클래스가 구현한다.
    /// </summary>
    protected abstract void MoveProjectile();

    /// <summary>
    /// Initialize가 끝난 직후 필요한 자식 전용 작업.
    /// 필요한 자식만 재정의한다.
    /// </summary>
    protected virtual void OnInitialized()
    {
    }

    /// <summary>
    /// HandleTargetHit 동작에 필요한 상태와 처리 흐름을 실행한다.
    /// </summary>
    protected virtual void HandleTargetHit(IDamageable target)
    {
        // 대상이 죽어도 이펙트가 남도록 독립 오브젝트로 먼저 생성한다.
        if (_hitEffectPrefab != null)
        {
            Instantiate(
                _hitEffectPrefab,
                HitPoint,
                Quaternion.identity);
        }

        // 기존 공격력을 적용한다.
        GameAudio.Play(_impactSound); // 유효한 접촉마다 카탈로그 제한을 적용한다.
        target.TakeHit(_damage);
    }

    /// <summary>
    /// 명중한 뒤 투사체를 제거할지 결정한다.
    /// Bullet과 Missile은 true, 관통 Laser는 false를 사용한다.
    /// </summary>
    protected virtual bool DestroyAfterHit => true;

    /// <summary>
    /// OnTriggerEnter2D 동작에 필요한 상태와 처리 흐름을 실행한다.
    /// </summary>
    protected virtual void OnTriggerEnter2D(Collider2D collision)
    {
        // 아직 초기화되지 않았거나 이미 소비된 투사체는 처리하지 않는다.
        if (!_isInitialized || _isHitConsumed)
        {
            return;
        }

        // 공격 가능한 대상을 공통 인터페이스로 찾는다.
        IDamageable target =
            collision.GetComponentInParent<IDamageable>();

        if (target == null || target.IsDead)
        {
            return;
        }

        // 피해 처리로 대상이 제거되기 전에 맞은 위치를 저장한다.
        HitPoint = HitEffectUtility.GetSurfacePoint(
            HitCollider, collision);

        if (DestroyAfterHit)
        {
            // Destroy가 실행되기 전에도 추가 명중을 막는다.
            _isHitConsumed = true;
        }

        // 자식 투사체에 맞는 타격 처리를 실행한다.
        HandleTargetHit(target);

        if (DestroyAfterHit)
        {
            // 충돌 처리가 끝난 투사체를 재사용할 수 있도록 반납한다.
            ReturnToPool();
        }
    }

    // 풀에서 다시 꺼낼 때 이전 발사의 상태를 초기화한다.
    protected virtual void OnEnable()
    {
        _isInitialized = false;
        _isHitConsumed = false;
        _remainingLife = 0f;

        Direction = Vector2.zero;
        HitPoint = Vector2.zero;

        Body.linearVelocity = Vector2.zero;
        Body.angularVelocity = 0f;
        Body.gravityScale = _defaultGravityScale;

        HitCollider.enabled = true;
    }

    // 초기화가 끝난 투사체의 수명을 계산한다.
    protected virtual void Update()
    {
        if (!_isInitialized)
        {
            return;
        }

        _remainingLife -= Time.deltaTime;

        if (_remainingLife <= 0f)
        {
            ReturnToPool();
        }
    }

    // 충돌, 폭발, 수명 종료 시 공통으로 호출한다.
    protected void ReturnToPool()
    {
        _isInitialized = false;
        _isHitConsumed = true;
        HitCollider.enabled = false;

        _poolObject.ReturnToPool();
    }

    // 반납 시 이전 발사의 예약 작업과 물리 움직임을 정리한다.
    protected virtual void OnDisable()
    {
        CancelInvoke();
        StopAllCoroutines();

        _isInitialized = false;

        Body.linearVelocity = Vector2.zero;
        Body.angularVelocity = 0f;
    }
}
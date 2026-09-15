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
    [SerializeField] int _damage;

    // 직접 명중할 때 생성할 이펙트다.
    // Bullet, Slash, Sniper 프리팹에 공통 HitEffect를 연결한다.
    // 자체 폭발을 사용하는 Missile, Grenade는 비워 둔다.
    [SerializeField] private SpriteOneShotEffect _hitEffectPrefab;

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
    protected int Damage => _damage;

    // 자식 클래스가 현재 진행 방향을 읽거나 변경할 수 있게 제공한다.
    protected Vector2 Direction { get; set; }

    // Weapon에서 정상적으로 초기화됐는지 저장한다.
    bool _isInitialized;

    protected virtual void Awake()
    {
        // 같은 오브젝트에 있는 Rigidbody2D를 가져온다.
        Body = GetComponent<Rigidbody2D>();

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
    public void Initialize(int damage, Vector2 direction)
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

        // Missile의 타깃 탐색처럼 자식 전용 초기화 작업을 실행한다.
        OnInitialized();

        // 화면 밖 판정을 놓쳐도 지정된 시간이 지나면 자동 제거한다.
        Destroy(gameObject, _lifeTime);
    }

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
        target.TakeHit(_damage);
    }

    /// <summary>
    /// 명중한 뒤 투사체를 제거할지 결정한다.
    /// Bullet과 Missile은 true, 관통 Laser는 false를 사용한다.
    /// </summary>
    protected virtual bool DestroyAfterHit => true;

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
            Destroy(gameObject);
        }
    }
}
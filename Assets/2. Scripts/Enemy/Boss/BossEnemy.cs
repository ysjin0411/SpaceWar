using System;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 모든 보스가 상속받는 공통 부모 클래스.
/// 일반 적 Enemy와는 별개의 생명주기를 가지며,
/// 체력, 피격, 플레이어 충돌, 사망 이벤트를 담당한다.
/// </summary>
public abstract class BossEnemy : MonoBehaviour, IDamageable
{
    [Header("보스 기본 정보")]

    // UI에 표시할 보스 이름이다.
    // 각 보스 프리팹의 인스펙터에서 설정한다.
    [SerializeField] string _bossName;

    [Header("보스 체력 설정")]
    [Tooltip("보스의 최대 체력")]
    [Min(1)][SerializeField] int _maxHp = 100;

    [Header("플레이어 충돌 설정")]
    [Tooltip("보스와 플레이어가 충돌했을 때 주는 피해")]
    [Min(0)]
    [SerializeField] int _crashDamage;

    // 현재 패턴이 전달한 접촉 피해량이다.
    int _patternCrashDamage;

    // true면 패턴 피해량, false면 기본 접촉 피해량을 사용한다.
    bool _isUsingPatternCrashDamage;

    // Animator의 Die Trigger 이름을 해시값으로 보관한다.
    static readonly int DieTriggerHash =
        Animator.StringToHash("Die");

    // 자식 Renderer에 있는 보스 Animator
    Animator _animator;

    // 사망 완료 처리가 중복 실행되는 것을 방지한다.
    bool _isDefeatCompleted;

    [Header("보스 런타임 데이터")]
    [Tooltip("현재 보스 체력")]
    [SerializeField] int _currentHp;
    [SerializeField] bool _isDead;  // 현재 보스가 사망 처리됐는지 확인


    [Header("보스 움직임 가능 여부")]
    [SerializeField] bool _isControllable; // 보스 움직임 여부

    [Header("보스 공통 랜덤 이동")]
    [Min(0f)][SerializeField] float _moveSpeed = 2f;                     // 보스가 랜덤 목표 위치로 이동하는 속도
    [SerializeField] Vector2 _movementHalfRange = new Vector2(2f, 3f);   // 생성 위치 기준 X, Y 이동 반경
    [Min(0.01f)][SerializeField] float _arrivalDistance = 0.1f;          // 다음 목표를 선정하는 도착 판정 거리

    [Header("보스 일반 패턴 설정")]

    [Min(0f)]
    [SerializeField] float _normalPatternFirstDelay = 2f; // 보스 등장 후 첫 일반 패턴까지 기다리는 시간

    [Min(0.1f)]
    [SerializeField] float _normalPatternInterval = 2f; // 일반 패턴 종료 후 다음 패턴까지 기다리는 시간

    [Range(0f, 1f)]
    [SerializeField] float _previousPatternWeightMultiplier = 0.1f; // 직전에 사용한 패턴의 가중치 배율

    [Header("보스 일반 시간 패턴")]
    [SerializeField]
    List<TimedBossPatternEntry> _timedPatternEntries =
    new List<TimedBossPatternEntry>(); // 시간 조건으로 실행할 일반 패턴 설정 목록

    Rigidbody2D _rigidbody;          // 보스 이동에 사용하는 Rigidbody2D
    BossRandomMover _randomMover;    // 공통 랜덤 이동을 처리하는 런타임 객체
    BossPatternContext _patternContext; // 현재 보스의 패턴 실행에 필요한 참조 모음
    BossPatternRunner _patternRunner; // 보스 패턴의 대기와 실행을 담당하는 객체
    bool _isInitialized;             // 외부 초기화가 끝났는지 확인하는 값
    TimedBossPatternController _timedPatternController; // 일반 패턴 타이머를 관리하는 객체

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
    public event Action<int> Damaged;

    /// <summary>
    /// 보스 패턴이 시스템 알림 메시지 출력을 요청할 때 발생한다.
    /// 문자열에는 화면에 출력할 메시지가 전달된다.
    /// </summary>
    public event Action<string> SystemDialogueRequested;

    /// <summary>
    /// 시스템 알림 메시지의 점멸이 완전히 끝났을 때 발생한다.
    /// 실행 중인 특수 패턴은 이 이벤트를 기다린 후 다음 행동을 진행한다.
    /// </summary>
    public event Action SystemDialogueCompleted;

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

    // 외부 UI에서 보스 이름을 읽을 수 있도록 제공한다.
    public string BossName => _bossName;

    // 패턴에서 보스의 원래 스폰 위치를 사용할 수 있게 제공한다.
    public Vector2 MovementCenter =>
        _randomMover.MovementCenter;



    protected virtual void Awake()
    {
        // 보스 프리팹에 표시 이름이 입력되지 않았다면 설정 오류로 처리한다.
        if (string.IsNullOrWhiteSpace(_bossName))
        {
            throw new InvalidOperationException(
                $"{name} 보스 프리팹에 Boss Name이 설정되지 않았습니다.");
        }

        // 보스가 생성되면 최대 체력으로 초기화한다.
        _currentHp = _maxHp;

        // 새로 생성된 보스는 살아 있는 상태다.
        _isDead = false;

        // 아직 사망 애니메이션 완료 처리를 하지 않은 상태다.
        _isDefeatCompleted = false;

        // 보스 자식 Renderer에서 Animator를 찾는다.
        _animator = GetComponentInChildren<Animator>();

        if (_animator == null)
        {
            throw new MissingComponentException(
                $"{name}의 자식에서 Animator를 찾을 수 없습니다.");
        }

        // 보스 이동에 사용할 Rigidbody2D를 가져온다.
        _rigidbody = GetComponent<Rigidbody2D>();

        if (_rigidbody == null)
        {
            throw new MissingComponentException(
                $"{name}에 Rigidbody2D가 없습니다.");
        }

        // 인스펙터에 설정한 이동 정보로 공통 랜덤 이동 객체를 생성한다.
        _randomMover = new BossRandomMover(_rigidbody, _moveSpeed, _movementHalfRange, _arrivalDistance);
    }

    private void Update()
    {
        // BossEncounterManager의 초기화 전에는 패턴 시간을 갱신하지 않는다.
        if (!_isInitialized)
        {
            return;
        }

        // 사망한 보스는 새로운 패턴을 요청하지 않는다.
        if (_isDead)
        {
            return;
        }

        // 일반 패턴들의 남은 시간을 갱신한다.
        _timedPatternController.Tick(
            Time.deltaTime);
    }

    private void FixedUpdate()
    {
        // BossEncounterManager의 Initialize 호출 전에는 이동하지 않는다.
        if (!_isInitialized)
        {
            return;
        }

        // 등장 연출이나 패턴 때문에 조작이 막혀 있으면 이동하지 않는다.
        if (!_isControllable)
        {
            return;
        }

        // 사망한 보스는 더 이상 이동하지 않는다.
        if (_isDead)
        {
            return;
        }

        // 물리 프레임마다 랜덤 목표 위치를 향해 이동한다.
        _randomMover.FixedTick(Time.fixedDeltaTime);
    }

    /// <summary>
    /// 보스의 이동과 행동 가능 상태를 변경한다.
    /// </summary>
    public void SetControllable(bool isOn)
    {
        _isControllable = isOn;

        // 아직 보스 초기화가 끝나지 않았다면 이동 객체를 제어하지 않는다.
        if (!_isInitialized)
        {
            return;
        }

        if (isOn)
        {
            // 보스 이동을 다시 시작한다.
            _randomMover.Resume();
        }
        else
        {
            // 보스 이동을 즉시 정지한다.
            _randomMover.Pause();
        }
    }

    /// <summary>
    /// 시간 조건으로 발동하는 일반 패턴을 요청한다.
    /// </summary>
    protected void RequestNormalPattern(
        BossPattern pattern)
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException(
                $"{name}은 초기화 전이라 일반 패턴을 요청할 수 없습니다.");
        }

        _patternRunner.RequestNormal(pattern);
    }

    /// <summary>
    /// 체력 조건으로 발동하는 특수 패턴을 요청한다.
    /// 현재 패턴 종료 후 일반 대기 패턴보다 먼저 실행된다.
    /// </summary>
    protected void RequestSpecialPattern(
        BossPattern pattern)
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException(
                $"{name}은 초기화 전이라 특수 패턴을 요청할 수 없습니다.");
        }

        _patternRunner.RequestSpecial(pattern);
    }

    /// <summary>
    /// 패턴의 접촉 피해량을 저장하고 사용 상태를 켠다.
    /// </summary>
    public void SetPatternCrashDamage(int damage)
    {
        // 음수 피해량은 잘못된 설정으로 처리한다.
        if (damage < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(damage),
                "패턴 접촉 피해는 음수일 수 없습니다.");
        }

        // 피해량은 각 패턴 Config에서 전달받는다.
        _patternCrashDamage = damage;
        _isUsingPatternCrashDamage = true;
    }

    /// <summary>
    /// 패턴 피해 사용을 해제하고 기본 접촉 피해로 돌아간다.
    /// </summary>
    public void ResetPatternCrashDamage()
    {
        _isUsingPatternCrashDamage = false;
        _patternCrashDamage = 0;
    }

    /// <summary>
    /// 현재 보스가 시스템 알림 UI에 메시지 출력을 요청한다.
    /// 실제 UI 출력은 BossEncounterManager가 연결한 SystemDialogueUI가 담당한다.
    /// </summary>
    public void RequestSystemDialogue(
        string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException(
                "시스템 알림 메시지가 비어 있습니다.",
                nameof(message));
        }

        // UI와 이벤트가 연결되지 않았다면 패턴이 완료 이벤트를
        // 영원히 기다리게 되므로 즉시 설정 오류를 알려준다.
        if (SystemDialogueRequested == null)
        {
            throw new InvalidOperationException(
                $"{name}의 시스템 알림 이벤트를 받는 UI가 없습니다.");
        }

        // 연결된 시스템 알림 UI에 메시지를 전달한다.
        SystemDialogueRequested.Invoke(message);
    }

    /// <summary>
    /// SystemDialogueUI의 점멸이 끝났다는 사실을 현재 보스에게 전달한다.
    /// BossEncounterManager가 UI의 완료 이벤트와 이 메서드를 연결한다.
    /// </summary>
    public void NotifySystemDialogueCompleted()
    {
        // 보스가 죽은 뒤 도착한 UI 완료 신호는 처리하지 않는다.
        if (_isDead)
        {
            return;
        }

        // 시스템 알림 완료를 기다리는 특수 패턴에 알린다.
        SystemDialogueCompleted?.Invoke();
    }

    /// <summary>
    /// BossEncounterManager가 보스를 생성한 직후 호출한다.
    /// 보스 패턴에서 사용할 현재 플레이어와 GameManager를 전달한다.
    /// </summary>
    public void Initialize(PlayerController playerController, GameManager gameManager)
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

        // 현재 보스의 패턴 실행에 필요한 참조를 하나의 Context로 구성한다.
        _patternContext = new BossPatternContext(
            this,
            TargetPlayer,
            _rigidbody,
            _animator.transform,
            GameManager);

        // BossEnemy 자신이 코루틴 실행 주체가 되어 패턴 실행기를 생성한다.
        _patternRunner = new BossPatternRunner(
            this,
            _patternContext);

        // 인스펙터에 등록된 일반 시간 패턴 설정으로 타이머 관리자를 생성한다.
        _timedPatternController =
    new TimedBossPatternController(
        _timedPatternEntries,
        _patternRunner,
        _normalPatternFirstDelay,
        _normalPatternInterval,
        _previousPatternWeightMultiplier);

        // 외부에서 필요한 참조를 모두 받았으므로 초기화 완료 상태로 변경한다.
        _isInitialized = true;

        // 현재 생성 위치를 이동 범위의 중심으로 설정하고 랜덤 이동을 시작한다.
        _randomMover.Start();

        // 보스가 실제로 움직일 수 있는 상태로 변경한다.
        _isControllable = true;

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

        // 살아남은 피격에서만 피격 애니메이션 이벤트를 발생시킨다.
        // 사망 공격에서는 Die 애니메이션이 우선되어야 한다.
        if (_currentHp > 0)
        {
            Damaged?.Invoke(damage);
        }

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

        // 실행 중인 패턴과 대기 중인 모든 패턴을 취소한다.
        _patternRunner?.StopAll();

        // 사망과 동시에 진행 중인 랜덤 이동을 정지한다.
        _randomMover.Pause();

        // 사망 후 플레이어 공격과 충돌하지 않도록 Collider를 비활성화한다.
        SetCollidersEnabled(false);

        // 보스 이동을 즉시 정지한다.
        if (TryGetComponent(out Rigidbody2D body))
        {
            body.linearVelocity = Vector2.zero;
        }

        // 보스별 폭발, 애니메이션, 사운드 처리를 실행한다.
        OnBossDefeated();

        // Animator를 사망 상태로 전환한다.
        _animator.SetTrigger(DieTriggerHash);
    }

    /// <summary>
    /// 사망 애니메이션의 마지막 프레임에서 호출된다.
    /// 이 시점에 보스 처치 이벤트를 보내고 보스 오브젝트를 삭제한다.
    /// </summary>
    public void CompleteDefeat()
    {
        // 아직 사망 상태가 아니라면 잘못 호출된 것이므로 처리하지 않는다.
        if (!_isDead)
        {
            return;
        }

        // Animation Event가 중복 호출돼도 한 번만 처리한다.
        if (_isDefeatCompleted)
        {
            return;
        }

        _isDefeatCompleted = true;

        // BossEncounterManager에 사망 연출까지 끝났다고 알린다.
        Defeated?.Invoke(this);

        // 사망 연출이 모두 끝났으므로 보스 오브젝트를 삭제한다.
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

        // 패턴 피해 사용 여부에 따라 실제 접촉 피해량을 선택한다.
        int damage = _isUsingPatternCrashDamage ? _patternCrashDamage : _crashDamage;

        player.TakeHit(damage);
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
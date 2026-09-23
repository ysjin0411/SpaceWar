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

    // 스테이지 선택 화면 등에 표시할 보스 초상화.
    [SerializeField] Sprite _bossIcon;

    // [추가] 각 보스의 투사체와 레이저가 시작되는 위치다.
    [SerializeField] Transform _firePoint;

    [Header("보스 체력 설정")]
    [Tooltip("보스의 최대 체력")]
    [Min(1)][SerializeField] int _maxHp = 100;

    [Header("플레이어 충돌 설정")]
    [Tooltip("보스와 플레이어가 충돌했을 때 주는 피해")]
    [Min(0)]
    [SerializeField] int _crashDamage;

    [Header("처치시 나오는 보상")]
    // 이 보스를 처치했을 때 사용자 계정에 지급할 경험치.
    // 각 보스 프리팹의 Inspector에서 설정한다.
    [SerializeField, Min(0)] private int _userExpReward;

    // 보스에 붙은 드롭 모듈이다.
    private Drop _drop;

    // 현재 실제로 특수패턴을 실행 중인지 나타낸다.
    public bool IsSpecialPatternRunning { get; private set; }

    // 현재 패턴이 전달한 접촉 피해량이다.
    int _patternCrashDamage;

    // true면 패턴 피해량, false면 기본 접촉 피해량을 사용한다.
    bool _isUsingPatternCrashDamage;

    // Animator의 Die Trigger 이름을 해시값으로 보관한다.
    static readonly int DieTriggerHash =
        Animator.StringToHash("Die");

    // 보스 본체의 Animator를 지정한다.
    // 크루저는 EntityRoot 아래 Renderer의 Animator를 연결한다.
    [SerializeField] private Animator _animator;

    // 사망 완료 처리가 중복 실행되는 것을 방지한다.
    bool _isDefeatCompleted;

    // 보스 본체의 피격과 접촉에 사용하는 콜라이더를 지정한다.
    // 크루저는 EntityRoot의 Collider2D를 연결한다.
    [SerializeField] private Collider2D _bossBodyCollider;

    // 현재 플레이어 루트에 있는 몸체 콜라이더다.
    private Collider2D _playerBodyCollider;

    // 보스별 페이즈 처리에서 본체 콜라이더를 제어할 수 있게 한다.
    protected Collider2D BodyCollider => _bossBodyCollider;

    // 기본 보스는 피해를 받을 수 있다.
    // 크루저는 이 조건을 재정의해 1페이즈의 본체 피해를 막는다.
    protected virtual bool CanReceiveDamage => true;


    // 복귀 때문에 몸체 충돌을 무시하고 있는지 기록한다.
    private bool _isReturnCollisionIgnored;

    // 복귀 전에 사용하던 두 콜라이더의 충돌 무시 설정을 보관한다.
    private bool _ignoreCollisionBeforeReturn;


    // true는 특수패턴 시작, false는 종료 또는 취소를 의미한다.
    public event Action<bool> SpecialPatternStateChanged;

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

    // 다음 페이즈의 보스 프리팹으로 교체해 달라고 관리자에게 알린다.
    // 순서대로 현재 보스, 다음 프리팹, 생성할 월드 위치, 회전이다.
    // 최종 처치를 뜻하는 Defeated와 구분하여 보상 지급을 막는다.
    public event Action<BossEnemy, BossEnemy, Vector3, Quaternion>
        PhaseReplacementRequested;

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

    // 진행 중인 시스템 경고를 완료 처리 없이 닫도록 UI에 요청한다.
    public event Action SystemDialogueCancelRequested;

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

    // 보스를 실제로 생성하지 않고도 프리팹에서 아이콘을 읽을 수 있다.
    public Sprite BossIcon => _bossIcon;

    // 모든 보스 패턴이 공통으로 사용하는 발사 위치다.
    public Transform FirePoint => _firePoint;

    // 패턴에서 보스의 원래 스폰 위치를 사용할 수 있게 제공한다.
    public Vector2 MovementCenter =>
        _randomMover.MovementCenter;

    // 입장을 마치고 Initialize가 호출되었는지 외부에 알려준다.
    public bool HasBattleStarted => _isInitialized;

    // 기본 보스는 일반 패턴을 사용할 수 있다.
    // 페이즈 제한이 필요한 보스는 이 조건을 재정의한다.
    protected virtual bool CanUseNormalPatterns => true;

    // 현재 체력을 파란 ShieldHpbar에 표시할지 UI에 알려준다.
    // 일반 보스는 기존 빨간 체력 바를 사용한다.
    public virtual bool UsesShieldHealthBar => false;

    // 보스의 전체 페이즈 수. 단일 페이즈 보스의 기본값은 1이다.
    public virtual int TotalPhaseCount => 1;

    // 현재 진행 중인 페이즈 번호. 1부터 시작한다.
    public virtual int CurrentPhaseNumber => 1;

    // 현재 페이즈 이후에 남아 있는 페이즈 수를 공통 계산한다.
    // 마지막 페이즈에서는 0이 되어 UI가 PhaseCount를 숨긴다.
    public int RemainingPhaseCount =>
        Mathf.Max(0, TotalPhaseCount - CurrentPhaseNumber);



    protected virtual void Awake()
    {
        // 보스 프리팹에 표시 이름이 입력되지 않았다면 설정 오류로 처리한다.
        if (string.IsNullOrWhiteSpace(_bossName))
        {
            throw new InvalidOperationException(
                $"{name} 보스 프리팹에 Boss Name이 설정되지 않았습니다.");
        }

        // [추가] 모든 보스가 자신의 발사 위치를 연결했는지 검사한다.
        if (_firePoint == null)
        {
            throw new InvalidOperationException(
                $"{name}: FirePoint를 연결하세요.");
        }

        // 보스가 생성되면 최대 체력으로 초기화한다.
        _currentHp = _maxHp;

        // 새로 생성된 보스는 살아 있는 상태다.
        _isDead = false;

        // 아직 사망 애니메이션 완료 처리를 하지 않은 상태다.
        _isDefeatCompleted = false;

        // 여러 자식 중 임의의 Animator가 선택되지 않도록 직접 연결을 요구한다.
        if (_animator == null)
        {
            throw new InvalidOperationException(
                $"{name}: 본체 Animator를 연결하세요.");
        }

        // 최상단뿐 아니라 자식에 있는 본체 콜라이더도 사용할 수 있다.
        if (_bossBodyCollider == null)
        {
            throw new InvalidOperationException(
                $"{name}: 본체 Collider2D를 연결하세요.");
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
        // 전투 초기화 전이나 사망 후에는 패턴 시간을 갱신하지 않는다.
        if (!_isInitialized || _isDead)
        {
            return;
        }

        // 일반 패턴을 사용할 수 없는 단계에서는 타이머도 멈춘다.
        if (!CanUseNormalPatterns)
        {
            return;
        }

        // 허용된 단계에서만 다음 일반 패턴까지의 시간을 계산한다.
        _timedPatternController.Tick(Time.deltaTime);
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

    // 자식 보스가 특수패턴을 요청하는 공통 진입점이다.
    // 기존 호출은 기본값 false를 사용하므로 동작이 유지된다.
    protected void RequestSpecialPattern(
        BossPattern pattern,
        bool prioritize = false)
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException(
                $"{name}은 초기화 전이라 특수패턴을 요청할 수 없습니다.");
        }

        _patternRunner.RequestSpecial(pattern, prioritize);
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

    // 패턴이 취소되었을 때 표시 중인 시스템 경고도 함께 종료한다.
    public void CancelSystemDialogue()
    {
        // UI가 이미 비활성화되어 구독이 없어졌다면 처리할 일이 없다.
        if (SystemDialogueCancelRequested != null)
        {
            SystemDialogueCancelRequested.Invoke();
        }
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

        _playerBodyCollider = TargetPlayer.GetComponent<Collider2D>();

        // 몸체가 누락된 프리팹은 초기화 단계에서 확인한다.
        if (_bossBodyCollider == null || _playerBodyCollider == null)
        {
            throw new InvalidOperationException(
                $"{name}: 보스와 플레이어 루트에 몸체 Collider2D가 필요합니다.");
        }

        // 보상 설정은 보스가 아닌 Drop 컴포넌트가 관리한다.
        _drop = GetComponent<Drop>();

        if (_drop != null)
        {
            _drop.Initialize(gameManager);
        }

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
        // 입장 중이거나 사망했거나 현재 페이즈가 무적이면 피해를 받지 않는다.
        if (!_isInitialized || _isDead || !CanReceiveDamage)
        {
            return;
        }

        // 0 이하의 피해는 처리하지 않는다.
        if (damage <= 0)
        {
            return;
        }

        // 체력을 차감하기 전에 보스별 피해 감소를 적용한다.
        damage = CalculateIncomingDamage(damage);

        // 피해 감소율이 100%라면 피격 연출과 체력 변경도 생략한다.
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

        // 체력이 소진되면 보스별 다음 페이즈 전환을 먼저 시도한다.
        // 전환할 페이즈가 없을 때만 최종 사망을 처리한다.
        if (_currentHp <= 0)
        {
            if (TryBeginNextPhase())
            {
                return;
            }

            Die();
        }
    }

    // 체력 소진 시 다음 페이즈를 시작할 수 있는지 확인한다.
    // 기본값은 false이므로 기존 보스의 사망 동작은 유지된다.
    protected virtual bool TryBeginNextPhase()
    {
        return false;
    }

    // 자식 보스가 전환 연출을 마친 뒤 호출하는 교체 요청 함수다.
    // 실제 생성과 현재 보스 참조 변경은 관리자가 담당한다.
    protected void RequestPhaseReplacement(
        BossEnemy nextPhasePrefab,
        Vector3 spawnPosition,
        Quaternion spawnRotation)
    {
        // 전투가 시작되지 않았거나 이미 종료된 보스는 요청하지 않는다.
        if (!isActiveAndEnabled || !HasBattleStarted || IsDead)
        {
            return;
        }

        // 플레이어가 사망하거나 비활성화됐다면 다음 보스를 생성하지 않는다.
        if (TargetPlayer == null ||
            !TargetPlayer.isActiveAndEnabled ||
            TargetPlayer.IsDead)
        {
            return;
        }

        // 다음 프리팹을 빠뜨린 설정을 명확하게 알린다.
        if (nextPhasePrefab == null)
        {
            throw new InvalidOperationException(
                $"{name}: 다음 페이즈의 보스 프리팹을 연결하세요.");
        }

        // 관리자가 연결되지 않은 상태에서 요청이 사라지는 것을 막는다.
        if (PhaseReplacementRequested == null)
        {
            throw new InvalidOperationException(
                $"{name}: 페이즈 교체 요청을 처리할 관리자가 연결되지 않았습니다.");
        }

        // 사망 처리 없이 다음 프리팹과 생성 좌표를 전달한다.
        PhaseReplacementRequested.Invoke(
            this,
            nextPhasePrefab,
            spawnPosition,
            spawnRotation);
    }

    // 다음 페이즈의 최대 체력과 현재 체력을 함께 설정한다.
    // 자식 보스는 페이즈 상태를 먼저 변경한 뒤 호출해야 한다.
    protected void ResetHealthForPhase(int maxHp)
    {
        // 잘못된 체력 설정으로 빈 체력바가 만들어지는 것을 막는다.
        if (maxHp <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxHp));
        }

        // 다음 페이즈를 가득 찬 체력으로 시작한다.
        _maxHp = maxHp;
        _currentHp = maxHp;

        // 새 페이즈를 기준으로 체력바와 PhaseCount를 갱신한다.
        HealthChanged?.Invoke(_currentHp, _maxHp);
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

        // 보스 사망이 확정되는 즉시 User 경험치를 한 번 지급한다.
        GameManager.AddUserExperience(_userExpReward);

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

    // 사망 애니메이션 종료 후 드롭을 실행한다.
    public void CompleteDefeat()
    {
        if (!_isDead || _isDefeatCompleted)
        {
            return;
        }

        _isDefeatCompleted = true;

        // 드롭 모듈이 없는 보스는 바로 처치 완료를 알린다.
        if (_drop == null)
        {
            FinishDefeat();
            return;
        }

        // 재화가 모두 실제로 회수된 뒤 처치 완료를 알린다.
        _drop.Execute(HandleDropsCompleted);
    }

    // 씬 전환 등으로 이미 제거된 보스에는 완료 처리를 하지 않는다.
    private void HandleDropsCompleted()
    {
        if (this == null)
        {
            return;
        }

        FinishDefeat();
    }

    // 플레이어 몸체와 접촉했을 때만 몸박 피해를 준다.
    protected virtual void OnTriggerEnter2D(Collider2D collision)
    {
        // 무기 등 다른 콜라이더의 접촉은 몸박으로 처리하지 않는다.
        if (_isDead || collision != _playerBodyCollider)
        {
            return;
        }

        // 현재 패턴에 맞는 몸박 피해량을 선택한다.
        int damage = _isUsingPatternCrashDamage
            ? _patternCrashDamage
            : _crashDamage;

        TargetPlayer.TakeHit(damage);
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

    /// <summary>
    /// 패턴 실행기가 실제 특수패턴 시작·종료 시 호출한다.
    /// 스포너 등 외부 시스템은 이 이벤트로 동작을 변경한다.
    /// </summary>
    public void SetSpecialPatternRunning(bool isRunning)
    {
        // 같은 상태를 중복 통보하지 않는다.
        if (IsSpecialPatternRunning == isRunning)
        {
            return;
        }

        IsSpecialPatternRunning = isRunning;

        if (SpecialPatternStateChanged != null)
        {
            SpecialPatternStateChanged.Invoke(isRunning);
        }
    }

    // 드롭 처리가 끝나면 기존 보스 처치 이후 흐름으로 돌아간다.
    private void FinishDefeat()
    {
        // 기존 관리자가 별도의 보물상자 등장과 스테이지 진행을 처리한다.
        if (Defeated != null)
        {
            Defeated.Invoke(this);
        }

        // 모든 드롭 처리가 끝난 보스를 제거한다.
        Destroy(gameObject);
    }

    // 패턴 후 복귀를 시작할 때 두 몸체 사이의 충돌을 무시한다.
    public void BeginReturnCollisionIgnore()
    {
        // 중복 호출로 기존 설정을 덮어쓰지 않는다.
        if (_isReturnCollisionIgnored)
        {
            return;
        }

        // 복귀가 끝나면 원래 설정으로 되돌릴 수 있도록 저장한다.
        _ignoreCollisionBeforeReturn = Physics2D.GetIgnoreCollision(
            _bossBodyCollider, _playerBodyCollider);

        _isReturnCollisionIgnored = true;

        // 이 두 몸체 사이의 충돌과 트리거만 무시한다.
        Physics2D.IgnoreCollision(
            _bossBodyCollider, _playerBodyCollider, true);
    }

    // 복귀 완료 또는 패턴 중단 시 원래 충돌 설정을 복원한다.
    public void RestoreReturnCollision()
    {
        // 복귀 중이 아니거나 이미 복원했다면 처리하지 않는다.
        if (!_isReturnCollisionIgnored)
        {
            return;
        }

        _isReturnCollisionIgnored = false;

        // 대상이 제거되었다면 복원할 콜라이더가 없다.
        if (_bossBodyCollider == null || _playerBodyCollider == null)
        {
            return;
        }

        // 비활성화된 콜라이더는 Unity가 충돌 무시 설정을 해제한다.
        if (!_bossBodyCollider.isActiveAndEnabled ||
            !_playerBodyCollider.isActiveAndEnabled)
        {
            return;
        }

        // 복귀 전에 사용하던 설정으로 되돌린다.
        Physics2D.IgnoreCollision(
            _bossBodyCollider,
            _playerBodyCollider,
            _ignoreCollisionBeforeReturn);
    }

    // 보스가 비활성화될 때 복귀 상태를 정리한다.
    protected virtual void OnDisable()
    {
        RestoreReturnCollision();
    }

    // 자식 보스가 페이즈를 전환할 때 기존 패턴 실행과 대기열을 정리한다.
    protected void StopPatternsForPhaseChange()
    {
        if (_patternRunner == null)
        {
            return;
        }

        // 기존 실행기의 중단 처리를 사용하므로 패턴의 finally도 실행된다.
        _patternRunner.StopAll();
    }

    // 보스별 방어 효과를 적용할 확장 지점이다.
    // 별도로 재정의하지 않은 보스는 원래 피해를 그대로 받는다.
    protected virtual int CalculateIncomingDamage(int damage)
    {
        return damage;
    }


}
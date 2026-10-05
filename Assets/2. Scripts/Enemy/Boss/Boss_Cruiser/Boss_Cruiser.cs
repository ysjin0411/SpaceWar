using System;
using UnityEngine;
using System.Collections.Generic;

public class Boss_Cruiser : BossEnemy
{
    // 크루저가 소환한 오브젝트와 당시 대여 번호를 보관한다.
    private struct SummonedEnemyEntry
    {
        // 소환한 적의 풀 반납을 담당하는 컴포넌트.
        public PoolObject Object;

        // 소환 당시의 대여 번호. 이후 재사용되었는지 판단한다.
        public ulong Version;

        // 소환 시점의 오브젝트와 대여 번호를 저장한다.
        public SummonedEnemyEntry(PoolObject poolObject, ulong version)
        {
            Object = poolObject;
            Version = version;
        }
    }

    // 타워와 전투하는 단계와 본체와 전투하는 단계를 구분한다.
    public enum CruiserPhase
    {
        Towers,
        Entity
    }

    // 타워들의 합산 체력을 제공하는 컴포넌트다.
    [SerializeField] private BossTowerShield _towerShield;

    // 최상단 보스가 공격 시간을 관리할 타워 공격 컴포넌트들이다.
    [SerializeField]
    private TowerAttack[] _towerAttacks =
        new TowerAttack[0];

    // 타워가 살아 있는 1페이즈에 본체의 무적 상태를 표시한다.
    [SerializeField] private SpriteRenderer _invulnerabilityShield;

    // 1페이즈: 타워 합산 체력을 기준으로 요청할 특수패턴 목록.
    // 여러 조건을 동시에 만족하면 목록 순서대로 요청한다.
    [SerializeField]
    private List<HealthBossPatternEntry> _phase1SpecialPatterns =
        new List<HealthBossPatternEntry>();

    // 2페이즈: 본체 체력을 기준으로 요청할 특수패턴 목록.
    [SerializeField]
    private List<HealthBossPatternEntry> _phase2SpecialPatterns =
        new List<HealthBossPatternEntry>();

    // 타워가 사라진 뒤에도 유지되는 구멍별 소환 위치.
    [SerializeField] private Transform _summonPointA;
    [SerializeField] private Transform _summonPointB;
    [SerializeField] private Transform _summonPointC;

    // 크루저가 소환한 적의 대여 정보를 보관한다.
    private readonly List<SummonedEnemyEntry> _summonedEnemies =
        new List<SummonedEnemyEntry>();

    // 같은 패턴 에셋을 여러 항목에 넣어도 각 항목을 한 번씩 실행한다.
    private bool[] _phase1Requested = Array.Empty<bool>();
    private bool[] _phase2Requested = Array.Empty<bool>();

    // 각 타워가 다음 공격까지 기다릴 시간을 별도로 저장한다.
    private float[] _remainingAttackTimes = new float[0];

    // 공격할 타워와 플레이어를 지정해 요청을 전달한다.
    public event Action<TowerAttack, PlayerController> TowerAttackRequested;

    // 생성 직후에는 타워를 먼저 파괴하는 1페이즈로 시작한다.
    private CruiserPhase _currentPhase = CruiserPhase.Towers;

    // 보스 관리자가 플레이어와 게임 정보를 전달했는지 기록한다.
    private bool _isBattleInitialized;

    // EntityRoot의 Renderer에 있는 본체 Animator다.
    [SerializeField]
    private Animator _phaseAnimator;

    // 타워 파괴 후 본체 전투를 시작하기 전까지의 연출 진행 상태다.
    private bool _isPhaseTransitioning;

    // 1페이즈에서 2페이즈로 넘어가는 전환 애니메이션 상태다.
    private static readonly int PhaseTransitionState =
        Animator.StringToHash(
            "Base Layer.Boss_Cruiser_PhaseTransition_3p5s");

    // 전환 연출이 끝난 뒤 돌아갈 기본 대기 상태다.
    private static readonly int EntityIdleState =
        Animator.StringToHash("Base Layer.Idle");

    // 외부에서 현재 페이즈를 확인할 수 있게 한다.
    public CruiserPhase CurrentPhase => _currentPhase;

    // 크루저는 타워와 본체로 구성된 2페이즈 보스다.
    public override int TotalPhaseCount => 2;

    // 기존 타워/본체 상태를 1부터 시작하는 페이즈 번호로 변환한다.
    public override int CurrentPhaseNumber =>
        _currentPhase == CruiserPhase.Towers ? 1 : 2;

    // 이후 공격 담당 코드가 페이즈 변경을 전달받을 때 사용한다.
    public event Action<CruiserPhase> PhaseChanged;

    // 초기화가 끝나고 2페이즈에 진입한 본체만 피해를 받는다.
    protected override bool CanReceiveDamage =>
        _isBattleInitialized &&
        _currentPhase == CruiserPhase.Entity;

    // 크루저는 전투 초기화가 끝나고 본체가 싸우는 2페이즈부터
    // 일반 패턴 타이머를 진행한다.
    protected override bool CanUseNormalPatterns =>
        _isBattleInitialized &&
        _currentPhase == CruiserPhase.Entity;

    /// <summary>
    /// 컴포넌트 참조와 초기 실행 상태를 준비한다.
    /// </summary>
    protected override void Awake()
    {
        // 공통 보스 체력과 이동, Animator 참조를 준비한다.
        base.Awake();

        // 본체 Animator와 전환에 필요한 상태가 준비됐는지 확인한다.
        if (_phaseAnimator == null ||
            !_phaseAnimator.HasState(0, PhaseTransitionState) ||
            !_phaseAnimator.HasState(0, EntityIdleState))
        {
            throw new InvalidOperationException(
                $"{name}: Phase Animator의 본체 연결과 전환·Idle 상태를 확인하세요.");
        }

        // 처음 생성될 때는 전환 연출을 진행하고 있지 않다.
        _isPhaseTransitioning = false;

        if (_towerShield == null)
        {
            throw new InvalidOperationException(
                $"{name}: BossTowerShield를 연결하세요.");
        }

        // 생성 직후부터 본체는 공격에 맞지 않도록 한다.
        _currentPhase = CruiserPhase.Towers;
        BodyCollider.enabled = false;

        // 무적 표시용 실드가 연결되어 있는지 확인한다.
        if (_invulnerabilityShield == null)
        {
            throw new InvalidOperationException(
                $"{name}: 무적 표시용 ShieldVisual을 연결하세요.");
        }

        // 본체가 보호받는 1페이즈에는 실드를 표시한다.
        _invulnerabilityShield.enabled = true;
    }

    /// <summary>
    /// 활성화 시 초기 상태와 이벤트 연결을 준비한다.
    /// </summary>
    private void OnEnable()
    {
        // 타워들의 합산 체력이 변경될 때 페이즈 전환을 검사한다.
        _towerShield.ShieldChanged += HandleShieldChanged;

        // 재활성화 전에 타워 체력이 바뀐 경우도 현재 값으로 확인한다.
        if (_isBattleInitialized)
        {
            HandleShieldChanged(
                _towerShield.CurrentShield,
                _towerShield.MaxShield);
        }
    }

    /// <summary>
    /// OnBossInitialized 동작에 필요한 상태와 처리 흐름을 실행한다.
    /// </summary>
    protected override void OnBossInitialized()
    {
        base.OnBossInitialized();

        // 타워마다 독립적인 첫 공격 대기 시간을 준비한다.
        _remainingAttackTimes = new float[_towerAttacks.Length];

        var registeredTowers =
            new System.Collections.Generic.HashSet<Tower>();

        for (int i = 0; i < _towerAttacks.Length; i++)
        {
            TowerAttack attack = _towerAttacks[i];

            // 같은 타워를 중복 등록하거나 다른 보스의 타워를 등록하지 못하게 한다.
            if (attack == null ||
                attack.Owner == null ||
                attack.GetComponentInParent<Boss_Cruiser>() != this ||
                !registeredTowers.Add(attack.Owner))
            {
                throw new InvalidOperationException(
                    $"{name}: 타워 공격 목록의 누락·중복·소속을 확인하세요.");
            }

            _remainingAttackTimes[i] = attack.FirstDelay;
        }

        // 체력 이벤트 처리를 허용하기 전에 두 목록을 준비한다.
        _phase1Requested = InitializeHealthPatterns(
            _phase1SpecialPatterns, "Phase 1 Special Patterns");

        _phase2Requested = InitializeHealthPatterns(
            _phase2SpecialPatterns, "Phase 2 Special Patterns");

        // 이제 보스 전투에 필요한 외부 참조가 준비되었다.
        _isBattleInitialized = true;

        // 초기화 전에 발생한 체력 이벤트를 놓쳤더라도 현재 값을 확인한다.
        HandleShieldChanged(
            _towerShield.CurrentShield,
            _towerShield.MaxShield);
    }

    /// <summary>
    /// 변경된 실드 수치를 표시와 상태에 반영한다.
    /// </summary>
    private void HandleShieldChanged(float currentShield, float maxShield)
    {
        // 전환 중에 들어오는 추가 알림으로 패턴을 요청하지 않는다.
        if (!_isBattleInitialized || IsDead || _isPhaseTransitioning)
        {
            return;
        }

        // 타워 합산 체력은 1페이즈에서만 사용한다.
        if (_currentPhase != CruiserPhase.Towers || maxShield <= 0)
        {
            return;
        }

        // 모든 타워가 파괴되면 새 패턴보다 페이즈 전환을 우선한다.
        if (currentShield <= 0)
        {
            // 즉시 본체 전투를 시작하지 않고 전환 연출부터 재생한다.
            BeginEntityPhaseTransition();
            return;
        }

        // 파란 체력바의 실제 체력 비율로 1페이즈 목록을 검사한다.
        RequestHealthPatterns(
            _phase1SpecialPatterns,
            _phase1Requested,
            currentShield,
            maxShield);
    }

    // 전환 연출이 끝난 뒤 본체 전투를 시작한다.
    /// <summary>
    /// 전환 연출이 끝난 뒤 본체 전투를 시작한다.
    /// </summary>
    private void EnterEntityPhase()
    {
        // 전환 중이 아니거나 이미 사망했다면 중복으로 진입하지 않는다.
        if (!_isPhaseTransitioning || IsDead)
            return;

        // 전환을 종료하고 본체 전투 단계로 바꾼다.
        _isPhaseTransitioning = false;
        _currentPhase = CruiserPhase.Entity;

        // 이제부터 본체가 피해를 받을 수 있도록 한다.
        BodyCollider.enabled = true;
        _invulnerabilityShield.enabled = false;

        // 전환 때문에 멈췄던 이동을 재개한다.
        SetControllable(true);

        // 외부 시스템에 실제 2페이즈 시작을 알린다.
        if (PhaseChanged != null)
        {
            PhaseChanged.Invoke(_currentPhase);
        }
    }

    // 마지막 타워가 파괴되면 본체 전투 대신 전환 연출을 시작한다.
    /// <summary>
    /// 마지막 타워가 파괴되면 본체 전투 대신 전환 연출을 시작한다.
    /// </summary>
    private void BeginEntityPhaseTransition()
    {
        // 초기화 전, 사망 후, 전환 중에는 중복으로 시작하지 않는다.
        if (!_isBattleInitialized ||
            IsDead ||
            _isPhaseTransitioning ||
            _currentPhase != CruiserPhase.Towers)
        {
            return;
        }

        // 기존 패턴을 종료하는 동안 재진입하지 않도록 먼저 기록한다.
        _isPhaseTransitioning = true;

        // 진행 중인 패턴과 대기 중인 요청을 정리한다.
        StopPatternsForPhaseChange();

        // 패턴 정리가 이동을 재개할 수 있으므로 정리 후 이동을 멈춘다.
        SetControllable(false);

        // 연출 중에는 본체를 공격할 수 없고, 보호막 이미지는 숨긴다.
        BodyCollider.enabled = false;
        _invulnerabilityShield.enabled = false;

        // 전환 클립의 첫 프레임부터 재생한다.
        _phaseAnimator.Play(PhaseTransitionState, 0, 0f);
        _phaseAnimator.Update(0f);
    }

    // 전환 클립이 끝났는지 확인하고, 완료되면 본체 전투를 시작한다.
    /// <summary>
    /// 전환 클립이 끝났는지 확인하고, 완료되면 본체 전투를 시작한다.
    /// </summary>
    private void UpdateEntityPhaseTransition()
    {
        // 일시정지나 전투 종료 중에는 다음 페이즈를 열지 않는다.
        if (!_isPhaseTransitioning ||
            IsDead ||
            Time.deltaTime <= 0f ||
            TargetPlayer == null ||
            !TargetPlayer.isActiveAndEnabled ||
            TargetPlayer.IsDead ||
            !_phaseAnimator.isActiveAndEnabled)
        {
            return;
        }

        AnimatorStateInfo state =
            _phaseAnimator.GetCurrentAnimatorStateInfo(0);

        // 전환 클립을 끝까지 재생한 경우에만 완료 처리한다.
        if (state.fullPathHash != PhaseTransitionState ||
            state.normalizedTime < 1f)
        {
            return;
        }

        // 전환 마지막 자세에서 기본 대기 애니메이션으로 돌아간다.
        _phaseAnimator.Play(EntityIdleState, 0, 0f);
        _phaseAnimator.Update(0f);

        // 기존 본체 페이즈 진입 처리를 호출한다.
        EnterEntityPhase();
    }

    /// <summary>
    /// OnTriggerEnter2D 동작에 필요한 상태와 처리 흐름을 실행한다.
    /// </summary>
    protected override void OnTriggerEnter2D(Collider2D collision)
    {
        // 자식 타워의 충돌이 최상단 Rigidbody2D에 전달되더라도
        // 1페이즈에는 공통 보스의 본체 접촉 피해를 실행하지 않는다.
        if (!_isBattleInitialized ||
            _currentPhase != CruiserPhase.Entity)
        {
            return;
        }

        // 2페이즈에는 기존 보스의 플레이어 접촉 피해를 사용한다.
        base.OnTriggerEnter2D(collision);
    }

    /// <summary>
    /// 비활성화 시 이벤트 연결과 실행 상태를 정리한다.
    /// </summary>
    protected override void OnDisable()
    {
        // 비활성화된 뒤 이전 전환 상태가 남지 않도록 정리한다.
        _isPhaseTransitioning = false;

        // 비활성화할 때 이벤트 구독을 해제한다.
        if (_towerShield != null)
        {
            _towerShield.ShieldChanged -= HandleShieldChanged;
        }

        // 보스가 비활성화되는 경우에도 남은 소환 적을 정리한다.
        ClearSummonedEnemies();

        // 공통 보스가 관리하는 충돌 무시 상태도 정리한다.
        base.OnDisable();
    }

    /// <summary>
    /// 프레임의 이동 결과에 맞춰 후속 상태를 갱신한다.
    /// </summary>
    private void LateUpdate()
    {
        // 전환 중에는 클립의 완료 여부만 확인하고 타워 공격은 갱신하지 않는다.
        if (_isPhaseTransitioning)
        {
            UpdateEntityPhaseTransition();
            return;
        }

        // 1페이즈에서만 타워의 공격 시간을 계산한다.
        if (!_isBattleInitialized ||
            IsDead ||
            _currentPhase != CruiserPhase.Towers ||
            Time.deltaTime <= 0f)
        {
            return;
        }

        for (int i = 0; i < _towerAttacks.Length; i++)
        {
            TowerAttack attack = _towerAttacks[i];

            // 파괴되거나 비활성화된 타워는 공격하지 않는다.
            if (attack == null ||
                !attack.isActiveAndEnabled ||
                !attack.Owner.isActiveAndEnabled ||
                attack.Owner.IsDead)
            {
                continue;
            }

            // 이 타워가 공격 중이어도 다른 타워의 시간은 계속 계산한다.
            // 쿨타임은 이 타워의 공격 애니메이션 종료 후부터 흐른다.
            if (attack.IsAttacking)
            {
                continue;
            }

            _remainingAttackTimes[i] =
                Mathf.Max(0f, _remainingAttackTimes[i] - Time.deltaTime);

            if (_remainingAttackTimes[i] > 0f)
            {
                continue;
            }

            // 해당 타워에 공격 요청을 전달한다.
            if (TowerAttackRequested != null)
            {
                TowerAttackRequested.Invoke(attack, TargetPlayer);
            }

            // 실제로 공격을 시작한 경우에만 다음 쿨타임을 설정한다.
            if (attack.IsAttacking)
            {
                _remainingAttackTimes[i] = attack.Cooldown;
            }
        }
    }

    // 목록 설정을 검사하고, 각 항목의 실행 여부를 기록할 배열을 만든다.
    /// <summary>
    /// 목록 설정을 검사하고, 각 항목의 실행 여부를 기록할 배열을 만든다.
    /// </summary>
    private bool[] InitializeHealthPatterns(
        List<HealthBossPatternEntry> entries,
        string listName)
    {
        if (entries == null)
        {
            throw new InvalidOperationException(
                $"{name}: {listName} 목록이 없습니다.");
        }

        for (int i = 0; i < entries.Count; i++)
        {
            HealthBossPatternEntry entry = entries[i];

            if (entry == null || entry.Pattern == null)
            {
                throw new InvalidOperationException(
                    $"{name}: {listName}의 Element {i}에 패턴을 연결하세요.");
            }

            // 사망 시점인 0%와 전투 시작 시점인 100%는 제외한다.
            if (!(entry.HealthPercent > 0f &&
                  entry.HealthPercent < 100f))
            {
                throw new InvalidOperationException(
                    $"{name}: {listName}의 Element {i} 체력 기준은 " +
                    "0보다 크고 100보다 작아야 합니다.");
            }
        }

        // 빈 목록도 허용한다. 해당 페이즈의 특수패턴이 없다는 의미다.
        return new bool[entries.Count];
    }

    // 현재 체력이 조건 이하인 항목을 기존 특수패턴 실행기에 요청한다.
    /// <summary>
    /// 현재 체력이 조건 이하인 항목을 기존 특수패턴 실행기에 요청한다.
    /// </summary>
    private void RequestHealthPatterns(
        List<HealthBossPatternEntry> entries,
        bool[] requested,
        float currentHealth,
        float maxHealth)
    {
        // 치명적인 피해에서는 새 패턴을 요청하지 않는다.
        if (IsDead || currentHealth <= 0 || maxHealth <= 0)
        {
            return;
        }

        float healthPercent = currentHealth * 100f / maxHealth;

        for (int i = 0; i < entries.Count; i++)
        {
            if (requested[i])
            {
                continue;
            }

            HealthBossPatternEntry entry = entries[i];

            if (healthPercent > entry.HealthPercent)
            {
                continue;
            }

            // 요청 과정에서 이벤트가 발생해도 중복 요청하지 않도록 먼저 기록한다.
            requested[i] = true;

            // BossEnemy에서 상속받은 특수패턴 요청 기능을 사용한다.
            RequestSpecialPattern(entry.Pattern);
        }
    }

    // BossEnemy.TakeHit()이 본체 체력을 감소시킨 뒤 호출하는 함수다.
    /// <summary>
    /// 보스 피격 이후 상태와 패턴을 갱신한다.
    /// </summary>
    protected override void OnBossDamaged(float damage)
    {
        base.OnBossDamaged(damage);

        // 본체 체력에 따른 패턴은 2페이즈에서만 요청한다.
        if (!_isBattleInitialized ||
            _currentPhase != CruiserPhase.Entity)
        {
            return;
        }

        // 본체의 현재 체력과 최대 체력은 BossEnemy에서 상속받는다.
        RequestHealthPatterns(
            _phase2SpecialPatterns,
            _phase2Requested,
            CurrentHp,
            MaxHp);
    }

    // 소환 패턴이 구멍의 현재 위치를 가져온다.
    // 0은 A, 1은 B, 2는 C다.
    /// <summary>
    /// 0은 A, 1은 B, 2는 C다.
    /// </summary>
    public Transform GetSummonPoint(int holeIndex)
    {
        Transform point;

        switch (holeIndex)
        {
            case 0:
                point = _summonPointA;
                break;

            case 1:
                point = _summonPointB;
                break;

            case 2:
                point = _summonPointC;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(holeIndex));
        }

        if (point == null || !point.gameObject.activeInHierarchy)
        {
            throw new InvalidOperationException(
                $"{name}: 소환 위치 {holeIndex}의 연결과 활성 상태를 확인하세요.");
        }

        return point;
    }

    // 크루저가 소환한 적의 현재 대여 정보를 등록한다.
    public void RegisterSummonedEnemy(SmallEnemy enemy)
    {
        if (enemy == null)
        {
            throw new ArgumentNullException(nameof(enemy));
        }

        // 적에게 붙은 PoolObject를 가져오고 대여 상태를 확인한다.
        if (!enemy.TryGetComponent(out PoolObject poolObject) ||
            !poolObject.IsBorrowed)
        {
            throw new InvalidOperationException(
                $"{name}: 소환 적은 풀에서 대여한 상태여야 합니다.");
        }

        // 파괴·반납·재대여된 기록을 제거한다.
        // 같은 오브젝트의 기존 기록도 제거하여 중복 등록을 막는다.
        _summonedEnemies.RemoveAll(entry =>
            entry.Object == null ||
            !entry.Object.IsBorrowed ||
            entry.Object.BorrowVersion != entry.Version ||
            entry.Object == poolObject);

        // 현재 오브젝트와 대여 번호를 구조체에 저장한다.
        _summonedEnemies.Add(
            new SummonedEnemyEntry(poolObject, poolObject.BorrowVersion));
    }

    // 크루저가 소환한 대여 건에 해당하는 적만 풀로 반납한다.
    private void ClearSummonedEnemies()
    {
        // 마지막 항목부터 꺼내며 목록이 빌 때까지 정리한다.
        while (_summonedEnemies.Count > 0)
        {
            int lastIndex = _summonedEnemies.Count - 1;
            SummonedEnemyEntry entry = _summonedEnemies[lastIndex];

            // 반납으로 OnDisable이 호출되기 전에 기록부터 제거한다.
            _summonedEnemies.RemoveAt(lastIndex);

            // 이미 파괴되었거나 반납된 오브젝트는 건너뛴다.
            if (entry.Object == null || !entry.Object.IsBorrowed)
            {
                continue;
            }

            // 다른 대여 건으로 재사용된 오브젝트는 건드리지 않는다.
            if (entry.Object.BorrowVersion != entry.Version)
            {
                continue;
            }

            // 처치 보상 없이 풀로 반납한다.
            // 비활성화와 보관 위치 이동은 풀 매니저가 처리한다.
            entry.Object.ReturnToPool();
        }
    }

    // 보스의 체력이 0이 되는 시점에 호출된다.
    /// <summary>
    /// 보스의 체력이 0이 되는 시점에 호출된다.
    /// </summary>
    protected override void OnBossDefeated()
    {
        ClearSummonedEnemies();
        base.OnBossDefeated();
    }


}
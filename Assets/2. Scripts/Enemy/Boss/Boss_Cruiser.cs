using System;
using UnityEngine;
using System.Collections.Generic;

public class Boss_Cruiser : BossEnemy
{
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

    // 보스 처치 시 남은 소환 적을 정리하기 위한 목록.
    private readonly List<SmallEnemy> _summonedEnemies =
        new List<SmallEnemy>();

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

    // 외부에서 현재 페이즈를 확인할 수 있게 한다.
    public CruiserPhase CurrentPhase => _currentPhase;

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

    protected override void Awake()
    {
        // 공통 보스 체력과 이동, Animator 참조를 준비한다.
        base.Awake();

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

    private void HandleShieldChanged(int currentShield, int maxShield)
    {
        // 등장 연출 중이거나 사망한 상태에서는 패턴을 요청하지 않는다.
        if (!_isBattleInitialized || IsDead)
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
            EnterEntityPhase();
            return;
        }

        // 파란 체력바의 실제 체력 비율로 1페이즈 목록을 검사한다.
        RequestHealthPatterns(
            _phase1SpecialPatterns,
            _phase1Requested,
            currentShield,
            maxShield);
    }

    private void EnterEntityPhase()
    {
        // 피해 허용 조건을 먼저 2페이즈로 변경한다.
        _currentPhase = CruiserPhase.Entity;

        // 이전 페이즈의 충전 중인 공격과 대기 중인 패턴 요청을 정리한다.
        // 이미 발사된 대형 탄은 독립적으로 남은 수명을 진행한다.
        StopPatternsForPhaseChange();

        // 본체의 기존 체력을 유지한 채 피격 콜라이더를 활성화한다.
        BodyCollider.enabled = true;

        // 본체가 피해를 받을 수 있는 순간 무적 표시도 없앤다.
        _invulnerabilityShield.enabled = false;

        // 이후 본체 공격과 페이즈 연출에서 사용할 전환 알림이다.
        if (PhaseChanged != null)
        {
            PhaseChanged.Invoke(_currentPhase);
        }
    }

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

    protected override void OnDisable()
    {
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

    private void LateUpdate()
    {
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
    private void RequestHealthPatterns(
        List<HealthBossPatternEntry> entries,
        bool[] requested,
        int currentHealth,
        int maxHealth)
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
    protected override void OnBossDamaged(int damage)
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

    // 생성된 적은 패턴이 끝난 뒤에도 독립적으로 행동한다.
    // 크루저는 보스 처치 시 정리할 수 있도록 참조만 보관한다.
    public void RegisterSummonedEnemy(SmallEnemy enemy)
    {
        if (enemy == null)
        {
            throw new ArgumentNullException(nameof(enemy));
        }

        // 이미 자폭하거나 처치된 적의 참조를 정리한다.
        _summonedEnemies.RemoveAll(item => item == null);
        _summonedEnemies.Add(enemy);
    }

    // 보스 처치 뒤 이동 연출에 소환 적의 공격이 남지 않도록 제거한다.
    private void ClearSummonedEnemies()
    {
        foreach (SmallEnemy enemy in _summonedEnemies)
        {
            if (enemy == null)
            {
                continue;
            }

            // Destroy가 처리되기 전에도 이동과 충돌을 즉시 중단한다.
            enemy.gameObject.SetActive(false);
            Destroy(enemy.gameObject);
        }

        _summonedEnemies.Clear();
    }

    // 보스의 체력이 0이 되는 시점에 호출된다.
    protected override void OnBossDefeated()
    {
        ClearSummonedEnemies();
        base.OnBossDefeated();
    }
}
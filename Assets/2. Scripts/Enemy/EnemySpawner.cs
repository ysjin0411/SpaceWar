using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 구간에 맞는 간격으로 일반 적 패턴을 선택한다.
/// 보스 특수패턴 중에는 새 생성과 순차 생성 예약을 중단한다.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    // 현재 스테이지에서 사용할 일반 적 패턴 목록이다.
    [SerializeField]
    List<EnemySpawnPattern> _patterns =
        new List<EnemySpawnPattern>();

    // 현재 스테이지 전투 구간을 확인한다.
    [SerializeField] StageFlowManager _stageFlowManager;

    // 생성된 보스와 특수패턴 이벤트를 연결한다.
    [SerializeField] BossEncounterManager _bossEncounterManager;

    // 일반 전투 구간에서 패턴을 선택하는 간격이다.
    [Min(0.1f)]
    [SerializeField] float _patternCoolTime = 3f;

    // 보스 전투 구간에서 패턴을 선택하는 별도의 간격이다.
    [Min(0.1f)]
    [SerializeField] float _bossPatternCoolTime = 6f;

    // 다음 패턴 선택까지 누적된 시간이다.
    float _timer;

    // 플레이어와 패턴 선택기의 초기화가 끝났는지 확인한다.
    bool _isInitialized;

    // 현재 일반 적 생성을 진행 중인지 확인한다.
    bool _isSpawning;

    // 이벤트를 구독 중인 보스다.
    BossEnemy _boundBoss;

    // 패턴에 전달할 생성 정보다.
    EnemySpawnContext _spawnContext;

    // 기존 가중치 선택과 패턴별 쿨다운을 담당한다.
    EnemySpawnPatternSelector _patternSelector;

    // 현재 구간이 중간보스 또는 최종보스 전투인지 확인한다.
    bool IsBossBattle =>
        _stageFlowManager.CurrentPhase == StagePhase.MidBossBattle ||
        _stageFlowManager.CurrentPhase == StagePhase.FinalBossBattle;

    private void Awake()
    {
        // 필수 참조 누락을 실행 시작 시 알려준다.
        if (_stageFlowManager == null ||
            _bossEncounterManager == null)
        {
            throw new InvalidOperationException(
                $"{name}: StageFlowManager와 BossEncounterManager를 연결하세요.");
        }
    }

    private void OnEnable()
    {
        // 구간 변경과 새 보스 생성을 감지한다.
        _stageFlowManager.PhaseChanged += HandlePhaseChanged;
        _bossEncounterManager.BossSpawned += HandleBossSpawned;

        // 스포너가 나중에 활성화되어도 현재 보스에 연결한다.
        HandleBossSpawned(_bossEncounterManager.CurrentBoss);
    }

    private void OnDisable()
    {
        // 비활성화 시 외부 이벤트와 생성 예약을 정리한다.
        _stageFlowManager.PhaseChanged -= HandlePhaseChanged;
        _bossEncounterManager.BossSpawned -= HandleBossSpawned;

        UnbindBoss();
        StopSpawning();
    }

    /// <summary>
    /// StageManager가 플레이어 생성 후 호출한다.
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

        // 순차 생성 코루틴은 이 스포너에서 관리한다.
        _spawnContext = new EnemySpawnContext(
            playerController,
            gameManager,
            RunPatternSequence);

        _patternSelector = new EnemySpawnPatternSelector(_patterns);

        _isInitialized = true;

        // 초기화 시점의 전투 구간과 보스 상태를 적용한다.
        RefreshSpawningState();
    }

    private void Update()
    {
        if (!_isInitialized)
        {
            return;
        }

        // 보스의 사망은 사망 애니메이션 완료 이벤트보다 먼저 발생한다.
        // 생성 직전에도 상태를 확인하여 사망 연출 중 생성을 막는다.
        RefreshSpawningState();

        if (!_isSpawning || Time.deltaTime <= 0f)
        {
            return;
        }

        // 생성이 허용된 동안에만 패턴별 쿨다운을 감소시킨다.
        _patternSelector.UpdateCooldowns(Time.deltaTime);
        _timer += Time.deltaTime;

        // 전투 구간에 맞는 공통 생성 간격을 사용한다.
        float interval = IsBossBattle
            ? _bossPatternCoolTime
            : _patternCoolTime;

        if (_timer < interval)
        {
            return;
        }

        // 모든 패턴이 쿨다운 중이면 선택 가능한 시점까지 기다린다.
        if (!_patternSelector.TrySelect(
            out EnemySpawnPatternRuntimeState selectedState))
        {
            return;
        }

        selectedState.Pattern.Execute(_spawnContext);
        selectedState.StartCooldown();

        _timer = 0f;
    }

    /// <summary>
    /// 현재 구간과 보스 상태로 생성 허용 여부를 판단한다.
    /// </summary>
    private bool CanSpawn()
    {
        if (!_isInitialized || !isActiveAndEnabled)
        {
            return false;
        }

        StagePhase phase = _stageFlowManager.CurrentPhase;

        if (phase == StagePhase.Section1 ||
            phase == StagePhase.Section2)
        {
            return true;
        }

        if (!IsBossBattle)
        {
            return false;
        }

        // 보스가 실제로 존재하고 살아 있으며 특수패턴 중이 아니어야 한다.
        return _boundBoss != null &&
               _boundBoss.isActiveAndEnabled &&
               !_boundBoss.IsDead &&
               !_boundBoss.IsSpecialPatternRunning;
    }

    /// <summary>
    /// 허용 상태가 바뀔 때만 생성 타이머를 변경한다.
    /// </summary>
    private void RefreshSpawningState()
    {
        bool canSpawn = CanSpawn();

        if (canSpawn == _isSpawning)
        {
            return;
        }

        if (canSpawn)
        {
            StartSpawning();
        }
        else
        {
            StopSpawning();
        }
    }

    /// <summary>
    /// 새로운 스테이지 구간에서는 쿨다운과 생성 예약을 초기화한다.
    /// </summary>
    private void HandlePhaseChanged(StagePhase phase)
    {
        StopSpawning();

        if (!_isInitialized)
        {
            return;
        }

        _patternSelector.ResetCooldowns();
        RefreshSpawningState();
    }

    /// <summary>
    /// 새 보스의 특수패턴 상태 이벤트를 연결한다.
    /// </summary>
    private void HandleBossSpawned(BossEnemy boss)
    {
        UnbindBoss();
        _boundBoss = boss;

        if (_boundBoss != null)
        {
            _boundBoss.SpecialPatternStateChanged +=
                HandleSpecialPatternStateChanged;
        }

        RefreshSpawningState();
    }

    /// <summary>
    /// 특수패턴 시작 시 즉시 중단하고 종료 시 생성 간격을 다시 측정한다.
    /// </summary>
    private void HandleSpecialPatternStateChanged(bool isRunning)
    {
        // 전달된 값만으로 재개하지 않고 현재 구간과 사망 여부도 확인한다.
        RefreshSpawningState();
    }

    /// <summary>
    /// 이전 보스의 이벤트 연결을 해제한다.
    /// </summary>
    private void UnbindBoss()
    {
        if (_boundBoss != null)
        {
            _boundBoss.SpecialPatternStateChanged -=
                HandleSpecialPatternStateChanged;
        }

        _boundBoss = null;
    }

    /// <summary>
    /// 현재 구간의 생성 간격을 처음부터 측정한다.
    /// 특수패턴 종료 시 개별 패턴 쿨다운은 보존한다.
    /// </summary>
    public void StartSpawning()
    {
        if (!CanSpawn() || _isSpawning)
        {
            return;
        }

        _timer = 0f;
        _isSpawning = true;
    }

    /// <summary>
    /// 생성 예약을 취소한다. 이미 생성된 적은 유지한다.
    /// </summary>
    public void StopSpawning()
    {
        _timer = 0f;
        _isSpawning = false;
        StopAllCoroutines();
    }

    /// <summary>
    /// 현재 생성 가능한 경우에만 순차 생성 작업을 시작한다.
    /// </summary>
    private void RunPatternSequence(IEnumerator sequence)
    {
        if (!_isSpawning || !CanSpawn())
        {
            return;
        }

        StartCoroutine(sequence);
    }
}
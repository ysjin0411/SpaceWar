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
    // Inspector에서 묶음 개수와 묶음별 패턴 목록을 설정한다.
    [SerializeField]
    List<EnemySpawnGroup> _patternGroups =
        new List<EnemySpawnGroup>();

    // 각 묶음의 독립적인 타이머와 선택기를 보관한다.
    readonly List<EnemySpawnGroupRuntimeState> _groupStates =
        new List<EnemySpawnGroupRuntimeState>();

    // 현재 스테이지 전투 구간을 확인한다.
    [SerializeField] StageFlowManager _stageFlowManager;

    // 생성된 보스와 특수패턴 이벤트를 연결한다.
    [SerializeField] BossEncounterManager _bossEncounterManager;


    // 플레이어와 패턴 선택기의 초기화가 끝났는지 확인한다.
    bool _isInitialized;

    // 현재 일반 적 생성을 진행 중인지 확인한다.
    bool _isSpawning;

    // 이벤트를 구독 중인 보스다.
    BossEnemy _boundBoss;

    // 패턴에 전달할 생성 정보다.
    EnemySpawnContext _spawnContext;

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

        // 본체의 특수패턴 상태를 기준으로 적 생성 여부를 판단하게 한다.
        _bossEncounterManager.BossReplaced += HandleBossSpawned;

        // 스포너가 나중에 활성화되어도 현재 보스에 연결한다.
        HandleBossSpawned(_bossEncounterManager.CurrentBoss);
    }

    private void OnDisable()
    {
        // 비활성화 시 외부 이벤트와 생성 예약을 정리한다.
        _stageFlowManager.PhaseChanged -= HandlePhaseChanged;
        _bossEncounterManager.BossSpawned -= HandleBossSpawned;

        // 페이즈 교체 이벤트 구독을 해제한다.
        _bossEncounterManager.BossReplaced -= HandleBossSpawned;

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

        // 초기화가 다시 호출될 경우 기존 묶음 실행 상태를 정리한다.
        _groupStates.Clear();

        if (_patternGroups == null || _patternGroups.Count == 0)
        {
            throw new InvalidOperationException(
                $"{name}: 소환 묶음을 하나 이상 등록하세요.");
        }

        // 묶음마다 별도의 실행 상태와 패턴 선택기를 생성한다.
        for (int i = 0; i < _patternGroups.Count; i++)
        {
            EnemySpawnGroup group = _patternGroups[i];

            // 비어 있는 묶음은 설정 오류로 알려준다.
            if (group == null ||
                group.Patterns == null ||
                group.Patterns.Count == 0)
            {
                throw new InvalidOperationException(
                    $"{name}: {i + 1}번 묶음에 패턴을 등록하세요.");
            }

            _groupStates.Add(
                new EnemySpawnGroupRuntimeState(group));
        }

        _isInitialized = true;

        // 초기화 시점의 전투 구간과 보스 상태를 적용한다.
        RefreshSpawningState();
    }

    // 소환 가능한 동안 모든 묶음의 타이머를 각각 갱신한다.
    private void Update()
    {
        if (!_isInitialized)
        {
            return;
        }

        // 현재 스테이지와 보스 상태를 확인해 소환 여부를 갱신한다.
        RefreshSpawningState();

        float deltaTime = Time.deltaTime;

        if (!_isSpawning || deltaTime <= 0f)
        {
            return;
        }

        for (int i = 0; i < _groupStates.Count; i++)
        {
            // 앞 묶음 실행 중 소환 불가 상태가 되었다면 이후 묶음은 실행하지 않는다.
            if (!_isSpawning || !CanSpawn())
            {
                break;
            }

            // 각 묶음은 자기 소환 간격과 자기 패턴 목록만 사용한다.
            _groupStates[i].Tick(
                deltaTime,
                IsBossBattle,
                _spawnContext);
        }
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

    // 스테이지 단계가 바뀌면 모든 묶음과 패턴의 대기 상태를 초기화한다.
    private void HandlePhaseChanged(StagePhase phase)
    {
        StopSpawning();

        if (!_isInitialized)
        {
            return;
        }

        for (int i = 0; i < _groupStates.Count; i++)
        {
            _groupStates[i].ResetAll();
        }

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

    // 소환을 재개하면 각 묶음은 자기 소환 간격만큼 기다린다.
    public void StartSpawning()
    {
        if (!CanSpawn() || _isSpawning)
        {
            return;
        }

        ResetGroupTimers();
        _isSpawning = true;
    }

    // 소환을 중단하고 진행 중인 소환 코루틴도 종료한다.
    public void StopSpawning()
    {
        _isSpawning = false;
        ResetGroupTimers();
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

    // 모든 묶음의 소환 타이머를 초기화한다.
    private void ResetGroupTimers()
    {
        for (int i = 0; i < _groupStates.Count; i++)
        {
            _groupStates[i].ResetTimer();
        }
    }
}
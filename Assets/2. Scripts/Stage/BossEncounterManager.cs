using System;
using UnityEngine;

/// <summary>
/// 스테이지 페이즈에 맞춰 중간보스와 최종보스를 생성한다.
/// 생성한 보스에게 현재 플레이어와 GameManager를 전달한다.
/// </summary>
public class BossEncounterManager : MonoBehaviour
{
    [Header("스테이지 진행 정보")]
    [Tooltip("현재 스테이지 페이즈를 관리하는 StageFlowManager")]
    [SerializeField] StageFlowManager _stageFlowManager;

    [Header("보스 프리팹")]
    [Tooltip("중간보스 전투에서 생성할 보스 프리팹")]
    [SerializeField] BossEnemy _midBossPrefab;

    [Tooltip("최종보스 전투에서 생성할 보스 프리팹")]
    [SerializeField] BossEnemy _finalBossPrefab;

    [Header("보스 생성 위치")]
    [Tooltip("중간보스를 생성할 위치")]
    [SerializeField] Transform _midBossSpawnPoint;

    [Tooltip("최종보스를 생성할 위치")]
    [SerializeField] Transform _finalBossSpawnPoint;

    // StageManager가 생성한 현재 플레이어
    PlayerController _playerController;

    // 현재 스테이지의 GameManager
    GameManager _gameManager;

    // 현재 전투 중인 보스
    BossEnemy _currentBoss;

    // StageManager로부터 필요한 참조를 전달받았는지 확인한다.
    bool _isInitialized;

    // 외부에서 현재 보스를 확인할 때 사용한다.
    public BossEnemy CurrentBoss => _currentBoss;

    private void Awake()
    {
        if (_stageFlowManager == null)
        {
            throw new InvalidOperationException(
                $"{name}의 BossEncounterManager에 StageFlowManager가 연결되지 않았습니다.");
        }

        if (_midBossPrefab == null)
        {
            throw new InvalidOperationException(
                $"{name}의 BossEncounterManager에 Mid Boss Prefab이 연결되지 않았습니다.");
        }

        if (_finalBossPrefab == null)
        {
            throw new InvalidOperationException(
                $"{name}의 BossEncounterManager에 Final Boss Prefab이 연결되지 않았습니다.");
        }

        if (_midBossSpawnPoint == null)
        {
            throw new InvalidOperationException(
                $"{name}의 BossEncounterManager에 Mid Boss Spawn Point가 연결되지 않았습니다.");
        }

        if (_finalBossSpawnPoint == null)
        {
            throw new InvalidOperationException(
                $"{name}의 BossEncounterManager에 Final Boss Spawn Point가 연결되지 않았습니다.");
        }
    }

    private void OnEnable()
    {
        if (_stageFlowManager != null)
        {
            _stageFlowManager.PhaseChanged += HandlePhaseChanged;
        }
    }

    private void OnDisable()
    {
        if (_stageFlowManager != null)
        {
            _stageFlowManager.PhaseChanged -= HandlePhaseChanged;
        }
    }

    /// <summary>
    /// StageManager가 플레이어 생성 후 호출한다.
    /// 생성될 보스가 사용할 현재 플레이어와 GameManager를 전달한다.
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

        _playerController = playerController;
        _gameManager = gameManager;
        _isInitialized = true;
    }

    /// <summary>
    /// 보스 전투 페이즈가 시작되면 해당 보스를 생성한다.
    /// </summary>
    private void HandlePhaseChanged(StagePhase phase)
    {
        switch (phase)
        {
            case StagePhase.MidBossBattle:

                SpawnBoss(
                    _midBossPrefab,
                    _midBossSpawnPoint);

                break;

            case StagePhase.FinalBossBattle:

                SpawnBoss(
                    _finalBossPrefab,
                    _finalBossSpawnPoint);

                break;
        }
    }

    /// <summary>
    /// 전달받은 보스 프리팹을 지정된 위치에 생성하고 초기화한다.
    /// </summary>
    private void SpawnBoss(
        BossEnemy bossPrefab,
        Transform spawnPoint)
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException(
                "BossEncounterManager가 StageManager로부터 초기화되지 않았습니다.");
        }

        // 살아 있는 보스가 있는데 다른 보스를 중복 생성하지 않는다.
        if (_currentBoss != null && !_currentBoss.IsDead)
        {
            Debug.LogWarning(
                $"이미 전투 중인 보스가 있습니다: {_currentBoss.name}");

            return;
        }

        // BossEnemy 컴포넌트를 통해 프리팹 전체를 생성한다.
        _currentBoss = Instantiate(
            bossPrefab,
            spawnPoint.position,
            spawnPoint.rotation);

        // 보스 패턴에서 사용할 플레이어와 GameManager를 전달한다.
        _currentBoss.Initialize(
            _playerController,
            _gameManager);

        Debug.Log($"보스를 생성했습니다: {_currentBoss.name}");
    }
}
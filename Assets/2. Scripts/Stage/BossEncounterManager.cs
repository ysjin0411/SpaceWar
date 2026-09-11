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

    // 낙하 연출 컴포넌트가 붙어 있는 공통 TreasureBox 프리팹이다.
    // 등급별 외형 프리팹이 아니라 공통 본체를 연결한다.
    [SerializeField]
    TreasureBoxEntrance _treasureBoxPrefab;

    // 씬에 배치한 상자 착지 지점이다.
    [SerializeField]
    Transform _treasureBoxPoint;

    // 중간보스 처치 시 상자가 등장할 확률이다.
    // 0은 등장하지 않음, 1은 반드시 등장함을 의미한다.
    [SerializeField, Range(0f, 1f)]
    float _midBossTreasureChance = 1f;

    // 최종보스 처치 시 상자가 등장할 확률이다.
    [SerializeField, Range(0f, 1f)]
    float _finalBossTreasureChance = 1f;

    // 현재 생성된 공통 상자 본체다.
    // 이후 보상 지급과 진행 재개를 연결할 때 사용한다.
    TreasureBox _currentTreasureBox;

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

    // 에디터가 이 씬에 등록된 보스 배정을 읽을 때 사용한다.
    // 실제 전투의 생성 방식은 기존 그대로 유지한다.
    public BossEnemy MidBossPrefab => _midBossPrefab;
    public BossEnemy FinalBossPrefab => _finalBossPrefab;

    // 새 보스가 생성되었을 때 생성된 보스를 UI에 전달한다.
    public event Action<BossEnemy> BossSpawned;

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

        // 상자 생성에 필요한 참조가 빠졌다면 시작할 때 바로 알린다.
        if (_treasureBoxPrefab == null || _treasureBoxPoint == null)
        {
            throw new InvalidOperationException(
                $"{name}: Treasure Box Prefab과 Treasure Box Point를 연결하세요.");
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

            // 상자 진행 중 플레이어가 죽거나 스테이지가 종료되면 상자를 제거한다.
            case StagePhase.Failed:
            case StagePhase.Cleared:
                ClearTreasureBox();
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

        // 생성된 보스가 처치됐을 때 알림을 받는다.
        _currentBoss.Defeated += HandleBossDefeated;

        // 보스 UI가 HealthChanged 이벤트를 먼저 구독할 수 있도록
        // 보스 초기화 전에 생성된 보스를 전달한다.
        BossSpawned?.Invoke(_currentBoss);

        // 보스 패턴에서 사용할 플레이어와 GameManager를 전달한다.
        _currentBoss.Initialize(
            _playerController,
            _gameManager);

        Debug.Log($"보스를 생성했습니다: {_currentBoss.name}");
    }

    // 보스 사망 애니메이션이 끝났을 때 호출된다.
    // 상자가 나오면 다음 단계로 넘어가지 않고 보스 전투 상태를 유지한다.
    private void HandleBossDefeated(BossEnemy defeatedBoss)
    {
        // 현재 관리 중인 보스의 유효한 사망 알림만 처리한다.
        if (defeatedBoss == null || defeatedBoss != _currentBoss)
        {
            return;
        }

        // 구독 해제와 참조 초기화로 같은 보스의 중복 처리를 막는다.
        defeatedBoss.Defeated -= HandleBossDefeated;
        _currentBoss = null;

        StagePhase phase = _stageFlowManager.CurrentPhase;

        // 플레이어 사망 등으로 이미 전투가 끝났다면 상자를 생성하지 않는다.
        if (phase != StagePhase.MidBossBattle &&
            phase != StagePhase.FinalBossBattle)
        {
            return;
        }

        // 처치한 보스 구간에 맞는 등장 확률을 선택한다.
        float chance = phase == StagePhase.MidBossBattle
            ? _midBossTreasureChance
            : _finalBossTreasureChance;

        // 상자가 등장했다면 보상 처리가 끝날 때까지 진행을 보류한다.
        if (TrySpawnTreasureBox(chance))
        {
            return;
        }

        // 상자가 나오지 않았다면 기존 흐름으로 즉시 진행한다.
        _stageFlowManager.NotifyBossDefeated();
    }

    // 등장 확률을 판정하고 성공하면 상자를 생성한다.
    // 반환값이 true이면 상자 처리가 진행 중이므로 상태 전환을 보류한다.
    private bool TrySpawnTreasureBox(float chance)
    {
        // 이미 처리 중인 상자가 있다면 추가 생성하지 않는다.
        if (_currentTreasureBox != null)
        {
            return true;
        }

        // 0%는 반드시 실패하고, 100%는 반드시 성공하도록 처리한다.
        if (chance <= 0f ||
            (chance < 1f && UnityEngine.Random.value >= chance))
        {
            return false;
        }

        Vector3 landingPosition = _treasureBoxPoint.position;

        // 등급별 외형이 아닌 공통 상자 본체를 생성한다.
        TreasureBoxEntrance entrance = Instantiate(
            _treasureBoxPrefab,
            landingPosition,
            Quaternion.identity);

        _currentTreasureBox = entrance.GetComponent<TreasureBox>();

        // 설정 오류로 연출 시작에 실패하면 생성한 본체를 정리한다.
        try
        {
            // 내부에서 최종 등급을 한 번 추첨하고 낙하를 시작한다.
            // 두 번의 반동이 끝나면 자동으로 피격이 허용된다.
            entrance.Play(landingPosition);
        }
        catch
        {
            Destroy(entrance.gameObject);
            _currentTreasureBox = null;
            throw;
        }

        return true;
    }

    // 스테이지 종료 시 남아 있는 상자와 연결된 낙하 연출을 정리한다.
    private void ClearTreasureBox()
    {
        if (_currentTreasureBox == null)
        {
            return;
        }

        Destroy(_currentTreasureBox.gameObject);
        _currentTreasureBox = null;
    }
}
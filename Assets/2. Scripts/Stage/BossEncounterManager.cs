using System;
using UnityEngine;
using System.Collections;      
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

    // 상자의 모든 보상을 획득한 뒤 다음 진행까지 기다리는 시간이다.
    [SerializeField, Min(0f)]
    float _treasureRewardWaitSeconds = 2f;

    // 보상 획득 후 대기가 끝나면 상자가 서서히 사라지는 시간이다.
    [SerializeField, Min(0.01f)]
    float _treasureExitDuration = 0.5f;

    // 실패하거나 상자를 정리할 때 대기를 취소하기 위해 보관한다.
    Coroutine _treasureRewardWaitCoroutine;


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

        // 최종 등급에서 개봉되면 해당 등급의 보상을 지급한다.
        _currentTreasureBox.Opened += HandleTreasureBoxOpened;

        // 설정 오류로 연출 시작에 실패하면 생성한 본체를 정리한다.
        try
        {
            // 내부에서 최종 등급을 한 번 추첨하고 낙하를 시작한다.
            // 두 번의 반동이 끝나면 자동으로 피격이 허용된다.
            entrance.Play(landingPosition);
        }
        catch
        {
            // 등장 실패 시 개봉 이벤트 연결과 생성된 상자를 함께 정리한다.
            ClearTreasureBox();
            throw;
        }

        return true;
    }

    // 상자와 연결된 개봉 이벤트 및 다음 진행 대기를 정리한다.
    private void ClearTreasureBox()
    {
        // 대기 중 실패했다면 예약된 다음 진행도 취소한다.
        if (_treasureRewardWaitCoroutine != null)
        {
            StopCoroutine(_treasureRewardWaitCoroutine);
            _treasureRewardWaitCoroutine = null;
        }

        if (_currentTreasureBox == null)
        {
            return;
        }

        // 상자를 제거하기 전에 개봉 이벤트 연결을 해제한다.
        _currentTreasureBox.Opened -= HandleTreasureBoxOpened;

        Destroy(_currentTreasureBox.gameObject);
        _currentTreasureBox = null;
    }

    // 최종 등급의 보상 목록을 읽고 각 항목의 Count만큼 골드를 생성한다.
    private void HandleTreasureBoxOpened(TreasureBoxGradeData gradeData)
    {
        if (_currentTreasureBox == null)
        {
            return;
        }

        // 보상 일부만 지급한 뒤 설정 오류가 발생하지 않도록 먼저 전부 검사한다.
        foreach (TreasureBoxRewardEntry reward in gradeData.Rewards)
        {
            if (reward == null ||
                reward.RewardPrefab == null ||
                reward.Count < 1)
            {
                throw new InvalidOperationException(
                    $"{gradeData.name}: 보상 프리팹과 Count를 확인하세요.");
            }

            // 현재 지급 경로는 Gold 컴포넌트가 있는 골드 프리팹을 지원한다.
            if (!reward.RewardPrefab.TryGetComponent<Gold>(out _))
            {
                throw new InvalidOperationException(
                    $"{gradeData.name}: {reward.RewardPrefab.name}에 Gold가 없습니다.");
            }
        }

        // 이 보상을 발생시킨 상자를 기억해 이전 상자의 완료 알림을 구분한다.
        TreasureBox openedBox = _currentTreasureBox;

        // 같은 상자의 보상을 다시 지급하지 않도록 개봉 이벤트를 해제한다.
        openedBox.Opened -= HandleTreasureBoxOpened;

        // 현재 등급의 열린 상자 내부를 골드 출발 위치로 사용한다.
        Vector3 rewardPosition = openedBox.RewardPosition;

        // 각 리스트 항목은 자기 Count 전체를 획득했을 때 한 번 완료된다.
        int remainingRewardGroups = gradeData.Rewards.Count;

        // 보상이 없는 등급은 기다릴 획득 연출도 없다.
        if (remainingRewardGroups == 0)
        {
            HandleTreasureRewardsCollected(openedBox);
            return;
        }

        foreach (TreasureBoxRewardEntry reward in gradeData.Rewards)
        {
            Gold goldPrefab = reward.RewardPrefab.GetComponent<Gold>();

            // 기존 골드 생성 경로를 사용해 퍼짐, UI 이동, 금액 반영을 처리한다.
            _gameManager.SpawnGold(
                goldPrefab,
                rewardPosition,
                reward.Count,
                () =>
                {
                    // 관리자가 파괴되거나 해당 상자가 정리됐다면 진행하지 않는다.
                    if (this == null ||
                        openedBox == null ||
                        _currentTreasureBox != openedBox)
                    {
                        return;
                    }

                    // 현재 항목의 골드가 전부 획득되었으므로 남은 항목 수를 줄인다.
                    remainingRewardGroups--;

                    // 목록의 모든 항목을 획득한 뒤에만 다음 처리를 시작한다.
                    if (remainingRewardGroups == 0)
                    {
                        HandleTreasureRewardsCollected(openedBox);
                    }
                });
        }
    }

    // 모든 보상이 실제 금액에 반영된 뒤 대기를 시작한다.
    private void HandleTreasureRewardsCollected(TreasureBox openedBox)
    {
        if (openedBox == null ||
            _currentTreasureBox != openedBox ||
            _treasureRewardWaitCoroutine != null)
        {
            return;
        }

        // 이미 실패하거나 클리어했다면 다음 진행을 예약하지 않는다.
        StagePhase phase = _stageFlowManager.CurrentPhase;

        if (phase != StagePhase.MidBossBattle &&
            phase != StagePhase.FinalBossBattle)
        {
            return;
        }

        _treasureRewardWaitCoroutine =
            StartCoroutine(WaitAfterTreasureRewards(openedBox, phase));
    }

    // 보상 획득 후 대기하고, 상자의 퇴장까지 끝낸 뒤 다음 단계로 진행한다.
    private IEnumerator WaitAfterTreasureRewards(
        TreasureBox openedBox,
        StagePhase rewardPhase)
    {
        // 마지막 골드를 획득한 시점부터 설정한 시간만큼 기다린다.
        yield return new WaitForSeconds(_treasureRewardWaitSeconds);

        // 기다리는 동안 실패하거나 상자가 정리됐다면 종료한다.
        if (openedBox == null ||
            _currentTreasureBox != openedBox ||
            _stageFlowManager.CurrentPhase != rewardPhase)
        {
            _treasureRewardWaitCoroutine = null;
            yield break;
        }

        // 상자와 효과가 완전히 사라질 때까지 기다린다.
        // 이 동안에도 보스 전투 상태를 유지해 결과창이 먼저 뜨지 않게 한다.
        yield return openedBox.PlayExit(_treasureExitDuration);

        // 퇴장 중에도 실패나 상자 정리가 발생할 수 있으므로 다시 확인한다.
        if (openedBox == null ||
            _currentTreasureBox != openedBox ||
            _stageFlowManager.CurrentPhase != rewardPhase)
        {
            _treasureRewardWaitCoroutine = null;
            yield break;
        }

        // 정리 함수가 현재 코루틴을 중단하지 않도록 참조를 먼저 비운다.
        _treasureRewardWaitCoroutine = null;

        // 화면에서 완전히 사라진 상자 오브젝트를 제거한다.
        ClearTreasureBox();

        // 중간보스는 이동 연출, 최종보스는 클리어와 결과창으로 이어진다.
        _stageFlowManager.NotifyBossDefeated();
    }
}
using System;
using UnityEngine;
using System.Collections;
using DG.Tweening;
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

    // 중간보스가 처음 생성되는 화면 밖 위치다.
    [SerializeField] private Transform _midBossEntrancePoint;

    // 최종보스가 처음 생성되는 화면 밖 위치다.
    [SerializeField] private Transform _finalBossEntrancePoint;

    // 화면 밖에서 기존 전투 위치까지 이동하는 시간이다.
    [SerializeField, Min(0.1f)] private float _bossEntranceDuration = 2f;

    // 입장 중 실패하거나 씬을 나가면 이동 연출을 취소한다.
    private Tween _bossEntranceTween;

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

    // 같은 보스전에서 페이즈 프리팹이 교체됐을 때 새 보스를 알린다.
    // UI는 최초 등장 연출을 반복하지 않고 구독 대상만 변경한다.
    public event Action<BossEnemy> BossReplaced;

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

        // 기존 SpawnPoint는 도착 위치이고, EntrancePoint는 화면 밖 시작 위치다.
        if (_midBossEntrancePoint == null || _finalBossEntrancePoint == null)
        {
            throw new InvalidOperationException(
                $"{name}: 중간보스와 최종보스의 Entrance Point를 연결하세요.");
        }

        if (_bossEntranceDuration <= 0f)
        {
            throw new InvalidOperationException(
                $"{name}: 보스 입장 시간은 0보다 커야 합니다.");
        }
    }

    private void OnEnable()
    {
        // 스테이지의 보스전 시작과 종료를 받는다.
        if (_stageFlowManager != null)
        {
            _stageFlowManager.PhaseChanged += HandlePhaseChanged;
        }

        // 관리자가 다시 활성화됐다면 전투 중인 보스에 재연결한다.
        // 아직 입장 중인 보스는 입장 완료 시 연결한다.
        if (_currentBoss != null && _currentBoss.HasBattleStarted)
        {
            BindBossEvents(_currentBoss);
        }
    }

    private void OnDisable()
    {
        // 비활성화된 관리자가 스테이지 변경을 처리하지 않게 한다.
        if (_stageFlowManager != null)
        {
            _stageFlowManager.PhaseChanged -= HandlePhaseChanged;
        }

        // 보스가 남아 있어도 비활성화된 관리자로 요청이 오지 않게 한다.
        UnbindBossEvents(_currentBoss);

        // 진행 중이던 최초 입장 연출을 취소한다.
        CancelBossEntrance();
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
                // 화면 밖 시작 위치에서 기존 중간보스 위치까지 입장한다.
                SpawnBoss(
                    _midBossPrefab,
                    _midBossSpawnPoint,
                    _midBossEntrancePoint);
                break;

            case StagePhase.FinalBossBattle:
                // 화면 밖 시작 위치에서 기존 최종보스 위치까지 입장한다.
                SpawnBoss(
                    _finalBossPrefab,
                    _finalBossSpawnPoint,
                    _finalBossEntrancePoint);
                break;

            // 상자 진행 중 플레이어가 죽거나 스테이지가 종료되면 상자를 제거한다.
            case StagePhase.Failed:
            case StagePhase.Cleared:
                // 실패 또는 종료 시 입장 중인 보스가 뒤늦게 전투를 시작하지 않게 한다.
                CancelBossEntrance();

                // 기존 보물상자 정리도 유지한다.
                ClearTreasureBox();
                break;
        }
    }

    // 현재 보스의 최종 처치와 페이즈 교체 요청을 함께 구독한다.
    private void BindBossEvents(BossEnemy boss)
    {
        if (boss == null)
            return;

        // 재연결 시 같은 함수가 중복 구독되지 않도록 먼저 해제한다.
        UnbindBossEvents(boss);

        boss.Defeated += HandleBossDefeated;
        boss.DeathAnimationCompleted += HandleBossDeathAnimationCompleted;
        boss.PhaseReplacementRequested += HandlePhaseReplacementRequested;
    }

    // 이전 보스나 비활성화된 관리자의 이벤트 연결을 정리한다.
    private void UnbindBossEvents(BossEnemy boss)
    {
        if (boss == null)
            return;

        boss.Defeated -= HandleBossDefeated;
        boss.DeathAnimationCompleted -= HandleBossDeathAnimationCompleted;
        boss.PhaseReplacementRequested -= HandlePhaseReplacementRequested;
    }

    // 이전 페이즈의 연출이 끝나면 새 프리팹으로 전투 대상을 교체한다.
    private void HandlePhaseReplacementRequested(
        BossEnemy previousBoss,
        BossEnemy nextPhasePrefab,
        Vector3 spawnPosition,
        Quaternion spawnRotation)
    {
        // 현재 관리 중인 살아 있는 보스의 요청만 처리한다.
        // 교체 후 늦게 도착한 이전 보스의 요청은 여기서 제외된다.
        if (!isActiveAndEnabled ||
            !_isInitialized ||
            previousBoss == null ||
            previousBoss != _currentBoss ||
            !previousBoss.isActiveAndEnabled ||
            !previousBoss.HasBattleStarted ||
            previousBoss.IsDead ||
            previousBoss.CurrentHp > 0)
        {
            return;
        }

        // 실패하거나 끝난 보스전에서는 다음 페이즈를 생성하지 않는다.
        StagePhase phase = _stageFlowManager.CurrentPhase;

        if (phase != StagePhase.MidBossBattle &&
            phase != StagePhase.FinalBossBattle)
        {
            return;
        }

        // 전환 연출 중 플레이어가 사망한 경우도 제외한다.
        if (_playerController == null ||
            !_playerController.isActiveAndEnabled ||
            _playerController.IsDead)
        {
            return;
        }

        // 잘못된 프리팹 참조를 명확하게 알린다.
        if (nextPhasePrefab == null)
        {
            throw new InvalidOperationException(
                $"{name}: 다음 페이즈 프리팹이 없습니다.");
        }

        // 최초 입장 이동 없이 지정된 위치에 다음 페이즈를 생성한다.
        BossEnemy nextBoss = Instantiate(
            nextPhasePrefab,
            spawnPosition,
            spawnRotation);

        Rigidbody2D nextBody = nextBoss.GetComponent<Rigidbody2D>();

        // 물리 충돌은 초기화와 외부 참조 교체가 끝난 뒤 허용한다.
        // 초기화 실패 시에는 새 오브젝트를 정리하고 이전 보스를 유지한다.
        try
        {
            if (nextBody == null)
            {
                throw new MissingComponentException(
                    $"{nextBoss.name}: Rigidbody2D가 없습니다.");
            }

            nextBody.simulated = false;
            nextBody.position = new Vector2(
                spawnPosition.x,
                spawnPosition.y);

            nextBoss.Initialize(_playerController, _gameManager);
        }
        catch
        {
            // 생성된 본체가 불완전한 상태로 씬에 남지 않게 한다.
            nextBoss.gameObject.SetActive(false);
            Destroy(nextBoss.gameObject);
            throw;
        }

        // 준비된 본체로 관리자 참조와 이벤트 연결을 넘긴다.
        UnbindBossEvents(previousBoss);
        _currentBoss = nextBoss;
        BindBossEvents(nextBoss);

        // 이전 로봇은 최종 사망 함수를 호출하지 않고 제거한다.
        // 따라서 경험치, 드롭, 보스전 종료 이벤트가 발생하지 않는다.
        previousBoss.gameObject.SetActive(false);
        Destroy(previousBoss.gameObject);

        // UI와 다른 시스템이 새 보스로 구독 대상을 변경하게 한다.
        // 새 보스는 이미 초기화됐으므로 현재 체력을 바로 읽을 수 있다.
        BossReplaced?.Invoke(nextBoss);

        // 알림 처리 중 전투가 중단됐으면 물리 충돌을 재개하지 않는다.
        if (isActiveAndEnabled &&
            _currentBoss == nextBoss &&
            nextBoss != null &&
            nextBoss.isActiveAndEnabled &&
            !nextBoss.IsDead &&
            _playerController != null &&
            _playerController.isActiveAndEnabled &&
            !_playerController.IsDead &&
            _stageFlowManager.CurrentPhase == phase)
        {
            nextBody.simulated = true;
        }
    }

    private void SpawnBoss(
    BossEnemy bossPrefab,
    Transform battlePoint,
    Transform entrancePoint)
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException(
                "BossEncounterManager가 초기화되지 않았습니다.");
        }

        // 입장 중인 보스도 현재 보스로 취급하여 중복 생성을 막는다.
        if (_currentBoss != null && !_currentBoss.IsDead)
        {
            return;
        }

        // 기존 SpawnPoint의 위치를 도착 위치로 보관한다.
        Vector3 destination = battlePoint.position;

        // 어느 전투 단계에서 시작한 입장인지 기록한다.
        StagePhase entrancePhase = _stageFlowManager.CurrentPhase;

        // 보스는 화면 밖에서 생성한다.
        BossEnemy enteringBoss = Instantiate(
            bossPrefab,
            entrancePoint.position,
            battlePoint.rotation);

        // 에일리언은 화면 안의 전투 도착 위치에서 페이즈 전환을 재생한다.
        if (enteringBoss is Boss_Alien alien)
        {
            alien.SetPhaseTransitionPosition(destination);
        }

        _currentBoss = enteringBoss;

        // 화면 밖에서 진입을 시작하는 순간, 보스별 등장음을 한 번 재생한다.
        // 소유 보스가 제거되면 중단되어 취소된 입장의 소리가 남지 않는다.
        if (enteringBoss is Boss_Head)
            GameAudio.PlayOwned(GameSound.HeadIntro, enteringBoss);
        else if (enteringBoss is Boss_Cruiser)
            GameAudio.PlayOwned(GameSound.CruiserIntro, enteringBoss);
        else if (enteringBoss is Boss_Alien)
            GameAudio.PlayOwned(GameSound.AlienRobotIntro, enteringBoss);

        Rigidbody2D body = enteringBoss.GetComponent<Rigidbody2D>();

        // 입장 중에는 자식 타워를 포함한 물리 충돌을 비활성화한다.
        // Collider의 개별 enabled 상태는 변경하지 않는다.
        body.simulated = false;

        // Initialize를 아직 호출하지 않았으므로
        // 보스 이동과 패턴, 크루저 타워의 공격 시간은 시작되지 않는다.
        _bossEntranceTween = enteringBoss.transform
            .DOMove(destination, _bossEntranceDuration)
            .SetEase(Ease.OutSine)
            .SetLink(enteringBoss.gameObject)
            .OnComplete(() =>
            {
                // 입장 도중 전투가 끝났거나 플레이어가 죽었다면 시작하지 않는다.
                if (!isActiveAndEnabled ||
                    enteringBoss == null ||
                    _currentBoss != enteringBoss ||
                    _stageFlowManager.CurrentPhase != entrancePhase ||
                    _playerController.IsDead)
                {
                    CancelBossEntrance();
                    return;
                }

                _bossEntranceTween = null;

                // Rigidbody 위치도 도착 위치로 맞춘다.
                // 이후 일반 이동의 기준점이 이 위치가 된다.
                body.position = new Vector2(
                    destination.x,
                    destination.y);

                // 최초 보스도 최종 처치와 페이즈 교체 요청을 모두 연결한다.
                BindBossEvents(enteringBoss);

                // 체력 초기 알림을 받을 수 있도록 UI를 먼저 연결한다.
                if (BossSpawned != null)
                {
                    BossSpawned.Invoke(enteringBoss);
                }

                // 도착 후에만 이동과 패턴, 타워별 공격 시간을 시작한다.
                enteringBoss.Initialize(
                    _playerController,
                    _gameManager);
                if (enteringBoss is Boss_Dog) GameAudio.Play(GameSound.DogIntroGrowl); // 실제 전투 시작.

                // 각 Collider의 기존 활성 상태를 유지하며 물리 참여를 재개한다.
                // 크루저의 1페이즈 본체 Collider는 계속 꺼져 있다.
                body.simulated = true;
            });
    }

    /// <summary>
    /// 현재 보스의 사망 연출이 끝나면 같은 씬의 남은 일반 적을 정리한다.
    /// </summary>
    private void HandleBossDeathAnimationCompleted(BossEnemy defeatedBoss)
    {
        // 교체된 이전 보스나 다른 전투의 알림은 처리하지 않는다.
        if (defeatedBoss == null || defeatedBoss != _currentBoss)
        {
            return;
        }

        // 보스 종료 시 한 번만 탐색하며, 비활성 풀 객체와 정렬 비용을 제외한다.
        Enemy[] enemies = FindObjectsByType<Enemy>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (Enemy enemy in enemies)
        {
            // 다른 씬의 적은 유지하고 현재 전투에 남은 적만 폭발시킨다.
            if (enemy != null && enemy.gameObject.scene == defeatedBoss.gameObject.scene)
            {
                enemy.DefeatAfterBossDeath();
            }
        }
    }

    // 보스 사망 연출과 드롭 회수가 끝났을 때 호출된다.
    // 상자가 나오면 다음 단계로 넘어가지 않고 보스 전투 상태를 유지한다.
    private void HandleBossDefeated(BossEnemy defeatedBoss)
    {
        // 현재 관리 중인 보스의 유효한 사망 알림만 처리한다.
        if (defeatedBoss == null || defeatedBoss != _currentBoss)
        {
            return;
        }

        // 최종 처치가 완료된 보스의 모든 관리자 구독을 해제한다.
        UnbindBossEvents(defeatedBoss);
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

            // 보물상자 보상이 Money 계열 프리팹인지 검사한다.
            if (!reward.RewardPrefab.TryGetComponent<Money>(out Money money))
            {
                throw new InvalidOperationException(
                    $"{gradeData.name}: {reward.RewardPrefab.name}에 Money 계열 컴포넌트가 없습니다.");
            }

            // 생성 전에 해당 재화의 목적지 UI까지 확인한다.
            _gameManager.GetMoneyUI(money.Type).ValidateConfiguration();
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
            // 골드뿐 아니라 모든 Money 자식 프리팹을 받는다.
            Money moneyPrefab = reward.RewardPrefab.GetComponent<Money>();

            // 기존 골드 생성 경로를 사용해 퍼짐, UI 이동, 금액 반영을 처리한다.
            _gameManager.SpawnMoney(
                moneyPrefab,
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

    private void CancelBossEntrance()
    {
        // 이미 입장을 끝내 전투 중이라면 이 함수에서 제거하지 않는다.
        if (_bossEntranceTween == null)
        {
            return;
        }

        // 완료 콜백을 실행하지 않고 입장 이동을 취소한다.
        _bossEntranceTween.Kill();
        _bossEntranceTween = null;

        if (_currentBoss == null || _currentBoss.HasBattleStarted)
        {
            return;
        }

        // 입장 중이던 보스는 처치 보상 없이 정리한다.
        BossEnemy cancelledBoss = _currentBoss;
        _currentBoss = null;

        cancelledBoss.gameObject.SetActive(false);
        Destroy(cancelledBoss.gameObject);
    }
}

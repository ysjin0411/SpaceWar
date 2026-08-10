using System;
using UnityEngine;

/// <summary>
/// Stage의 적 생성 패턴을 관리한다.
/// 캐릭터 프리팹은 Stage 시작 시 동적으로 생성되므로, Inspector에 고정된 PlayerController를 사용하는 대신
/// StageManager가 생성한 PlayerController를 Initialize()로 전달받는다.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("적 프리팹")]
    [Tooltip("패턴에서 생성할 FastEnemy 프리팹")]
    [SerializeField] GameObject _fastEnemyPrefab;

    [Header("런타임 의존성")]
    [Tooltip("StageManager가 생성한 현재 플레이어")]
    [SerializeField] PlayerController _playerController;
    [Tooltip("현재 Stage의 GameManager")]
    [SerializeField] GameManager _gameManager;

    [Header("스폰 설정")]
    [Tooltip("하나의 패턴을 생성한 후 다음 패턴까지의 시간")]
    [SerializeField] float _patternCoolTime;
    [Tooltip("오른쪽에서 생성되는 적의 기본 X 위치")]
    [SerializeField] float _spawnX = 10f;

    [Header("패턴 확률")]
    [Tooltip("패턴이 등장할 때마다 다음 확률에 적용할 변화량")]
    [SerializeField] int _reducedPercent = 15;
    [Tooltip("패턴 A의 현재 가중치")]
    [SerializeField] int _patternAPercent = 50;
    [Tooltip("패턴 B의 현재 가중치")]
    [SerializeField] int _patternBPercent = 50;

    // 다음 패턴까지 흐른 시간
    float _timer;

    // StageManager가 새 플레이어를 전달했는지 확인한다.
    bool _isInitialized;

    /// <summary>
    /// StageManager가 생성한 플레이어와 현재 Stage의 GameManager를 전달한다.
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

        if (_fastEnemyPrefab == null)
        {
            throw new InvalidOperationException("EnemySpawner에 FastEnemy 프리팹이 연결되지 않았습니다.");
        }

        // 비활성화된 옛 Player 참조를 새로 생성된 선택 캐릭터로 교체한다.
        _playerController = playerController;
        _gameManager = gameManager;

        // Stage 시작 시 패턴 타이머를 초기화한다.
        _timer = 0f;

        // 이제 적을 생성해도 되는 상태다.
        _isInitialized = true;
    }

    private void Update()
    {
        // 새 플레이어가 연결되기 전에는 적을 생성하지 않는다.
        if (!_isInitialized)
        {
            return;
        }

        _timer += Time.deltaTime;

        if (_timer < _patternCoolTime)
        {
            return;
        }

        PickPattern();

        _timer = 0f;
    }

    /// <summary>
    /// 오른쪽에서 왼쪽으로 이동하는 적 5명을 생성한다.
    /// </summary>
    public void SpawnFastEnemyPatternA()
    {
        // 패턴 A가 등장했으므로 다음에는 패턴 B가 등장할 가능성을 높인다.
        _patternAPercent -= _reducedPercent;
        _patternBPercent += _reducedPercent;

        for (int i = 0; i < 5; i++)
        {
            Vector3 spawnPosition = new Vector3(_spawnX + 2f * i, 2f, 0f);

            SpawnFastEnemy(spawnPosition, MoveType.RightToLeft);
        }
    }

    /// <summary>
    /// 아래에서 위로 이동하는 적 5명을 생성한다.
    /// </summary>
    public void SpawnFastEnemyPatternB()
    {
        // 패턴 B가 등장했으므로 다음에는 패턴 A가 등장할 가능성을 높인다.
        _patternBPercent -= _reducedPercent;
        _patternAPercent += _reducedPercent;

        for (int i = 0; i < 5; i++)
        {
            Vector3 spawnPosition = new Vector3(5f, -6f - i, 0f);

            SpawnFastEnemy(spawnPosition, MoveType.DownToUp);
        }
    }

    /// <summary>
    /// FastEnemy를 한 명 생성하고
    /// 현재 플레이어와 GameManager를 전달한다.
    /// </summary>
    private void SpawnFastEnemy(Vector3 spawnPosition, MoveType moveType)
    {
        GameObject enemyObject = Instantiate(_fastEnemyPrefab, spawnPosition, Quaternion.identity);

        FastEnemy enemy = enemyObject.GetComponent<FastEnemy>();

        if (enemy == null)
        {
            Destroy(enemyObject);

            throw new MissingComponentException("FastEnemy 프리팹에 FastEnemy 컴포넌트가 없습니다.");
        }

        // 적과 적 투사체가 새로 생성된 플레이어를 추적하도록 전달한다.
        enemy.InitPlayerController(_playerController);

        // 적 처치 시 보너스 게이지를 처리할 GameManager를 전달한다.
        enemy.InitGameManager(_gameManager);

        enemy.SetMoveType(moveType);
    }

    /// <summary>
    /// 현재 패턴 가중치에 따라 다음 패턴을 선택한다.
    /// </summary>
    private void PickPattern()
    {
        // 확률이 음수가 되지 않도록 보정한다.
        _patternAPercent = Mathf.Max(0, _patternAPercent);

        _patternBPercent = Mathf.Max(0, _patternBPercent);

        int totalPercent = _patternAPercent + _patternBPercent;

        // 두 패턴의 확률이 모두 0이면 선택할 수 없다.
        if (totalPercent <= 0)
        {
            throw new InvalidOperationException("EnemySpawner의 패턴 확률 합계가 0입니다.");
        }

        int randomValue = UnityEngine.Random.Range(0, totalPercent);

        if (randomValue < _patternAPercent)
        {
            SpawnFastEnemyPatternA();
        }
        else
        {
            SpawnFastEnemyPatternB();
        }
    }
}
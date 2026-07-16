using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("프리팹")]
    [SerializeField] GameObject _fastEnemyPrefab;

    [Header("컴포넌트")]
    [SerializeField] PlayerController _playerController;
    [SerializeField] GameManager _gameManager;


    [Header("적 종류")]
    [SerializeField] FastEnemy _fastenemy;

    [Header("스폰 설정")]
    [SerializeField] float _patternCoolTime;
    [SerializeField] float _spawnX = 10f;
    [SerializeField] float _minY = -4f;
    [SerializeField] float _maxY = 4f;

    [Header("패턴 확률")]
    [SerializeField] int _reducedPercent = 15;
    [SerializeField] int _totalPercent = 100;
    [SerializeField] int _patternAPercent = 50;
    [SerializeField] int _patternBPercent = 50;

    [SerializeField] float _timer;

    private void Update()
    {
        _timer += Time.deltaTime;

        if(_timer >= _patternCoolTime)
        {
            PickPattern();
            _timer = 0;
        }
    }

    public void SpawnFastEnemyPatternA()
    {
        _patternBPercent += _reducedPercent;
        _patternAPercent -= _reducedPercent;
        for (int i = 0; i < 5; i++)
        {
            Vector3 spawnPos = new Vector3(10f + 2 * i, 2f, 0f);
            GameObject enemyObj = Instantiate(_fastEnemyPrefab, spawnPos, Quaternion.identity);
            FastEnemy enemy = enemyObj.GetComponent<FastEnemy>();
            enemy.InitPlayerController(_playerController);
            enemy.InitGameManager(_gameManager);
            enemy.SetMoveType(MoveType.RightToLeft);
        }
    }

    public void SpawnFastEnemyPatternB()
    {
        _patternBPercent -= _reducedPercent;
        _patternAPercent += _reducedPercent;
            for (int i = 0; i < 5; i++)
            {
                Vector3 spawnPos = new Vector3(5f, -6f - i, 0f);
                GameObject enemyObj = Instantiate(_fastEnemyPrefab, spawnPos, Quaternion.identity);
                FastEnemy enemy = enemyObj.GetComponent<FastEnemy>();
            enemy.InitPlayerController(_playerController);
            enemy.InitGameManager(_gameManager);
            enemy.SetMoveType(MoveType.DownToUp);
            }        
    }

    private void PickPattern()
    {
        _totalPercent = _patternAPercent + _patternBPercent;
        int num = Random.Range(0, _totalPercent);
        if(num <= _patternAPercent)
        {
            SpawnFastEnemyPatternA();
        }
        else
        {
            SpawnFastEnemyPatternB(); 
        }
    }
}
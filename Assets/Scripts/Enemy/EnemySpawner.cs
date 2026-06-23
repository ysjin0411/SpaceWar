using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("프리팹")]
    [SerializeField] GameObject _normalEnemyPrefab;
    [SerializeField] GameObject _bossEnemyPrefab;

    [Header("스폰 설정")]
    [SerializeField] float _spawnInterval = 2f;
    [SerializeField] float _spawnX = 10f;
    [SerializeField] float _minY = -4f;
    [SerializeField] float _maxY = 4f;

    float _timer;

    private void Update()
    {
        _timer += Time.deltaTime;

        if (_timer >= _spawnInterval)
        {
            SpawnNormalEnemy();
            _timer = 0f;
        }
    }

    void SpawnNormalEnemy()
    {
        float randomY = Random.Range(_minY, _maxY);
        Vector3 spawnPos = new Vector3(_spawnX, randomY, 0f);

        Instantiate(_normalEnemyPrefab, spawnPos, Quaternion.identity);
    }

    public void SpawnBoss()
    {
        Vector3 spawnPos = new Vector3(_spawnX, 0f, 0f);

        Instantiate(_bossEnemyPrefab, spawnPos, Quaternion.identity);
    }
}
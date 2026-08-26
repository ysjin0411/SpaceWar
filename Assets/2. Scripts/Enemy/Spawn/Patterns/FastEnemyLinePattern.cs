using System;
using UnityEngine;

/// <summary>
/// 일정한 간격으로 FastEnemy 여러 마리를 직선 형태로 생성한다.
/// 가로 편대와 세로 편대는 같은 클래스를 사용하고 에셋 설정만 다르게 만든다.
/// </summary>
[CreateAssetMenu(
    fileName = "FastEnemyLinePattern",
    menuName = "EnemyPattern/FastEnemy Line")]
public sealed class FastEnemyLinePattern : EnemySpawnPattern
{
    [Header("적 설정")]

    [SerializeField] FastEnemy _enemyPrefab; // 이 패턴에서 생성할 FastEnemy 프리팹

    [SerializeField] FastEnemyMoveType _moveType; // 생성된 FastEnemy가 사용할 이동 방향

    [Header("편대 설정")]

    [Min(1)]
    [SerializeField] int _enemyCount = 5; // 한 번에 생성할 FastEnemy 수

    [SerializeField] Vector2 _startPosition; // 첫 번째 적이 생성되는 월드 위치

    [SerializeField] Vector2 _spawnOffset; // 다음 적을 생성할 때마다 더할 위치 간격

    /// <summary>
    /// 설정된 시작 위치와 간격을 사용해 FastEnemy 직선 편대를 생성한다.
    /// </summary>
    public override void Execute(
        EnemySpawnContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(
                nameof(context));
        }

        if (_enemyPrefab == null)
        {
            throw new InvalidOperationException(
                $"{name}에 FastEnemy Prefab이 연결되지 않았습니다.");
        }

        for (int enemyIndex = 0;
             enemyIndex < _enemyCount;
             enemyIndex++)
        {
            // 첫 위치에 적 순서만큼 간격을 더해 생성 위치를 계산한다.
            Vector2 spawnPosition =
                _startPosition +
                _spawnOffset * enemyIndex;

            // 공통 생성 기능을 통해 적을 생성하고 Enemy.Init()까지 처리한다.
            FastEnemy spawnedEnemy =
                context.SpawnEnemy(
                    _enemyPrefab,
                    spawnPosition,
                    Quaternion.identity);

            // 이 패턴 에셋에 설정된 이동 방향을 전달한다.
            spawnedEnemy.SetMoveType(
                _moveType);
        }
    }
}
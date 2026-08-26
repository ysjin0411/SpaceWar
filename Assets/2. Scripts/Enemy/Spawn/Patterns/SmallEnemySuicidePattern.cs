using System;
using UnityEngine;

/// <summary>
/// SmallEnemy 여러 마리를 나란히 생성하고
/// 각 적에게 진입 위치와 그물 대형 위치를 전달한다.
/// </summary>
[CreateAssetMenu(
    fileName = "SmallEnemySuicidePattern",
    menuName = "EnemyPattern/SmallEnemy Suicide")]
public sealed class SmallEnemySuicidePattern : EnemySpawnPattern
{
    [Header("적 설정")]

    [SerializeField] SmallEnemy _enemyPrefab; // 자폭 편대에 사용할 SmallEnemy 프리팹

    [Min(1)]
    [SerializeField] int _enemyCount = 5; // 한 편대를 구성하는 SmallEnemy 수

    [Header("생성 대형 설정")]

    [SerializeField]
    Vector2 _spawnCenterPosition = new Vector2(10f, 0f); // 화면 밖 생성 대형의 중앙 위치

    [Min(0f)]
    [SerializeField] float _spawnYInterval = 0.7f; // 생성될 때 적 사이의 Y축 간격

    [Header("진입 대형 설정")]

    [SerializeField]
    Vector2 _enterCenterPosition = new Vector2(7f, 0f); // 화면 안으로 진입한 대형의 중앙 위치

    [Header("그물 대형 설정")]

    [SerializeField]
    Vector2 _formationCenterPosition = new Vector2(5f, 0f); // 펼쳐진 그물 대형의 중앙 위치

    [Min(0f)]
    [SerializeField] float _formationYInterval = 1.15f; // 펼쳐진 적 사이의 Y축 간격

    [Min(0f)]
    [SerializeField] float _formationXInterval = 0.8f; // 바깥쪽 적이 뒤로 벌어지는 X축 간격

    /// <summary>
    /// SmallEnemy 자폭 편대를 생성하고 각 개체의 이동 위치를 설정한다.
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
                $"{name}에 SmallEnemy Prefab이 연결되지 않았습니다.");
        }

        // 홀수와 짝수 편대 모두 중앙을 기준으로 배치하기 위한 값이다.
        float centerIndex =
            (_enemyCount - 1) * 0.5f;

        for (int enemyIndex = 0;
             enemyIndex < _enemyCount;
             enemyIndex++)
        {
            // 현재 적이 편대 중앙에서 몇 칸 떨어져 있는지 계산한다.
            float indexOffset =
                enemyIndex - centerIndex;

            // 좁은 Y축 간격으로 화면 밖 생성 위치를 계산한다.
            Vector2 spawnPosition =
                _spawnCenterPosition +
                Vector2.up *
                (indexOffset * _spawnYInterval);

            // 생성 대형과 같은 간격을 유지한 진입 위치를 계산한다.
            Vector2 enterPosition =
                _enterCenterPosition +
                Vector2.up *
                (indexOffset * _spawnYInterval);

            // 중앙에서 멀리 떨어진 적일수록 오른쪽으로 물러나게 한다.
            float distanceFromCenter =
                Mathf.Abs(indexOffset);

            // 플레이어 방향으로 벌어진 그물 대형 위치를 계산한다.
            Vector2 formationPosition =
                _formationCenterPosition +
                new Vector2(
                    distanceFromCenter *
                    _formationXInterval,
                    indexOffset *
                    _formationYInterval);

            // 공통 생성 기능을 통해 SmallEnemy를 생성하고 초기화한다.
            SmallEnemy spawnedEnemy =
                context.SpawnEnemy(
                    _enemyPrefab,
                    spawnPosition,
                    Quaternion.identity);

            // 이 개체가 담당할 진입 위치와 그물 대형 위치를 전달한다.
            spawnedEnemy.InitializeSuicidePattern(
                enterPosition,
                formationPosition);
        }
    }
}
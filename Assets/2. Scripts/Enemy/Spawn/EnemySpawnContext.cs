using System;
using UnityEngine;

/// <summary>
/// 적 패턴 실행에 필요한 현재 스테이지 정보를 전달하고
/// 적 생성과 초기화 과정을 공통으로 처리한다.
/// </summary>
public sealed class EnemySpawnContext
{
    // 현재 스테이지에서 생성된 플레이어
    public PlayerController PlayerController { get; }

    // 적 보상 처리 등에 사용하는 현재 스테이지 GameManager
    public GameManager GameManager { get; }

    /// <summary>
    /// EnemySpawner가 현재 스테이지의 런타임 참조를 전달한다.
    /// </summary>
    public EnemySpawnContext(PlayerController playerController, GameManager gameManager)
    {
        if (playerController == null)
        {
            throw new ArgumentNullException(
                nameof(playerController));
        }

        if (gameManager == null)
        {
            throw new ArgumentNullException(
                nameof(gameManager));
        }

        PlayerController = playerController;
        GameManager = gameManager;
    }

    /// <summary>
    /// 적 프리팹을 생성하고 Enemy 부모 클래스의 공통 초기화를 실행한다.
    /// </summary>
    public T SpawnEnemy<T>(T enemyPrefab, Vector3 spawnPosition, Quaternion spawnRotation)
        where T : Enemy
    {
        if (enemyPrefab == null)
        {
            throw new ArgumentNullException(
                nameof(enemyPrefab));
        }

        // 패턴이 지정한 위치와 회전값으로 적을 생성한다.
        T spawnedEnemy = UnityEngine.Object.Instantiate(enemyPrefab, spawnPosition, spawnRotation);

        // 모든 일반 적이 공통으로 사용하는 런타임 참조를 전달한다.
        spawnedEnemy.Init(PlayerController, GameManager);

        return spawnedEnemy;
    }
}
using System;
using UnityEngine;
using System.Collections;

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

    // 순차 생성 작업을 스포너에 전달하는 함수다.
    readonly Action<IEnumerator> _runSequence;

    /// <summary>
    /// 스테이지 참조와 순차 생성 실행 함수를 전달받는다.
    /// </summary>
    public EnemySpawnContext(
        PlayerController playerController,
        GameManager gameManager,
        Action<IEnumerator> runSequence)
    {
        if (playerController == null)
        {
            throw new ArgumentNullException(nameof(playerController));
        }

        if (gameManager == null)
        {
            throw new ArgumentNullException(nameof(gameManager));
        }

        if (runSequence == null)
        {
            throw new ArgumentNullException(nameof(runSequence));
        }

        PlayerController = playerController;
        GameManager = gameManager;
        _runSequence = runSequence;
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



    /// <summary>
    /// 패턴이 만든 생성 순서를 스포너에서 실행한다.
    /// </summary>
    public void RunSequence(IEnumerator sequence)
    {
        if (sequence == null)
        {
            throw new ArgumentNullException(nameof(sequence));
        }

        _runSequence(sequence);
    }
}
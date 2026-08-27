using System;
using UnityEngine;

/// <summary>
/// 보스 패턴 실행에 필요한 공통 참조를 전달하는 객체다.
/// 패턴 ScriptableObject가 실행 중인 보스의 상태를 직접 저장하지 않게 한다.
/// </summary>
public sealed class BossPatternContext
{
    // 현재 패턴을 실행하는 보스다.
    public BossEnemy Boss { get; }

    // 보스 패턴의 공격 대상이 되는 플레이어다.
    public PlayerController TargetPlayer { get; }

    // 보스의 물리 이동에 사용하는 Rigidbody2D다.
    public Rigidbody2D Rigidbody { get; }

    // 보스의 외형과 애니메이션이 들어 있는 자식 Transform이다.
    // 박치기 패턴에서 플레이어 방향으로 외형을 회전할 때 사용한다.
    public Transform VisualTransform { get; }

    // 적 생성이나 게임 공통 기능이 필요할 때 사용하는 GameManager다.
    public GameManager GameManager { get; }

    /// <summary>
    /// 보스 패턴들이 공통으로 사용할 참조를 전달받는다.
    /// </summary>
    public BossPatternContext(
        BossEnemy boss,
        PlayerController targetPlayer,
        Rigidbody2D rigidbody,
        Transform visualTransform,
        GameManager gameManager)
    {
        Boss = boss
            ?? throw new ArgumentNullException(nameof(boss));

        TargetPlayer = targetPlayer
            ?? throw new ArgumentNullException(nameof(targetPlayer));

        Rigidbody = rigidbody
            ?? throw new ArgumentNullException(nameof(rigidbody));

        VisualTransform = visualTransform
            ?? throw new ArgumentNullException(nameof(visualTransform));

        GameManager = gameManager
            ?? throw new ArgumentNullException(nameof(gameManager));
    }
}
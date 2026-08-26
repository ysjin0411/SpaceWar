using System;
using UnityEngine;

/// <summary>
/// EnemySpawnPattern의 실행 중 상태를 관리한다.
/// ScriptableObject 원본에는 변하는 값을 저장하지 않는다.
/// </summary>
public sealed class EnemySpawnPatternRuntimeState
{
    // 이 런타임 상태가 관리하는 원본 패턴
    public EnemySpawnPattern Pattern { get; }

    // 이 패턴을 다시 선택할 수 있을 때까지 남은 시간
    public float RemainingCooldown { get; private set; }

    // 쿨다운이 끝나 현재 선택할 수 있는지 반환한다.
    public bool IsAvailable =>
        RemainingCooldown <= 0f;

    /// <summary>
    /// 패턴 원본을 받아 새로운 런타임 상태를 만든다.
    /// </summary>
    public EnemySpawnPatternRuntimeState(
        EnemySpawnPattern pattern)
    {
        if (pattern == null)
        {
            throw new ArgumentNullException(
                nameof(pattern));
        }

        Pattern = pattern;

        // 스테이지가 시작될 때는 모든 패턴을 바로 선택할 수 있다.
        RemainingCooldown = 0f;
    }

    /// <summary>
    /// 매 프레임 남은 쿨다운을 감소시킨다.
    /// </summary>
    public void UpdateCooldown(float deltaTime)
    {
        // 이미 쿨다운이 끝났다면 추가 계산을 하지 않는다.
        if (RemainingCooldown <= 0f)
        {
            return;
        }

        // 음수 시간이 전달되어 쿨다운이 증가하는 것을 방지한다.
        if (deltaTime <= 0f)
        {
            return;
        }

        RemainingCooldown = Mathf.Max(
            0f,
            RemainingCooldown - deltaTime);
    }

    /// <summary>
    /// 패턴이 선택됐을 때 원본 패턴의 쿨다운을 적용한다.
    /// </summary>
    public void StartCooldown()
    {
        RemainingCooldown = Pattern.Cooldown;
    }

    /// <summary>
    /// 스테이지를 다시 시작할 때 쿨다운을 즉시 초기화한다.
    /// </summary>
    public void ResetCooldown()
    {
        RemainingCooldown = 0f;
    }
}
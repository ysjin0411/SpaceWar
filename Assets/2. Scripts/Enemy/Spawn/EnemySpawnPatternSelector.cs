using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 등록된 적 패턴들의 쿨다운을 관리하고
/// 실행 가능한 패턴 중 하나를 가중치 방식으로 선택한다.
/// </summary>
public sealed class EnemySpawnPatternSelector
{
    // 패턴 원본과 실행 중 쿨다운을 묶어서 관리한다.
    readonly List<EnemySpawnPatternRuntimeState> _runtimeStates =
        new List<EnemySpawnPatternRuntimeState>();

    /// <summary>
    /// EnemySpawner에 등록된 패턴들로 런타임 상태를 생성한다.
    /// </summary>
    public EnemySpawnPatternSelector(
        IReadOnlyList<EnemySpawnPattern> patterns)
    {
        if (patterns == null)
        {
            throw new ArgumentNullException(
                nameof(patterns));
        }

        if (patterns.Count == 0)
        {
            throw new ArgumentException(
                "등록된 적 생성 패턴이 없습니다.",
                nameof(patterns));
        }

        // 같은 ScriptableObject가 중복 등록되는 것을 검사한다.
        HashSet<EnemySpawnPattern> uniquePatterns =
            new HashSet<EnemySpawnPattern>();

        for (int patternIndex = 0;
             patternIndex < patterns.Count;
             patternIndex++)
        {
            EnemySpawnPattern pattern =
                patterns[patternIndex];

            if (pattern == null)
            {
                throw new ArgumentException(
                    $"패턴 목록의 {patternIndex}번 요소가 비어 있습니다.",
                    nameof(patterns));
            }

            if (!uniquePatterns.Add(pattern))
            {
                throw new ArgumentException(
                    $"동일한 패턴이 중복 등록됐습니다: {pattern.PatternId}",
                    nameof(patterns));
            }

            // ScriptableObject마다 독립적인 런타임 쿨다운 상태를 만든다.
            _runtimeStates.Add(
                new EnemySpawnPatternRuntimeState(pattern));
        }
    }

    /// <summary>
    /// 모든 패턴의 남은 쿨다운을 감소시킨다.
    /// </summary>
    public void UpdateCooldowns(float deltaTime)
    {
        for (int stateIndex = 0;
             stateIndex < _runtimeStates.Count;
             stateIndex++)
        {
            _runtimeStates[stateIndex]
                .UpdateCooldown(deltaTime);
        }
    }

    /// <summary>
    /// 실행 가능한 패턴 중 가중치에 따라 하나를 선택한다.
    /// 모든 패턴이 쿨다운 중이면 false를 반환한다.
    /// </summary>
    public bool TrySelect(
        out EnemySpawnPatternRuntimeState selectedState)
    {
        selectedState = null;

        // 현재 실행 가능한 패턴들의 전체 가중치를 계산한다.
        int totalWeight = 0;

        for (int stateIndex = 0;
             stateIndex < _runtimeStates.Count;
             stateIndex++)
        {
            EnemySpawnPatternRuntimeState runtimeState =
                _runtimeStates[stateIndex];

            if (!runtimeState.IsAvailable)
            {
                continue;
            }

            totalWeight += runtimeState.Pattern.Weight;
        }

        // 모든 패턴이 쿨다운 중이면 지금은 선택하지 않는다.
        if (totalWeight <= 0)
        {
            return false;
        }

        // 실행 가능한 전체 가중치 범위에서 임의의 값을 선택한다.
        int randomValue =
            UnityEngine.Random.Range(0, totalWeight);

        for (int stateIndex = 0;
             stateIndex < _runtimeStates.Count;
             stateIndex++)
        {
            EnemySpawnPatternRuntimeState runtimeState =
                _runtimeStates[stateIndex];

            // 쿨다운 중인 패턴은 선택 후보에서 제외한다.
            if (!runtimeState.IsAvailable)
            {
                continue;
            }

            int patternWeight =
                runtimeState.Pattern.Weight;

            // 현재 패턴의 가중치 구간에 포함되면 선택을 완료한다.
            if (randomValue < patternWeight)
            {
                selectedState = runtimeState;
                return true;
            }

            // 다음 패턴의 가중치 구간을 검사할 수 있도록 차감한다.
            randomValue -= patternWeight;
        }

        // 가중치 계산이 정상이라면 도달하지 않는 방어 코드다.
        return false;
    }

    /// <summary>
    /// 스테이지를 다시 시작할 때 모든 패턴 쿨다운을 초기화한다.
    /// </summary>
    public void ResetCooldowns()
    {
        for (int stateIndex = 0;
             stateIndex < _runtimeStates.Count;
             stateIndex++)
        {
            _runtimeStates[stateIndex]
                .ResetCooldown();
        }
    }
}
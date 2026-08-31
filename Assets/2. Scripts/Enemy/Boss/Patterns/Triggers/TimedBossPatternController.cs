using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 일정 시간마다 일반 패턴 중 하나를 가중치 방식으로 선택한다.
/// 직전에 실행한 패턴은 선택 가중치를 낮춘다.
/// </summary>
public sealed class TimedBossPatternController
{
    readonly List<TimedBossPatternEntry> _entries =
        new List<TimedBossPatternEntry>(); // 선택할 수 있는 일반 패턴 목록

    readonly BossPatternRunner _patternRunner; // 선택된 일반 패턴을 실행할 Runner
    readonly float _patternInterval; // 일반 패턴 실행 사이의 대기시간
    readonly float _previousPatternWeightMultiplier; // 직전 패턴에 적용할 가중치 배율

    float _remainingTime; // 다음 일반 패턴 선택까지 남은 시간
    BossPattern _previousPattern; // 직전에 실행을 요청한 일반 패턴

    /// <summary>
    /// 일반 패턴 목록과 공통 시간 및 가중치 설정을 전달받는다.
    /// </summary>
    public TimedBossPatternController(
        IReadOnlyList<TimedBossPatternEntry> entries,
        BossPatternRunner patternRunner,
        float firstDelay,
        float patternInterval,
        float previousPatternWeightMultiplier)
    {
        if (entries == null)
        {
            throw new ArgumentNullException(
                nameof(entries));
        }

        _patternRunner = patternRunner
            ?? throw new ArgumentNullException(
                nameof(patternRunner));

        if (firstDelay < 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(firstDelay));
        }

        if (patternInterval <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(patternInterval));
        }

        if (previousPatternWeightMultiplier < 0f ||
            previousPatternWeightMultiplier > 1f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(previousPatternWeightMultiplier));
        }

        HashSet<BossPattern> uniquePatterns =
            new HashSet<BossPattern>();

        for (int entryIndex = 0;
             entryIndex < entries.Count;
             entryIndex++)
        {
            TimedBossPatternEntry entry =
                entries[entryIndex];

            if (entry == null)
            {
                throw new ArgumentException(
                    $"일반 패턴 설정의 {entryIndex}번 요소가 비어 있습니다.",
                    nameof(entries));
            }

            if (entry.Pattern == null)
            {
                throw new ArgumentException(
                    $"일반 패턴 설정의 {entryIndex}번 Pattern이 비어 있습니다.",
                    nameof(entries));
            }

            if (!uniquePatterns.Add(entry.Pattern))
            {
                throw new ArgumentException(
                    $"같은 일반 패턴이 중복 등록되었습니다: {entry.Pattern.PatternId}",
                    nameof(entries));
            }

            _entries.Add(entry);
        }

        _patternInterval = patternInterval;
        _previousPatternWeightMultiplier =
            previousPatternWeightMultiplier;

        _remainingTime = firstDelay;
    }

    /// <summary>
    /// 패턴을 실행하지 않는 동안 공통 선택 타이머를 갱신한다.
    /// </summary>
    public void Tick(float deltaTime)
    {
        // 등록된 일반 패턴이 없다면 선택하지 않는다.
        if (_entries.Count == 0)
        {
            return;
        }

        // 일반 또는 특수 패턴 실행 중에는 선택 시간을 정지한다.
        if (_patternRunner.IsRunning)
        {
            return;
        }

        if (deltaTime <= 0f)
        {
            return;
        }

        _remainingTime = Mathf.Max(
            0f,
            _remainingTime - deltaTime);

        if (_remainingTime > 0f)
        {
            return;
        }

        BossPattern selectedPattern =
            SelectPattern();

        _previousPattern = selectedPattern;

        // 선택된 패턴을 일반 패턴 대기열에 요청한다.
        _patternRunner.RequestNormal(
            selectedPattern);

        // 현재 패턴 종료 후 다시 이 시간만큼 기다리게 된다.
        _remainingTime = _patternInterval;
    }

    /// <summary>
    /// 기본 가중치와 직전 패턴 배율을 사용하여 패턴 하나를 선택한다.
    /// </summary>
    private BossPattern SelectPattern()
    {
        float totalWeight = 0f;

        for (int entryIndex = 0;
             entryIndex < _entries.Count;
             entryIndex++)
        {
            totalWeight += GetEffectiveWeight(
                _entries[entryIndex]);
        }

        float randomValue =
            UnityEngine.Random.Range(
                0f,
                totalWeight);

        for (int entryIndex = 0;
             entryIndex < _entries.Count;
             entryIndex++)
        {
            TimedBossPatternEntry entry =
                _entries[entryIndex];

            float effectiveWeight =
                GetEffectiveWeight(entry);

            if (randomValue <= effectiveWeight)
            {
                return entry.Pattern;
            }

            randomValue -= effectiveWeight;
        }

        // 부동소수점 계산 오차가 발생하면 마지막 패턴을 선택한다.
        return _entries[_entries.Count - 1].Pattern;
    }

    /// <summary>
    /// 직전 패턴 여부를 반영한 실제 선택 가중치를 계산한다.
    /// </summary>
    private float GetEffectiveWeight(
        TimedBossPatternEntry entry)
    {
        // 일반 패턴이 하나뿐이면 가중치를 낮추지 않고 계속 사용한다.
        if (_entries.Count == 1)
        {
            return entry.Weight;
        }

        if (entry.Pattern == _previousPattern)
        {
            return entry.Weight *
                   _previousPatternWeightMultiplier;
        }

        return entry.Weight;
    }
}
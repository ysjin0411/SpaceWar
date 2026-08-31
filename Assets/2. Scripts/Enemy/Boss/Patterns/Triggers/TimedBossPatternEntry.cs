using System;
using UnityEngine;

/// <summary>
/// 일반 보스 패턴 하나와 기본 선택 가중치를 저장한다.
/// </summary>
[Serializable]
public sealed class TimedBossPatternEntry
{
    [SerializeField] BossPattern _pattern; // 가중치 선택을 통해 실행할 일반 패턴

    [Min(1)]
    [SerializeField] int _weight = 10; // 직전 패턴이 아닐 때 사용하는 기본 선택 가중치

    public BossPattern Pattern =>
        _pattern;

    public int Weight =>
        _weight;
}
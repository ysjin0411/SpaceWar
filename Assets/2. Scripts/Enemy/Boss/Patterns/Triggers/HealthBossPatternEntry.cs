using System;
using UnityEngine;

/// <summary>
/// 특정 체력 퍼센트에 도달했을 때 실행할 특수 패턴 설정이다.
/// </summary>
[Serializable]
public sealed class HealthBossPatternEntry
{
    [Range(1f, 99f)]
    [SerializeField] float _healthPercent = 80f; // 특수 패턴이 한 번 발동할 체력 퍼센트

    [SerializeField] BossPattern _pattern; // 해당 체력 퍼센트에서 실행할 특수 패턴

    public float HealthPercent =>
        _healthPercent;

    public BossPattern Pattern =>
        _pattern;
}
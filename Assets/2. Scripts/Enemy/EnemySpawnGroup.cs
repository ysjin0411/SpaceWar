using System;
using System.Collections.Generic;
using UnityEngine;

// Inspector에서 묶음별 패턴 목록과 소환 간격을 설정한다.
[Serializable]
public sealed class EnemySpawnGroup
{
    // Inspector에서 묶음을 구분하기 위한 이름이다.
    [SerializeField] string _groupName;

    // 이 묶음 안에서 가중치 선택에 참여할 패턴들이다.
    [SerializeField]
    List<EnemySpawnPattern> _patterns =
        new List<EnemySpawnPattern>();

    // 일반 구간에서 이 묶음의 패턴을 소환하는 간격이다.
    [Min(0.1f)]
    [SerializeField] float _patternCoolTime = 3f;

    // 보스 전투에서 이 묶음의 패턴을 소환하는 간격이다.
    [Min(0.1f)]
    [SerializeField] float _bossPatternCoolTime = 6f;

    // 외부에서는 묶음 설정을 읽기만 한다.
    public string GroupName => _groupName;
    public IReadOnlyList<EnemySpawnPattern> Patterns => _patterns;

    // 현재 전투 상태에 맞는 묶음 소환 간격을 반환한다.
    public float GetInterval(bool isBossBattle)
    {
        return isBossBattle
            ? Mathf.Max(0.1f, _bossPatternCoolTime)
            : Mathf.Max(0.1f, _patternCoolTime);
    }
}
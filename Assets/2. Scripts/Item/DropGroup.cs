using System;
using System.Collections.Generic;
using UnityEngine;

// 드롭할 프리팹 하나와 생성 개수를 묶는다.
[Serializable]
public sealed class DropPrefabEntry
{
    // 생성할 프리팹이다.
    [SerializeField] private GameObject _prefab;

    // 해당 프리팹을 생성할 개수다.
    [SerializeField, Min(1)] private int _count = 1;

    // 보스의 드롭 처리에서 설정값을 읽는다.
    public GameObject Prefab => _prefab;
    public int Count => _count;
}

// Inspector에서 드롭 항목들을 그룹별로 관리한다.
[Serializable]
public sealed class DropGroup
{
    // 설정을 구분하기 위한 이름이다. 드롭 종류 판정에는 사용하지 않는다.
    [SerializeField] private string _groupName;

    // 이 그룹에서 생성할 프리팹과 개수 목록이다.
    [SerializeField]
    private List<DropPrefabEntry> _entries =
        new List<DropPrefabEntry>();

    // 외부에서는 목록을 조회만 한다.
    public string GroupName => _groupName;
    public IReadOnlyList<DropPrefabEntry> Entries => _entries;
}
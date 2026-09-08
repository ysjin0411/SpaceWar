using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class DropGroup
{
    // 드랍 그룹 이름
    [SerializeField] string _groupName;

    [SerializeField] GameObject[] _prefabs;


    public string GroupName => _groupName;
}

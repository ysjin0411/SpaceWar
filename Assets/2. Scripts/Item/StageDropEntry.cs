using System;
using UnityEngine;

// 해당 스테이지에서만 사용할 아이템의 드랍 설정이다.
// 각 항목은 다른 항목과 관계없이 자신의 확률로 추첨한다.
[Serializable]
public sealed class StageDropEntry
{
    // 확률 추첨에 성공했을 때 생성할 프리팹이다.
    [SerializeField] private GameObject _prefab;

    // 백분율 기준 드랍 확률이다.
    // 0이면 생성하지 않고, 100이면 반드시 생성한다.
    [SerializeField, Range(0f, 100f)]
    private float _dropChancePercent = 10f;

    // 드랍 실행을 담당하는 Drop에서 설정을 읽는다.
    public GameObject Prefab => _prefab;
    public float DropChancePercent => _dropChancePercent;

    // 다른 드랍 항목의 확률이나 개수에 영향을 받지 않는다.
    public bool Roll()
    {
        // Random.value의 경계값과 관계없이 0%를 보장한다.
        if (_dropChancePercent <= 0f)
        {
            return false;
        }

        // Random.value의 경계값과 관계없이 100%를 보장한다.
        if (_dropChancePercent >= 100f)
        {
            return true;
        }

        // 0~1 범위의 난수를 백분율로 환산하여 비교한다.
        return UnityEngine.Random.value * 100f < _dropChancePercent;
    }
}
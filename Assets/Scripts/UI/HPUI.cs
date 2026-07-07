using UnityEngine;

public class HPUI : MonoBehaviour
{
    [Header("체력UI")]
    [SerializeField] GameObject[] _hpIcons;
    [SerializeField] int _hp;

    [Header("플레이어")]
    [SerializeField] PlayerController _currentHp;

    public void UpdateHpUI()
    {
        _hp--;
        _hpIcons[_hp].SetActive(false);
    }
}




using UnityEngine;


public class PlayerHealth : MonoBehaviour
{
    [Header("플레이어 체력")]
    [SerializeField] int _maxHp;
    [SerializeField] int _currentHp;

    public void TakeHit(int damage)
    {
        _currentHp -= damage;
        Debug.Log($"입은 데미지: {damage}");
        if( _currentHp < 0)
        {
            Die();
        }
    }

    public void Die()
    {
        Debug.Log("사망");
    }
}

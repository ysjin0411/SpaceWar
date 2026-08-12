using UnityEngine;

/// <summary>
/// 총알들의 부모
/// </summary>
public class Projectile : MonoBehaviour
{
    [SerializeField] Enemy _enemy;

    [Header("모든 총알들의 능력치 옵션")]
    [SerializeField] float _speed;              
    [SerializeField] int _damage;
    [SerializeField] float _moveSpeed;

    public float Speed => _speed;
    public int Damage => _damage;
    public float MoveSpeed => _moveSpeed;

    Vector2 _dir;       // 모든 총알들의 진행방향

}

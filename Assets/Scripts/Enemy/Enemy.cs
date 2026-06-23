using UnityEngine;

public class Enemy : MonoBehaviour
{
    [Header("체력")]
    [SerializeField] int _maxHp = 10;
    [SerializeField] int _currentHp;

    [Header("이동")]
    [SerializeField] float _moveSpeed = 3f;

    private void Awake()
    {
        _currentHp = _maxHp;
    }

    private void Update()
    {
        transform.Translate(Vector2.left * _moveSpeed * Time.deltaTime);

        if (transform.position.x < -12f)
        {
            Destroy(gameObject);
        }
    }

    public void TakeHit(int damage)
    {
        _currentHp -= damage;

        if (_currentHp <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Destroy(gameObject);
    }
}
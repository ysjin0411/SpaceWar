using UnityEngine;

public class Bullet : MonoBehaviour
{
    [SerializeField] float _speed = 10f;
    [SerializeField] int _damage = 2;

    private void Update()
    {
        transform.Translate(Vector2.right * _speed * Time.deltaTime);

        if (transform.position.x > 15f)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        FastEnemy enemy = collision.GetComponent<FastEnemy>();

        if (enemy != null)
        {
            enemy.TakeHit(_damage);
            Destroy(gameObject);
        }
    }
}
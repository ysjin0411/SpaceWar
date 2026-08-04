using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("초기 옵션")]
    [SerializeField] float _speed = 10f;
    [SerializeField] int _damage = 2;

    public float BulletSpeed => _speed;
    public int BulletDamage => _damage;

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

    /// <summary>
    /// 적용중인 공격력값 가져오기
    /// </summary>
    /// <param name="damage">공격력</param>
	public void SetDamage(int damage)
	{
		_damage = damage;
	}
}
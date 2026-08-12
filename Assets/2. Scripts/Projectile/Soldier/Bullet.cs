using UnityEngine;

public class Bullet : Projectile
{

    private void Update()
    {
        transform.Translate(Vector2.right * MoveSpeed * Time.deltaTime);

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
            enemy.TakeHit(Damage);
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 적용중인 공격력값 가져오기
    /// </summary>
    /// <param name="damage">공격력</param>
	public void SetDamage(int damage)
	{
		//Damage = damage;
	}
}
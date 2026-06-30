using UnityEngine;

/// <summary>
/// 적 총알 - 플레이어 위치를 향해 직선 이동
/// </summary>
public class EnemyBullet : MonoBehaviour
{
    [Header("이동")]
    [SerializeField] float _speed = 5f;

    [Header("삭제")]
    [SerializeField] float _lifeTime = 5f;

    [Header("컴포넌트")]
    [SerializeField] PlayerController _playerController;
    [SerializeField] Animator _animator;

    [Header("스탯")]
    [SerializeField] int _damage;


    Vector2 _dir;

    private void Update()
    {
        transform.Translate(_dir * _speed * Time.deltaTime,
                            Space.World);
        if(gameObject.transform.position.x > 9.0f || gameObject.transform.position.x < -9.0f 
            || gameObject.transform.position.y > 5.0f || gameObject.transform.position.y < -5.0f)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 방향 설정
    /// </summary>
    public void SetDirection(Vector3 targetPos)
    {
        _dir = (targetPos - transform.position).normalized;
    }

    public void OnTriggerEnter2D(Collider2D collision)
    {
        PlayerController player = collision.GetComponent<PlayerController>();
        if (player == null) return;
        player.TakeHit(_damage);
        Destroy(gameObject);
        _animator.Play("Hit");
    }


}
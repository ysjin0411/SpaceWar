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

    Vector2 _dir;

    private void Start()
    {
        Destroy(gameObject, _lifeTime);
    }

    private void Update()
    {
        transform.Translate(_dir * _speed * Time.deltaTime,
                            Space.World);
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
        PlayerHealth player = collision.GetComponent<PlayerHealth>();
        if(player != null)
        {
            return;
        }
    }
}
using UnityEngine;

/// <summary>
/// 적 총알 - 플레이어 위치를 향해 직선 이동
/// </summary>
public class EnemyBullet : MonoBehaviour
{
    [Header("이동")]
    [SerializeField] float _speed = 5f;



    [Header("컴포넌트")]
    [SerializeField] PlayerController _playerController;
    [SerializeField] Animator _animator;

    [Header("스탯")]
    [SerializeField] int _damage;


    Vector2 _dir;

    // 패턴이 발사 방향을 직접 지정했는지 확인한다.
    bool _hasExplicitDirection;

    public void Init(PlayerController playerController)
    {
        _playerController = playerController;
    }

    /// <summary>
    /// 패턴이 지정한 방향과 속도, 피해량으로 총알을 초기화한다.
    /// 생성 직후 호출하며, 전달받은 방향으로 직진한다.
    /// </summary>
    public void Init(Vector2 direction, float speed, int damage)
    {
        // 방향 벡터의 길이가 이동 속도에 영향을 주지 않도록 한다.
        _dir = direction.normalized;

        // 생성된 총알에 패턴별 속도와 피해량을 적용한다.
        _speed = speed;
        _damage = damage;

        // Start에서 플레이어 방향으로 덮어쓰지 않도록 한다.
        _hasExplicitDirection = true;
    }

    private void Start()
    {
        // 패턴에서 방향을 지정한 총알은 해당 방향을 유지한다.
        if (_hasExplicitDirection)
        {
            return;
        }

        // 일반 적이 발사한 총알은 기존처럼 플레이어를 조준한다.
        SetDirection();
    }

    private void Update()
    {
        transform.Translate(_dir * _speed * Time.deltaTime, Space.World);

        if(gameObject.transform.position.x > 9.0f 
            || gameObject.transform.position.x < -9.0f 
            || gameObject.transform.position.y > 5.0f 
            || gameObject.transform.position.y < -5.0f)
        {
            Destroy(gameObject);
        }
    }

    public void SetDirection()
    {
        //_playerController.transform.position = targetPos;
        Vector3 targetPos = _playerController.transform.position;
        _dir = (targetPos - transform.position).normalized;
    }

    public void OnTriggerEnter2D(Collider2D collision)
    {
        // 플레이어의 자식 Collider에 닿은 경우도 처리한다.
        PlayerController player =
            collision.GetComponentInParent<PlayerController>();

        // 보스나 다른 총알 등 플레이어가 아닌 대상은 무시한다.
        if (player == null)
        {
            return;
        }
        player.TakeHit(_damage);
        Destroy(gameObject);
        _animator.Play("Hit");
    }
}
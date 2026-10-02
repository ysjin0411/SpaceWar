using System;
using UnityEngine;

/// <summary>
/// FastEnemy가 이동할 방향과 패턴.
/// </summary>
public enum FastEnemyMoveType
{
    DownToUp,
    RightToLeft
}

/// <summary>
/// 빠르게 이동하며 지정된 위치에서 한 번 총알을 발사하는 적.
/// 체력, 피해, 사망, 골드, 몸박 처리는 Enemy 부모가 담당한다.
/// </summary>
public class FastEnemy : Enemy
{
    [Header("이동 설정")]
    [Tooltip("FastEnemy의 이동속도")]
    [Min(0f)]
    [SerializeField] float _moveSpeed = 2f;

    [Tooltip("현재 적이 사용할 이동 패턴")]
    [SerializeField] FastEnemyMoveType _moveType;

    [Header("공격 설정")]
    [Tooltip("FastEnemy가 발사할 적 총알 프리팹")]
    [SerializeField] GameObject _enemyBulletPrefab;

    [Tooltip("적 총알이 생성될 위치")]
    [SerializeField] Transform _firePoint;

    [Header("FastEnemy 런타임 데이터")]
    [Tooltip("현재 이동 중 총알을 이미 발사했는지 확인")]
    [SerializeField] bool _isFired;

    // Renderer 자식의 Animator다.
    [SerializeField] Animator _animator;

    // 공격 애니메이션이 진행 중인지 확인한다.
    bool _attackPlaying;

    // 현재 공격에서 총알을 이미 생성했는지 확인한다.
    bool _firedThisAttack;

    // 공통 컨트롤러의 공격 상태 경로다.
    static readonly int AttackHash =
        Animator.StringToHash("Base Layer.Attack_4Frame");

    /// <summary>
    /// 부모의 체력 런타임 데이터를 초기화하고
    /// FastEnemy에 필요한 참조를 검사한다.
    /// </summary>
    protected override void Awake()
    {
        // Enemy 부모의 현재 체력과 사망 상태를 초기화한다.
        base.Awake();

        // FastEnemy가 사용할 총알 프리팹이 없으면 발사할 수 없다.
        if (_enemyBulletPrefab == null)
        {
            throw new InvalidOperationException(
                $"{name}의 FastEnemy에 Enemy Bullet Prefab이 연결되지 않았습니다.");
        }

        // FirePoint가 없으면 총알 생성 위치를 결정할 수 없다.
        if (_firePoint == null)
        {
            throw new InvalidOperationException(
                $"{name}의 FastEnemy에 FirePoint가 연결되지 않았습니다.");
        }

        // 새로 생성된 FastEnemy는 아직 총알을 발사하지 않은 상태다.
        _isFired = false;

        // 공격 애니메이션을 재생할 Animator를 검사한다.
        if (_animator == null)
        {
            throw new InvalidOperationException(
                $"{name}: Animator를 연결하세요.");
        }
    }

    // 최초 등장과 풀에서 다시 꺼낼 때 공격 상태를 초기화한다.
    private void OnEnable()
    {
        // 이번 등장에서는 아직 발사하지 않은 상태로 되돌린다.
        _isFired = false;

        // 이전 공격의 애니메이션 이벤트가 발사로 이어지지 않도록 한다.
        _attackPlaying = false;
        _firedThisAttack = false;

        // Animator를 초기 상태로 되돌리고 정상 재생 속도를 적용한다.
        _animator.Rebind();
        _animator.speed = 1f;

        // 초기 애니메이션 상태를 즉시 화면에 반영한다.
        _animator.Update(0f);
    }

    private void Update()
    {
        // 이미 사망 처리된 적은 이동하거나 공격하지 않는다.
        if (IsDead)
        {
            return;
        }

        // EnemySpawner가 전달한 이동 패턴에 맞게 움직인다.
        switch (_moveType)
        {
            case FastEnemyMoveType.RightToLeft:
                MoveRightToLeft();
                break;

            case FastEnemyMoveType.DownToUp:
                MoveDownToUp();
                break;
        }
    }

    /// <summary>
    /// EnemySpawner가 생성한 FastEnemy의 이동 패턴을 지정한다.
    /// </summary>
    public void SetMoveType(FastEnemyMoveType moveType)
    {
        // 전달받은 이동 패턴을 현재 FastEnemy에 저장한다.
        _moveType = moveType;
    }

    /// <summary>
    /// 현재 플레이어를 향해 적 총알을 한 발 생성한다.
    /// </summary>
    private void Fire()
    {
        // Enemy.Init이 호출되지 않았다면 추적할 플레이어가 없는 상태다.
        if (PlayerController == null)
        {
            throw new InvalidOperationException(
                $"{name}의 FastEnemy에 PlayerController가 초기화되지 않았습니다.");
        }

        // 미리 준비된 풀에서 탄환을 꺼내 발사 위치에 활성화한다.
        GameObject enemyBulletObject = ProjectilePoolManager.Instance.Spawn(
            _enemyBulletPrefab,
            _firePoint.position,
            Quaternion.identity);

        // 생성된 오브젝트에서 EnemyBullet 컴포넌트를 가져온다.
        if (!enemyBulletObject.TryGetComponent(out EnemyBullet enemyBullet))
        {
            // 탄환 컴포넌트가 잘못 설정된 경우 꺼낸 오브젝트를 풀에 반납한다.
            enemyBulletObject.GetComponent<PoolObject>().ReturnToPool();

            // 프리팹 설정 오류를 확인할 수 있도록 예외를 발생시킨다.
            throw new MissingComponentException(
                $"{_enemyBulletPrefab.name} 프리팹에 EnemyBullet 컴포넌트가 없습니다.");
        }

        // 적 총알이 현재 플레이어를 향해 날아갈 수 있도록 전달한다.
        enemyBullet.Init(PlayerController);
    }

    /// <summary>
    /// 화면 오른쪽에서 왼쪽으로 이동한다.
    /// X 위치가 0 이하가 되면 총알을 한 번 발사한다.
    /// </summary>
    private void MoveRightToLeft()
    {
        // 월드 좌표 기준 왼쪽으로 이동한다.
        transform.Translate(
            Vector2.left * _moveSpeed * Time.deltaTime,
            Space.World);

        // 발사 위치에 도달했고 아직 발사하지 않았다면 총알을 발사한다.
        if (transform.position.x <= 0f && !_isFired)
        {
            BeginAttack();
            Fire();

            // 같은 적이 여러 번 발사하지 않도록 상태를 저장한다.
            _isFired = true;
        }

        // 왼쪽 화면 밖으로 나간 적을 퇴장시킨다.
        if (transform.position.x < -12f)
        {
            Despawn();
        }
    }

    /// <summary>
    /// 화면 아래에서 위쪽으로 이동한다.
    /// Y 위치가 0 이상이 되면 총알을 한 번 발사한다.
    /// </summary>
    private void MoveDownToUp()
    {
        // 월드 좌표 기준 위쪽으로 이동한다.
        transform.Translate(
            Vector2.up * _moveSpeed * Time.deltaTime,
            Space.World);

        // 발사 위치에 도달했고 아직 발사하지 않았다면 총알을 발사한다.
        if (transform.position.y >= 0f && !_isFired)
        {
            BeginAttack();
            Fire();

            // 같은 적이 여러 번 발사하지 않도록 상태를 저장한다.
            _isFired = true;
        }

        // 위쪽 화면 밖으로 나간 적을 퇴장시킨다.
        if (transform.position.y > 6f)
        {
            Despawn();
        }
    }

    /// <summary>
    /// 지정 좌표에 도착하면 공격 애니메이션을 한 번 시작한다.
    /// </summary>
    private void BeginAttack()
    {
        // 다음 프레임에도 같은 좌표 조건으로 공격을 요청하지 않도록 한다.
        // 이 값은 이제 공격을 이미 시작했는지를 뜻한다.
        _isFired = true;

        _attackPlaying = true;
        _firedThisAttack = false;

        _animator.Play(AttackHash, 0, 0f);
    }

    /// <summary>
    /// 세 번째 공격 컷에서 기존 총알 생성 함수를 실행한다.
    /// </summary>
    public override void FireFromAnimation()
    {
        if (IsDead || !_attackPlaying || _firedThisAttack)
        {
            return;
        }

        _firedThisAttack = true;
        Fire();
    }

    /// <summary>
    /// 공통 Relay가 Idle로 복귀시킨 뒤 공격 상태를 정리한다.
    /// </summary>
    public override void FinishAttackFromAnimation()
    {
        _attackPlaying = false;
    }
}
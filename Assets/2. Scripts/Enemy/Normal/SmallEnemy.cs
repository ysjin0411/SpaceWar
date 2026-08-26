using UnityEngine;

public enum SmallEnemyState
{
    Entering,       // 화면 밖에서 진입 지점까지 이동
    Spreading,      // 각자 지정된 그물 대형 위치로 전개
    Aiming,         // 잠시 멈추며 플레이어 위치를 조준
    Charging,       // 조준 순간의 방향으로 직선 돌진
    Exploding       // 범위 피해를 주고 제거
}

public enum SmallEnemyMoveType
{
    Suicide,
    Twin
}
public class SmallEnemy : Enemy
{
    [Header("발사위치")]
    [SerializeField] Transform _firePoint;

    [Header("패턴타입")]
    [SerializeField] SmallEnemyMoveType _moveType;

    [Header("자폭 패턴 이동 설정")]
    [SerializeField] float _enterMoveSpeed = 3f; // 화면 밖에서 진입 위치까지 이동하는 속도
    [Min(0.01f)][SerializeField] float _spreadDuration = 0.6f; // 모든 적이 그물 대형으로 펼쳐지는 데 걸리는 시간

    [Header("자폭 패턴 조준 설정")]
    [Min(0f)][SerializeField] float _aimDuration = 0.8f; // 그물 대형 완성 후 플레이어를 조준하는 시간

    [Header("자폭 패턴 돌진 설정")]
    [Min(0f)][SerializeField] float _chargeMoveSpeed = 9f; // 저장된 플레이어 방향으로 돌진하는 속도
    [Min(0.01f)][SerializeField] float _explosionDelay = 1.2f; // 돌진을 시작한 후 자폭하기까지 걸리는 시간

    [Header("자폭 피해 설정")]
    [Min(0f)][SerializeField] float _explosionRadius = 1.5f; // 자폭 피해가 적용되는 반경
    [Min(0)][SerializeField] int _explosionDamage = 20; // 자폭 범위 안의 플레이어가 받는 피해
    [SerializeField] SpriteExplosionEffect _explosionEffectPrefab; // 자폭할 때 생성할 선택적 폭발 이펙트

    // 현재 자폭 패턴에서 실행 중인 행동 상태
    SmallEnemyState _currentState;

    // 화면 밖에서 들어온 뒤 처음 도착해야 하는 위치
    Vector2 _enterPosition;

    // 그물 대형에서 이 SmallEnemy가 담당할 위치
    Vector2 _formationPosition;

    // EnemySpawner로부터 자폭 패턴 정보를 전달받았는지 확인
    bool _isPatternInitialized;

    // 그물 대형 전개가 시작된 위치
    Vector2 _spreadStartPosition;

    // 현재 상태에서 흐른 시간
    float _stateTimer;

    // 조준이 끝나는 순간 저장한 플레이어 위치
    Vector2 _chargeTargetPosition;

    // 저장된 플레이어 위치를 향하는 돌진 방향
    Vector2 _chargeDirection;

    // 폭발 피해와 제거가 중복 실행되는 것을 방지한다.
    bool _hasExploded;

    protected override void Awake()
    {
        base.Awake();
    }


    private void Update()
    {
        // 죽었거나 EnemySpawner에서 패턴 정보를 받기 전에는 행동하지 않는다.
        if (IsDead || !_isPatternInitialized)
        {
            return;
        }

        // SmallEnemy의 이동 타입에 맞는 패턴을 실행한다.
        switch (_moveType)
        {
            case SmallEnemyMoveType.Suicide:
                UpdateSuicidePattern();
                break;

            case SmallEnemyMoveType.Twin:
                // Twin 패턴은 나중에 구현한다.
                break;
        }
    }

    /// <summary>
    /// EnemySpawner로부터 자폭 패턴에 필요한 위치를 전달받는다.
    /// </summary>
    /// <param name="enterPosition">화면밖에서부터 도착해야하는 위치</param>
    /// <param name="formationPosition">해당 개체가 위치해야하는 포지션</param>
    public void InitializeSuicidePattern(Vector2 enterPosition, Vector2 formationPosition)
    {
        // 이 SmallEnemy가 자폭 패턴으로 행동하도록 설정한다.
        _moveType = SmallEnemyMoveType.Suicide;

        // 화면 안으로 진입할 때 도착해야 하는 위치를 저장한다.
        _enterPosition = enterPosition;

        // 그물 형태로 펼쳐졌을 때 담당할 위치를 저장한다.
        _formationPosition = formationPosition;

        // 첫 행동은 화면 밖에서 진입하는 단계다.
        _currentState = SmallEnemyState.Entering;

        // Update에서 행동을 시작할 수 있도록 초기화 완료를 기록한다.
        _isPatternInitialized = true;
    }

    /// <summary>
    /// 현재 자폭 패턴의 상태에 맞는 행동을 실행한다.
    /// </summary>
    private void UpdateSuicidePattern()
    {
        switch (_currentState)
        {
            case SmallEnemyState.Entering:
                UpdateEntering();
                break;

            case SmallEnemyState.Spreading:
                UpdateSpreading();
                break;

            case SmallEnemyState.Aiming:
                UpdateAiming();
                break;

            case SmallEnemyState.Charging:
                UpdateCharging();
                break;

            case SmallEnemyState.Exploding:
                Explode();
                break;
        }
    }

    /// <summary>
    /// 화면 밖에서 지정된 진입 위치까지 이동한다.
    /// </summary>
    private void UpdateEntering()
    {
        // 현재 위치에서 진입 목표 위치까지 일정한 속도로 이동한다.
        transform.position = Vector2.MoveTowards(transform.position, _enterPosition, _enterMoveSpeed * Time.deltaTime);

        // 아직 진입 위치에 도착하지 않았다면 이동을 계속한다.
        if (Vector2.Distance(transform.position, _enterPosition) > 0.01f)
        {
            return;
        }

        // 위치 오차가 남지 않도록 정확한 진입 위치로 맞춘다.
        transform.position = _enterPosition;

        // 그물 전개가 시작되는 현재 위치를 저장한다.
        _spreadStartPosition = transform.position;

        // 그물 전개 시간을 처음부터 측정한다.
        _stateTimer = 0f;

        // 진입이 끝났으므로 그물 대형으로 펼쳐지는 상태로 전환한다.
        _currentState = SmallEnemyState.Spreading;
    }

    /// <summary>
    /// 진입 위치에서 각자 담당하는 그물 대형 위치로 펼쳐진다.
    /// </summary>
    private void UpdateSpreading()
    {
        // 현재 전개 상태에서 흐른 시간을 누적한다.
        _stateTimer += Time.deltaTime;

        // 전체 전개 시간 중 현재 진행 비율을 계산한다.
        float progress = Mathf.Clamp01(_stateTimer / _spreadDuration);

        // 처음과 끝에서 부드럽게 감속·가속하도록 진행 비율을 보정한다.
        float easedProgress = Mathf.SmoothStep(0f, 1f, progress);

        // 전개 시작 위치에서 담당 대형 위치까지 이동한다.
        transform.position = Vector2.Lerp(_spreadStartPosition, _formationPosition, easedProgress);

        // 아직 전개 시간이 끝나지 않았다면 현재 상태를 유지한다.
        if (progress < 1f)
        {
            return;
        }

        // 위치 오차가 남지 않도록 정확한 대형 위치로 맞춘다.
        transform.position = _formationPosition;

        // 다음 조준 단계에서 사용할 시간을 초기화한다.
        _stateTimer = 0f;

        // 그물 대형 전개가 끝났으므로 조준 상태로 전환한다.
        _currentState = SmallEnemyState.Aiming;
    }

    /// <summary>
    /// 그물 대형에서 잠시 대기한 후 플레이어 위치를 돌진 목표로 저장한다.
    /// </summary>
    private void UpdateAiming()
    {
        // 조준 상태에서 흐른 시간을 누적한다.
        _stateTimer += Time.deltaTime;

        // 조준 시간이 끝나기 전까지 현재 위치에서 대기한다.
        if (_stateTimer < _aimDuration)
        {
            return;
        }

        // Enemy.Init()이 호출되지 않았다면 플레이어 위치를 가져올 수 없다.
        if (PlayerController == null)
        {
            return;
        }

        // 조준이 끝나는 순간의 플레이어 위치를 한 번만 저장한다.
        _chargeTargetPosition = PlayerController.transform.position;

        // 현재 위치에서 저장된 플레이어 위치로 향하는 방향을 계산한다.
        Vector2 direction = _chargeTargetPosition - (Vector2)transform.position;

        // 플레이어와 위치가 거의 같을 때 잘못된 방향이 생기는 것을 방지한다.
        if (direction.sqrMagnitude <= 0.0001f)
        {
            _chargeDirection = Vector2.left;
        }
        else
        {
            _chargeDirection = direction.normalized;
        }

        // 다음 돌진 단계에서 사용할 시간을 초기화한다.
        _stateTimer = 0f;

        // 조준이 끝났으므로 돌진 상태로 전환한다.
        _currentState = SmallEnemyState.Charging;
    }

    /// <summary>
    /// 조준이 끝날 때 저장한 방향으로 직선 돌진한다.
    /// </summary>
    private void UpdateCharging()
    {
        // 돌진을 시작한 후 흐른 시간을 누적한다.
        _stateTimer += Time.deltaTime;

        // 조준 종료 시 저장한 방향으로 이동한다.
        // 돌진 중에는 플레이어의 현재 위치를 다시 추적하지 않는다.
        transform.Translate(_chargeDirection * _chargeMoveSpeed * Time.deltaTime, Space.World);

        // 설정한 자폭 시간이 지나기 전까지 돌진을 계속한다.
        if (_stateTimer < _explosionDelay)
        {
            return;
        }

        // 폭발 처리에서 사용할 시간을 초기화한다.
        _stateTimer = 0f;

        // 자폭 제한시간이 끝났으므로 폭발 상태로 전환한다.
        _currentState = SmallEnemyState.Exploding;
    }

    /// <summary>
    /// 폭발 범위 안의 플레이어에게 피해를 주고 SmallEnemy를 제거한다.
    /// </summary>
    private void Explode()
    {
        // 같은 프레임에 충돌과 타이머 폭발이 함께 실행돼도 한 번만 처리한다.
        if (_hasExploded)
        {
            return;
        }

        _hasExploded = true;

        // 자폭 이펙트가 연결되어 있다면 현재 위치에 생성하고 재생한다.
        if (_explosionEffectPrefab != null)
        {
            SpriteExplosionEffect explosionEffect =
                Instantiate(
                    _explosionEffectPrefab,
                    transform.position,
                    Quaternion.identity);

            // Awake에서 투명해진 폭발 이미지를 표시하고 애니메이션을 시작한다.
            explosionEffect.Play();
        }

        // 플레이어가 정상적으로 초기화되어 있을 때만 폭발 피해를 계산한다.
        if (PlayerController != null)
        {
            // 폭발 중심과 플레이어 중심 사이의 거리를 계산한다.
            float distanceToPlayer = Vector2.Distance(transform.position, PlayerController.transform.position);

            // 플레이어가 폭발 반경 안에 있을 때만 피해를 준다.
            if (distanceToPlayer <= _explosionRadius)
            {
                PlayerController.TakeHit(_explosionDamage);
            }
        }

        // 자폭은 플레이어가 처치한 것이 아니므로
        // Enemy의 골드와 경험치 보상 처리를 거치지 않고 제거한다.
        Destroy(gameObject);
    }

    /// <summary>
    /// EnemySpawner가 생성한 FastEnemy의 이동 패턴을 지정한다.
    /// </summary>
    public void SetMoveType(SmallEnemyMoveType moveType)
    {
        // 전달받은 이동 패턴을 현재 FastEnemy에 저장한다.
        _moveType = moveType;
    }

    /// <summary>
    /// 자폭형 SmallEnemy가 플레이어와 충돌하면 남은 시간과 관계없이 폭발한다.
    /// </summary>
    protected override void OnTriggerEnter2D(Collider2D collision)
    {
        // Twin 타입은 Enemy 부모 클래스의 기본 충돌 처리를 사용한다.
        if (_moveType != SmallEnemyMoveType.Suicide)
        {
            base.OnTriggerEnter2D(collision);
            return;
        }

        // 충돌한 Collider 또는 부모 오브젝트에서 플레이어를 찾는다.
        PlayerController player = collision.GetComponentInParent<PlayerController>();

        // 플레이어가 아닌 오브젝트와의 충돌은 무시한다.
        if (player == null)
        {
            return;
        }

        // 자폭형 적은 플레이어와 접촉하는 즉시 폭발한다.
        Explode();
    }

    /// <summary>
    /// Scene 뷰에서 자폭 범위를 확인할 수 있도록 표시한다.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // 폭발 범위를 반투명한 붉은 원으로 표시한다.
        Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.35f);

        Gizmos.DrawSphere(transform.position, _explosionRadius);
    }

}

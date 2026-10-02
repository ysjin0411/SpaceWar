using UnityEngine;
using System.Collections.Generic;

public enum SmallEnemyState
{
    Entering,      // 기존 일반 소환의 직선 진입.
    Spreading,     // 기존 대형 위치로 이동.
    Aiming,        // 플레이어를 조준.
    Charging,      // 조준한 방향으로 돌진.
    Exploding,     // 자폭.

    // 크루저의 구멍에서 곡선으로 출격.
    // 기존 상태의 번호를 유지하기 위해 마지막에 추가한다.
    CurvedEntering
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
    [Min(0)][SerializeField] float _explosionDamage = 20; // 자폭 범위 안의 플레이어가 받는 피해
    [SerializeField] SpriteExplosionEffect _explosionEffectPrefab; // 자폭할 때 생성할 선택적 폭발 이펙트

    // Renderer 자식의 Animator다.
    [SerializeField] Animator _animator;

    // 공통 컨트롤러의 공격 상태 경로다.
    static readonly int AttackHash =
        Animator.StringToHash("Base Layer.Attack_4Frame");

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

    // 자폭병의 실제 몸 크기로 접촉과 이동 경로를 검사한다.
    Collider2D _bodyCollider;

    // 플레이어 몸과 실드만 검사하는 필터.
    ContactFilter2D _contactFilter;

    // 검사 결과 목록을 재사용해 매 프레임 메모리 생성을 줄인다.
    readonly List<Collider2D> _contactOverlaps = new();
    readonly List<RaycastHit2D> _contactHits = new();

    // 소환 순간의 위치. 곡선의 시작점으로 사용한다.
    private Vector2 _curveStart;

    // 구멍에서 솟아오르는 방향과 높이를 결정하는 첫 번째 제어점.
    private Vector2 _curveControl1;

    // 플레이어 쪽으로 휘어지는 모양을 결정하는 두 번째 제어점.
    private Vector2 _curveControl2;

    // 곡선 이동을 마치고 조준을 시작할 위치.
    private Vector2 _curveEnd;

    // 이 적 한 마리가 곡선을 따라 이동하는 시간.
    // 패턴 전체의 소환 시간과는 별개다.
    private float _curveDuration;

    /// <summary>
    /// 컴포넌트 참조와 초기 실행 상태를 준비한다.
    /// </summary>
    protected override void Awake()
    {
        base.Awake();

        // 기존 공격 애니메이션 연결을 검사한다.
        if (_animator == null)
        {
            throw new System.InvalidOperationException(
                $"{name}: Animator를 연결하세요.");
        }

        // Normal_small 루트의 기존 몸 콜라이더를 사용한다.
        _bodyCollider = GetComponent<Collider2D>();

        if (_bodyCollider == null)
        {
            throw new MissingComponentException(
                $"{name}: 루트에 Collider2D가 필요합니다.");
        }

        int playerLayer = LayerMask.NameToLayer("Player");
        int shieldLayer = LayerMask.NameToLayer("PlayerShield");

        if (playerLayer < 0 || shieldLayer < 0)
        {
            throw new System.InvalidOperationException(
                "Player와 PlayerShield 레이어가 필요합니다.");
        }

        // 일반 물리 충돌 설정과 별도로 검사 대상을 지정한다.
        _contactFilter = new ContactFilter2D();
        _contactFilter.SetLayerMask(
            (1 << playerLayer) | (1 << shieldLayer));

        _contactFilter.useTriggers = true;
    }

    // 최초 등장과 풀에서 다시 꺼낼 때 이전 사용 상태를 초기화한다.
    private void OnEnable()
    {
        // 새로운 패턴 정보를 받기 전에는 이동을 시작하지 않는다.
        _isPatternInitialized = false;

        // 이전에 자폭했던 상태를 해제한다.
        _hasExploded = false;

        // 이전 이동 단계를 지우고 상태별 경과 시간을 초기화한다.
        _currentState = SmallEnemyState.Entering;
        _stateTimer = 0f;

        // 이전 돌진의 목표와 방향을 지운다.
        _chargeTargetPosition = Vector2.zero;
        _chargeDirection = Vector2.zero;

        // 충돌 검사 목록에 남은 이전 대상의 참조를 지운다.
        _contactOverlaps.Clear();
        _contactHits.Clear();

        // 이전 돌진 애니메이션을 끝내고 기본 상태로 되돌린다.
        _animator.Rebind();
        _animator.speed = 1f;
        _animator.Update(0f);

        // 자폭하면서 껐던 콜라이더를 다시 켠다.
        _bodyCollider.enabled = true;
    }

    // 풀 반납이나 구간 전환으로 비활성화될 때 패턴 진행을 중단한다.
    private void OnDisable()
    {
        // 이전 패턴이 초기화된 상태로 남지 않도록 한다.
        _isPatternInitialized = false;

        // 대기 중에는 이전 충돌 대상의 참조를 보관하지 않는다.
        _contactOverlaps.Clear();
        _contactHits.Clear();
    }


    /// <summary>
    /// 매 프레임 입력과 경과 시간에 따른 상태를 갱신한다.
    /// </summary>
    private void Update()
    {
        // 일시정지, 사망, 폭발 완료, 초기화 전에는 행동하지 않는다.
        if (Time.timeScale <= 0f ||
            IsDead ||
            _hasExploded ||
            !_isPatternInitialized)
        {
            return;
        }

        switch (_moveType)
        {
            case SmallEnemyMoveType.Suicide:
                // 이동 거리가 0이어도 현재 겹쳐 있는 대상은 검사한다.
                // 실드를 들고 자폭병에 다가가는 상황도 처리한다.
                if (!MoveWithContactCheck(transform.position))
                {
                    return;
                }

                UpdateSuicidePattern();
                break;

            case SmallEnemyMoveType.Twin:
                // 기존 Twin 처리 위치를 유지한다.
                break;
        }
    }

    // 전달받은 월드 방향으로 몸체와 충돌 판정을 함께 회전시킨다.
    private void FaceDirection(Vector2 direction)
    {
        // 방향 벡터가 거의 0이면 현재 회전을 유지한다.
        if (direction.sqrMagnitude <= 0.00000001f)
        {
            return;
        }

        // 오른쪽을 0도로 기준 삼아 방향 벡터를 각도로 변환한다.
        float angle =
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // 원본 이미지가 왼쪽을 바라보므로 180도를 보정한다.
        // 루트를 회전시켜 자식 이미지와 몸체 콜라이더를 함께 돌린다.
        transform.rotation = Quaternion.Euler(0f, 0f, angle - 180f);
    }

    // 목표 위치까지 이동하되, 몸이나 실드에 먼저 닿으면 폭발한다.
    // 정상적으로 이동을 마쳤으면 true, 폭발했으면 false를 반환한다.
    /// <summary>
    /// 정상적으로 이동을 마쳤으면 true, 폭발했으면 false를 반환한다.
    /// </summary>
    private bool MoveWithContactCheck(Vector3 nextPosition)
    {
        if (_hasExploded || IsDead)
        {
            return false;
        }

        // 현재 위치에서 이번 프레임에 이동할 위치로 향하는 방향을 계산한다.
        Vector2 moveDirection =
            (Vector2)(nextPosition - transform.position);

        // 일반 자폭과 크루저 곡선 비행 모두 이동 방향을 바라보게 한다.
        // 이동 거리가 거의 0이면 기존 회전을 유지한다.
        FaceDirection(moveDirection);

        // 변경된 회전을 반영한 뒤 겹침과 이동 경로를 검사한다.
        Physics2D.SyncTransforms();

        _contactOverlaps.Clear();
        _bodyCollider.Overlap(_contactFilter, _contactOverlaps);

        // 시작부터 몸과 실드에 함께 겹쳐 있다면 활성 실드를 우선한다.
        for (int i = 0; i < _contactOverlaps.Count; i++)
        {
            Collider2D target = _contactOverlaps[i];

            if (target.TryGetComponent(out Shield shield) &&
                TryExplodeOnContact(target, transform.position))
            {
                return false;
            }
        }

        // 실드가 막지 못했다면 플레이어 몸과의 접촉을 처리한다.
        for (int i = 0; i < _contactOverlaps.Count; i++)
        {
            if (TryExplodeOnContact(
                _contactOverlaps[i],
                transform.position))
            {
                return false;
            }
        }

        Vector3 startPosition = transform.position;
        Vector2 movement = nextPosition - startPosition;
        float distance = movement.magnitude;

        if (distance > 0f)
        {
            Vector2 direction = movement / distance;

            _contactHits.Clear();

            // 몸 콜라이더 전체를 이동시켜 빠른 돌진의 통과를 방지한다.
            _bodyCollider.Cast(
                direction,
                _contactFilter,
                _contactHits,
                distance);

            // 실드와 플레이어 중 이동 경로에서 먼저 만난 대상을 처리한다.
            _contactHits.Sort(
                (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < _contactHits.Count; i++)
            {
                // 이동 경로에서 만난 대상의 충돌 정보를 가져온다.
                RaycastHit2D hit = _contactHits[i];

                // 자폭병이 상대에게 닿았을 때 멈출 위치다.
                Vector3 contactPosition =
                    startPosition +
                    (Vector3)(direction * hit.distance);

                // 상대 표면에서 실제로 맞은 위치다.
                // 작은 히트 이펙트를 여기에 생성한다.
                Vector2 hitPoint =
                    HitEffectUtility.GetCastPoint(_bodyCollider, hit);

                // 충돌 대상, 자폭병의 위치, 이펙트의 위치를 전달한다.
                if (TryExplodeOnContact(
                    hit.collider,
                    contactPosition,
                    hitPoint))
                {
                    // 폭발했다면 이동 처리를 종료한다.
                    return false;
                }
            }
        }

        // 막는 대상이 없으면 원래 예정했던 위치까지 이동한다.
        transform.position = nextPosition;
        return true;
    }

    // 루트의 이동 위치와 이펙트를 생성할 접촉 위치를 따로 받는다.
    /// <summary>
    /// 루트의 이동 위치와 이펙트를 생성할 접촉 위치를 따로 받는다.
    /// </summary>
    private bool TryExplodeOnContact(
        Collider2D target,
        Vector3 contactPosition,
        Vector2 hitPoint)
    {
        if (_hasExploded || IsDead || target == null)
        {
            return false;
        }

        if (target.TryGetComponent(out Shield shield))
        {
            // 실드 표면에 방어 이펙트를 생성하도록 접촉 위치를 전달한다.
            if (!shield.TryBlockDamage(_explosionDamage, hitPoint))
            {
                return false;
            }

            transform.position = contactPosition;

            // 실드가 피해를 이미 받았으므로 플레이어 범위 피해는 생략한다.
            Explode(true);
            return true;
        }

        PlayerController player =
            target.GetComponentInParent<PlayerController>();

        if (player == null)
        {
            return false;
        }

        // 몸에 먼저 닿은 경우에는 기존 일반 폭발을 실행한다.
        transform.position = contactPosition;
        Explode();

        return true;
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

            case SmallEnemyState.CurvedEntering:
                // 크루저에서 소환된 적의 곡선 이동을 갱신한다.
                UpdateCurvedEntering();
                break;
        }
    }

    /// <summary>
    /// 화면 밖에서 지정된 진입 위치까지 이동한다.
    /// </summary>
    private void UpdateEntering()
    {
        // 이번 프레임에 이동할 위치를 계산한다.
        Vector2 nextPosition = Vector2.MoveTowards(
            transform.position,
            _enterPosition,
            _enterMoveSpeed * Time.deltaTime);

        // 이동 중 접촉하여 폭발했다면 이후 상태 변경을 중단한다.
        if (!MoveWithContactCheck(nextPosition))
        {
            return;
        }


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

        // 대형으로 펼쳐지는 이동에도 같은 접촉 검사를 적용한다.
        Vector2 nextPosition = Vector2.Lerp(
            _spreadStartPosition,
            _formationPosition,
            easedProgress);

        if (!MoveWithContactCheck(nextPosition))
        {
            return;
        }

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
        // 조준하는 동안 현재 플레이어 위치를 바라본다.
        if (PlayerController != null)
        {
            // 현재 위치에서 플레이어로 향하는 월드 방향을 계산한다.
            Vector2 aimDirection =
                (Vector2)PlayerController.transform.position -
                (Vector2)transform.position;

            // 몸체와 충돌 판정을 플레이어 방향으로 회전시킨다.
            FaceDirection(aimDirection);
        }

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

        // 돌진을 시작하기 전에 확정된 돌진 방향으로 몸체를 회전시킨다.
        FaceDirection(_chargeDirection);

        // 다음 돌진 단계에서 사용할 시간을 초기화한다.
        _stateTimer = 0f;

        // 조준이 끝났으므로 돌진 상태로 전환한다.
        _currentState = SmallEnemyState.Charging;

        // 돌진 시작과 함께 입을 여는 공격 애니메이션을 재생한다.
        _animator.Play(AttackHash, 0, 0f);
    }

    /// <summary>
    /// 조준이 끝날 때 저장한 방향으로 직선 돌진한다.
    /// </summary>
    private void UpdateCharging()
    {
        // 돌진을 시작한 후 흐른 시간을 누적한다.
        _stateTimer += Time.deltaTime;

        // 조준 때 저장한 방향으로 이동할 다음 위치를 계산한다.
        Vector3 nextPosition =
            transform.position +
            (Vector3)(_chargeDirection * _chargeMoveSpeed * Time.deltaTime);

        // 실드나 플레이어 몸에 닿으면 그 자리에서 폭발한다.
        if (!MoveWithContactCheck(nextPosition))
        {
            return;
        }

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

    // 실드로 막힌 폭발은 플레이어에게 범위 피해를 주지 않는다.
    // 인수를 생략한 기존 Explode() 호출은 일반 폭발로 처리한다.
    /// <summary>
    /// 인수를 생략한 기존 Explode() 호출은 일반 폭발로 처리한다.
    /// </summary>
    private void Explode(bool blockedByShield = false)
    {
        // 사망하거나 이미 폭발한 개체는 중복 처리하지 않는다.
        if (IsDead || _hasExploded)
        {
            return;
        }

        _hasExploded = true;
        _currentState = SmallEnemyState.Exploding;

        // 제거되기 전까지 추가 접촉이 발생하지 않도록 몸 판정을 끈다.
        _bodyCollider.enabled = false;

        // 실드에 막혀도 같은 폭발 이펙트를 재생한다.
        if (_explosionEffectPrefab != null)
        {
            SpriteExplosionEffect explosionEffect = Instantiate(
                _explosionEffectPrefab,
                transform.position,
                Quaternion.identity);

            explosionEffect.Play();
        }

        // 일반 폭발일 때만 기존 반경 피해를 적용한다.
        if (!blockedByShield && PlayerController != null)
        {
            float distanceToPlayer = Vector2.Distance(
                transform.position,
                PlayerController.transform.position);

            if (distanceToPlayer <= _explosionRadius)
            {
                PlayerController.TakeHit(_explosionDamage);
            }
        }

        // 자폭 피해와 이펙트 처리를 마친 뒤 보상 없이 퇴장한다.
        Despawn();
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
    /// OnTriggerEnter2D 동작에 필요한 상태와 처리 흐름을 실행한다.
    /// </summary>
    protected override void OnTriggerEnter2D(Collider2D collision)
    {
        // 자폭형은 MoveWithContactCheck에서 접촉 순서를 판단한다.
        // 여기서 다시 폭발하면 실드 검사보다 몸 피해가 먼저 발생할 수 있다.
        if (_moveType == SmallEnemyMoveType.Suicide)
        {
            return;
        }

        // 다른 이동 타입은 부모 Enemy의 기존 몸 충돌 처리를 사용한다.
        base.OnTriggerEnter2D(collision);
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

    // 이미 겹친 상태에서 자폭병이 실드나 몸에 접촉한 경우다.
    /// <summary>
    /// 이미 겹친 상태에서 자폭병이 실드나 몸에 접촉한 경우다.
    /// </summary>
    private bool TryExplodeOnContact(
        Collider2D target,
        Vector3 contactPosition)
    {
        if (_hasExploded || IsDead || target == null)
        {
            return false;
        }

        Vector2 hitPoint = HitEffectUtility.GetSurfacePoint(
            _bodyCollider, target);

        return TryExplodeOnContact(
            target, contactPosition, hitPoint);
    }

    // 크루저 패턴이 적을 생성하고 기본 Init()을 호출한 뒤 사용한다.
    // 제어점과 도착점은 모두 월드 좌표다.
    /// <summary>
    /// 제어점과 도착점은 모두 월드 좌표다.
    /// </summary>
    public void InitializeCurvedSuicidePattern(
        Vector2 control1,
        Vector2 control2,
        Vector2 endPosition,
        float duration)
    {
        if (!(duration > 0f) || float.IsInfinity(duration))
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(duration),
                "곡선 이동 시간은 유효한 양수여야 합니다.");
        }

        // 기존 자폭 방식과 플레이어·실드 접촉 검사를 사용한다.
        _moveType = SmallEnemyMoveType.Suicide;

        // 출격 순간의 경로를 저장한다.
        // 출격한 뒤에는 크루저가 움직여도 이 경로를 따라간다.
        _curveStart = transform.position;
        _curveControl1 = control1;
        _curveControl2 = control2;
        _curveEnd = endPosition;
        _curveDuration = duration;

        // 기존 상태 타이머를 초기화하고 곡선 출격을 시작한다.
        _stateTimer = 0f;
        _currentState = SmallEnemyState.CurvedEntering;
        _isPatternInitialized = true;
    }

    // 구멍에서 곡선으로 출격한 뒤 기존 조준 상태로 전환한다.
    /// <summary>
    /// 구멍에서 곡선으로 출격한 뒤 기존 조준 상태로 전환한다.
    /// </summary>
    private void UpdateCurvedEntering()
    {
        _stateTimer += Time.deltaTime;

        // 이동 진행도를 0~1로 계산한다.
        float t = Mathf.Clamp01(_stateTimer / _curveDuration);
        float remaining = 1f - t;

        // 시작점, 제어점 두 개, 도착점으로 베지어 곡선 위치를 계산한다.
        Vector2 nextPosition =
            remaining * remaining * remaining * _curveStart +
            3f * remaining * remaining * t * _curveControl1 +
            3f * remaining * t * t * _curveControl2 +
            t * t * t * _curveEnd;

        // 기존 이동 경로의 충돌 검사를 그대로 사용한다.
        // 출격 중 플레이어나 실드에 접촉하면 기존 자폭 처리가 실행된다.
        if (!MoveWithContactCheck(nextPosition))
        {
            return;
        }

        if (t < 1f)
        {
            return;
        }

        // 곡선 끝에서는 기존 조준 시간을 새로 계산한다.
        _stateTimer = 0f;
        _currentState = SmallEnemyState.Aiming;
    }
}

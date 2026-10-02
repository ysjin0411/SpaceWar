using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 패턴에서 전달받은 설정으로 이동하고 공격하는 회전형 적이다.
/// </summary>
public sealed class RotEnemy : Enemy
{
    // Twin 패턴의 현재 행동 단계다.
    enum TwinState
    {
        Entering,
        Retreating,
        Wandering,
        Exiting
    }

    // 총알이 생성되는 위치다.
    [SerializeField] Transform _firePoint;

    // Renderer 자식의 Animator다.
    [SerializeField] Animator _animator;

    // 패턴 에셋에서 복사해 받은 설정이다.
    RotEnemyTwinSettings _settings;

    // 현재 행동 단계다.
    TwinState _state;

    // 첫 도착 위치이자 고정 부채꼴의 꼭짓점이다.
    Vector2 _arrival;

    // 현재 이동할 목적지다.
    Vector2 _moveTarget;

    // 부드러운 잔류 이동에서 유지하는 속도다.
    Vector2 _wanderVelocity;

    // 고정 부채꼴의 중앙 방향 각도다.
    float _aimAngle;

    // 첫 공격을 시작한 뒤 지난 시간이다.
    float _attackElapsed;

    // 다음 공격 시작까지 남은 시간이다.
    float _shotRemaining;

    // 패턴 설정을 전달받았는지 확인한다.
    bool _initialized;

    // 공격 애니메이션이 진행 중인지 확인한다.
    bool _attackPlaying;

    // 같은 공격에서 중복 발사를 방지한다.
    bool _firedThisAttack;

    // Animator 상태의 전체 경로다.
    static readonly int IdleHash =
        Animator.StringToHash("Base Layer.Idle");

    // 현재 공통 컨트롤러의 공격 상태 경로다.
    static readonly int AttackHash =
        Animator.StringToHash("Base Layer.Attack_4Frame");

    /// <summary>
    /// 부모의 체력을 초기화하고 필수 컴포넌트를 검사한다.
    /// </summary>
    protected override void Awake()
    {
        base.Awake();

        if (_firePoint == null || _animator == null)
        {
            throw new InvalidOperationException(
                $"{name}: FirePoint와 Animator를 연결하세요.");
        }

        if (!_animator.HasState(0, IdleHash) ||
            !_animator.HasState(0, AttackHash))
        {
            throw new InvalidOperationException(
                $"{name}: Animator에 Idle과 Attack 상태가 필요합니다.");
        }
    }

    // 최초 등장과 풀에서 다시 꺼낼 때 패턴 초기화를 기다린다.
    private void OnEnable()
    {
        // InitializeTwinPattern이 호출되기 전에는 이동과 발사를 막는다.
        _initialized = false;

        // 이전 공격의 진행 상태를 지운다.
        _attackPlaying = false;
        _firedThisAttack = false;

        // 대기 애니메이션을 정상 속도로 처음부터 재생한다.
        _animator.speed = 1f;
        _animator.Play(IdleHash, 0, 0f);

        // 대기 상태를 즉시 반영한다.
        _animator.Update(0f);
    }

    /// <summary>
    /// Twin 패턴의 도착 위치와 설정을 전달받는다.
    /// </summary>
    public void InitializeTwinPattern(
        Vector2 arrival,
        RotEnemyTwinSettings settings)
    {
        _arrival = arrival;
        _settings = settings;

        // 후퇴해도 부채꼴이 따라오지 않도록 중앙 방향을 저장한다.
        Vector2 direction = settings.AimPoint - arrival;
        _aimAngle = Mathf.Atan2(
            direction.y, direction.x) * Mathf.Rad2Deg;

        _attackElapsed = 0f;
        _shotRemaining = 0f;
        _wanderVelocity = Vector2.zero;
        _attackPlaying = false;
        _firedThisAttack = false;
        _state = TwinState.Entering;
        _initialized = true;

        _animator.Play(IdleHash, 0, 0f);
    }

    /// <summary>
    /// 이동 상태와 공격 주기를 갱신한다.
    /// </summary>
    private void Update()
    {
        if (!_initialized || IsDead || Time.deltaTime <= 0f)
        {
            return;
        }

        switch (_state)
        {
            case TwinState.Entering:
                UpdateEntering();
                return;

            case TwinState.Exiting:
                UpdateExiting();
                return;
        }

        // 후퇴와 잔류 시간은 첫 도착 순간부터 함께 계산한다.
        _attackElapsed += Time.deltaTime;

        if (_attackElapsed >= _settings.AttackDuration)
        {
            _state = TwinState.Exiting;
            _attackPlaying = false;
            _animator.Play(IdleHash, 0, 0f);

            UpdateExiting();
            return;
        }

        if (_state == TwinState.Retreating)
        {
            UpdateRetreating();
        }
        else
        {
            UpdateWandering();
        }

        // 이동하면서도 공격 간격을 계속 계산한다.
        _shotRemaining -= Time.deltaTime;

        if (_shotRemaining <= 0f && !_attackPlaying)
        {
            BeginAttack();
        }
    }

    /// <summary>
    /// 자신의 X를 유지하며 EndY까지 이동한다.
    /// </summary>
    private void UpdateEntering()
    {
        Vector2 next = Vector2.MoveTowards(
            transform.position,
            _arrival,
            _settings.EnterSpeed * Time.deltaTime);

        SetPosition(next);

        if ((next - _arrival).sqrMagnitude > 0.000001f)
        {
            return;
        }

        SetPosition(_arrival);

        // 최초 후퇴 목적지는 잔류 원 전체에서 고른다.
        _moveTarget = _settings.StayCenter +
            Random.insideUnitCircle * _settings.StayRadius;

        _state = TwinState.Retreating;
        _attackElapsed = 0f;

        // 도착 즉시 공격 애니메이션을 시작한다.
        BeginAttack();
    }

    /// <summary>
    /// 설정된 후퇴 속도로 원 내부의 첫 목적지까지 이동한다.
    /// </summary>
    private void UpdateRetreating()
    {
        Vector2 next = Vector2.MoveTowards(
            transform.position,
            _moveTarget,
            _settings.RetreatSpeed * Time.deltaTime);

        SetPosition(next);

        if ((next - _moveTarget).sqrMagnitude > 0.000001f)
        {
            return;
        }

        _state = TwinState.Wandering;
        _wanderVelocity = Vector2.zero;
        SelectWanderTarget();
    }

    /// <summary>
    /// 현재 위치에서 가까운 원 내부 목적지를 고른다.
    /// </summary>
    private void SelectWanderTarget()
    {
        Vector2 current = transform.position;

        float step = Mathf.Min(
            _settings.WanderStep,
            _settings.StayRadius);

        Vector2 candidate = _settings.StayCenter +
            Random.insideUnitCircle * _settings.StayRadius;

        // 너무 가까운 점이면 원의 반대편을 후보로 사용한다.
        if ((candidate - current).sqrMagnitude < step * step * 0.25f)
        {
            Vector2 outward = current - _settings.StayCenter;

            if (outward.sqrMagnitude < 0.0001f)
            {
                outward = Vector2.up;
            }

            candidate = _settings.StayCenter -
                outward.normalized * _settings.StayRadius;
        }

        // 원 내부 두 점을 잇는 선분 위에서 가까운 목적지를 선택한다.
        _moveTarget = Vector2.MoveTowards(
            current,
            candidate,
            Random.Range(step * 0.5f, step));
    }

    /// <summary>
    /// 목적지 근처에서 다음 지점을 골라 부드럽게 이동한다.
    /// </summary>
    private void UpdateWandering()
    {
        Vector2 current = transform.position;

        float changeDistance = Mathf.Min(
            _settings.WanderStep,
            _settings.StayRadius) * 0.2f;

        // 완전히 정지하기 전에 다음 목적지를 선택한다.
        if ((_moveTarget - current).sqrMagnitude <=
            changeDistance * changeDistance)
        {
            SelectWanderTarget();
        }

        Vector2 next = Vector2.SmoothDamp(
            current,
            _moveTarget,
            ref _wanderVelocity,
            _settings.WanderSmoothTime,
            _settings.WanderSpeed,
            Time.deltaTime);

        // 방향 전환의 관성 때문에 원 밖으로 나가는 것을 제한한다.
        Vector2 offset = next - _settings.StayCenter;

        if (offset.sqrMagnitude >
            _settings.StayRadius * _settings.StayRadius)
        {
            Vector2 normal = offset.normalized;

            next = _settings.StayCenter +
                normal * _settings.StayRadius;

            // 원 바깥으로 향하는 속도만 제거한다.
            float outwardSpeed =
                Vector2.Dot(_wanderVelocity, normal);

            if (outwardSpeed > 0f)
            {
                _wanderVelocity -= normal * outwardSpeed;
            }
        }

        SetPosition(next);
    }

    /// <summary>
    /// 공격을 시작한다. 총알 생성은 발사 프레임에서 실행한다.
    /// </summary>
    private void BeginAttack()
    {
        _shotRemaining = _settings.ShotInterval;
        _attackPlaying = true;
        _firedThisAttack = false;

        _animator.Play(AttackHash, 0, 0f);
    }

    /// <summary>
    /// Attack의 발사 프레임에서 호출한다.
    /// </summary>
    public override void FireFromAnimation()
    {
        if (!_initialized || IsDead ||
            !_attackPlaying || _firedThisAttack ||
            _state == TwinState.Exiting)
        {
            return;
        }

        _firedThisAttack = true;

        for (int i = 0; i < _settings.BulletsPerShot; i++)
        {
            float angle = _aimAngle + Random.Range(
                -_settings.SectorAngle * 0.5f,
                _settings.SectorAngle * 0.5f);

            float radians = angle * Mathf.Deg2Rad;

            Vector2 radial = new Vector2(
                Mathf.Cos(radians),
                Mathf.Sin(radians));

            // 부채꼴 면적 전체에서 고르게 랜덤 지점을 선택한다.
            float distance = Mathf.Sqrt(Random.value) *
                _settings.SectorRadius;

            Vector2 target = _arrival + radial * distance;

            // 현재 총구에서 고정된 부채꼴 영역을 향해 발사한다.
            Vector2 direction =
                target - (Vector2)_firePoint.position;

            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = radial;
            }

            // 설정된 탄환 프리팹의 풀에서 오브젝트를 꺼낸다.
            GameObject bulletObject = ProjectilePoolManager.Instance.Spawn(
                _settings.BulletPrefab.gameObject,
                _firePoint.position,
                Quaternion.identity);

            // 꺼낸 오브젝트에서 이동과 충돌을 담당하는 컴포넌트를 가져온다.
            EnemyBullet bullet = bulletObject.GetComponent<EnemyBullet>();

            bullet.Init(
                direction,
                _settings.BulletSpeed,
                _settings.BulletDamage);
        }
    }

    /// <summary>
    /// 공통 Relay가 Idle로 돌려놓은 뒤 다음 공격을 허용한다.
    /// </summary>
    public override void FinishAttackFromAnimation()
    {
        _attackPlaying = false;
    }

    /// <summary>
    /// 현재 Y를 유지하며 오른쪽으로 퇴장한다.
    /// </summary>
    private void UpdateExiting()
    {
        transform.Translate(
            Vector3.right * _settings.ExitSpeed * Time.deltaTime,
            Space.World);

        // 설정된 퇴장 위치에 도착하면 공통 퇴장 처리를 호출한다.
        if (transform.position.x >= _settings.ExitX)
        {
            // 처치가 아니므로 보상 없이 풀에 반납한다.
            Despawn();
        }
    }

    /// <summary>
    /// 기존 Z를 유지하며 XY 좌표를 변경한다.
    /// </summary>
    private void SetPosition(Vector2 position)
    {
        transform.position = new Vector3(
            position.x,
            position.y,
            transform.position.z);
    }
}
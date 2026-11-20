using System;
using DG.Tweening;
using UnityEngine;

// 구체형 접촉 판정과 투사체 이동에 필요한 컴포넌트.
[RequireComponent(typeof(CircleCollider2D), typeof(Rigidbody2D))]
public sealed class CruiserBigBullet : MonoBehaviour
{
    // 화면 반사 시 그림이 화면 밖으로 잘리지 않도록 크기를 확인한다.
    [SerializeField] private SpriteRenderer _bulletRenderer;

    // 충전과 비행 스프라이트 애니메이션을 재생한다.
    [SerializeField] private Animator _bulletAnimator;

    // 충전 시작 시 재생할 상태의 전체 경로.
    private static readonly int ChargeState =
        Animator.StringToHash("Base Layer.CruiserBigBullet_Charge");

    // 충전 완료 후 대기 중과 비행 중에 반복할 상태의 전체 경로.
    private static readonly int ActiveFlightState =
        Animator.StringToHash("Base Layer.CruiserBigBullet_ActiveFlight");

    // 충전부터 비행까지 유지할 탄환 전체 크기 배율.
    [SerializeField, Min(0.01f)] private float _bulletScale = 1.8f;

    // 충전 스프라이트가 처음부터 끝까지 재생되는 시간.
    [SerializeField, Min(0.01f)] private float _growDuration = 2f;

    // 충전 완료 후 완성된 모습을 반복하며 발사를 기다리는 시간.
    [SerializeField, Min(0f)] private float _holdDuration = 1f;

    // 발사 후 초당 이동 거리.
    [SerializeField, Min(0.01f)] private float _speed = 5f;

    // 충전 시간을 제외한 발사 후 생존 시간.
    [SerializeField, Min(0.01f)] private float _lifeDuration = 8f;

    // 이 시간만큼 누적 접촉할 때마다 피해를 한 번 준다.
    [SerializeField, Min(0.01f)] private float _damageInterval = 0.5f;

    // 접촉 시간이 피해 간격에 도달했을 때 적용할 피해량.
    [SerializeField, Min(1)] private float _damage = 5;

    // 투사체를 생성한 보스와 피해를 받을 플레이어.
    private BossEnemy _owner;
    private PlayerController _player;

    // 충전 중 따라갈 위치와 화면 경계를 계산할 카메라.
    private Transform _firePoint;
    private Camera _battleCamera;

    // 탄환의 원형 피해 영역.
    private CircleCollider2D _bulletCollider;
    private Collider2D _playerCollider;

    // 투사체 이동은 이 스크립트에서 직접 계산한다.
    private Rigidbody2D _body;

    // 발사 순간 현재 위치에서 이 월드 좌표를 향한다.
    private Vector2 _targetPosition;

    // 발사 후 이동 방향과 초당 속도를 함께 저장한다.
    private Vector2 _velocity;

    // 투사체 크기를 제외하고 계산한 중심점의 이동 가능 범위.
    private Rect _movementBounds;

    // 화면 밖에서 발사된 축은 안쪽에 진입한 뒤부터 반사를 적용한다.
    private bool _enteredHorizontalBounds;
    private bool _enteredVerticalBounds;

    // 남은 수명과 아직 피해로 환산되지 않은 접촉 시간.
    private float _remainingLife;
    private float _contactTime;

    // 초기화와 발사 여부를 구분한다.
    private bool _initialized;
    private bool _launched;
    // 충전 취소와 발사 전환에서 정지할 소리.
    private GameAudio.Handle _chargeAudio;

    // 비활성화될 때 충전 연출도 중단할 수 있도록 보관한다.
    private Sequence _chargeSequence;

    // 다음 단계의 보스 패턴에서 발사 완료 여부를 확인한다.
    public bool HasLaunched => _launched;

    /// <summary>
    /// 컴포넌트 참조와 초기 실행 상태를 준비한다.
    /// </summary>
    private void Awake()
    {
        // 루트의 원형 콜라이더를 접촉 판정에 사용한다.
        _bulletCollider = GetComponent<CircleCollider2D>();
        _body = GetComponent<Rigidbody2D>();

        // 충전 중에는 접촉 피해를 판정하지 않는다.
        _bulletCollider.isTrigger = true;
        _bulletCollider.enabled = false;

        // 화면 반사는 직접 계산하므로 물리 반발력은 사용하지 않는다.
        _body.bodyType = RigidbodyType2D.Kinematic;
        _body.gravityScale = 0f;
        _body.linearVelocity = Vector2.zero;
        _body.angularVelocity = 0f;
        _body.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    // 보스 패턴이 투사체를 생성한 직후 호출한다.
    /// <summary>
    /// 보스 패턴이 투사체를 생성한 직후 호출한다.
    /// </summary>
    public void Initialize(
        BossEnemy owner,
        PlayerController player,
        Transform firePoint,
        Camera battleCamera,
        Vector2 targetPosition)
    {
        if (_initialized)
        {
            throw new InvalidOperationException(
                $"{name}: 이미 초기화된 투사체입니다.");
        }

        if (owner == null || player == null ||
            firePoint == null || battleCamera == null ||
            _bulletRenderer == null)
        {
            throw new InvalidOperationException(
                $"{name}: 보스, 플레이어, FirePoint, 카메라, Renderer를 확인하세요.");
        }

        // 그림을 표시하는 오브젝트의 Animator가 연결됐는지 확인한다.
        if (_bulletAnimator == null ||
            _bulletAnimator.gameObject != _bulletRenderer.gameObject ||
            !_bulletAnimator.isActiveAndEnabled ||
            _bulletAnimator.runtimeAnimatorController == null)
        {
            throw new InvalidOperationException(
                $"{name}: Renderer의 Animator와 Controller를 확인하세요.");
        }

        // 충전·비행 상태가 모두 있어야 연출을 정상 전환할 수 있다.
        if (!_bulletAnimator.HasState(0, ChargeState) ||
            !_bulletAnimator.HasState(0, ActiveFlightState))
        {
            throw new InvalidOperationException(
                $"{name}: 충전 또는 비행 애니메이션 상태가 없습니다.");
        }

        // 현재 게임처럼 회전하지 않은 XY 평면의 직교 카메라를 사용한다.
        if (!battleCamera.orthographic ||
            Vector3.Dot(battleCamera.transform.right, Vector3.right) < 0.9999f ||
            Vector3.Dot(battleCamera.transform.up, Vector3.up) < 0.9999f)
        {
            throw new InvalidOperationException(
                $"{name}: XY 평면을 보는 회전 없는 직교 카메라가 필요합니다.");
        }

        if (_bulletScale <= 0f || 
            _growDuration <= 0f ||
            _holdDuration < 0f ||
            _speed <= 0f ||
            _lifeDuration <= 0f || 
            _damageInterval <= 0f ||
            _damage <= 0)
        {
            throw new InvalidOperationException(
                $"{name}: 시간, 속도, 피해 설정을 확인하세요.");
        }

        _playerCollider = player.GetComponent<Collider2D>();

        if (_playerCollider == null)
        {
            throw new InvalidOperationException(
                $"{name}: 플레이어 루트의 Collider2D가 필요합니다.");
        }

        _owner = owner;
        _player = player;
        _firePoint = firePoint;
        _battleCamera = battleCamera;
        _targetPosition = targetPosition;

        // 보스의 스케일과 회전에 영향을 받지 않도록 독립시킨다.
        transform.SetParent(null);
        transform.SetPositionAndRotation(
            firePoint.position, Quaternion.identity);

        // 새 스프라이트는 PPU 236, Scale 1을 기준으로 제작했다.
        // 충전 중에도 크기는 고정하고 이미지 변화로 충전을 표현한다.
        _bulletCollider.offset = Vector2.zero;
        // 충전과 비행 내내 지정한 크기를 유지한다.
        transform.localScale = Vector3.one * _bulletScale;

        // 충전 클립을 첫 프레임부터 재생하고 길이를 확인한다.
        _bulletAnimator.speed = 1f;
        _bulletAnimator.Play(ChargeState, 0, 0f);
        _bulletAnimator.Update(0f);

        float chargeClipLength =
            _bulletAnimator.GetCurrentAnimatorStateInfo(0).length;

        if (chargeClipLength <= 0f)
        {
            throw new InvalidOperationException(
                $"{name}: 충전 애니메이션 클립의 길이를 확인하세요.");
        }

        // 충전 시간을 조절해도 전체 충전 이미지가 그 시간 안에 재생된다.
        _bulletAnimator.speed = chargeClipLength / _growDuration;

        _initialized = true;
        _chargeAudio = GameAudio.PlayOwned(GameSound.CruiserBigshotCharge, this);

        // 충전과 발사 대기는 게임 시간으로 진행한다.
        _chargeSequence = DOTween.Sequence();
        _chargeSequence.AppendInterval(_growDuration);

        // 충전이 끝나면 완성된 탄환 애니메이션을 정상 속도로 반복한다.
        _chargeSequence.AppendCallback(() =>
        {
            _bulletAnimator.speed = 1f;
            _bulletAnimator.Play(ActiveFlightState, 0, 0f);
            _bulletAnimator.Update(0f);
        });

        // 완성된 모습으로 대기한 뒤 발사한다.
        // 발사 후에도 같은 비행 애니메이션을 계속 반복한다.
        _chargeSequence.AppendInterval(_holdDuration);
        _chargeSequence.OnComplete(Launch);
    }

    /// <summary>
    /// 프레임의 이동 결과에 맞춰 후속 상태를 갱신한다.
    /// </summary>
    private void LateUpdate()
    {
        if (!_initialized)
        {
            return;
        }

        // 보스 처치 이후 이동 연출에 공격이 남지 않도록 제거한다.
        if (_owner == null || _owner.IsDead ||
            !_owner.isActiveAndEnabled)
        {
            Destroy(gameObject);
            return;
        }

        // 충전과 발사 대기 중에는 본체의 FirePoint를 따라간다.
        if (!_launched)
        {
            if (_firePoint == null)
            {
                Destroy(gameObject);
                return;
            }

            transform.position = _firePoint.position;
        }
    }

    /// <summary>
    /// Launch 동작에 필요한 상태와 처리 흐름을 실행한다.
    /// </summary>
    private void Launch()
    {
        if (_owner == null || _owner.IsDead ||
            !_owner.isActiveAndEnabled || _firePoint == null)
        {
            Destroy(gameObject);
            return;
        }

        // 발사 순간의 FirePoint 위치에서 목표 좌표 방향을 계산한다.
        transform.position = _firePoint.position;

        Vector2 direction =
            _targetPosition - (Vector2)transform.position;

        if (direction.sqrMagnitude < 0.000001f)
        {
            Destroy(gameObject);

            throw new InvalidOperationException(
                $"{name}: 발사 위치와 목표 좌표가 같습니다.");
        }

        // 성장한 크기가 콜라이더와 Renderer의 경계에 반영되게 한다.
        _bulletCollider.enabled = true;
        Physics2D.SyncTransforms();

        CalculateMovementBounds();

        Vector2 startPosition = transform.position;

        // 포구가 가장자리에 있으면 현재 위치에서 자연스럽게 화면 안으로 진입한다.
        // 진입 전에는 해당 축의 벽 반사를 유예하고 속도를 안쪽으로 향하게 한다.
        _body.position = startPosition;
        _velocity = direction.normalized * _speed;
        _enteredHorizontalBounds = startPosition.x >= _movementBounds.xMin && startPosition.x <= _movementBounds.xMax;
        _enteredVerticalBounds = startPosition.y >= _movementBounds.yMin && startPosition.y <= _movementBounds.yMax;
        if (!_enteredHorizontalBounds)
            _velocity.x = startPosition.x < _movementBounds.xMin ? Mathf.Abs(_velocity.x) : -Mathf.Abs(_velocity.x);
        if (!_enteredVerticalBounds)
            _velocity.y = startPosition.y < _movementBounds.yMin ? Mathf.Abs(_velocity.y) : -Mathf.Abs(_velocity.y);
        // 축 속도가 0이어도 바깥에 머물지 않도록 화면 안의 목표를 향한다.
        if ((!_enteredHorizontalBounds && Mathf.Abs(_velocity.x) < 0.0001f) ||
            (!_enteredVerticalBounds && Mathf.Abs(_velocity.y) < 0.0001f))
            _velocity = (_movementBounds.center - startPosition).normalized * _speed;
        _remainingLife = _lifeDuration;
        _contactTime = 0f;
        _launched = true;
        GameAudio.Stop(_chargeAudio);
        GameAudio.Play(GameSound.CruiserBigshotFire);
    }

    // 발사 시점의 카메라 화면을 반사 영역으로 사용한다.
    /// <summary>
    /// 발사 시점의 카메라 화면을 반사 영역으로 사용한다.
    /// </summary>
    private void CalculateMovementBounds()
    {
        float depth = Vector3.Dot(
            transform.position - _battleCamera.transform.position,
            _battleCamera.transform.forward);

        Vector3 bottomLeft = _battleCamera.ViewportToWorldPoint(
            new Vector3(0f, 0f, depth));

        Vector3 topRight = _battleCamera.ViewportToWorldPoint(
            new Vector3(1f, 1f, depth));

        // 그림과 충돌 영역을 모두 포함하는 경계를 구한다.
        Bounds bounds = _bulletCollider.bounds;
        bounds.Encapsulate(_bulletRenderer.bounds);

        Vector3 origin = transform.position;

        float left = origin.x - bounds.min.x;
        float right = bounds.max.x - origin.x;
        float bottom = origin.y - bounds.min.y;
        float top = bounds.max.y - origin.y;

        _movementBounds = Rect.MinMaxRect(
            bottomLeft.x + left,
            bottomLeft.y + bottom,
            topRight.x - right,
            topRight.y - top);

        if (_movementBounds.width <= 0f ||
            _movementBounds.height <= 0f)
        {
            Destroy(gameObject);

            throw new InvalidOperationException(
                $"{name}: 투사체 크기가 화면보다 큽니다.");
        }
    }

    /// <summary>
    /// 물리 갱신 주기에 맞춰 이동과 충돌 처리를 수행한다.
    /// </summary>
    private void FixedUpdate()
    {
        if (!_launched)
        {
            return;
        }

        if (_owner == null || _owner.IsDead ||
            !_owner.isActiveAndEnabled)
        {
            Destroy(gameObject);
            return;
        }

        // 마지막 프레임에도 실제 남은 수명만큼만 이동하고 피해를 계산한다.
        float duration = Mathf.Min(
            Time.fixedDeltaTime, _remainingLife);

        // 빠른 이동에서 접촉을 건너뛰는 일을 줄이기 위해
        // 투사체 크기에 비례하여 이동과 접촉 검사를 잘게 나눈다.
        Vector3 size = _bulletCollider.bounds.size;
        float sampleDistance =
            Mathf.Max(0.001f, Mathf.Min(size.x, size.y) * 0.25f);

        int steps = Mathf.Max(
            1, Mathf.CeilToInt(_speed * duration / sampleDistance));

        float stepDuration = duration / steps;

        for (int i = 0; i < steps; i++)
        {
            Vector2 position =
                _body.position + _velocity * stepDuration;

            ReflectAfterEntry(
                ref _enteredHorizontalBounds, ref position.x, ref _velocity.x,
                _movementBounds.xMin, _movementBounds.xMax);

            ReflectAfterEntry(
                ref _enteredVerticalBounds, ref position.y, ref _velocity.y,
                _movementBounds.yMin, _movementBounds.yMax);

            _body.position = position;

            // 변경된 위치를 접촉 검사에 즉시 반영한다.
            Physics2D.SyncTransforms();
            AccumulateContactDamage(stepDuration);

            // 이번 피해로 플레이어가 사망하면 추가 계산을 중단한다.
            if (_player != null && _player.IsDead)
            {
                break;
            }
        }

        _remainingLife -= duration;

        if (_remainingLife <= 0f)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>화면 밖에서 출발한 축은 안쪽 경계를 통과한 순간부터 반사합니다.</summary>
    private static void ReflectAfterEntry(ref bool entered, ref float position,
        ref float velocity, float minimum, float maximum)
    {
        // 이동 방향 기준으로 진입 여부를 판단하므로 한 스텝에 영역을 지나도 반사된다.
        if (!entered)
            entered = velocity > 0f ? position >= minimum : position <= maximum;
        if (entered) ReflectAxis(ref position, ref velocity, minimum, maximum);
    }

    // 벽을 초과한 이동 거리도 반사하여 속도가 줄지 않도록 한다.
    // 모서리에서는 X와 Y가 각각 반사된다.
    /// <summary>
    /// 모서리에서는 X와 Y가 각각 반사된다.
    /// </summary>
    private static void ReflectAxis(
        ref float position,
        ref float velocity,
        float minimum,
        float maximum)
    {
        while (position < minimum || position > maximum)
        {
            if (position < minimum)
            {
                position = minimum + (minimum - position);
                velocity = Mathf.Abs(velocity);
            }
            else
            {
                position = maximum - (position - maximum);
                velocity = -Mathf.Abs(velocity);
            }
        }
    }

    /// <summary>
    /// AccumulateContactDamage 동작에 필요한 상태와 처리 흐름을 실행한다.
    /// </summary>
    private void AccumulateContactDamage(float duration)
    {
        // 이동 연출 중에는 접촉 시간도 쌓지 않는다.
        if (_player == null || _player.IsDead ||
            !_player.isActiveAndEnabled || !_player.CanControl ||
            _playerCollider == null ||
            !_playerCollider.isActiveAndEnabled)
        {
            return;
        }

        // 플레이어의 실제 콜라이더와 겹치는지 검사한다.
        ColliderDistance2D contact =
            Physics2D.Distance(_bulletCollider, _playerCollider);

        if (!contact.isOverlapped)
        {
            // 떨어져 있는 동안에는 시간이 증가하지 않는다.
            // 이전 접촉 시간의 나머지는 유지한다.
            return;
        }

        _contactTime += duration;

        // 누적 접촉 시간이 길어도 필요한 피해 횟수를 빠뜨리지 않는다.
        while (_contactTime >= _damageInterval)
        {
            _contactTime -= _damageInterval;
            _player.TakeContinuousDamage(_damage);

            if (_player.IsDead)
            {
                return;
            }
        }
    }

    /// <summary>
    /// 비활성화 시 이벤트 연결과 실행 상태를 정리한다.
    /// </summary>
    private void OnDisable()
    {
        GameAudio.Stop(_chargeAudio);
        // 충전 도중 제거되면 예약된 발사 콜백도 함께 취소한다.
        if (_chargeSequence != null)
        {
            _chargeSequence.Kill();
            _chargeSequence = null;
        }

        _bulletCollider.enabled = false;
    }
}
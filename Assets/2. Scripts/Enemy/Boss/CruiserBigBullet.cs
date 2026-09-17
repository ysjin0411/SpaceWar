using System;
using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D), typeof(Rigidbody2D))]
public sealed class CruiserBigBullet : MonoBehaviour
{
    // 화면 반사 시 그림이 화면 밖으로 잘리지 않도록 크기를 확인한다.
    [SerializeField] private SpriteRenderer _bulletRenderer;

    // 충전이 끝났을 때 도달할 루트 오브젝트의 스케일.
    [SerializeField, Min(0.01f)] private float _targetScale = 4f;

    // 스케일이 0에서 목표 크기까지 성장하는 시간.
    [SerializeField, Min(0.01f)] private float _growDuration = 2f;

    // 성장이 끝난 뒤 발사하기 전까지 기다리는 시간.
    [SerializeField, Min(0f)] private float _holdDuration = 1f;

    // 발사 후 초당 이동 거리.
    [SerializeField, Min(0.01f)] private float _speed = 5f;

    // 충전 시간을 제외한 발사 후 생존 시간.
    [SerializeField, Min(0.01f)] private float _lifeDuration = 8f;

    // 이 시간만큼 누적 접촉할 때마다 피해를 한 번 준다.
    [SerializeField, Min(0.01f)] private float _damageInterval = 0.5f;

    // 접촉 시간이 피해 간격에 도달했을 때 적용할 피해량.
    [SerializeField, Min(1)] private int _damage = 5;

    // 투사체를 생성한 보스와 피해를 받을 플레이어.
    private BossEnemy _owner;
    private PlayerController _player;

    // 충전 중 따라갈 위치와 화면 경계를 계산할 카메라.
    private Transform _firePoint;
    private Camera _battleCamera;

    // 실제 접촉 여부를 확인할 두 콜라이더.
    private BoxCollider2D _bulletCollider;
    private Collider2D _playerCollider;

    // 투사체 이동은 이 스크립트에서 직접 계산한다.
    private Rigidbody2D _body;

    // 발사 순간 현재 위치에서 이 월드 좌표를 향한다.
    private Vector2 _targetPosition;

    // 발사 후 이동 방향과 초당 속도를 함께 저장한다.
    private Vector2 _velocity;

    // 투사체 크기를 제외하고 계산한 중심점의 이동 가능 범위.
    private Rect _movementBounds;

    // 남은 수명과 아직 피해로 환산되지 않은 접촉 시간.
    private float _remainingLife;
    private float _contactTime;

    // 초기화와 발사 여부를 구분한다.
    private bool _initialized;
    private bool _launched;

    // 비활성화될 때 충전 연출도 중단할 수 있도록 보관한다.
    private Sequence _chargeSequence;

    // 다음 단계의 보스 패턴에서 발사 완료 여부를 확인한다.
    public bool HasLaunched => _launched;

    private void Awake()
    {
        _bulletCollider = GetComponent<BoxCollider2D>();
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

        // 현재 게임처럼 회전하지 않은 XY 평면의 직교 카메라를 사용한다.
        if (!battleCamera.orthographic ||
            Vector3.Dot(battleCamera.transform.right, Vector3.right) < 0.9999f ||
            Vector3.Dot(battleCamera.transform.up, Vector3.up) < 0.9999f)
        {
            throw new InvalidOperationException(
                $"{name}: XY 평면을 보는 회전 없는 직교 카메라가 필요합니다.");
        }

        if (_targetScale <= 0f || _growDuration <= 0f ||
            _holdDuration < 0f || _speed <= 0f ||
            _lifeDuration <= 0f || _damageInterval <= 0f ||
            _damage <= 0)
        {
            throw new InvalidOperationException(
                $"{name}: 크기, 시간, 속도, 피해 설정을 확인하세요.");
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

        // 현재 프리팹처럼 중앙에 있는 BoxCollider2D를 사용한다.
        _bulletCollider.offset = Vector2.zero;
        transform.localScale = Vector3.zero;

        _initialized = true;

        // 기본 게임 시간으로 진행하므로 일시정지 시 충전도 멈춘다.
        _chargeSequence = DOTween.Sequence();
        _chargeSequence.Append(
            transform.DOScale(_targetScale, _growDuration)
                .SetEase(Ease.Linear));

        _chargeSequence.AppendInterval(_holdDuration);
        _chargeSequence.OnComplete(Launch);
    }

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

        // 발사 위치에서 투사체 전체가 화면 안에 들어와야 한다.
        if (startPosition.x < _movementBounds.xMin ||
            startPosition.x > _movementBounds.xMax ||
            startPosition.y < _movementBounds.yMin ||
            startPosition.y > _movementBounds.yMax)
        {
            Destroy(gameObject);

            throw new InvalidOperationException(
                $"{name}: FirePoint 위치 또는 Target Scale을 조정하세요. " +
                "발사 시 투사체 전체가 화면 안에 있어야 합니다.");
        }

        _body.position = startPosition;
        _velocity = direction.normalized * _speed;
        _remainingLife = _lifeDuration;
        _contactTime = 0f;
        _launched = true;
    }

    // 발사 시점의 카메라 화면을 반사 영역으로 사용한다.
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

            ReflectAxis(
                ref position.x, ref _velocity.x,
                _movementBounds.xMin, _movementBounds.xMax);

            ReflectAxis(
                ref position.y, ref _velocity.y,
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

    // 벽을 초과한 이동 거리도 반사하여 속도가 줄지 않도록 한다.
    // 모서리에서는 X와 Y가 각각 반사된다.
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

    private void OnDisable()
    {
        // 충전 도중 제거되면 예약된 발사 콜백도 함께 취소한다.
        if (_chargeSequence != null)
        {
            _chargeSequence.Kill();
            _chargeSequence = null;
        }

        _bulletCollider.enabled = false;
    }
}
using System;
using UnityEngine;

// 필드 건전지의 반사 이동, 수명, 플레이어 획득을 담당한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public sealed class UltimatePickup : MonoBehaviour
{
    // 초당 이동하는 월드 거리다.
    [SerializeField, Min(0.01f)] private float _moveSpeed = 4f;

    // 생성 후 필드에 남아 있는 게임 시간이다.
    [SerializeField, Min(0.01f)] private float _lifeTime = 10f;

    // 스프라이트가 경계를 넘어가지 않도록 확보하는 여백이다.
    // 건전지의 월드 반너비와 반높이를 기준으로 설정한다.
    [SerializeField]
    private Vector2 _edgePadding =
        new Vector2(0.45f, 0.36f);

    private Rigidbody2D _body;
    private CircleCollider2D _trigger;

    // 충돌 반사에 따라 부호가 바뀌는 단위 방향 벡터다.
    private Vector2 _direction;

    // 여백을 제외한 실제 이동 중심의 허용 범위다.
    private Rect _moveBounds;

    private float _remainingTime;

    // 같은 물리 프레임에 여러 Collider가 닿아도 한 번만 소비한다.
    private bool _isConsumed;

    private void Awake()
    {
        _body = GetComponent<Rigidbody2D>();
        _trigger = GetComponent<CircleCollider2D>();

        if (_moveSpeed <= 0f ||
            _lifeTime <= 0f ||
            _edgePadding.x < 0f ||
            _edgePadding.y < 0f)
        {
            throw new InvalidOperationException(
                $"{name}: 이동 속도, 수명, 경계 여백을 확인해주세요.");
        }

        // 이동은 이 컴포넌트가 제어하고 물리 힘에는 영향을 받지 않는다.
        _body.bodyType = RigidbodyType2D.Kinematic;
        _body.gravityScale = 0f;
        _body.constraints = RigidbodyConstraints2D.FreezeRotation;
        _body.interpolation = RigidbodyInterpolation2D.Interpolate;

        // 플레이어를 밀지 않고 접촉만 감지한다.
        _trigger.isTrigger = true;
    }

    private void Start()
    {
        Rect bounds = PlayerController.MovementBounds;

        _moveBounds = Rect.MinMaxRect(
            bounds.xMin + _edgePadding.x,
            bounds.yMin + _edgePadding.y,
            bounds.xMax - _edgePadding.x,
            bounds.yMax - _edgePadding.y);

        if (_moveBounds.width <= 0f || _moveBounds.height <= 0f)
        {
            throw new InvalidOperationException(
                $"{name}: 경계 여백이 이동 범위보다 큽니다.");
        }

        // 화면 밖에서 죽은 몬스터가 드랍해도 이동 범위 안에서 시작한다.
        Vector2 position = _body.position;
        position.x = Mathf.Clamp(
            position.x, _moveBounds.xMin, _moveBounds.xMax);
        position.y = Mathf.Clamp(
            position.y, _moveBounds.yMin, _moveBounds.yMax);
        _body.position = position;

        // 각도를 추첨하므로 길이가 0인 방향은 나오지 않는다.
        float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        _direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

        _remainingTime = _lifeTime;
    }

    private void FixedUpdate()
    {
        if (_isConsumed)
        {
            return;
        }

        // 게임이 일시정지되면 FixedUpdate도 멈추므로 수명이 보존된다.
        _remainingTime -= Time.fixedDeltaTime;

        if (_remainingTime <= 0f)
        {
            Remove();
            return;
        }

        Vector2 nextPosition =
            _body.position +
            _direction * _moveSpeed * Time.fixedDeltaTime;

        // 각 축을 독립적으로 반사하므로 모서리에서는 두 축이 반사된다.
        nextPosition.x = ReflectAxis(
            nextPosition.x,
            _moveBounds.xMin,
            _moveBounds.xMax,
            ref _direction.x);

        nextPosition.y = ReflectAxis(
            nextPosition.y,
            _moveBounds.yMin,
            _moveBounds.yMax,
            ref _direction.y);

        _body.MovePosition(nextPosition);
    }

    // 경계를 넘은 거리까지 반영하여 벽 안쪽으로 되돌린다.
    private static float ReflectAxis(
        float position,
        float min,
        float max,
        ref float direction)
    {
        // 한 번의 이동으로 여러 경계를 넘은 경우도 처리한다.
        while (position < min || position > max)
        {
            if (position < min)
            {
                position = min + (min - position);
                direction = Mathf.Abs(direction);
            }
            else
            {
                position = max - (position - max);
                direction = -Mathf.Abs(direction);
            }
        }

        return position;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryCollect(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // 접촉 당시 조작 불가였더라도 전투가 재개되면 획득할 수 있다.
        TryCollect(other);
    }

    private void TryCollect(Collider2D other)
    {
        if (_isConsumed)
        {
            return;
        }

        // 현재 캐릭터는 몸 Collider와 UltimateCharge가 루트에 있다.
        // 자식에 있는 무기 공격 판정으로는 건전지를 획득하지 않는다.
        UltimateCharge charge =
            other.GetComponent<UltimateCharge>();

        if (charge == null || !charge.TryCollect())
        {
            return;
        }

        // 최대 충전 상태에서도 아이템 자체는 사라진다.
        Remove();
    }

    private void Remove()
    {
        _isConsumed = true;

        // Destroy는 프레임 끝에 처리되므로 접촉 판정을 먼저 끈다.
        _trigger.enabled = false;
        _body.simulated = false;

        Destroy(gameObject);
    }
}
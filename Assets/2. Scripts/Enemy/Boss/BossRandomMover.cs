using System;
using UnityEngine;

/// <summary>
/// 보스를 시작 위치 기준의 사각형 범위 안에서 계속 랜덤 이동시킨다.
/// </summary>
public sealed class BossRandomMover
{
    readonly Rigidbody2D _rigidbody; // 실제 보스 이동에 사용하는 Rigidbody2D
    readonly float _moveSpeed; // 보스의 초당 이동 거리
    readonly Vector2 _movementHalfRange; // 시작 위치를 기준으로 이동할 수 있는 X, Y 반경
    readonly float _arrivalDistance; // 목표 지점에 도착했다고 판단할 거리

    Vector2 _movementCenter; // 랜덤 이동 범위의 중심 위치
    Vector2 _targetPosition; // 현재 이동하고 있는 목표 위치
    bool _isActive; // 현재 랜덤 이동을 수행할 수 있는지 확인하는 값

    // 보스가 생성되었을 때 저장한 랜덤 이동 범위의 중심 위치다.
    public Vector2 MovementCenter =>
        _movementCenter;

    public BossRandomMover(
        Rigidbody2D rigidbody,
        float moveSpeed,
        Vector2 movementHalfRange,
        float arrivalDistance)
    {
        if (rigidbody == null)
        {
            throw new ArgumentNullException(nameof(rigidbody));
        }

        _rigidbody = rigidbody;
        _moveSpeed = moveSpeed;
        _movementHalfRange = movementHalfRange;
        _arrivalDistance = arrivalDistance;
    }

    /// <summary>
    /// 현재 보스 위치를 이동 범위의 중심으로 설정하고 이동을 시작한다.
    /// </summary>
    public void Start()
    {
        _movementCenter = _rigidbody.position;
        _isActive = true;

        SelectNextTarget();
    }

    /// <summary>
    /// 보스의 랜덤 이동을 일시 정지한다.
    /// 패턴 연출이나 사망 처리 중에 사용할 수 있다.
    /// </summary>
    public void Pause()
    {
        _isActive = false;
        _rigidbody.linearVelocity = Vector2.zero;
    }

    /// <summary>
    /// 정지되어 있던 랜덤 이동을 다시 시작한다.
    /// </summary>
    public void Resume()
    {
        if (_isActive)
        {
            return;
        }

        _isActive = true;
        SelectNextTarget();
    }

    /// <summary>
    /// FixedUpdate에서 호출하여 보스를 목표 위치로 이동시킨다.
    /// </summary>
    public void FixedTick(float fixedDeltaTime)
    {
        if (!_isActive)
        {
            return;
        }

        float arrivalDistanceSqr =
            _arrivalDistance * _arrivalDistance;

        // 목표 지점에 도착하면 즉시 다음 랜덤 위치를 선정한다.
        if ((_targetPosition - _rigidbody.position).sqrMagnitude
            <= arrivalDistanceSqr)
        {
            SelectNextTarget();
        }

        Vector2 nextPosition =
            Vector2.MoveTowards(
                _rigidbody.position,
                _targetPosition,
                _moveSpeed * fixedDeltaTime);

        _rigidbody.MovePosition(nextPosition);
    }

    /// <summary>
    /// 설정된 이동 범위 안에서 새로운 목표 위치를 선정한다.
    /// </summary>
    private void SelectNextTarget()
    {
        float targetX = UnityEngine.Random.Range(
            _movementCenter.x - _movementHalfRange.x,
            _movementCenter.x + _movementHalfRange.x);

        float targetY = UnityEngine.Random.Range(
            _movementCenter.y - _movementHalfRange.y,
            _movementCenter.y + _movementHalfRange.y);

        _targetPosition = new Vector2(targetX, targetY);
    }
}
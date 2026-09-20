using System;
using UnityEngine;

// 손 추적과 Animator 갱신 이후에 던지기 위치와 외형 회전을 적용한다.
[DefaultExecutionOrder(300)]
[RequireComponent(typeof(PlayerController), typeof(CapsuleCollider2D))]
public sealed class PlayerThrownMotion : MonoBehaviour
{
    // 회전시킬 외형이다. 플레이어 루트 대신 자식 Renderer를 연결한다.
    [SerializeField] private Transform _visual;

    // 벽에 닿은 뒤 아주 조금 떨어뜨려 같은 면에 끼는 것을 방지한다.
    private const float Skin = 0.01f;

    // 한 프레임의 연속 충돌 처리 횟수를 제한한다.
    private const int MaxImpactsPerFrame = 4;

    // 플레이어 본체와 실제 몸 크기를 가진 콜라이더다.
    private PlayerController _player;
    private CapsuleCollider2D _body;

    // 이번 던지기를 실행하는 객체와 설정이다.
    private BossAlienGrabController _grab;
    private Boss_Alien _boss;
    private BossAlienGrabArena _arena;
    private BossAlienGrabConfig _config;

    // 비행 속도와 현재 단계의 종료 시각이다.
    private Vector2 _velocity;
    private float _deadline;

    // 한 번의 던지기에서 첫 충돌만 피해와 균열을 발생시킨다.
    private bool _hasImpacted;
    private bool _running;

    // 종료 시 되돌릴 외형 회전과 현재 회전량이다.
    private Quaternion _originalRotation;
    private float _spinAngle;
    private float _spinDirection;

    // Grab 패턴이 던지기 종료 여부를 확인한다.
    public bool IsRunning => _running;

    private void Awake()
    {
        _player = GetComponent<PlayerController>();
        _body = GetComponent<CapsuleCollider2D>();

        if (_visual == null ||
            _visual == transform ||
            !_visual.IsChildOf(transform))
        {
            throw new InvalidOperationException(
                $"{name}: Visual에 자식 Renderer를 연결하세요.");
        }
    }

    private void OnEnable()
    {
        // 사망으로 시간이 멈춰도 즉시 회전과 이동 권한을 정리한다.
        _player.Died += Stop;
    }

    // 던지기 애니메이션 이벤트를 받은 패턴이 호출한다.
    public bool TryBegin(
        BossAlienGrabController grab,
        Boss_Alien boss,
        BossAlienGrabArena arena,
        BossAlienGrabConfig config)
    {
        if (_running || !isActiveAndEnabled ||
            _player.IsDead || !_body.enabled ||
            grab == null || !grab.isActiveAndEnabled ||
            boss == null || !boss.isActiveAndEnabled || boss.IsDead ||
            arena == null || !arena.isActiveAndEnabled ||
            config == null ||
            grab.CapturedPlayer != _player)
        {
            return false;
        }

        ValidateConfig(config);

        // 손 추적을 끄되, 플레이어 이동 권한은 GrabController가 유지한다.
        if (!grab.BeginThrow())
        {
            return false;
        }

        _grab = grab;
        _boss = boss;
        _arena = arena;
        _config = config;

        // 왼쪽 기준 각도를 한 번 선택해 직선 비행을 시작한다.
        float angle = UnityEngine.Random.Range(
            config.ThrowAngleRange.x,
            config.ThrowAngleRange.y) * Mathf.Deg2Rad;

        _velocity = new Vector2(
            -Mathf.Cos(angle), Mathf.Sin(angle)) * config.ThrowSpeed;

        _originalRotation = _visual.localRotation;
        _spinAngle = 0f;
        _spinDirection = 1f;
        _hasImpacted = false;
        _deadline = Time.time + config.MaxFlightDuration;
        _running = true;
        return true;
    }

    private void LateUpdate()
    {
        if (!_running)
        {
            return;
        }

        // 패턴 취소·사망·비활성화도 정상 종료와 같은 방법으로 정리한다.
        if (!CanContinue())
        {
            Stop();
            return;
        }

        if (Time.deltaTime <= 0f)
        {
            return;
        }

        if (Time.time >= _deadline)
        {
            Stop();
            return;
        }

        MoveWithReflection(Time.deltaTime);

        if (!_running || !_hasImpacted)
        {
            return;
        }

        // 첫 벽 충돌 이후부터 외형만 회전시킨다.
        _spinAngle = Mathf.Repeat(
            _spinAngle +
            _config.SpinSpeed * _spinDirection * Time.deltaTime,
            360f);

        _visual.localRotation = _originalRotation *
            Quaternion.Euler(0f, 0f, _spinAngle);
    }

    // 이동할 거리를 검사하고 충돌 이후 남은 시간도 반사 방향으로 이동한다.
    private void MoveWithReflection(float deltaTime)
    {
        float remainingTime = deltaTime;

        for (int i = 0;
             i < MaxImpactsPerFrame && remainingTime > 0.00001f;
             i++)
        {
            float speed = _velocity.magnitude;

            // 반사 배율이 0이면 멈춘 채 회복 시간만 기다린다.
            if (speed <= 0.0001f)
            {
                return;
            }

            Vector2 movement = _velocity * remainingTime;
            Vector2 position = transform.position;

            if (!_arena.TryFindImpact(_body, movement, out RaycastHit2D hit))
            {
                MovePlayer(position + movement);
                return;
            }

            Vector2 direction = _velocity / speed;

            // 충돌 직전까지 이동해 몸이 경계 안으로 파고들지 않게 한다.
            float travel = Mathf.Max(0f, hit.distance - Skin);

            if (!MovePlayer(position + direction * travel))
            {
                return;
            }

            // 접촉까지 사용한 시간을 제외하고 나머지는 반사 이동에 사용한다.
            remainingTime = Mathf.Max(
                0f, remainingTime - hit.distance / speed);

            Vector2 incomingVelocity = _velocity;
            _velocity = Vector2.Reflect(incomingVelocity, hit.normal) *
                _config.ReboundSpeedMultiplier;

            if (!_hasImpacted)
            {
                // 이벤트 처리 전에 표시하여 중복 피해와 균열을 막는다.
                _hasImpacted = true;
                _deadline = Time.time + _config.ReboundDuration;

                // 입사 방향과 충돌 면에 따라 외형 회전 방향을 선택한다.
                float cross = incomingVelocity.x * hit.normal.y -
                    incomingVelocity.y * hit.normal.x;
                _spinDirection = cross < 0f ? -1f : 1f;

                _arena.NotifyFirstImpact(_boss, hit);

                // 이벤트에서 패턴이 취소됐을 수 있으므로 다시 확인한다.
                if (!CanContinue())
                {
                    Stop();
                    return;
                }

                // 이동 권한을 가진 GrabController를 전달해 그랩 피해만 적용한다.
                // 이 코드는 기존 _hasImpacted 검사 안에 있어 첫 충돌에만 실행된다.
                _player.ApplyForcedMovementDamage(_grab, _config.WallImpactDamage);
            }

            // 충돌 피해로 사망했거나 외부에서 중단한 경우 남은 이동을 취소한다.
            if (!CanContinue())
            {
                Stop();
                return;
            }
        }

        // 모서리에서 충돌이 너무 많이 반복되면 이번 프레임 이동만 종료한다.
        // 검사하지 않은 남은 거리를 그대로 이동시키지 않는다.
    }

    // 플레이어의 이동 권한을 가진 GrabController를 통해서만 위치를 변경한다.
    private bool MovePlayer(Vector2 position)
    {
        if (_grab.TryMoveThrownPlayer(position))
        {
            return true;
        }

        Stop();
        return false;
    }

    private bool CanContinue()
    {
        return _running &&
            _player.isActiveAndEnabled && !_player.IsDead &&
            _body.enabled &&
            _boss != null && _boss.isActiveAndEnabled && !_boss.IsDead &&
            _arena != null && _arena.isActiveAndEnabled &&
            _grab != null && _grab.isActiveAndEnabled &&
            _grab.CapturedPlayer == _player;
    }

    // 정상 회복과 강제 중단 모두 원래 외형과 조작 상태로 복구한다.
    public void Stop()
    {
        if (!_running)
        {
            return;
        }

        _running = false;
        _velocity = Vector2.zero;
        _visual.localRotation = _originalRotation;

        BossAlienGrabController grab = _grab;
        _grab = null;

        if (grab != null && grab.CapturedPlayer == _player)
        {
            grab.Release();
        }

        _boss = null;
        _arena = null;
        _config = null;
    }

    private void OnDisable()
    {
        _player.Died -= Stop;
        Stop();
    }

    // 잘못된 설정으로 무한 비행하거나 위치가 손상되는 것을 막는다.
    private static void ValidateConfig(BossAlienGrabConfig config)
    {
        Vector2 angles = config.ThrowAngleRange;

        if (!IsFinite(angles.x) || !IsFinite(angles.y) ||
            angles.x > angles.y ||
            !IsFinite(config.ThrowSpeed) || config.ThrowSpeed <= 0f ||
            !IsFinite(config.ReboundDuration) || config.ReboundDuration <= 0f ||
            !IsFinite(config.MaxFlightDuration) || config.MaxFlightDuration <= 0f ||
            !IsFinite(config.SpinSpeed) || config.SpinSpeed < 0f ||
            !IsFinite(config.ReboundSpeedMultiplier) ||
            config.ReboundSpeedMultiplier < 0f ||
            config.ReboundSpeedMultiplier > 1f ||
            config.WallImpactDamage < 0)
        {
            throw new InvalidOperationException(
                $"{config.name}: 던지기 설정값을 확인하세요.");
        }
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
using System;
using UnityEngine;

// 팔 위치가 갱신된 다음 플레이어를 손에 붙인다.
[DefaultExecutionOrder(200)]
public sealed class BossAlienGrabController : MonoBehaviour
{
    // 손의 잡기 위치와 열린 손·닫힌 손 이미지를 관리한다.
    [SerializeField] private BossAlienArmRig _armRig;

    // 사망하거나 비활성화되면 잡기를 해제할 보스다.
    [SerializeField] private Boss_Alien _boss;

    // 현재 이동 권한을 가진 플레이어다.
    private PlayerController _player;

    // 잡힌 순간 플레이어와 GrabPoint 사이의 거리다.
    private Vector2 _initialOffset;

    // 손 안으로 부드럽게 붙이는 시간과 진행 시간이다.
    private float _attachDuration;
    private float _attachElapsed;

    // 손 추적 중이면 true, 던지는 동안에는 false다.
    private bool _followingHand;

    // 패턴이 잡기 유지 여부와 붙이기 완료 여부를 확인한다.
    public bool HasCapturedPlayer => _player != null;
    public bool IsAttached =>
        HasCapturedPlayer &&
        _followingHand &&
        _attachElapsed >= _attachDuration;

    // 던지기 경로의 시작 위치를 읽을 때 사용한다.
    public PlayerController CapturedPlayer => _player;

    private void Awake()
    {
        // 프리팹의 필수 연결을 확인한다.
        if (_boss == null || _armRig == null ||
            _armRig.GrabPoint == null)
        {
            throw new InvalidOperationException(
                $"{name}: Boss와 Arm Rig를 연결하세요.");
        }
    }

    // 손의 접촉 판정에 성공한 뒤 패턴이 호출한다.
    public bool TryCapture(PlayerController player, float attachDuration)
    {
        if (float.IsNaN(attachDuration) ||
            float.IsInfinity(attachDuration) ||
            attachDuration <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attachDuration), "붙이기 시간은 0보다 커야 합니다.");
        }

        if (!isActiveAndEnabled ||
            !_boss.isActiveAndEnabled ||
            _boss.IsDead ||
            HasCapturedPlayer ||
            player == null)
        {
            return false;
        }

        // 다른 시스템이 이미 잡고 있다면 권한을 빼앗지 않는다.
        if (!player.TryBeginForcedMovement(this))
        {
            return false;
        }

        _player = player;
        _attachDuration = attachDuration;
        _attachElapsed = 0f;
        _followingHand = true;

        _initialOffset = (Vector2)_player.transform.position -
            (Vector2)_armRig.GrabPoint.position;

        // 사망 시 시간이 멈추더라도 즉시 잡기를 정리한다.
        _player.Died += Release;
        _armRig.SetHandClosed(true);
        return true;
    }

    private void LateUpdate()
    {
        if (!HasCapturedPlayer)
        {
            return;
        }

        // 던지는 도중에도 보스와 플레이어 상태를 검사한다.
        if (_boss == null ||
            !_boss.isActiveAndEnabled ||
            _boss.IsDead ||
            !_player.isActiveAndEnabled ||
            _player.IsDead)
        {
            Release();
            return;
        }

        if (!_followingHand || Time.deltaTime <= 0f)
        {
            return;
        }

        _attachElapsed = Mathf.Min(
            _attachElapsed + Time.deltaTime, _attachDuration);

        // 초기 거리만 서서히 줄여 움직이는 손에도 자연스럽게 붙인다.
        float progress = _attachElapsed / _attachDuration;
        float remaining = 1f - Mathf.SmoothStep(0f, 1f, progress);

        Vector2 position = (Vector2)_armRig.GrabPoint.position +
            _initialOffset * remaining;

        // 붙이기가 끝나면 매 프레임 GrabPoint 위치를 그대로 따른다.
        if (!_player.TrySetForcedPosition(this, position))
        {
            Release();
        }
    }

    // 던지기 이벤트에서 호출한다. 조작 잠금은 벽 도착까지 유지한다.
    public bool BeginThrow()
    {
        if (!IsAttached)
        {
            return false;
        }

        _followingHand = false;
        _armRig.SetHandClosed(false);
        return true;
    }

    // 던지기 중 패턴이 계산한 위치를 플레이어에게 전달한다.
    public bool TryMoveThrownPlayer(Vector2 position)
    {
        if (!HasCapturedPlayer || _followingHand)
        {
            return false;
        }

        if (!_player.TrySetForcedPosition(this, position))
        {
            Release();
            return false;
        }

        return true;
    }

    // 정상 종료·패턴 취소·사망에서 공통으로 호출한다.
    public void Release()
    {
        // 이벤트 처리 중 다시 호출돼도 안전하게 참조를 먼저 비운다.
        PlayerController player = _player;
        _player = null;
        _followingHand = false;

        if (player != null)
        {
            player.Died -= Release;
            player.EndForcedMovement(this);
        }

        if (_armRig != null)
        {
            _armRig.SetHandClosed(false);
        }
    }

    private void OnDisable()
    {
        // 컴포넌트가 꺼져도 플레이어 조작 잠금을 남기지 않는다.
        Release();
    }
}
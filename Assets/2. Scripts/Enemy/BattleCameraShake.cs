using UnityEngine;

/// <summary>기존 보스 패턴의 흔들림 요청을 공용 카메라 컨트롤러에 전달한다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CameraController))]
public sealed class BattleCameraShake : MonoBehaviour
{
    // 실제 위치 변경은 CameraController 하나에서 담당한다.
    private CameraController _controller;
    // 비활성화 시 자신이 전달한 보스 요청만 정리한다.
    private BossEnemy _owner;

    /// <summary>같은 카메라의 공용 컨트롤러를 준비한다.</summary>
    private void Awake()
    {
        // RequireComponent로 보장된 컴포넌트를 캐싱한다.
        _controller = GetComponent<CameraController>();
    }

    /// <summary>보스의 흔들림을 시작한다.</summary>
    public void Shake(BossEnemy owner, float strength, float duration)
    {
        // 기존 패턴의 공개 호출 방식을 유지한다.
        if (!isActiveAndEnabled) return;
        _owner = owner;
        _controller.ShakeBoss(owner, strength, duration);
    }

    /// <summary>해당 보스의 흔들림만 정지한다.</summary>
    public void StopShake(BossEnemy owner)
    {
        // 플레이어 피격 채널은 취소하지 않는다.
        if (_controller != null) _controller.StopBossShake(owner);
    }
    /// <summary>전달 컴포넌트가 꺼지면 자신이 맡은 보스 흔들림을 종료한다.</summary>
    private void OnDisable()
    {
        // 카메라 컨트롤러가 먼저 종료되어도 안전하게 정리한다.
        if (_controller != null && _owner != null) _controller.StopBossShake(_owner);
        _owner = null;
    }}

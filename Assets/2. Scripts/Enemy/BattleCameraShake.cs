using UnityEngine;

// 고정 전투 카메라에 일시적인 XY 흔들림을 적용한다.
// 전투 경계나 공격 오브젝트의 위치는 변경하지 않는다.
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(1000)]
public sealed class BattleCameraShake : MonoBehaviour
{
    // 흔들림을 요청한 보스다. 해당 보스만 자신의 흔들림을 취소한다.
    private BossEnemy _owner;

    // 흔들림을 시작하기 전의 카메라 위치다.
    private Vector3 _restPosition;

    // 이번 흔들림의 강도, 지속시간, 경과시간이다.
    private float _strength;
    private float _duration;
    private float _elapsed;

    // 흔들림 진행 여부다.
    private bool _shaking;

    // 연속으로 호출되면 기존 흔들림을 정리하고 새 충격을 적용한다.
    public void Shake(BossEnemy owner, float strength, float duration)
    {
        if (!isActiveAndEnabled ||
            owner == null ||
            !owner.isActiveAndEnabled ||
            owner.IsDead ||
            !Positive(strength) ||
            !Positive(duration))
        {
            return;
        }

        // 흔들린 위치를 다음 기준 위치로 저장하지 않도록 먼저 복구한다.
        Restore();

        _owner = owner;
        _restPosition = transform.localPosition;
        _strength = strength;
        _duration = duration;
        _elapsed = 0f;
        _shaking = true;
    }

    private void LateUpdate()
    {
        if (!_shaking)
        {
            return;
        }

        // 보스가 제거되거나 사망하면 시간이 멈춰도 즉시 복구한다.
        if (_owner == null ||
            !_owner.isActiveAndEnabled ||
            _owner.IsDead)
        {
            Restore();
            return;
        }

        // 일시정지 중에는 카메라를 기준 위치에 두고 시간도 진행하지 않는다.
        if (Time.deltaTime <= 0f)
        {
            transform.localPosition = _restPosition;
            return;
        }

        _elapsed += Time.deltaTime;

        if (_elapsed >= _duration)
        {
            Restore();
            return;
        }

        // 종료 시점에 가까워질수록 강도를 줄인다.
        float remaining = 1f - _elapsed / _duration;
        float amplitude = _strength * remaining * remaining;

        // 서로 다른 주기의 진동을 사용한다.
        // 게임의 패턴 선택에 쓰이는 Random 상태는 변경하지 않는다.
        float x = Mathf.Sin(_elapsed * 137f);
        float y = Mathf.Sin(_elapsed * 173f);

        transform.localPosition = _restPosition +
            new Vector3(x, y, 0f) * amplitude;
    }

    // 패턴의 finally에서 자신의 흔들림만 취소한다.
    public void StopShake(BossEnemy owner)
    {
        if (_shaking && _owner == owner)
        {
            Restore();
        }
    }

    // 정상 종료와 강제 중단의 공통 정리다.
    private void Restore()
    {
        if (!_shaking)
        {
            return;
        }

        transform.localPosition = _restPosition;
        _shaking = false;
        _owner = null;
    }

    private void OnDisable()
    {
        Restore();
    }

    // 0 강도는 흔들림을 끄는 설정으로 취급한다.
    private static bool Positive(float value)
    {
        return !float.IsNaN(value) &&
            !float.IsInfinity(value) &&
            value > 0f;
    }
}
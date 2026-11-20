using UnityEngine;

using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>플레이어 피격과 보스 흔들림 및 비네트를 합성한다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(-1000)]
public sealed class CameraController : MonoBehaviour
{
    // 일반 피격의 최대 이동량과 지속 시간이다.
    [SerializeField, Min(0f)] private float _hitStrength = 0.04f;
    [SerializeField, Min(0f)] private float _hitDuration = 0.12f;
    // 지속 피해 틱 사이의 약한 연출 유지 시간이다.
    [SerializeField, Min(0.01f)] private float _repeatDamageWindow = 0.7f;
    // 지속 피해는 일반 피격보다 약하게 유지하고 마지막에 부드럽게 감쇠한다.
    [SerializeField, Min(0f)] private float _continuousStrength = 0.012f;
    [SerializeField, Min(0.01f)] private float _continuousFadeDuration = 0.12f;
    // 피격 전용 프로필은 런타임에 소유하여 기존 씬의 색보정과 분리한다.
    [SerializeField, Range(0f, 1f)] private float _hitVignette = 0.32f;
    [SerializeField, Range(0f, 1f)] private float _continuousVignette = 0.09f;
    [SerializeField, Min(0.01f)] private float _vignetteDuration = 0.3f;
    private Volume _damageVolume;
    private VolumeProfile _damageProfile;
    private Vignette _vignette;
    private float _vignetteRemaining;
    // 반복 타격 때 위상을 초기화하지 않아 지속 흔들림이 끊기지 않는다.
    private float _continuousRemaining;
    private float _continuousElapsed;
    // 피격과 보스 패턴은 별도 시간으로 진행해 서로 취소하지 않는다.
    private float _hitRemaining;
    private float _hitElapsed;
    private BossEnemy _boss;
    private float _bossStrength;
    private float _bossDuration;
    private float _bossElapsed;
    // 직전 프레임의 오프셋만 제거해 외부 카메라 연출의 위치는 보존한다.
    private Vector3 _offset;
    private Vector3 _appliedPosition;

    /// <summary>피격 전용 비네트 프로필과 카메라 후처리를 준비한다.</summary>
    private void Awake()
    {
        // 카메라가 전용 Volume의 레이어를 읽게 한다.
        var cameraData = GetComponent<Camera>().GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = true;
        cameraData.volumeLayerMask |= 1 << gameObject.layer;
        // 공유 에셋을 수정하지 않는 독립 프로필에 비네트만 추가한다.
        _damageProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        _vignette = _damageProfile.Add<Vignette>(true);
        _vignette.color.Override(new Color(0.35f, 0.015f, 0.025f));
        _vignette.intensity.Override(_hitVignette);
        _vignette.smoothness.Override(0.45f);
        // 카메라와 함께 제거되는 자식 Volume을 사용한다.
        var child = new GameObject("Player Damage Vignette");
        child.layer = gameObject.layer;
        child.transform.SetParent(transform, false);
        _damageVolume = child.AddComponent<Volume>();
        _damageVolume.isGlobal = true;
        _damageVolume.priority = 100f;
        _damageVolume.sharedProfile = _damageProfile;
        _damageVolume.weight = 0f;
    }

    /// <summary>피해 경로가 명시한 종류에 따라 일반 또는 약한 지속 피격 연출을 시작한다.</summary>
    public void CameraShake(float damage, bool continuous = false)
    {
        // 무효한 피해는 비교 기록과 화면 반응을 변경하지 않는다.
        if (!isActiveAndEnabled || !Positive(damage)) return;
        if (continuous)
        {
            // 첫 틱부터 약하게 재생하며 일반 피격 채널은 취소하지 않는다.
            if (_continuousRemaining <= 0f) _continuousElapsed = 0f;
            _continuousRemaining = _repeatDamageWindow + _continuousFadeDuration;
        }
        else
        {
            // 일반 피격은 짧은 흔들림과 비네트를 새로 시작한다.
            _hitElapsed = 0f;
            _hitRemaining = Positive(_hitDuration) ? _hitDuration : 0f;
            _vignetteRemaining = _vignetteDuration;
        }
    }

    /// <summary>보스가 소유한 흔들림을 시작하고 이전 보스 요청을 교체한다.</summary>
    public void ShakeBoss(BossEnemy owner, float strength, float duration)
    {
        // 죽거나 비활성화된 보스는 새로운 연출을 요청할 수 없다.
        if (!isActiveAndEnabled || owner == null || !owner.isActiveAndEnabled ||
            owner.IsDead || !Positive(strength) || !Positive(duration)) return;
        _boss = owner;
        _bossStrength = strength;
        _bossDuration = duration;
        _bossElapsed = 0f;
    }

    /// <summary>해당 보스의 흔들림만 취소하고 플레이어 피격은 유지한다.</summary>
    public void StopBossShake(BossEnemy owner)
    {
        // 다른 보스가 시작한 연출은 중단하지 않는다.
        if (_boss == owner) _boss = null;
    }

    /// <summary>다른 카메라 연출이 실행되기 전에 이전 오프셋을 제거한다.</summary>
    private void Update()
    {
        // DOTween 등의 절대 위치 연출에 누적 오프셋이 섞이지 않게 한다.
        RemoveOffset();
    }

    /// <summary>각 흔들림의 시간을 갱신하고 XY 오프셋을 한 번만 적용한다.</summary>
    private void LateUpdate()
    {
        // 일시정지 중에는 일반 피격과 전투 흔들림의 시간도 정지한다.
        if (Time.deltaTime <= 0f) return;
        Vector3 offset = Vector3.zero;
        // 비네트도 게임 시간으로 감쇠한다.
        _vignetteRemaining = Mathf.Max(0f, _vignetteRemaining - Time.deltaTime);
        float hitFade = Mathf.Clamp01(_vignetteRemaining / Mathf.Max(0.01f, _vignetteDuration));
        if (_hitRemaining > 0f)
        {
            _hitElapsed += Time.deltaTime;
            _hitRemaining = Mathf.Max(0f, _hitDuration - _hitElapsed);
            offset += EvaluateOffset(_hitElapsed, _hitDuration, _hitStrength);
        }
        if (_continuousRemaining > 0f)
        {
            // 다음 피해가 들어오면 유지 시간이 갱신되며 사인파의 위상은 이어진다.
            _continuousElapsed += Time.deltaTime;
            _continuousRemaining = Mathf.Max(0f, _continuousRemaining - Time.deltaTime);
            float fade = Mathf.Clamp01(_continuousRemaining / Mathf.Max(0.01f, _continuousFadeDuration));
            float amplitude = Mathf.Max(0f, _continuousStrength) * fade;
            offset += new Vector3(Mathf.Sin(_continuousElapsed * 137f),
                Mathf.Sin(_continuousElapsed * 173f), 0f) * amplitude;
        }
        // 패턴 종료, 사망, 비활성화 시 보스 채널만 종료한다.
        if (_boss != null && _boss.isActiveAndEnabled && !_boss.IsDead)
        {
            _bossElapsed += Time.deltaTime;
            offset += EvaluateOffset(_bossElapsed, _bossDuration, _bossStrength);
            if (_bossElapsed >= _bossDuration) _boss = null;
        }
        else _boss = null;
        // 가중치가 0이 되면 기존 씬의 비네트 설정으로 돌아간다.
        float continuousFade = Mathf.Clamp01(_continuousRemaining / Mathf.Max(0.01f, _continuousFadeDuration));
        float intensity = Mathf.Max(_hitVignette * hitFade, _continuousVignette * continuousFade);
        _vignette.intensity.value = Mathf.Max(_hitVignette, _continuousVignette);
        _damageVolume.weight = intensity / Mathf.Max(0.001f, _vignette.intensity.value);
        // Z 깊이는 유지하며 두 채널의 흔들림을 합산한다.
        _offset = offset;
        transform.localPosition += offset;
        _appliedPosition = transform.localPosition;
    }

    /// <summary>시간이 지날수록 부드럽게 감소하는 XY 흔들림을 계산한다.</summary>
    private static Vector3 EvaluateOffset(float elapsed, float duration, float strength)
    {
        // 게임의 전역 난수 상태를 바꾸지 않는 사인파를 사용한다.
        float remaining = Mathf.Clamp01(1f - elapsed / duration);
        float amplitude = strength * remaining * remaining;
        return new Vector3(Mathf.Sin(elapsed * 137f), Mathf.Sin(elapsed * 173f), 0f) * amplitude;
    }

    /// <summary>자신이 적용한 이동량만 제거하고 외부의 위치 교체는 보존한다.</summary>
    private void RemoveOffset()
    {
        // 외부 코드가 위치를 덮어썼다면 오래된 오프셋을 빼지 않는다.
        if (transform.localPosition == _appliedPosition) transform.localPosition -= _offset;
        _offset = Vector3.zero;
    }

    /// <summary>비활성화 시 흔들림과 잔여 이동량을 정리한다.</summary>
    private void OnDisable()
    {
        // 씬 종료와 컴포넌트 비활성화 모두 원래 위치로 복구한다.
        RemoveOffset();
        _hitRemaining = 0f;
        _continuousRemaining = 0f;
        _vignetteRemaining = 0f;
        if (_damageVolume != null) _damageVolume.weight = 0f;
        _boss = null;
    }

    /// <summary>카메라가 소유한 런타임 프로필과 내부 효과를 해제한다.</summary>
    private void OnDestroy()
    {
        // 생성한 ScriptableObject만 제거하고 프로젝트 에셋은 유지한다.
        if (_damageProfile == null) return;
        foreach (var component in _damageProfile.components) Destroy(component);
        Destroy(_damageProfile);
    }

    /// <summary>유한한 양수 설정만 허용한다.</summary>
    private static bool Positive(float value)
    {
        // NaN과 무한대가 카메라 좌표에 전파되지 않도록 막는다.
        return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }
}

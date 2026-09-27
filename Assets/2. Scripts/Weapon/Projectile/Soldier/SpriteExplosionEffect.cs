using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 미사일 폭발 중심부와 충격파를 재생한 뒤
/// 폭발 이펙트 오브젝트를 제거한다.
/// </summary>
public class SpriteExplosionEffect : MonoBehaviour
{
    [Header("폭발 중심부")]

    // 폭발 중심부의 크기를 조절할 Transform이다.
    [SerializeField] Transform _explosionCoreTransform;

    // 폭발 중심부의 투명도를 조절할 SpriteRenderer다.
    [SerializeField] SpriteRenderer _explosionCoreRenderer;

    [Header("충격파")]

    // 폭발 범위를 보여주는 충격파의 크기를 조절할 Transform이다.
    [SerializeField] Transform _shockwaveTransform;

    // 충격파의 투명도를 조절할 SpriteRenderer다.
    [SerializeField] SpriteRenderer _shockwaveRenderer;

    [Header("재생 시간")]

    // 폭발 중심부가 최종 크기까지 커지는 시간이다.
    [Min(0.01f)]
    [SerializeField] float _coreExpansionDuration = 0.14f;

    // 충격파가 실제 대미지 범위까지 확장되는 시간이다.
    [Min(0.01f)]
    [SerializeField] float _shockwaveExpansionDuration = 0.18f;

    // 폭발이 유지되는 시간이다.
    [Min(0f)]
    [SerializeField] float _holdDuration = 0.05f;

    // 폭발 중심부가 사라지는 시간이다.
    [Min(0.01f)]
    [SerializeField] float _coreFadeDuration = 0.18f;

    // 충격파가 사라지는 시간이다.
    [Min(0.01f)]
    [SerializeField] float _shockwaveFadeDuration = 0.14f;

    // 현재 재생 중인 폭발 연출이다.
    Sequence _effectSequence;


    // 프레임 자체의 확대 동작을 사용하며 기존 Tween 확대와 중복하지 않는다.
    [SerializeField] bool _useFrameAnimation;
    [SerializeField] Sprite[] _coreFrames;
    [SerializeField] Sprite[] _shockwaveFrames;
    [SerializeField] Sprite[] _smokeFrames;
    [SerializeField] SpriteRenderer _smokeRenderer;
    // 폭발과 충격파는 24fps, 연기는 18fps로 재생한다.
    [SerializeField, Min(1f)] float _animationFps = 24f;
    [SerializeField, Min(1f)] float _smokeFps = 18f;
    [SerializeField, Min(0f)] float _smokeDelay = 0.22f;
    Coroutine _frameRoutine;

    private void Awake()
    {
        ValidateReferences();

        // 연출이 시작되기 전에는 두 이펙트를 숨긴다.
        SetRendererAlpha(
            _explosionCoreRenderer,
            0f);

        SetRendererAlpha(
            _shockwaveRenderer,
            0f);
    }

    /// <summary>
    /// 폭발 중심부와 충격파를 동시에 재생한다.
    /// </summary>
    public void Play()
    {
        if (_useFrameAnimation)
        {
            if (_frameRoutine != null) StopCoroutine(_frameRoutine);
            if (_effectSequence != null) _effectSequence.Kill();
            _frameRoutine = StartCoroutine(PlayFrames());
            return;
        }

        // 중복 재생 시 기존 연출을 정리한다.
        _effectSequence?.Kill();

        // 폭발 중심부는 작은 크기에서 시작한다.
        _explosionCoreTransform.localScale =
            Vector3.one * 0.15f;

        // 충격파도 중심에서 작은 크기로 시작한다.
        _shockwaveTransform.localScale =
            Vector3.one * 0.05f;

        // 연출 시작 시 두 이미지를 표시한다.
        SetRendererAlpha(
            _explosionCoreRenderer,
            1f);

        SetRendererAlpha(
            _shockwaveRenderer,
            1f);

        _effectSequence = DOTween.Sequence();

        // 중심 폭발이 빠르게 확장된다.
        _effectSequence.Append(
            _explosionCoreTransform
                .DOScale(
                    Vector3.one,
                    _coreExpansionDuration)
                .SetEase(Ease.OutCubic));

        // 충격파도 실제 대미지 범위까지 동시에 확장된다.
        _effectSequence.Join(
            _shockwaveTransform
                .DOScale(
                    Vector3.one,
                    _shockwaveExpansionDuration)
                .SetEase(Ease.OutCubic));

        // 최대 크기를 잠시 보여준다.
        _effectSequence.AppendInterval(
            _holdDuration);

        // 폭발 중심부를 서서히 숨긴다.
        _effectSequence.Append(
            _explosionCoreRenderer
                .DOFade(
                    0f,
                    _coreFadeDuration)
                .SetEase(Ease.InQuad));

        // 충격파도 중심부와 함께 사라지게 한다.
        _effectSequence.Join(
            _shockwaveRenderer
                .DOFade(
                    0f,
                    _shockwaveFadeDuration)
                .SetEase(Ease.InQuad));

        _effectSequence.OnComplete(() =>
        {
            _effectSequence = null;

            // 재생이 끝난 독립 이펙트 오브젝트를 제거한다.
            Destroy(gameObject);
        });
    }

    /// <summary>
    /// SpriteRenderer의 RGB를 유지하면서 알파값만 변경한다.
    /// </summary>
    private void SetRendererAlpha(
        SpriteRenderer targetRenderer,
        float alpha)
    {
        Color rendererColor =
            targetRenderer.color;

        rendererColor.a = alpha;
        targetRenderer.color = rendererColor;
    }

    private void ValidateReferences()
    {
        if (_explosionCoreTransform == null ||
            _explosionCoreRenderer == null)
        {
            throw new InvalidOperationException(
                $"{name}에 폭발 중심부가 연결되지 않았습니다.");
        }

        if (_shockwaveTransform == null ||
            _shockwaveRenderer == null)
        {
            throw new InvalidOperationException(
                $"{name}에 충격파가 연결되지 않았습니다.");
        }
    }


    // 고정된 크기로 순서대로 표시하여 실제 피해 범위보다 커지지 않게 한다.
    private System.Collections.IEnumerator PlayFrames()
    {
        ValidateFrames(_coreFrames);
        ValidateFrames(_shockwaveFrames);
        ValidateFrames(_smokeFrames);
        if (_smokeRenderer == null || _animationFps <= 0f || _smokeFps <= 0f)
            throw new InvalidOperationException("Explosion frame settings are incomplete.");
        _explosionCoreTransform.localScale = Vector3.one;
        _shockwaveTransform.localScale = Vector3.one;
        float duration = Mathf.Max(_coreFrames.Length / _animationFps,
            Mathf.Max(_shockwaveFrames.Length / _animationFps, _smokeDelay + _smokeFrames.Length / _smokeFps));
        float elapsed = 0f;
        while (elapsed < duration)
        {
            ShowFrame(_explosionCoreRenderer, _coreFrames, elapsed, _animationFps);
            ShowFrame(_shockwaveRenderer, _shockwaveFrames, elapsed, _animationFps);
            ShowFrame(_smokeRenderer, _smokeFrames, elapsed - _smokeDelay, _smokeFps);
            yield return null;
            elapsed += Time.deltaTime;
        }
        _frameRoutine = null;
        Destroy(gameObject);
    }

    // 시작 전과 마지막 프레임 이후에는 해당 레이어를 숨긴다.
    private void ShowFrame(SpriteRenderer renderer, Sprite[] frames, float elapsed, float fps)
    {
        int index = Mathf.FloorToInt(elapsed * fps);
        bool visible = index >= 0 && index < frames.Length;
        SetRendererAlpha(renderer, visible ? 1f : 0f);
        if (visible) renderer.sprite = frames[index];
    }

    private void ValidateFrames(Sprite[] frames)
    {
        if (frames == null || frames.Length == 0)
            throw new InvalidOperationException("Explosion frames are empty.");
        foreach (Sprite frame in frames)
            if (frame == null) throw new InvalidOperationException("Explosion sprite is missing.");
    }

    private void OnDestroy()
    {
        // 다른 원인으로 제거될 때 실행 중인 Tween을 정리한다.
        _effectSequence?.Kill();
        _effectSequence = null;
    }
}
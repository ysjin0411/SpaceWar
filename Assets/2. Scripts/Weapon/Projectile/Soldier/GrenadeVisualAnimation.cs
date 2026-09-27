using System;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class GrenadeVisualAnimation : MonoBehaviour
{
    // 회전이 그려진 프레임이므로 수류탄의 물리 회전 속도는 0으로 사용한다.
    [SerializeField] Sprite[] _tumbleFrames;
    [SerializeField] Sprite[] _armedFrames;
    [SerializeField, Min(1f)] float _tumbleFps = 18f;
    [SerializeField, Min(1f)] float _armedFps = 12f;
    SpriteRenderer _renderer;
    Grenade _grenade;
    float _elapsed;

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        _grenade = GetComponentInParent<Grenade>();
        if (_grenade == null || _tumbleFrames == null || _tumbleFrames.Length == 0 ||
            _armedFrames == null || _armedFrames.Length == 0)
            throw new InvalidOperationException("Grenade animation references are missing.");
    }

    private void OnEnable() { _elapsed = 0f; }

    private void Update()
    {
        // 신관 시간을 변경해도 마지막 1/3 구간(최대 0.34초)에 점멸한다.
        float armedStart = _grenade.FuseTime - Mathf.Min(0.34f, _grenade.FuseTime / 3f);
        bool armed = _elapsed >= armedStart;
        Sprite[] frames = armed ? _armedFrames : _tumbleFrames;
        float time = armed ? _elapsed - armedStart : _elapsed;
        float fps = armed ? _armedFps : _tumbleFps;
        _renderer.sprite = frames[Mathf.FloorToInt(time * fps) % frames.Length];
        _elapsed += Time.deltaTime;
    }
}

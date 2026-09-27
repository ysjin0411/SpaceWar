using System;
using UnityEngine;

// 착지 충격파의 확대와 페이드아웃만 담당한다.
// 충돌 검사나 피해 처리는 하지 않는다.
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class BossAlienLeapImpact : MonoBehaviour
{
    // 지면 균열 이미지 12장을 001부터 012까지 순서대로 등록한다.
    [SerializeField]
    private Sprite[] _frames = Array.Empty<Sprite>();

    // 초당 표시할 이미지 수다. 12장을 24 FPS로 재생하면 0.5초다.
    [SerializeField, Min(1f)]
    private float _framesPerSecond = 24f;

    // 이미지 수와 FPS로 계산하는 전체 재생 시간이다.
    private float _duration;

    // 이미지에 적용할 색상과 초기 투명도다.
    [SerializeField]
    private Color _color = Color.white;

    // 재생에 필요한 참조와 실행 상태다.
    private SpriteRenderer _visual;
    private Vector3 _targetScale;
    private float _elapsed;
    private bool _playing;

    private void Awake()
    {
        // 위치와 크기 설정 전에는 표시하지 않는다.
        _visual = GetComponent<SpriteRenderer>();
        _visual.drawMode = SpriteDrawMode.Simple;
        _visual.enabled = false;
    }

    // 착지 중심과 공격 반경을 받아 시각 효과를 재생한다.
    public void Play(Vector2 center, float radius)
    {
        // 부모의 크기와 회전이 이펙트에 영향을 주지 않게 한다.
        if (transform.parent != null)
        {
            throw new InvalidOperationException(
                $"{name}: 착지 이펙트는 부모 없이 생성하세요.");
        }
        // 재생할 이미지 목록과 초당 프레임 수가 올바른지 확인한다.
        if (_frames == null || _frames.Length == 0 ||
            !IsFinite(_framesPerSecond) || _framesPerSecond <= 0f)
        {
            throw new InvalidOperationException(
                $"{name}: Frames와 Frames Per Second를 확인하세요.");
        }

        // 등록된 이미지 중 비어 있는 항목이 있으면 알려준다.
        for (int i = 0; i < _frames.Length; i++)
        {
            if (_frames[i] == null)
            {
                throw new InvalidOperationException(
                    $"{name}: Frames의 Element {i}가 비어 있습니다.");
            }
        }

        // 기존 이미지 대신 균열의 첫 번째 이미지로 재생을 시작한다.
        _visual.sprite = _frames[0];

        // 12장 ÷ 24 FPS = 0.5초 동안 재생한다.
        _duration = _frames.Length / _framesPerSecond;

        if (_visual.sprite == null ||
            !IsFinite(radius) || radius <= 0f ||
            !IsFinite(center.x) || !IsFinite(center.y) ||
            !IsFinite(_duration) || _duration <= 0f)
        {
            throw new InvalidOperationException(
                $"{name}: 스프라이트, 좌표, 반경, 재생 시간을 확인하세요.");
        }

        // 스프라이트 전체 크기를 기준으로 목표 지름을 계산한다.
        Vector2 sourceSize = _visual.sprite.bounds.size;

        if (sourceSize.x <= 0f || sourceSize.y <= 0f)
        {
            throw new InvalidOperationException(
                $"{name}: 스프라이트 크기가 올바르지 않습니다.");
        }

        float diameter = radius * 2f;
        _targetScale = new Vector3(
            diameter / sourceSize.x,
            diameter / sourceSize.y,
            1f);

        transform.SetPositionAndRotation(
            new Vector3(center.x, center.y, 0f),
            Quaternion.identity);

        _elapsed = 0f;
        _playing = true;

        // 생성된 프레임부터 초기 크기와 색상으로 보여준다.
        ApplyProgress(0f);
        _visual.enabled = true;
    }

    private void Update()
    {
        if (!_playing)
            return;

        // 게임 시간에 맞춰 재생하므로 일시정지 중에는 진행하지 않는다.
        _elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(_elapsed / _duration);

        ApplyProgress(progress);

        // 마지막 프레임에서 투명해진 뒤 오브젝트를 제거한다.
        if (progress >= 1f)
        {
            _playing = false;
            _visual.enabled = false;
            Destroy(gameObject);
        }
    }

    // 재생 진행률에 맞춰 균열 이미지를 순서대로 표시한다.
    private void ApplyProgress(float progress)
    {
        // 진행률 0~1을 이미지 번호 0~11로 변환한다.
        // 종료 지점에서도 배열 범위를 넘지 않도록 마지막 번호로 제한한다.
        int frameIndex = Mathf.Clamp(
            Mathf.FloorToInt(progress * _frames.Length),
            0,
            _frames.Length - 1);

        _visual.sprite = _frames[frameIndex];

        // 확대와 소멸은 이미지에 포함되어 있으므로 추가 변형하지 않는다.
        transform.localScale = _targetScale;
        _visual.color = _color;
    }

    // NaN과 무한대가 위치·크기 계산에 들어가지 않게 한다.
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
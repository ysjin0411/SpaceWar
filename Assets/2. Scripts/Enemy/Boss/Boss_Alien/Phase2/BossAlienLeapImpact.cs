using System;
using UnityEngine;

// 착지 충격파의 확대와 페이드아웃만 담당한다.
// 충돌 검사나 피해 처리는 하지 않는다.
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class BossAlienLeapImpact : MonoBehaviour
{
    // 충격파가 나타났다가 사라지는 전체 시간이다.
    [SerializeField, Min(0.01f)]
    private float _duration = 0.35f;

    // 최초 표시 크기는 최종 크기의 일정 비율로 시작한다.
    [SerializeField, Range(0.1f, 1f)]
    private float _startScaleRatio = 0.7f;

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

    // 초반에는 빠르게 퍼지고, 남은 시간 동안 서서히 사라진다.
    private void ApplyProgress(float progress)
    {
        float expansion = 1f - (1f - progress) * (1f - progress);
        float scaleRatio = Mathf.Lerp(
            _startScaleRatio, 1f, expansion);

        transform.localScale = new Vector3(
            _targetScale.x * scaleRatio,
            _targetScale.y * scaleRatio,
            1f);

        Color color = _color;
        color.a *= 1f - progress;
        _visual.color = color;
    }

    // NaN과 무한대가 위치·크기 계산에 들어가지 않게 한다.
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
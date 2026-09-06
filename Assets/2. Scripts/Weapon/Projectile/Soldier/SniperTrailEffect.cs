using System;
using UnityEngine;

/// <summary>
/// 생성된 위치에서 Trail 스프라이트를 순서대로 재생한다.
/// 마지막에는 투명해지며 제거된다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SniperTrailEffect : MonoBehaviour
{
    // 시간 순서대로 재생할 Trail 스프라이트들이다.
    // Inspector에서 1번부터 4번까지 차례대로 연결한다.
    [SerializeField] Sprite[] _frames = new Sprite[4];

    // 모든 프레임을 한 번 재생하는 총시간이다.
    [Min(0.01f)]
    [SerializeField] float _duration = 0.2f;

    // 전체 재생 시간 중 투명해지기 시작하는 지점이다.
    // 0.5이면 재생 시간의 절반부터 서서히 사라진다.
    [Range(0f, 0.99f)]
    [SerializeField] float _fadeStartRatio = 0.5f;

    // 스프라이트와 투명도를 변경할 렌더러다.
    SpriteRenderer _spriteRenderer;

    // Inspector에서 설정한 원래 색상이다.
    Color _baseColor;

    // 현재 잔상이 생성된 뒤 흐른 시간이다.
    float _elapsedTime;

    // 같은 스프라이트를 매 프레임 다시 넣지 않도록 기억한다.
    int _currentFrameIndex;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _baseColor = _spriteRenderer.color;

        // 빠진 이미지가 있으면 개발 중 바로 확인한다.
        if (_frames == null || _frames.Length == 0)
        {
            throw new InvalidOperationException(
                $"{name}의 Trail Frames가 비어 있습니다.");
        }

        for (int index = 0; index < _frames.Length; index++)
        {
            if (_frames[index] == null)
            {
                throw new InvalidOperationException(
                    $"{name}의 Trail {index + 1}번 스프라이트가 없습니다.");
            }
        }
    }

    private void OnEnable()
    {
        // 잔상마다 활성화된 순간부터 독립적으로 재생한다.
        _elapsedTime = 0f;
        _currentFrameIndex = 0;

        _spriteRenderer.sprite = _frames[0];
        _spriteRenderer.color = _baseColor;
    }

    private void Update()
    {
        // 게임이 일시정지되면 잔상 재생도 멈춘다.
        _elapsedTime += Time.deltaTime;

        if (_elapsedTime >= _duration)
        {
            // 제거 예약 이후에도 마지막 이미지가 보이지 않게 숨긴다.
            Color invisibleColor = _baseColor;
            invisibleColor.a = 0f;
            _spriteRenderer.color = invisibleColor;

            Destroy(gameObject);
            return;
        }

        float progress = _elapsedTime / _duration;

        // 전체 시간을 프레임 개수로 나누어 순서대로 재생한다.
        int frameIndex = Mathf.Min(
            Mathf.FloorToInt(progress * _frames.Length),
            _frames.Length - 1);

        if (frameIndex != _currentFrameIndex)
        {
            _currentFrameIndex = frameIndex;
            _spriteRenderer.sprite = _frames[frameIndex];
        }

        // 앞부분은 원래 밝기를 유지하고 후반부에만 흐려진다.
        float fadeProgress = Mathf.InverseLerp(
            _fadeStartRatio,
            1f,
            progress);

        Color color = _baseColor;
        color.a *= 1f - fadeProgress;
        _spriteRenderer.color = color;
    }
}
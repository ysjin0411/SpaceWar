using System;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SpriteOneShotEffect : MonoBehaviour
{
    // 처음부터 끝까지 재생할 스프라이트를 순서대로 연결한다.
    [SerializeField] private Sprite[] _frames;

    // 1초 동안 재생할 스프라이트 개수다.
    [SerializeField, Min(1f)] private float _framesPerSecond = 20f;

    // 현재 프레임을 표시할 렌더러다.
    private SpriteRenderer _spriteRenderer;

    // 재생 시작 후 흐른 시간이다.
    private float _elapsed;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();

        // 잘못된 프리팹 설정을 확인한다.
        if (_frames == null || _frames.Length == 0 ||
            _framesPerSecond <= 0f)
        {
            throw new InvalidOperationException(
                $"{name}: Frames와 Frames Per Second를 설정하세요.");
        }

        for (int i = 0; i < _frames.Length; i++)
        {
            if (_frames[i] == null)
            {
                throw new InvalidOperationException(
                    $"{name}: Frames의 {i}번 스프라이트가 비어 있습니다.");
            }
        }
    }

    private void OnEnable()
    {
        // 생성되거나 다시 활성화되면 첫 프레임부터 재생한다.
        _elapsed = 0f;
        _spriteRenderer.sprite = _frames[0];
    }

    private void Update()
    {
        // 게임이 일시정지되면 이펙트 재생도 멈춘다.
        _elapsed += Time.deltaTime;

        int frameIndex = Mathf.FloorToInt(
            _elapsed * _framesPerSecond);

        // 마지막 프레임의 표시 시간까지 끝나면 제거한다.
        if (frameIndex >= _frames.Length)
        {
            Destroy(gameObject);
            return;
        }

        _spriteRenderer.sprite = _frames[frameIndex];
    }
}
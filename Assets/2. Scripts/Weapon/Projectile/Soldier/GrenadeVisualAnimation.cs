using System;
using UnityEngine;

/// <summary>직선 비행하는 수류탄의 회전 프레임만 반복한다.</summary>
[RequireComponent(typeof(SpriteRenderer))]
public class GrenadeVisualAnimation : MonoBehaviour
{
    // 기존 프리팹에 연결된 비행 프레임과 재생 속도.
    [SerializeField] private Sprite[] _tumbleFrames;
    [SerializeField, Min(1f)] private float _tumbleFps = 18f;
    // 표시 대상과 이번 발사의 경과 시간.
    private SpriteRenderer _renderer;
    private float _elapsed;

    /// <summary>Renderer와 비행 프레임 설정을 확인한다.</summary>
    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        if (_tumbleFrames == null || _tumbleFrames.Length == 0 || _tumbleFps <= 0f)
            throw new InvalidOperationException($"{name}: Tumble Frames와 FPS를 확인해주세요.");
        // 빈 프레임으로 깜빡이지 않도록 모든 참조를 검사한다.
        foreach (Sprite frame in _tumbleFrames)
            if (frame == null) throw new InvalidOperationException($"{name}: 비행 프레임이 비어 있습니다.");
    }

    /// <summary>풀에서 다시 꺼낼 때 첫 프레임부터 시작한다.</summary>
    private void OnEnable()
    {
        _elapsed = 0f;
        _renderer.sprite = _tumbleFrames[0];
    }

    /// <summary>시간 폭발 예고 없이 비행 애니메이션을 반복한다.</summary>
    private void Update()
    {
        // 일시정지 중에는 게임 시간과 함께 재생도 멈춘다.
        _elapsed += Time.deltaTime;
        _renderer.sprite = _tumbleFrames[Mathf.FloorToInt(_elapsed * _tumbleFps) % _tumbleFrames.Length];
    }
}

using UnityEngine;

/// <summary>실드에 막힌 탄환의 접촉 섬광을 확대하고 흐리게 만든 뒤 정리한다.</summary>
[RequireComponent(typeof(SpriteRenderer))]
public sealed class ShieldProjectileImpact : MonoBehaviour
{
    // 섬광이 나타난 뒤 완전히 사라지기까지의 시간.
    [SerializeField, Min(0.01f)] private float _duration = 0.22f;
    // 처음에는 작게 시작해 빠르게 퍼지는 접촉감을 만든다.
    [SerializeField, Range(0.1f, 1f)] private float _initialScale = 0.55f;
    // 프리팹에서 지정한 최종 크기와 색상을 보관한다.
    private Vector3 _baseScale;
    private Color _baseColor;
    private SpriteRenderer _renderer;
    // 일시정지와 함께 멈추는 게임 시간 기준의 경과 시간.
    private float _elapsed;

    /// <summary>렌더러와 프리팹의 표시 기준을 한 번 저장한다.</summary>
    private void Awake()
    {
        // 다른 탄환의 효과와 상태를 공유하지 않는다.
        _renderer = GetComponent<SpriteRenderer>();
        _baseScale = transform.localScale;
        _baseColor = _renderer.color;
    }

    /// <summary>생성된 접촉 섬광을 작은 크기와 원래 밝기로 시작한다.</summary>
    private void OnEnable()
    {
        // 시작 프레임부터 섬광이 보이도록 표시값을 초기화한다.
        _elapsed = 0f;
        transform.localScale = _baseScale * _initialScale;
        _renderer.color = _baseColor;
    }

    /// <summary>빠르게 확장한 섬광을 서서히 지우고 수명이 끝나면 제거한다.</summary>
    private void Update()
    {
        // 게임이 멈추면 효과도 같은 시점에 멈춘다.
        _elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(_elapsed / Mathf.Max(0.01f, _duration));

        // 초반에 빠르게 퍼지고 마지막에는 크기 변화가 잦아든다.
        float expansion = 1f - (1f - progress) * (1f - progress);
        transform.localScale = _baseScale * Mathf.Lerp(_initialScale, 1f, expansion);

        // 첫 20% 구간은 선명하게 유지해 빠른 충돌도 인지할 수 있게 한다.
        Color color = _baseColor;
        color.a *= 1f - Mathf.InverseLerp(0.2f, 1f, progress);
        _renderer.color = color;

        // 탄환 회수 여부와 무관하게 자신의 연출이 끝난 뒤 정리한다.
        if (progress >= 1f)
            Destroy(gameObject);
    }
}

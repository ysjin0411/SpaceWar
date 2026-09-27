using System;
using UnityEngine;

// Leap 경고의 원형 크기 계산과 이미지 재생을 바탕으로 만든 Trace 전용 경고다.
// 생성한 위치에 Aim 표시만 유지하며, 공격 시점과 제거 요청은 패턴이 관리한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class BossAlienTraceWarning : MonoBehaviour
{
    // 경고가 유지되는 동안 순서대로 반복 재생할 Aim 이미지다.
    [SerializeField]
    private Sprite[] _aimingFrames = Array.Empty<Sprite>();

    // 경고 이미지의 초당 재생 프레임 수다.
    [SerializeField, Min(1f)]
    private float _framesPerSecond = 24f;

    // Aim 경고에 적용할 색상과 투명도다.
    [SerializeField]
    private Color _aimingColor = new Color(0.8f, 0.2f, 1f, 0.25f);

    // 경고를 생성한 보스와 원형 이미지를 표시하는 렌더러다.
    private Boss_AlienBody _owner;
    private SpriteRenderer _visual;

    // 초기화 이후에만 이미지 갱신과 전투 종료 처리를 실행한다.
    private bool _initialized;

    // Aim 이미지의 현재 재생 위치를 계산할 누적 게임 시간이다.
    private float _animationElapsed;

    private void Awake()
    {
        // 위치와 범위가 설정되기 전에는 경고를 표시하지 않는다.
        _visual = GetComponent<SpriteRenderer>();
        _visual.enabled = false;
        _visual.drawMode = SpriteDrawMode.Simple;
    }

    // 기록된 위치에 경고를 한 번 배치한다.
    // 이후 위치를 갱신하거나 Locked 표시로 전환하는 단계는 없다.
    public void Initialize(Boss_AlienBody owner, Vector2 center, float radius)
    {
        // 부모 Scale 때문에 표시 범위가 피해 반경과 달라지는 것을 막는다.
        if (transform.parent != null)
        {
            throw new InvalidOperationException(
                $"{name}: Trace 경고는 부모 없이 생성하세요.");
        }

        if (owner == null ||
            !IsFinite(center.x) || !IsFinite(center.y) ||
            !IsFinite(radius) || radius <= 0f)
        {
            throw new ArgumentException(
                "Trace 경고의 보스, 중심 좌표, 반경을 확인하세요.");
        }

        // Trace는 Aim 이미지만 필요하며 Locked 이미지 목록은 사용하지 않는다.
        if (_aimingFrames == null || _aimingFrames.Length == 0 ||
            !IsFinite(_framesPerSecond) || _framesPerSecond <= 0f)
        {
            throw new InvalidOperationException(
                $"{name}: Aiming Frames와 Frames Per Second를 확인하세요.");
        }

        // 재생 도중 비어 있는 프레임이 나오지 않도록 미리 검사한다.
        foreach (Sprite frame in _aimingFrames)
        {
            if (frame == null)
            {
                throw new InvalidOperationException(
                    $"{name}: Aim 이미지 목록에 비어 있는 항목이 있습니다.");
            }
        }

        // Leap과 동일하게 첫 이미지의 전체 크기로 원형 지름을 맞춘다.
        Vector2 sourceSize = _aimingFrames[0].bounds.size;

        if (sourceSize.x <= 0f || sourceSize.y <= 0f)
        {
            throw new InvalidOperationException(
                $"{name}: 경고 스프라이트의 크기가 올바르지 않습니다.");
        }

        _owner = owner;
        _animationElapsed = 0f;
        _visual.sprite = _aimingFrames[0];

        float diameter = radius * 2f;
        transform.localScale = new Vector3(
            diameter / sourceSize.x,
            diameter / sourceSize.y,
            1f);

        // 이 위치는 생성 시 기록한 좌표이며 플레이어를 따라 이동하지 않는다.
        transform.SetPositionAndRotation(
            new Vector3(center.x, center.y, 0f),
            Quaternion.identity);

        _visual.color = _aimingColor;
        _initialized = true;
        _visual.enabled = true;
    }

    private void Update()
    {
        if (!_initialized)
            return;

        // 전투가 중단되면 패턴 정리와 별개로 남은 경고도 제거한다.
        if (_owner == null || !_owner.CanRunPhaseTwoPattern)
        {
            Remove();
            return;
        }

        // 게임 시간으로 재생하므로 일시정지 중에는 이미지 진행도 멈춘다.
        _animationElapsed += Time.deltaTime;
        int frameIndex =
            Mathf.FloorToInt(_animationElapsed * _framesPerSecond)
            % _aimingFrames.Length;

        _visual.sprite = _aimingFrames[frameIndex];
    }

    // 공격 시점이나 패턴 취소 시 경고를 즉시 숨기고 제거한다.
    public void Remove()
    {
        _initialized = false;

        if (_visual != null)
            _visual.enabled = false;

        Destroy(gameObject);
    }

    // 잘못된 좌표, 반경, 재생 속도가 계산에 들어오지 않게 검사한다.
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

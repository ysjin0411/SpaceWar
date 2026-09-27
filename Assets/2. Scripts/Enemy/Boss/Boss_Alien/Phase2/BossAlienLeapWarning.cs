using System;
using UnityEngine;

// 도약의 예상 착지 범위와 위치 확정 상태를 표시한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class BossAlienLeapWarning : MonoBehaviour
{
    // 착지점을 추적하는 동안 반복 재생할 이미지 12장이다.
    [SerializeField]
    private Sprite[] _aimingFrames = Array.Empty<Sprite>();

    // 착지점이 고정된 뒤 반복 재생할 이미지 8장이다.
    [SerializeField]
    private Sprite[] _lockedFrames = Array.Empty<Sprite>();

    // 경고 이미지의 초당 재생 프레임 수다.
    [SerializeField, Min(1f)]
    private float _framesPerSecond = 24f;

    // 현재 경고 상태에서 애니메이션을 재생한 시간이다.
    private float _animationElapsed;

    // 조준 중에는 연한 보라색으로 표시한다.
    [SerializeField]
    private Color _aimingColor = new Color(0.8f, 0.2f, 1f, 0.25f);

    // 착지점이 확정되면 더 진한 색으로 표시한다.
    [SerializeField]
    private Color _lockedColor = new Color(1f, 0.2f, 0.65f, 0.5f);

    // 경고를 요청한 본체와 표시용 렌더러다.
    private Boss_AlienBody _owner;
    private SpriteRenderer _visual;

    // 초기화 여부와 착지점 확정 여부를 저장한다.
    private bool _initialized;
    private bool _isLocked;

    private void Awake()
    {
        _visual = GetComponent<SpriteRenderer>();

        // 위치와 크기가 설정되기 전에는 화면에 표시하지 않는다.
        _visual.enabled = false;
        _visual.drawMode = SpriteDrawMode.Simple;
    }

    // 부모 없이 생성한 경고에 소유자, 중심, 피해 반경을 전달한다.
    public void Initialize(
        Boss_AlienBody owner,
        Vector2 center,
        float radius)
    {
        // 부모 Scale의 영향을 받으면 표시 반경이 달라지므로 금지한다.
        if (transform.parent != null)
        {
            throw new InvalidOperationException(
                $"{name}: 도약 경고는 부모 없이 생성하세요.");
        }

        if (owner == null ||
            !IsFinite(radius) ||
            radius <= 0f)
        {
            throw new ArgumentException(
                "도약 경고의 보스와 반경을 확인하세요.");
        }

        // 두 상태의 이미지 목록과 재생 속도가 설정됐는지 확인한다.
        if (_aimingFrames == null || _aimingFrames.Length == 0 ||
            _lockedFrames == null || _lockedFrames.Length == 0 ||
            !IsFinite(_framesPerSecond) || _framesPerSecond <= 0f)
        {
            throw new InvalidOperationException(
                $"{name}: Aiming Frames, Locked Frames, FPS를 확인하세요.");
        }

        // 두 목록에 비어 있는 이미지가 있으면 재생 전에 알려준다.
        foreach (Sprite[] frames in new[] { _aimingFrames, _lockedFrames })
        {
            foreach (Sprite frame in frames)
            {
                if (frame == null)
                {
                    throw new InvalidOperationException(
                        $"{name}: 경고 이미지 목록에 비어 있는 항목이 있습니다.");
                }
            }
        }

        // 경고가 생성되면 조준용 첫 이미지부터 시작한다.
        _animationElapsed = 0f;
        _visual.sprite = _aimingFrames[0];

        if (_visual.sprite == null)
        {
            throw new InvalidOperationException(
                $"{name}: 원형 경고 스프라이트를 연결하세요.");
        }

        // 투명 여백이 없는 원형 이미지의 전체 크기를 사용한다.
        Vector2 sourceSize = _visual.sprite.bounds.size;

        if (sourceSize.x <= 0f || sourceSize.y <= 0f)
        {
            throw new InvalidOperationException(
                $"{name}: 경고 스프라이트의 크기가 올바르지 않습니다.");
        }

        _owner = owner;
        _isLocked = false;
        _initialized = true;

        // 이미지의 지름을 실제 피해 반경의 두 배로 맞춘다.
        float diameter = radius * 2f;
        transform.localScale = new Vector3(
            diameter / sourceSize.x,
            diameter / sourceSize.y,
            1f);

        transform.rotation = Quaternion.identity;
        SetCenter(center);

        _visual.color = _aimingColor;
        _visual.enabled = true;
    }

    // 조준 중에만 경고 위치를 갱신한다.
    public void SetCenter(Vector2 center)
    {
        if (!_initialized || _isLocked)
            return;

        if (!IsFinite(center.x) || !IsFinite(center.y))
        {
            throw new ArgumentException(
                "도약 경고의 중심 좌표가 올바르지 않습니다.");
        }

        transform.position = new Vector3(center.x, center.y, 0f);
    }

    // 마지막 좌표를 반영한 뒤 위치 변경을 잠근다.
    public void LockPosition(Vector2 center)
    {
        if (!_initialized || _isLocked)
            return;

        SetCenter(center);
        _isLocked = true;
        _visual.color = _lockedColor;

        // 착지점이 고정되면 고정용 애니메이션을 첫 이미지부터 시작한다.
        _animationElapsed = 0f;
        _visual.sprite = _lockedFrames[0];
    }

    // 착지하거나 패턴이 취소되면 즉시 숨기고 제거한다.
    public void Remove()
    {
        _initialized = false;

        if (_visual != null)
        {
            _visual.enabled = false;
        }

        Destroy(gameObject);
    }

    private void Update()
    {
        // 초기화 전이거나 이미 제거 중이면 재생하지 않는다.
        if (!_initialized)
            return;

        // 보스나 플레이어의 사망 등으로 패턴이 중단되면 경고를 제거한다.
        if (_owner == null || !_owner.CanRunPhaseTwoPattern)
        {
            Remove();
            return;
        }

        // 착지점 고정 여부에 따라 재생할 이미지 목록을 선택한다.
        Sprite[] frames = _isLocked ? _lockedFrames : _aimingFrames;

        // 현재 상태의 이미지들을 지정한 FPS로 반복 재생한다.
        _animationElapsed += Time.deltaTime;
        int frameIndex =
            Mathf.FloorToInt(_animationElapsed * _framesPerSecond)
            % frames.Length;

        _visual.sprite = frames[frameIndex];
    }

    // 좌표와 반경에 사용할 수 있는 유한한 숫자인지 확인한다.
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
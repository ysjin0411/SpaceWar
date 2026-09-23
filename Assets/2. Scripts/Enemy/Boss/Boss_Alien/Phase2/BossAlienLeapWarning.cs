using System;
using UnityEngine;

// 도약의 예상 착지 범위와 위치 확정 상태를 표시한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class BossAlienLeapWarning : MonoBehaviour
{
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
        // 패턴의 정리 코드 외에도 보스·플레이어의 전투 종료를 확인한다.
        if (_initialized &&
            (_owner == null || !_owner.CanRunPhaseTwoPattern))
        {
            Remove();
        }
    }

    // 좌표와 반경에 사용할 수 있는 유한한 숫자인지 확인한다.
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
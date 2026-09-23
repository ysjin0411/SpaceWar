using System;
using UnityEngine;

// 콤보의 사각형 공격 범위를 표시한다.
// 확정한 좌표와 각도는 실제 피해 판정에서도 사용한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class BossAlienComboWarning : MonoBehaviour
{
    // 조준 중에는 연하게, 방향 확정 후에는 진하게 표시한다.
    [SerializeField, Range(0f, 1f)]
    private float _aimingAlpha = 0.3f;

    [SerializeField, Range(0f, 1f)]
    private float _lockedAlpha = 0.75f;

    // 경고를 요청한 본체와 표시용 렌더러다.
    private Boss_AlienBody _owner;
    private SpriteRenderer _visual;

    // 초기화 여부와 조준 확정 여부다.
    private bool _initialized;
    private bool _isLocked;

    // 실제 공격도 이 값들을 읽어 표시 범위와 판정을 일치시킨다.
    public Vector2 Center { get; private set; }
    public Vector2 Size { get; private set; }
    public float Angle { get; private set; }

    private void Awake()
    {
        _visual = GetComponent<SpriteRenderer>();

        // 위치와 크기 설정 전에는 표시하지 않는다.
        _visual.enabled = false;

        // 사각형을 늘려도 테두리 두께를 유지하도록 9-slice를 사용한다.
        if (_visual.sprite == null ||
            _visual.sprite.border == Vector4.zero)
        {
            throw new InvalidOperationException(
                $"{name}: Border가 설정된 사각형 경고 이미지를 연결하세요.");
        }

        _visual.drawMode = SpriteDrawMode.Sliced;
    }

    // 한 타격에 사용할 크기와 최초 조준 방향을 설정한다.
    public void Initialize(
        Boss_AlienBody owner,
        Vector2 center,
        Vector2 size,
        float angle)
    {
        // 부모의 Scale과 회전이 공격 범위를 바꾸지 않게 한다.
        if (transform.parent != null)
        {
            throw new InvalidOperationException(
                $"{name}: 콤보 경고는 부모 없이 생성하세요.");
        }

        if (owner == null ||
            !IsFinite(size.x) || size.x <= 0f ||
            !IsFinite(size.y) || size.y <= 0f)
        {
            throw new ArgumentException(
                "콤보 경고의 보스와 크기를 확인하세요.");
        }

        _owner = owner;
        Size = size;
        _isLocked = false;
        _initialized = true;

        // 월드 크기와 SpriteRenderer.size가 일치하도록 설정한다.
        transform.localScale = Vector3.one;
        _visual.size = size;

        SetAim(center, angle);
        SetAlpha(_aimingAlpha);
        _visual.enabled = true;
    }

    // 조준 중에만 위치와 각도를 갱신한다.
    public void SetAim(Vector2 center, float angle)
    {
        if (!_initialized || _isLocked)
            return;

        if (!IsFinite(center.x) ||
            !IsFinite(center.y) ||
            !IsFinite(angle))
        {
            throw new ArgumentException(
                "콤보 경고의 좌표와 각도가 올바르지 않습니다.");
        }

        Center = center;
        Angle = angle;

        // 사각형의 로컬 X축이 공격 방향을 향하게 한다.
        transform.SetPositionAndRotation(
            new Vector3(center.x, center.y, 0f),
            Quaternion.Euler(0f, 0f, angle));
    }

    // 마지막 조준 상태를 고정한다. 이후 SetAim 호출은 무시한다.
    public void LockAim()
    {
        if (!_initialized || _isLocked)
            return;

        _isLocked = true;
        SetAlpha(_lockedAlpha);
    }

    // 실제 공격 판정이 활성화되는 구간을 밝게 표시한다.
    public void ShowAttack()
    {
        if (_initialized && _isLocked)
        {
            SetAlpha(1f);
        }
    }

    // 공격 종료나 패턴 취소 시 즉시 숨기고 제거한다.
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
        // 패턴 정리와 별개로 보스·플레이어의 전투 종료도 확인한다.
        if (_initialized &&
            (_owner == null || !_owner.CanRunPhaseTwoPattern))
        {
            Remove();
        }
    }

    // 이미지 자체의 색상을 유지하면서 투명도만 조절한다.
    private void SetAlpha(float alpha)
    {
        _visual.color = new Color(1f, 1f, 1f, alpha);
    }

    // 위치와 크기에 사용할 수 있는 숫자인지 검사한다.
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
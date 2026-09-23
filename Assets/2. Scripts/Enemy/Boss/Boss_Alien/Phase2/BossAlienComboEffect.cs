using System;
using UnityEngine;

// 연속 공격의 시각 효과만 표시한다.
// 피해 판정과 재생 시간은 패턴 코드가 관리한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class BossAlienComboEffect : MonoBehaviour
{
    // 이미지를 표시할 렌더러와 효과를 생성한 본체다.
    private SpriteRenderer _visual;
    private Boss_AlienBody _owner;

    // 초기화된 효과만 상태를 확인하고 표시를 갱신한다.
    private bool _initialized;

    private void Awake()
    {
        // 위치와 이미지가 설정되기 전에는 표시하지 않는다.
        _visual = GetComponent<SpriteRenderer>();
        _visual.drawMode = SpriteDrawMode.Simple;
        _visual.enabled = false;
    }

    // 경고와 피해 판정에 사용한 동일한 위치·크기·각도를 전달받는다.
    // 원본 이미지는 가운데 Pivot, 오른쪽이 공격 진행 방향인 기준이다.
    public void Initialize(
        Boss_AlienBody owner,
        Sprite sprite,
        Vector2 center,
        Vector2 size,
        float angle)
    {
        if (transform.parent != null)
        {
            throw new InvalidOperationException(
                $"{name}: 공격 효과는 부모 없이 생성하세요.");
        }

        if (owner == null || sprite == null ||
            !IsFinite(center.x) || !IsFinite(center.y) ||
            !IsFinite(size.x) || !IsFinite(size.y) ||
            !IsFinite(angle) ||
            size.x <= 0f || size.y <= 0f)
        {
            throw new ArgumentException(
                $"{name}: 소유자, 이미지, 위치, 크기, 각도를 확인하세요.");
        }

        Vector2 sourceSize = sprite.bounds.size;

        if (sourceSize.x <= 0f || sourceSize.y <= 0f)
        {
            throw new InvalidOperationException(
                $"{name}: 효과 이미지의 크기가 올바르지 않습니다.");
        }

        _owner = owner;

        // 공격 확정 이후에는 보스나 플레이어를 따라가지 않는다.
        transform.SetPositionAndRotation(
            new Vector3(center.x, center.y, 0f),
            Quaternion.Euler(0f, 0f, angle));

        // 이미지 전체 영역을 공격 사각형 크기에 맞춘다.
        // 투명 여백이 있는 이미지의 실제 보이는 부분은 더 작을 수 있다.
        transform.localScale = new Vector3(
            size.x / sourceSize.x,
            size.y / sourceSize.y,
            1f);

        _visual.sprite = sprite;
        _visual.flipX = false;
        _visual.flipY = false;
        _visual.color = Color.white;
        _visual.enabled = true;
        _initialized = true;
    }

    // 공격 진행도에 따라 후반부에서 이미지를 흐리게 한다.
    public void SetProgress(float progress)
    {
        if (!_initialized)
            return;

        // 공격 시간의 앞 절반은 선명하게 유지한다.
        float fade = Mathf.InverseLerp(
            0.5f, 1f, Mathf.Clamp01(progress));

        _visual.color = new Color(1f, 1f, 1f, 1f - fade);
    }

    private void Update()
    {
        // 전투가 끝나면 패턴의 정리 처리와 별개로 효과도 제거한다.
        if (_initialized &&
            (_owner == null || !_owner.CanRunPhaseTwoPattern))
        {
            Remove();
        }
    }

    // 정상 종료와 패턴 취소 모두 같은 방식으로 제거한다.
    public void Remove()
    {
        _initialized = false;

        if (_visual != null)
            _visual.enabled = false;

        Destroy(gameObject);
    }

    // 잘못된 수치가 Transform 계산에 들어가지 않도록 검사한다.
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
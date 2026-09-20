using System;
using UnityEngine;

// 9-slice 스프라이트로 실제 공격 크기의 직사각형 예고를 표시한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class BossAlienAttackWarning : MonoBehaviour
{
    // 예고 시작과 공격 직전의 불투명도다. 원본 이미지의 알파와 곱해진다.
    [SerializeField, Range(0f, 1f)] private float _startAlpha = 0.35f;
    [SerializeField, Range(0f, 1f)] private float _endAlpha = 0.9f;

    // 같은 오브젝트의 외형과 예고를 요청한 보스다.
    private SpriteRenderer _visual;
    private Boss_Alien _owner;
    private bool _initialized;

    private void Awake()
    {
        _visual = GetComponent<SpriteRenderer>();

        // 테두리를 유지하려면 Border가 설정된 Full Rect 스프라이트가 필요하다.
        if (_visual.sprite == null || _visual.sprite.border == Vector4.zero)
        {
            throw new InvalidOperationException(
                $"{name}: Border가 설정된 경고 스프라이트를 연결하세요.");
        }

        _visual.drawMode = SpriteDrawMode.Sliced;
        _visual.enabled = false;
    }

    // 기존 패턴과 같은 호출 형식을 유지한다.
    // size는 월드 단위이며 프리팹 루트의 Scale은 (1,1,1)을 사용한다.
    public void Initialize(
        Boss_Alien owner, Vector2 center, Vector2 size, float angle)
    {
        if (owner == null || !Positive(size.x) || !Positive(size.y) ||
            float.IsNaN(angle) || float.IsInfinity(angle))
        {
            throw new InvalidOperationException(
                $"{name}: 예고의 보스·크기·회전을 확인하세요.");
        }

        _owner = owner;
        transform.SetPositionAndRotation(
            new Vector3(center.x, center.y, 0f),
            Quaternion.Euler(0f, 0f, angle));

        // Transform을 늘리지 않고 중앙과 직선 테두리만 늘린다.
        _visual.size = size;
        _initialized = true;
        SetProgress(0f);
        _visual.enabled = true;
    }

    // 예고의 위치와 크기는 유지하고 공격 직전까지 밝기를 올린다.
    public void SetProgress(float progress)
    {
        if (!_initialized)
        {
            return;
        }

        float alpha = Mathf.Lerp(
            _startAlpha, _endAlpha, Mathf.Clamp01(progress));

        // 이미지 고유의 붉은 분홍색을 그대로 사용한다.
        _visual.color = new Color(1f, 1f, 1f, alpha);
    }

    // 마무리 공격이 시작되면 테두리를 최대 밝기로 표시한다.
    public void ShowAttack()
    {
        if (_initialized)
        {
            _visual.color = Color.white;
        }
    }

    private void Update()
    {
        // 사망·비활성화 시 시간이 멈춰도 예고를 정리한다.
        if (_initialized && (_owner == null || !_owner.CanRunArmPattern))
        {
            Destroy(gameObject);
        }
    }

    // 공격 영역 크기에 사용할 유효한 양수인지 확인한다.
    private static bool Positive(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }
}

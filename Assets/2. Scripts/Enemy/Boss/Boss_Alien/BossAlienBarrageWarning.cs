using System;
using UnityEngine;

// 미사일 한 발의 폭발 범위를 반투명 원으로 표시한다.
// 회차마다 별도 오브젝트를 만들어 겹친 영역의 색이 누적되게 한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class BossAlienBarrageWarning : MonoBehaviour
{
    // 흰색 원 스프라이트에 적용할 경고 색상이다.
    // 알파를 낮게 유지하여 여러 장판이 겹친 곳이 더 진하게 보이게 한다.
    [SerializeField]
    private Color _warningColor = new Color(1f, 0.15f, 0.15f, 0.2f);

    // 장판 외형과 전투 종료 여부를 확인할 보스다.
    private SpriteRenderer _visual;
    private Boss_Alien _owner;

    // 위치와 반경이 설정된 이후에만 전투 종료를 검사한다.
    private bool _initialized;

    private void Awake()
    {
        // 같은 오브젝트의 원형 스프라이트를 사용한다.
        _visual = GetComponent<SpriteRenderer>();

        if (_visual.sprite == null)
        {
            throw new InvalidOperationException(
                $"{name}: SpriteRenderer에 흰색 원 스프라이트를 연결하세요.");
        }

        // 원 전체를 확대하므로 9-slice 방식은 사용하지 않는다.
        _visual.drawMode = SpriteDrawMode.Simple;

        // 초기화 전에는 잘못된 위치에 장판이 표시되지 않게 한다.
        _visual.enabled = false;
    }

    // 예고와 실제 폭격이 공유할 월드 좌표와 피해 반경을 전달받는다.
    // 이 오브젝트는 부모 없이 생성하여 부모 Scale의 영향을 받지 않게 한다.
    public void Initialize(Boss_Alien owner, Vector2 center, float radius)
    {
        if (owner == null ||
            !IsFinite(center.x) ||
            !IsFinite(center.y) ||
            !IsFinite(radius) ||
            radius <= 0f)
        {
            throw new ArgumentException(
                "장판의 보스, 월드 좌표, 반경을 확인하세요.");
        }

        // 스프라이트의 원본 크기는 PPU가 반영된 로컬 유닛이다.
        Vector2 sourceSize = _visual.sprite.bounds.size;

        if (sourceSize.x <= 0f || sourceSize.y <= 0f)
        {
            throw new InvalidOperationException(
                $"{name}: 원 스프라이트의 크기가 올바르지 않습니다.");
        }

        _owner = owner;

        // 전달받은 월드 위치에 고정한다.
        transform.SetPositionAndRotation(
            new Vector3(center.x, center.y, 0f),
            Quaternion.identity);

        // 원의 지름을 피해 반경의 두 배로 맞춘다.
        // 투명 여백 없이 원이 꽉 찬 스프라이트를 사용해야 정확하다.
        float diameter = radius * 2f;
        transform.localScale = new Vector3(
            diameter / sourceSize.x,
            diameter / sourceSize.y,
            1f);

        // 시간에 따라 진해지지 않고 일정한 반투명 상태를 유지한다.
        _visual.color = _warningColor;
        _initialized = true;
        _visual.enabled = true;
    }

    // 이 장판 하나만 숨기고 제거한다.
    // 같은 위치에 겹쳐 있는 다른 회차의 장판에는 영향을 주지 않는다.
    public void Remove()
    {
        _initialized = false;

        // Destroy는 프레임 끝에 처리되므로 외형은 즉시 숨긴다.
        if (_visual != null)
        {
            _visual.enabled = false;
        }

        Destroy(gameObject);
    }

    private void Update()
    {
        // 보스나 플레이어의 사망·비활성화 시 장판을 정리한다.
        // deltaTime을 사용하지 않으므로 시간이 멈춰도 검사한다.
        if (_initialized &&
            (_owner == null || !_owner.CanRunArmPattern))
        {
            Remove();
        }
    }

    // 좌표나 반경에 사용할 수 없는 숫자를 걸러낸다.
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
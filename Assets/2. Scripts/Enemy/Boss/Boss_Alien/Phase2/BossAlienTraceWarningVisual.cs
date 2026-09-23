using System;
using UnityEngine;

// 흔적 경고의 원형 범위 안에 전용 문양을 표시한다.
// 실제 범위와 위치는 부모의 기존 경고 컴포넌트가 관리한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class BossAlienTraceWarningVisual : MonoBehaviour
{
    // 부모 경고 지름에 대한 문양 이미지의 크기 비율이다.
    [SerializeField, Range(0.1f, 1f)]
    private float _sizeRatio = 0.9f;

    // 문양의 색상과 투명도다.
    [SerializeField]
    private Color _color = new Color(1f, 1f, 1f, 0.75f);

    // 부모의 범위 표시와 자식의 문양 렌더러다.
    private SpriteRenderer _rangeVisual;
    private SpriteRenderer _visual;

    private void Awake()
    {
        _visual = GetComponent<SpriteRenderer>();
        _visual.enabled = false;

        // 이 컴포넌트는 경고 루트 바로 아래의 자식에 배치한다.
        if (transform.parent == null ||
            !transform.parent.TryGetComponent(out _rangeVisual) ||
            _rangeVisual.sprite == null ||
            _visual.sprite == null)
        {
            throw new InvalidOperationException(
                $"{name}: 부모의 원형 경고와 자식의 문양 이미지를 연결하세요.");
        }

        Vector2 rangeSize = _rangeVisual.sprite.bounds.size;
        Vector2 motifSize = _visual.sprite.bounds.size;

        float rangeDiameter = Mathf.Min(rangeSize.x, rangeSize.y);
        float motifDiameter = Mathf.Max(motifSize.x, motifSize.y);

        if (rangeDiameter <= 0f || motifDiameter <= 0f)
        {
            throw new InvalidOperationException(
                $"{name}: 경고 또는 문양 이미지 크기가 올바르지 않습니다.");
        }

        // 부모가 반경에 맞춰 커지면 문양도 같은 비율로 따라 커진다.
        // 문양 자체의 가로세로 비율은 유지한다.
        float scale = rangeDiameter / motifDiameter * _sizeRatio;

        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = new Vector3(scale, scale, 1f);

        _visual.drawMode = SpriteDrawMode.Simple;
        _visual.color = _color;

        // 범위 표시 바로 위에 문양을 표시한다.
        _visual.sortingLayerID = _rangeVisual.sortingLayerID;
        _visual.sortingOrder = _rangeVisual.sortingOrder + 1;
    }

    private void LateUpdate()
    {
        // 부모 경고가 숨겨지면 문양도 같은 프레임에 숨긴다.
        // 오브젝트 제거는 부모 경고와 함께 처리된다.
        _visual.enabled =
            _rangeVisual != null && _rangeVisual.enabled;
    }
}
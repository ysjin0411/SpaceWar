using System;
using UnityEngine;

// 실제 피해 사각형 안에 광역 방전 이미지를 반복 배치한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class BossAlienFinalAreaEffect : MonoBehaviour
{
    // 처음부터 끝까지 재생할 방전 이미지.
    [SerializeField] private Sprite[] _frames;

    // 같은 오브젝트의 렌더러와 실제 공격 영역 크기.
    private SpriteRenderer _visual;
    private Vector2 _areaSize;

    // 피해 영역을 전달받기 전에는 표시하지 않는다.
    private bool _initialized;

    private void Awake()
    {
        _visual = GetComponent<SpriteRenderer>();
        _visual.enabled = false;

        // 빈 이미지로 바뀌는 프레임이 없도록 검사한다.
        if (_frames == null || _frames.Length == 0)
        {
            throw new InvalidOperationException(
                $"{name}: Frames에 방전 이미지를 등록하세요.");
        }

        for (int i = 0; i < _frames.Length; i++)
        {
            if (_frames[i] == null)
            {
                throw new InvalidOperationException(
                    $"{name}: Frames의 {i}번 이미지가 비어 있습니다.");
            }
        }
    }

    // 피해 판정에 사용하는 Rect를 그대로 전달받는다.
    public void Initialize(Rect damageArea)
    {
        if (!Positive(damageArea.width) ||
            !Positive(damageArea.height))
        {
            throw new InvalidOperationException(
                $"{name}: 공격 영역의 너비와 높이를 확인하세요.");
        }

        // 월드 좌표의 피해 영역과 일치하도록 부모·회전·배율을 고정한다.
        transform.SetParent(null);
        transform.SetPositionAndRotation(
            new Vector3(damageArea.center.x, damageArea.center.y, 0f),
            Quaternion.identity);
        transform.localScale = Vector3.one;

        _areaSize = damageArea.size;

        // 무늬를 늘리지 않고 반복하며, 끝 타일은 영역 경계에서 잘린다.
        _visual.drawMode = SpriteDrawMode.Tiled;
        _visual.tileMode = SpriteTileMode.Continuous;
        _visual.color = Color.white;

        _initialized = true;
        SetProgress(0f);
        _visual.enabled = true;
    }

    // 패턴의 공격 진행률로 프레임을 선택해 판정 시간과 맞춘다.
    public void SetProgress(float progress)
    {
        if (!_initialized)
        {
            return;
        }

        int index = Mathf.Min(
            Mathf.FloorToInt(Mathf.Clamp01(progress) * _frames.Length),
            _frames.Length - 1);

        _visual.sprite = _frames[index];

        // 이미지가 바뀌어도 표시 영역은 실제 피해 사각형으로 유지한다.
        _visual.size = _areaSize;
    }

    // 크기 설정에 사용할 유효한 양수인지 검사한다.
    private static bool Positive(float value)
    {
        return !float.IsNaN(value) &&
               !float.IsInfinity(value) &&
               value > 0f;
    }
}
using System;
using UnityEngine;
using UnityEngine.UI;

public class MenuBackgroundScroller : MonoBehaviour
{
    // 배경이 보이는 영역. RectMask2D가 붙은 오브젝트를 연결한다.
    [SerializeField] RectTransform _viewport;

    // 실제로 이동할 배경 이미지. Viewport의 직접적인 자식이어야 한다.
    [SerializeField] Image _background;

    // 중앙을 기준으로 좌우 각각 이동하는 거리다.
    // 400이면 X=-400부터 X=400까지 이동한다.
    [SerializeField, Min(0f)] float _moveRange = 400f;

    // 중앙을 지날 때의 최대 이동 속도다.
    // Canvas 좌표 기준이며, 양 끝에서는 부드럽게 감속한다.
    [SerializeField, Min(0f)] float _scrollSpeed = 20f;

    // 켜면 처음에 왼쪽으로, 끄면 오른쪽으로 이동한다.
    [SerializeField] bool _startMovingLeft = true;

    // 배경을 멈추고 싶을 때 해제한다.
    [SerializeField] bool _isScrolling = true;

    // 실제 배경의 RectTransform.
    private RectTransform _backgroundRect;

    // 왕복 움직임의 현재 진행 각도.
    private float _phase;

    // 필수 연결 검사를 통과했는지 기록한다.
    private bool _initialized;

    private void Start()
    {
        if (_viewport == null ||
            _background == null ||
            _background.sprite == null)
        {
            throw new InvalidOperationException(
                "MenuBackgroundScroller의 Viewport와 배경 이미지를 연결해주세요.");
        }

        _backgroundRect = _background.rectTransform;

        if (_backgroundRect.parent != _viewport)
        {
            throw new InvalidOperationException(
                "Background는 BackgroundViewport의 직접적인 자식이어야 합니다.");
        }

        // 중앙 기준으로 크기와 이동 거리를 계산한다.
        _backgroundRect.anchorMin = new Vector2(0.5f, 0.5f);
        _backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
        _backgroundRect.pivot = new Vector2(0.5f, 0.5f);
        _backgroundRect.localScale = Vector3.one;
        _backgroundRect.localRotation = Quaternion.identity;
        _backgroundRect.anchoredPosition = Vector2.zero;

        // 이미지 비율은 RectTransform 크기 계산에서 유지한다.
        _background.type = Image.Type.Simple;
        _background.preserveAspect = false;
        _background.raycastTarget = false;

        _initialized = true;
    }

    private void LateUpdate()
    {
        if (!_initialized)
        {
            return;
        }

        float viewportWidth = _viewport.rect.width;
        float viewportHeight = _viewport.rect.height;

        // Canvas 배치가 아직 계산되지 않은 프레임은 건너뛴다.
        if (viewportWidth <= 0f || viewportHeight <= 0f)
        {
            return;
        }

        float range = Mathf.Max(0f, _moveRange);

        // 창 크기가 바뀌어도 원본 비율과 화면 덮기를 유지한다.
        ResizeBackground(viewportWidth, viewportHeight, range);

        if (range <= 0f)
        {
            _backgroundRect.anchoredPosition = Vector2.zero;
            return;
        }

        if (_isScrolling)
        {
            // 시간 배율과 관계없이 메뉴 배경을 움직인다.
            // 사인 곡선을 사용해 양 끝에서 자연스럽게 방향을 바꾼다.
            float angularSpeed =
                Mathf.Max(0f, _scrollSpeed) / range;

            _phase = Mathf.Repeat(
                _phase + angularSpeed * Time.unscaledDeltaTime,
                Mathf.PI * 2f);
        }

        float direction = _startMovingLeft ? -1f : 1f;
        float positionX = Mathf.Sin(_phase) * range * direction;

        _backgroundRect.anchoredPosition =
            new Vector2(positionX, 0f);
    }

    private void ResizeBackground(
        float viewportWidth,
        float viewportHeight,
        float range)
    {
        // 잘라서 등록된 스프라이트도 실제 Sprite Rect 비율을 사용한다.
        Rect spriteRect = _background.sprite.rect;
        float aspect = spriteRect.width / spriteRect.height;

        // 화면 높이를 덮는 데 필요한 너비.
        float widthFromHeight = viewportHeight * aspect;

        // 양 끝까지 이동해도 화면 밖이 드러나지 않는 최소 너비.
        float widthWithMovement = viewportWidth + range * 2f;

        float width = Mathf.Max(widthFromHeight, widthWithMovement);
        float height = width / aspect;

        Vector2 size = new Vector2(width, height);

        // 크기가 달라졌을 때만 UI 크기를 갱신한다.
        if ((_backgroundRect.sizeDelta - size).sqrMagnitude > 0.01f)
        {
            _backgroundRect.sizeDelta = size;
        }
    }
}
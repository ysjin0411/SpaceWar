using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UIButtonSelectionEffect :
    MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    // 선택했을 때 원래 크기에 곱하는 배율.
    [SerializeField, Min(1f)] float _selectedScale = 1.05f;

    // 확대와 밝기 변화에 걸리는 시간.
    [SerializeField, Min(0.01f)] float _transitionDuration = 0.18f;

    // 선택되지 않았을 때 버튼 이미지의 밝기.
    [SerializeField, Range(0f, 1f)] float _normalBrightness = 0.7f;

    // 선택되었을 때 버튼 이미지의 밝기.
    [SerializeField, Range(0f, 1f)] float _selectedBrightness = 1f;

    // 같은 오브젝트에 있는 버튼과 대상 이미지.
    private Button _button;
    private Graphic _graphic;

    // 선택 해제 시 돌아갈 원래 크기와 색상.
    private Vector3 _originalScale;
    private Color _originalColor;

    // 현재 적용된 선택 상태.
    private bool _selected;

    // 현재 버튼 위에 마우스 커서가 있는지 확인한다.
    private bool _isPointerInside;

    // 이 컴포넌트가 생성한 연출만 관리한다.
    private Tween _scaleTween;
    private Tween _colorTween;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _graphic = _button.targetGraphic;

        if (_graphic == null)
        {
            throw new MissingReferenceException(
                $"{name}의 Button에 Target Graphic을 연결해주세요.");
        }

        _originalScale = transform.localScale;
        _originalColor = _graphic.color;

        // 기본 Button 색상 전환과 커스텀 연출이 충돌하지 않게 한다.
        _button.transition = Selectable.Transition.None;
    }

    private void OnEnable()
    {
        // 다시 활성화할 때 이전 마우스 상태와 선택 연출을 초기화한다.
        _isPointerInside = false;
        _selected = false;
        ApplyAppearance(false, true);
    }

    private void Update()
    {
        // Navigation이 None이면 마우스로만 사용하는 버튼이다.
        bool pointerOnly =
            _button.navigation.mode == Navigation.Mode.None;

        bool selected;

        if (pointerOnly)
        {
            // Back처럼 마우스 전용인 버튼은 커서가 올라왔을 때만 강조한다.
            selected =
                _button.IsInteractable() && _isPointerInside;
        }
        else
        {
            // 키보드를 지원하는 버튼은 기존 EventSystem 선택을 따른다.
            selected =
                _button.IsInteractable() &&
                EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject == gameObject;
        }

        // 강조 상태가 달라졌을 때만 연출을 갱신한다.
        if (selected == _selected)
        {
            return;
        }

        _selected = selected;
        ApplyAppearance(selected, false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // 마우스가 버튼 영역 안에 들어왔음을 기록한다.
        _isPointerInside = true;

        if (!_button.IsInteractable())
        {
            return;
        }

        // 마우스 전용 버튼은 키보드 선택을 가져가지 않는다.
        if (_button.navigation.mode == Navigation.Mode.None)
        {
            return;
        }

        // 키보드를 지원하는 버튼은 기존처럼 마우스와 선택을 연동한다.
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(gameObject);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // 마우스 전용 버튼은 다음 Update에서 원래 크기와 밝기로 돌아간다.
        _isPointerInside = false;
    }

    private void ApplyAppearance(bool selected, bool immediately)
    {
        StopTweens();

        float scale = selected ? _selectedScale : 1f;
        float brightness = selected
            ? _selectedBrightness
            : _normalBrightness;

        Vector3 targetScale = _originalScale * scale;

        // 원래 투명도는 유지하고 RGB 밝기만 바꾼다.
        Color targetColor = new Color(
            _originalColor.r * brightness,
            _originalColor.g * brightness,
            _originalColor.b * brightness,
            _originalColor.a);

        if (immediately)
        {
            transform.localScale = targetScale;
            _graphic.color = targetColor;
            return;
        }

        // 버튼 아래의 문구도 함께 확대된다.
        _scaleTween = transform
            .DOScale(targetScale, _transitionDuration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true);

        // 버튼 이미지의 밝기를 부드럽게 변경한다.
        _colorTween = _graphic
            .DOColor(targetColor, _transitionDuration)
            .SetEase(Ease.OutSine)
            .SetUpdate(true);
    }

    private void StopTweens()
    {
        if (_scaleTween != null)
        {
            _scaleTween.Kill();
            _scaleTween = null;
        }

        if (_colorTween != null)
        {
            _colorTween.Kill();
            _colorTween = null;
        }
    }

    private void OnDisable()
    {
        // 비활성화 중에 연출이 남지 않도록 정리한다.
        StopTweens();
        transform.localScale = _originalScale;

        if (_graphic != null)
        {
            _graphic.color = _originalColor;
        }

        _isPointerInside = false;
        _selected = false;
    }
}
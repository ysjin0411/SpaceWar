using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 증강 슬롯이 공통으로 사용하는 선택 기능을 담당한다.
/// 마우스 포인터와 키보드 선택을 Unity EventSystem의 선택 상태로 통합한다.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]    // 모든 선택 슬롯에는 페이드 연출용 CanvasGroup이 필요하다. 
public abstract class SelectableSlotView : MonoBehaviour,
    IPointerEnterHandler,
    ISelectHandler,
    IDeselectHandler
{
    [Header("선택 입력")]

    // 이 슬롯의 클릭과 키보드 Submit 입력을 처리하는 버튼
    [SerializeField] private Button _button;

    [Header("선택 표시")]

    // 현재 슬롯이 선택됐을 때 깜빡이는 화살표
    [SerializeField] private Image _arrowImage;

    // 화살표가 한 번 어두워지는 데 걸리는 시간
    [SerializeField] private float _blinkDuration = 0.45f;

    // 화살표가 깜빡일 때 도달할 최소 알파값
    [Range(0f, 1f)]
    [SerializeField] private float _minimumAlpha = 0.2f;

    [Header("슬롯 애니메이션")]

    // 카드 전체의 투명도를 조절한다.
    private CanvasGroup _canvasGroup;

    // 슬롯 위치와 크기 애니메이션에 사용하는 RectTransform
    private RectTransform _rectTransform;

    // 현재 실행 중인 카드 이동 Tween
    private Tween _motionTween;

    // Presenter에 선택된 슬롯 번호를 전달한다.
    public event Action<int> Clicked;

    // 이 프리팹 인스턴스가 담당하는 슬롯 번호
    private int _slotIndex;

    // 실행 중인 화살표 점멸 Tween
    private Tween _blinkTween;

    // View가 실제 선택된 슬롯을 찾을 때 사용하는 런타임 슬롯 번호
    public int SlotIndex => _slotIndex;

    /// <summary>
    /// 슬롯 번호와 버튼 이벤트를 초기화한다.
    /// 파생 View의 Bind 메서드에서 호출한다.
    /// </summary>
    protected void InitializeSelectable(int slotIndex)
    {
        if (_button == null)
        {
            throw new InvalidOperationException(
                $"{name}에 Button이 연결되지 않았습니다.");
        }

        if (_arrowImage == null)
        {
            throw new InvalidOperationException(
                $"{name}에 ArrowImage가 연결되지 않았습니다.");
        }

        _slotIndex = slotIndex;

        // 프리팹이 재사용될 경우 기존 이벤트가 중복되지 않도록 제거한다.
        _button.onClick.RemoveListener(NotifyClicked);
        _button.onClick.AddListener(NotifyClicked);

        // 처음 생성될 때는 선택 표시를 숨긴다.
        StopBlink();

        // 슬롯 루트에 붙어 있는 필수 컴포넌트를 자동으로 가져온다.
        _rectTransform = GetComponent<RectTransform>();
        _canvasGroup = GetComponent<CanvasGroup>();

        if (_rectTransform == null)
        {
            throw new MissingComponentException(
                $"{name}에 RectTransform이 없습니다.");
        }

        if (_canvasGroup == null)
        {
            throw new MissingComponentException(
                $"{name}에 CanvasGroup이 없습니다.");
        }

        // 프리팹이 다시 생성되거나 재사용될 경우 기본 표시 상태로 복구한다.
        _canvasGroup.alpha = 1f;
        transform.localScale = Vector3.one;
    }

    /// <summary>
    /// 슬롯이 위쪽에서 최종 위치로 떨어지는 등장 연출을 실행한다.
    /// Time.timeScale이 0이어도 실제 시간 기준으로 재생한다.
    /// </summary>
    /// <param name="delay">이 슬롯이 떨어지기 시작할 지연시간</param>
    /// <param name="verticalOffset">최종 위치에서 위쪽으로 떨어질 거리</param>
    /// <param name="duration">등장 연출 시간</param>
    /// <returns>마지막 슬롯의 완료 시점을 받을 수 있는 Tween</returns>
    public Tween PlayEntrance(
        float delay,
        float verticalOffset,
        float duration)
    {
        if (_rectTransform == null)
        {
            throw new InvalidOperationException(
                $"{name}의 RectTransform이 초기화되지 않았습니다.");
        }

        if (_canvasGroup == null)
        {
            throw new InvalidOperationException(
                $"{name}의 CanvasGroup이 연결되지 않았습니다.");
        }

        if (duration <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                "등장 연출 시간은 0보다 커야 합니다.");
        }

        // 이전 카드 애니메이션이 남아 있다면 종료한다.
        _motionTween?.Kill();

        // LayoutGroup이 계산한 최종 위치를 보관한다.
        Vector2 destination =
            _rectTransform.anchoredPosition;

        // 최종 위치보다 위쪽에서 시작한다.
        _rectTransform.anchoredPosition =
            destination + Vector2.up * verticalOffset;

        // 처음에는 투명하고 약간 작은 상태로 시작한다.
        _canvasGroup.alpha = 0f;
        transform.localScale =
            Vector3.one * 0.92f;

        Sequence sequence = DOTween.Sequence();

        // 각 슬롯에 전달된 지연시간 이후 떨어지기 시작한다.
        sequence.SetDelay(delay);

        // 위쪽에서 내려온 뒤 살짝 튕기며 정착한다.
        sequence.Join(
            _rectTransform
                .DOAnchorPos(destination, duration)
                .SetEase(Ease.OutBack));

        // 이동하면서 자연스럽게 화면에 나타난다.
        sequence.Join(
            _canvasGroup
                .DOFade(1f, duration * 0.7f)
                .SetEase(Ease.OutQuad));

        // 작은 크기에서 원래 크기로 복구한다.
        sequence.Join(
            transform
                .DOScale(Vector3.one, duration)
                .SetEase(Ease.OutBack));

        // 인게임 시간이 멈춰 있어도 UI 연출은 계속 실행한다.
        sequence.SetUpdate(true);

        _motionTween = sequence;

        return _motionTween;
    }

    /// <summary>
    /// Presenter가 슬롯의 입력을 잠그거나 풀 때 사용한다.
    /// 선택 연출 도중 중복 클릭을 방지할 수 있다.
    /// </summary>
    public void SetInteractable(bool interactable)
    {
        _button.interactable = interactable;
    }

    /// <summary>
    /// 마우스가 슬롯 위에 들어오면 이 버튼을 현재 선택 대상으로 만든다.
    /// 이후 키보드와 마우스가 같은 선택 상태를 공유한다.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_button.interactable)
        {
            return;
        }

        // EventSystem의 현재 선택 오브젝트를 이 슬롯으로 변경한다.
        _button.Select();
    }

    /// <summary>
    /// 키보드 이동이나 마우스 진입으로 이 슬롯이 선택됐을 때 호출된다.
    /// </summary>
    public void OnSelect(BaseEventData eventData)
    {
        StartBlink();
    }

    /// <summary>
    /// 다른 슬롯으로 선택이 이동했을 때 호출된다.
    /// </summary>
    public void OnDeselect(BaseEventData eventData)
    {
        StopBlink();
    }

    /// <summary>
    /// 외부 View가 첫 번째 슬롯을 기본 선택할 때 사용한다.
    /// </summary>
    public void Select()
    {
        if (_button.interactable)
        {
            _button.Select();
        }
    }

    /// <summary>
    /// 버튼 클릭 또는 키보드 Submit 결과를 Presenter에 전달한다.
    /// </summary>
    private void NotifyClicked()
    {
        Clicked?.Invoke(_slotIndex);
    }

    /// <summary>
    /// 선택된 슬롯의 화살표 점멸을 시작한다.
    /// 증강 중에는 시간이 멈춰 있으므로 실제 시간 기준으로 실행한다.
    /// </summary>
    private void StartBlink()
    {
        // 이전 Tween이 남아 있다면 먼저 종료한다.
        _blinkTween?.Kill();

        _arrowImage.gameObject.SetActive(true);

        // 항상 완전히 보이는 상태에서 점멸을 시작한다.
        Color arrowColor = _arrowImage.color;
        arrowColor.a = 1f;
        _arrowImage.color = arrowColor;

        _blinkTween = _arrowImage
            .DOFade(_minimumAlpha, _blinkDuration)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine)

            // Time.timeScale이 0이어도 화살표가 깜빡이게 한다.
            .SetUpdate(true);
    }

    /// <summary>
    /// 점멸을 종료하고 선택 화살표를 숨긴다.
    /// </summary>
    private void StopBlink()
    {
        _blinkTween?.Kill();
        _blinkTween = null;

        if (_arrowImage == null)
        {
            return;
        }

        Color arrowColor = _arrowImage.color;
        arrowColor.a = 1f;
        _arrowImage.color = arrowColor;

        _arrowImage.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        // 패널이 닫힐 때 화살표 점멸을 종료한다.
        StopBlink();

        // 비활성화된 슬롯의 이동 Tween도 정리한다.
        _motionTween?.Kill();
        _motionTween = null;
    }

    private void OnDestroy()
    {
        // 파괴된 프리팹의 Button 이벤트와 Tween을 정리한다.
        if (_button != null)
        {
            _button.onClick.RemoveListener(NotifyClicked);
        }

        _blinkTween?.Kill();

        _motionTween?.Kill();
    }

    /// <summary>
    /// 동적으로 생성된 슬롯의 좌우 키보드 이동 대상을 연결한다.
    /// </summary>
    /// <param name="leftSlot">왼쪽 방향으로 이동할 슬롯</param>
    /// <param name="rightSlot">오른쪽 방향으로 이동할 슬롯</param>
    public void SetHorizontalNavigation(
        SelectableSlotView leftSlot,
        SelectableSlotView rightSlot)
    {
        if (_button == null)
        {
            throw new InvalidOperationException(
                $"{name}에 Button이 연결되지 않았습니다.");
        }

        // 프리팹이 실행 중 생성되므로 인접 슬롯을 명시적으로 연결한다.
        Navigation navigation = _button.navigation;
        navigation.mode = Navigation.Mode.Explicit;

        // 같은 부모 클래스 내부이므로 다른 슬롯의 private Button에도 접근할 수 있다.
        navigation.selectOnLeft =
            leftSlot != null
                ? leftSlot._button
                : null;

        navigation.selectOnRight =
            rightSlot != null
                ? rightSlot._button
                : null;

        _button.navigation = navigation;
    }

    /// <summary>
    /// 선택 확정 후 슬롯이 사라지는 연출을 실행한다.
    /// 선택된 슬롯은 잠시 강조된 뒤 위로 사라지고,
    /// 선택되지 않은 슬롯은 빠르게 축소되며 사라진다.
    /// </summary>
    public Tween PlayExit(
        bool isSelected,
        float duration,
        float selectedVerticalOffset)
    {
        if (_rectTransform == null ||
            _canvasGroup == null)
        {
            throw new InvalidOperationException(
                $"{name}의 애니메이션 컴포넌트가 초기화되지 않았습니다.");
        }

        if (duration <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                "퇴장 연출 시간은 0보다 커야 합니다.");
        }

        // 퇴장 연출 중에는 추가 선택을 받지 않는다.
        _button.interactable = false;

        // 선택 화살표 점멸을 종료한다.
        StopBlink();

        // 실행 중인 등장 또는 퇴장 Tween을 종료한다.
        _motionTween?.Kill();

        Sequence sequence = DOTween.Sequence();

        if (isSelected)
        {
            float emphasisDuration =
                duration * 0.35f;

            float disappearDuration =
                duration - emphasisDuration;

            // 선택된 카드는 먼저 살짝 커지면서 선택 확정을 강조한다.
            sequence.Append(
                transform
                    .DOScale(1.08f, emphasisDuration)
                    .SetEase(Ease.OutBack));

            // 강조 후 위로 이동하면서 자연스럽게 사라진다.
            sequence.Append(
                _rectTransform
                    .DOAnchorPosY(
                        _rectTransform.anchoredPosition.y +
                        selectedVerticalOffset,
                        disappearDuration)
                    .SetEase(Ease.InCubic));

            sequence.Join(
                _canvasGroup
                    .DOFade(0f, disappearDuration)
                    .SetEase(Ease.InQuad));

            sequence.Join(
                transform
                    .DOScale(0.9f, disappearDuration)
                    .SetEase(Ease.InQuad));
        }
        else
        {
            // 선택되지 않은 카드는 선택 카드보다 빠르게 흐려진다.
            sequence.Join(
                _canvasGroup
                    .DOFade(0f, duration * 0.6f)
                    .SetEase(Ease.OutQuad));

            sequence.Join(
                transform
                    .DOScale(0.9f, duration * 0.6f)
                    .SetEase(Ease.InQuad));
        }

        // Time.timeScale이 0이어도 UI 연출을 실행한다.
        sequence.SetUpdate(true);

        _motionTween = sequence;

        return _motionTween;
    }
}
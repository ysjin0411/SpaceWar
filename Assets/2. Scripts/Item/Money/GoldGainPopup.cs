using DG.Tweening;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshProUGUI))]
public class GoldGainPopup : MonoBehaviour
{
    // 텍스트가 위로 이동할 거리다. UI 좌표 단위로 사용한다.
    [SerializeField, Min(0f)] private float _riseDistance = 55f;

    // 텍스트가 나타나서 완전히 사라지기까지의 시간이다.
    [SerializeField, Min(0.01f)] private float _duration = 0.7f;

    // 전체 시간 중 선명하게 보이는 구간의 비율이다.
    [SerializeField, Range(0f, 0.9f)]
    private float _fadeStartRatio = 0.2f;

    // 획득 금액을 표시할 UI 텍스트다.
    private TextMeshProUGUI _text;

    // 텍스트의 UI 위치를 조절한다.
    private RectTransform _rectTransform;

    private void Awake()
    {
        _text = GetComponent<TextMeshProUGUI>();
        _rectTransform = GetComponent<RectTransform>();

        // 연출용 텍스트가 마우스 입력을 가로막지 않게 한다.
        _text.raycastTarget = false;
    }

    // 이번 골드 한 개의 획득량을 표시하고 위로 떠오르게 한다.
    public void Play(int amount, Vector2 startPosition)
    {
        // 부모인 골드 아이콘의 중앙을 좌표 기준으로 사용한다.
        _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        _rectTransform.pivot = new Vector2(0.5f, 0.5f);
        _rectTransform.anchoredPosition3D =
            new Vector3(startPosition.x, startPosition.y, 0f);

        // 프리팹을 만들 때의 부모 크기나 회전 영향을 제거한다.
        _rectTransform.localScale = Vector3.one;
        _rectTransform.localRotation = Quaternion.identity;

        // 이번에 들어온 금액만 표시한다.
        _text.text = "+" + amount.ToString("N0");
        _text.alpha = 1f;

        float fadeDelay = _duration * _fadeStartRatio;
        float fadeDuration = _duration - fadeDelay;

        Sequence sequence = DOTween.Sequence();

        // 씬 종료 등으로 오브젝트가 제거되면 연출도 정리한다.
        sequence.SetLink(gameObject);

        // 일정한 속도로 위쪽으로 이동한다.
        sequence.Append(
            _rectTransform.DOAnchorPosY(
                startPosition.y + _riseDistance,
                _duration)
            .SetEase(Ease.Linear));

        // 처음에는 선명하게 보이고 이후 서서히 투명해진다.
        sequence.Insert(
            fadeDelay,
            _text.DOFade(0f, fadeDuration)
                .SetEase(Ease.Linear));

        // 재생이 끝난 텍스트만 제거한다.
        sequence.OnComplete(() => Destroy(gameObject));
    }
}
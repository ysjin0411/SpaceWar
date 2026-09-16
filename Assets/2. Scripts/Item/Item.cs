using UnityEngine;
using System;
using System.Collections;
using DG.Tweening;
using UnityEngine.UI;

// 아이템의 분류를 나타낸다.
public enum ItemType
{
    Material = 0,   // 일반 재료
    Quest = 1,      // 퀘스트 아이템
    Consumable = 2  // 소비 아이템
}

// 아이템 프리팹에 붙이는 기본 정보다.
// 획득과 저장은 이 컴포넌트에서 자동 실행하지 않는다.
public class Item : MonoBehaviour
{
    // 인벤토리 툴팁에 표시할 아이템 설명이다.
    [SerializeField, TextArea(3, 6)]
    private string _description;

    // 외부 UI에서 아이템 설명을 읽는다.
    public string Description => _description;

    // 저장 데이터와 퀘스트에서 사용하는 고유 ID다.
    // 같은 아이템은 항상 같은 ID를 사용한다.
    [SerializeField] private string _id;

    // 이 아이템이 속하는 분류다.
    [SerializeField] private ItemType _type;

    // 인벤토리 화면에 표시할 이름이다.
    [SerializeField] private string _displayName;

    // 인벤토리 슬롯에 표시할 이미지다.
    [SerializeField] private Sprite _icon;


    // 외부에서는 아이템 설정을 읽기만 한다.
    public string Id => _id;
    public ItemType Type => _type;
    public string DisplayName => _displayName;
    public Sprite Icon => _icon;

    // Canvas 기준으로 표시할 아이템 아이콘의 크기.
    [SerializeField, Min(1f)]
    private float _effectIconSize = 64f;

    // 보스 머리 위에서 아이템이 나타나는 시간.
    [SerializeField, Min(0.01f)]
    private float _appearDuration = 0.2f;

    // 등장한 위치에서 잠시 머무르는 시간.
    [SerializeField, Min(0f)]
    private float _appearHoldDuration = 0.15f;

    // 화면 중앙까지 이동하는 시간.
    [SerializeField, Min(0.01f)]
    private float _moveDuration = 0.6f;

    // 중앙에 도착한 뒤 확대할 배율.
    [SerializeField, Min(1f)]
    private float _emphasisScale = 1.25f;

    // 중앙에서 확대되는 시간.
    [SerializeField, Min(0.01f)]
    private float _emphasisDuration = 0.2f;

    // 확대된 상태를 유지하는 시간.
    [SerializeField, Min(0f)]
    private float _centerHoldDuration = 0.25f;

    // 투명해지면서 사라지는 시간.
    [SerializeField, Min(0.01f)]
    private float _fadeDuration = 0.5f;

    // 이번 아이템 인스턴스가 사용하는 UI 이미지.
    private Image _effectImage;

    // 오브젝트가 제거될 때 정리할 획득 연출.
    private Sequence _collectSequence;

    // 월드에서 획득한 아이템을 Canvas 위에 표시하고 연출한다.
    // 인벤토리 지급은 Drop에서 이미 끝냈으므로 여기서는 다시 지급하지 않는다.
    public IEnumerator PlayCollectEffect(
        RectTransform effectRoot,
        Vector2 startScreenPosition)
    {
        if (effectRoot == null || _icon == null)
        {
            throw new InvalidOperationException(
                $"{name}: 연출 영역과 아이템 아이콘을 확인해주세요.");
        }

        // 이 연출은 화면 전체를 덮는 Overlay Canvas를 기준으로 한다.
        Canvas canvas = effectRoot.GetComponentInParent<Canvas>();

        if (canvas == null ||
            canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            throw new InvalidOperationException(
                "아이템 연출 영역은 Screen Space - Overlay Canvas에 배치해주세요.");
        }

        // 기존 월드 스프라이트와 UI 아이콘이 동시에 보이지 않게 한다.
        foreach (SpriteRenderer renderer
                 in GetComponentsInChildren<SpriteRenderer>(true))
        {
            renderer.enabled = false;
        }

        // 화면 좌표를 연출 영역의 로컬 좌표로 변환한다.
        Vector2 startLocalPosition;
        Vector2 centerLocalPosition;

        bool startConverted =
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                effectRoot,
                startScreenPosition,
                null,
                out startLocalPosition);

        bool centerConverted =
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                effectRoot,
                new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
                null,
                out centerLocalPosition);

        if (!startConverted || !centerConverted)
        {
            throw new InvalidOperationException(
                "아이템 연출의 화면 좌표를 변환하지 못했습니다.");
        }

        // Item이 직접 사용할 표시용 이미지를 생성한다.
        // 별도의 아이템 연출 스크립트는 만들지 않는다.
        GameObject visual = new GameObject(
            $"{name}_CollectIcon",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        _effectImage = visual.GetComponent<Image>();
        _effectImage.sprite = _icon;
        _effectImage.preserveAspect = true;
        _effectImage.raycastTarget = false;
        _effectImage.color = Color.white;

        RectTransform rect = _effectImage.rectTransform;
        rect.SetParent(effectRoot, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.one * _effectIconSize;
        rect.localPosition = new Vector3(
            startLocalPosition.x, startLocalPosition.y, 0f);
        rect.localScale = Vector3.zero;
        rect.SetAsLastSibling();

        _collectSequence = DOTween.Sequence();

        // UI 획득 연출은 게임 시간이 정지해도 끝까지 재생한다.
        _collectSequence.SetUpdate(true);
        _collectSequence.SetRecyclable(false);
        _collectSequence.SetLink(gameObject);

        // 머리 위에서 작은 크기로 시작해 나타난다.
        _collectSequence.Append(
            rect.DOScale(Vector3.one, _appearDuration)
                .SetEase(Ease.OutBack));

        _collectSequence.AppendInterval(_appearHoldDuration);

        // 화면 중앙으로 이동한다.
        _collectSequence.Append(
            rect.DOLocalMove(
                    new Vector3(
                        centerLocalPosition.x,
                        centerLocalPosition.y,
                        0f),
                    _moveDuration)
                .SetEase(Ease.InOutQuad));

        // 중앙에서 살짝 확대한다.
        _collectSequence.Append(
            rect.DOScale(
                    Vector3.one * _emphasisScale,
                    _emphasisDuration)
                .SetEase(Ease.OutQuad));

        _collectSequence.AppendInterval(_centerHoldDuration);

        // 알파를 낮춰 서서히 사라지게 한다.
        _collectSequence.Append(
            _effectImage.DOFade(0f, _fadeDuration)
                .SetEase(Ease.InQuad));

        // 호출한 쪽에서 연출이 끝날 때까지 기다릴 수 있게 한다.
        yield return _collectSequence.WaitForCompletion();

        _collectSequence = null;

        Destroy(_effectImage.gameObject);
        _effectImage = null;
    }

    // 장면 전환 등으로 Item이 먼저 제거되어도 연출을 정리한다.
    private void OnDestroy()
    {
        if (_collectSequence != null)
        {
            _collectSequence.Kill();
            _collectSequence = null;
        }

        if (_effectImage != null)
        {
            Destroy(_effectImage.gameObject);
            _effectImage = null;
        }
    }
}
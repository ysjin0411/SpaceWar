using TMPro;
using UnityEngine;

// 일반 대화의 고정 기능 영역과 보상 대화의 스크롤 본문을 분리한다.
public class NpcDialogueBodyView : MonoBehaviour
{
    // 보상 대사와 보상 목록만 스크롤하는 영역.
    [SerializeField] private UnityEngine.UI.ScrollRect _rewardScroll;

    // 스크롤하지 않는 일반 대사를 배치할 영역.
    [SerializeField] private RectTransform _normalBody;

    // 퀘스트 선택과 NPC 기능 버튼을 같은 시작 위치에 배치하는 영역.
    [SerializeField] private GameObject _functionRoot;

    // 기존 타이핑 처리가 사용하는 대사 텍스트를 그대로 재사용한다.
    [SerializeField] private TMP_Text _dialogueText;

    // 일반 대화에서는 아래 기능 영역을 남기고, 퀘스트 대화는 본문을 넓게 쓴다.
    [SerializeField] private float _normalHeight = 95f;
    [SerializeField] private float _questHeight = 190f;

    // 같은 상태를 매 프레임 다시 배치하거나 스크롤 위치를 초기화하지 않는다.
    private bool _initialized;
    private bool _questDialogue;
    private bool _rewardsVisible;
    private bool _resetPending;

    // 대화 UI가 초기화되기 전에 Inspector 연결을 검사한다.
    public bool IsConfigured =>
        _rewardScroll != null &&
        _rewardScroll.content != null &&
        _rewardScroll.viewport != null &&
        _rewardScroll.verticalScrollbar != null &&
        _normalBody != null &&
        _functionRoot != null &&
        _dialogueText != null;

    // Presenter가 결정한 대화 상태를 화면 배치와 스크롤 허용 여부에 반영한다.
    public void SetMode(bool questDialogue, bool rewardsVisible)
    {
        if (!IsConfigured)
        {
            return;
        }

        if (_initialized && _questDialogue == questDialogue &&
            _rewardsVisible == rewardsVisible)
        {
            return;
        }

        _initialized = true;
        _questDialogue = questDialogue;
        _rewardsVisible = rewardsVisible;
        _rewardScroll.StopMovement();

        RectTransform textRect = _dialogueText.rectTransform;
        if (rewardsVisible)
        {
            // 대사와 보상을 동일한 Content 아래에 두어 함께 이동시킨다.
            _rewardScroll.gameObject.SetActive(true);
            textRect.SetParent(_rewardScroll.content, false);
            textRect.SetAsFirstSibling();
            textRect.localScale = Vector3.one;
            textRect.pivot = new Vector2(0.5f, 1f);
        }
        else
        {
            // 일반 대사는 마스크와 스크롤 입력에서 완전히 분리한다.
            textRect.SetParent(_normalBody, false);
            textRect.localScale = Vector3.one;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 1f);
            textRect.offsetMin = new Vector2(10f, 0f);
            textRect.offsetMax = new Vector2(-30f, -10f);
            _rewardScroll.gameObject.SetActive(false);
        }

        _normalBody.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            questDialogue ? _questHeight : _normalHeight);
        _normalBody.gameObject.SetActive(!rewardsVisible);
        _functionRoot.SetActive(!questDialogue);

        // 보상 화면에서만 스크롤바를 표시하고 실제 이동은 넘치는 경우에만 허용한다.
        _rewardScroll.horizontal = false;
        _rewardScroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
        _rewardScroll.inertia = false;
        _rewardScroll.verticalScrollbarVisibility =
            UnityEngine.UI.ScrollRect.ScrollbarVisibility.Permanent;
        _rewardScroll.enabled = rewardsVisible;
        ResetPosition();
    }

    // 새 대사나 새 보상으로 바뀐 뒤 레이아웃이 확정되면 맨 위에서 시작한다.
    public void ResetPosition()
    {
        _resetPending = true;
    }

    // 타이핑과 보상 생성이 끝난 프레임에서 높이를 갱신해 이전 대화 위치가 남지 않게 한다.
    private void LateUpdate()
    {
        if (!_resetPending || !IsConfigured)
        {
            return;
        }

        _resetPending = false;
        if (!_rewardsVisible)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_rewardScroll.content);
        _rewardScroll.StopMovement();
        _rewardScroll.verticalNormalizedPosition = 1f;

        // 짧은 보상은 바 전체가 손잡이로 표시되며 휠이나 드래그로 움직이지 않는다.
        bool overflows = _rewardScroll.content.rect.height >
            _rewardScroll.viewport.rect.height + 0.5f;
        // 세로 축을 끄면 Unity가 Permanent 스크롤바도 숨긴다.
        // 축은 유지하고 Clamped 경계로 짧은 본문의 이동을 막는다.
        _rewardScroll.vertical = true;
        _rewardScroll.verticalScrollbar.interactable = overflows;
    }
}

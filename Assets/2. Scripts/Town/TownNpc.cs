using System.Collections.Generic;
using TMPro;
using UnityEngine;

// NPC별 상호작용 범위와 안내 표시를 관리한다.
public class TownNpc : MonoBehaviour
{
    // 플레이어가 접근할 기준점. NPC의 발 위치에 배치한다.
    [SerializeField] private Transform _interactionPoint;

    // 가까이 왔을 때 켜줄 안내 오브젝트.
    [SerializeField] private GameObject _interactionHint;

    // 실제 상호작용 키와 안내 문구를 표시할 TMP 텍스트.
    [SerializeField] private TMP_Text _interactionHintText;

    // 대화창에 표시할 NPC 이름.
    [SerializeField] private string _displayName;

    // 이 NPC의 고유 기능.
    // None이면 대화창의 이용하기 버튼을 표시하지 않는다.
    [SerializeField] private NpcActionType _actionType;

    // 이 NPC의 기본 대화 설정 에셋.
    [SerializeField] private DialogueData _defaultDialogue;

    // 이 NPC가 담당하는 퀘스트들. 표시 순서는 배열 순서를 따른다.
    [SerializeField] private QuestData[] _quests = new QuestData[0];

    // 현재 Idle 스프라이트와 Flip 정보를 가져올 Visual의 렌더러.
    [SerializeField] private SpriteRenderer _dialogueVisual;

    // 기준점에서 좌우로 상호작용을 허용할 거리.
    [SerializeField, Min(0.1f)]
    private float _horizontalRange = 1.8f;

    // 다른 층의 NPC가 선택되지 않도록 발 높이 차이를 제한한다.
    [SerializeField, Min(0.1f)]
    private float _verticalRange = 0.7f;

    // 외부에서는 NPC의 표시 정보와 기본 대화를 읽기만 한다.
    public string DisplayName => _displayName;
    // Presenter가 NPC의 기능 종류를 읽는다.
    public NpcActionType ActionType => _actionType;
    public DialogueData DefaultDialogue => _defaultDialogue;
    public SpriteRenderer DialogueVisual => _dialogueVisual;

    // Presenter가 NPC의 퀘스트 목록을 읽을 때 사용한다.
    public IReadOnlyList<QuestData> Quests => _quests;

    // 상호작용에 필요한 연결을 확인하고 안내를 숨긴다.
    private void Awake()
    {
        if (_interactionPoint == null ||
            _interactionHint == null ||
            _interactionHintText == null)
        {
            Debug.LogError(
                "TownNpc의 Interaction Point, Hint, Hint Text를 연결해주세요.",
                this);

            enabled = false;
            return;
        }

        SetHintVisible(false);
    }

    // 플레이어가 범위 안에 있는지 확인하고 비교용 거리를 반환한다.
    public bool TryGetInteractionDistance(
        Vector2 playerFeetPosition, out float distanceSquared)
    {
        distanceSquared = float.PositiveInfinity;

        if (!isActiveAndEnabled || _interactionPoint == null)
        {
            return false;
        }

        Vector2 difference =
            (Vector2)_interactionPoint.position - playerFeetPosition;

        if (Mathf.Abs(difference.x) > _horizontalRange ||
            Mathf.Abs(difference.y) > _verticalRange)
        {
            return false;
        }

        // 제곱 거리만으로도 가까운 순서를 비교할 수 있다.
        distanceSquared = difference.sqrMagnitude;
        return true;
    }

    // 플레이어가 실제 사용하는 상호작용 키를 안내에 반영한다.
    public void SetInteractionKey(KeyCode key)
    {
        _interactionHintText.text = $"[{key}] 대화";
    }

    // 안내 오브젝트의 활성 상태를 변경한다.
    public void SetHintVisible(bool visible)
    {
        if (_interactionHint != null &&
            _interactionHint.activeSelf != visible)
        {
            _interactionHint.SetActive(visible);
        }
    }

    // NPC가 꺼질 때 안내도 함께 정리한다.
    private void OnDisable()
    {
        SetHintVisible(false);
    }
}
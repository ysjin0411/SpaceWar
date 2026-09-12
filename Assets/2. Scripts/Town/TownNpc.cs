using UnityEngine;

// NPC별 상호작용 범위와 안내 표시를 관리한다.
public class TownNpc : MonoBehaviour
{
    // 플레이어가 접근할 기준점. NPC의 발 위치에 배치한다.
    [SerializeField] private Transform _interactionPoint;

    // 가까이 왔을 때 켜줄 안내 오브젝트.
    [SerializeField] private GameObject _interactionHint;

    // 기준점에서 좌우로 상호작용을 허용할 거리.
    [SerializeField, Min(0.1f)]
    private float _horizontalRange = 1.8f;

    // 다른 층의 NPC가 선택되지 않도록 발 높이 차이를 제한한다.
    [SerializeField, Min(0.1f)]
    private float _verticalRange = 0.7f;

    // 필수 연결을 확인하고 안내를 숨긴 상태로 시작한다.
    private void Awake()
    {
        if (_interactionPoint == null || _interactionHint == null)
        {
            Debug.LogError(
                "TownNpc의 Interaction Point와 Interaction Hint를 연결해주세요.",
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
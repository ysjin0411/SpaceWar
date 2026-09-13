using UnityEngine;

// 가까운 NPC를 선택하고 상호작용 키로 대화를 시작한다.
[RequireComponent(
    typeof(CapsuleCollider2D), typeof(TownPlayerController))]
public class TownPlayerInteractor : MonoBehaviour
{
    // 현재 공간에서 상호작용할 NPC들.
    [SerializeField] private TownNpc[] _npcs = new TownNpc[0];

    // 대화 진행을 담당하는 Presenter.
    [SerializeField] private NpcDialoguePresenter _dialogueUI;

    // Inspector에서 변경할 수 있는 상호작용 키.
    [SerializeField] private KeyCode _interactionKey = KeyCode.E;

    // 플레이어의 발 위치와 조작 가능 상태를 확인한다.
    private CapsuleCollider2D _bodyCollider;
    private TownPlayerController _playerController;

    // 현재 안내를 표시하는 NPC.
    private TownNpc _currentNpc;

    // 필요한 컴포넌트와 연결을 준비한다.
    private void Awake()
    {
        _bodyCollider = GetComponent<CapsuleCollider2D>();
        _playerController = GetComponent<TownPlayerController>();

        if (_dialogueUI == null)
        {
            Debug.LogError(
                "TownPlayerInteractor의 Dialogue UI를 연결해주세요.", this);

            enabled = false;
        }
    }

    // 현재 위치로 대상을 갱신한 뒤 상호작용 입력을 처리한다.
    private void LateUpdate()
    {
        // 대화하거나 조작이 잠긴 동안에는 다른 NPC를 선택하지 않는다.
        if (Time.timeScale == 0f ||
            _dialogueUI.IsOpen ||
            !_playerController.CanControl)
        {
            ChangeTarget(null);
            return;
        }

        Bounds bounds = _bodyCollider.bounds;
        Vector2 feetPosition =
            new Vector2(bounds.center.x, bounds.min.y);

        TownNpc nearestNpc = null;
        float nearestDistance = float.PositiveInfinity;

        for (int i = 0; i < _npcs.Length; i++)
        {
            TownNpc npc = _npcs[i];

            if (npc == null)
            {
                continue;
            }

            if (!npc.TryGetInteractionDistance(
                    feetPosition, out float distanceSquared))
            {
                continue;
            }

            if (distanceSquared < nearestDistance)
            {
                nearestDistance = distanceSquared;
                nearestNpc = npc;
            }
        }

        ChangeTarget(nearestNpc);

        // 같은 상호작용 키를 대화 시작과 대사 넘기기에 사용한다.
        if (_currentNpc != null &&
            Input.GetKeyDown(_interactionKey) &&
            _dialogueUI.TryOpen(
                _currentNpc, _playerController, _interactionKey))
        {
            ChangeTarget(null);
        }
    }

    // 대상이 바뀌면 새 NPC의 안내 문구를 설정하고 표시한다.
    private void ChangeTarget(TownNpc nextNpc)
    {
        if (_currentNpc == nextNpc)
        {
            return;
        }

        if (_currentNpc != null)
        {
            _currentNpc.SetHintVisible(false);
        }

        _currentNpc = nextNpc;

        if (_currentNpc != null)
        {
            // 안내를 켜기 전에 실제 입력 키로 문구를 바꾼다.
            _currentNpc.SetInteractionKey(_interactionKey);
            _currentNpc.SetHintVisible(true);
        }
    }

    // 플레이어가 꺼지면 안내와 진행 중인 대화를 정리한다.
    private void OnDisable()
    {
        ChangeTarget(null);

        if (_dialogueUI != null)
        {
            _dialogueUI.Close();
        }
    }
}
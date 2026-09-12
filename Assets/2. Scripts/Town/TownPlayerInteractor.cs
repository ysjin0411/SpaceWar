using UnityEngine;

// 플레이어 주변 NPC 중 가장 가까운 하나를 상호작용 대상으로 선택한다.
[RequireComponent(typeof(CapsuleCollider2D))]
public class TownPlayerInteractor : MonoBehaviour
{
    // 현재 공간에서 상호작용할 NPC들을 Inspector에서 연결한다.
    [SerializeField] private TownNpc[] _npcs = new TownNpc[0];

    // 플레이어의 실제 발 위치를 구하는 데 사용한다.
    private CapsuleCollider2D _bodyCollider;

    // 현재 안내를 표시하고 있는 NPC.
    private TownNpc _currentNpc;

    // 플레이어 콜라이더를 준비한다.
    private void Awake()
    {
        _bodyCollider = GetComponent<CapsuleCollider2D>();
    }

    // 이동이 반영된 위치를 기준으로 가까운 NPC를 선택한다.
    private void LateUpdate()
    {
        if (Time.timeScale == 0f)
        {
            ChangeTarget(null);
            return;
        }

        Bounds bounds = _bodyCollider.bounds;

        // 캐릭터 중심이 아닌 발 위치를 NPC의 바닥 위치와 비교한다.
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
    }

    // 대상이 바뀔 때만 기존 안내를 끄고 새 안내를 켠다.
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
            _currentNpc.SetHintVisible(true);
        }
    }

    // 플레이어가 꺼지거나 공간을 떠나면 안내를 정리한다.
    private void OnDisable()
    {
        ChangeTarget(null);
    }
}
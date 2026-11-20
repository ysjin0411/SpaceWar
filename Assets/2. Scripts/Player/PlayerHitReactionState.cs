using UnityEngine;

// 플레이어 피격 애니메이션 상태의 종료를 감지한다.
public class PlayerHitReactionState : StateMachineBehaviour
{
    // 깜빡임 상태에서 빠져나갈 때 플레이어의 피격 무적을 해제한다.
    public override void OnStateExit(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex)
    {
        // Animator는 Renderer 자식에 있으므로 부모에서 플레이어를 찾는다.
        PlayerController player =
            animator.GetComponentInParent<PlayerController>();

        // 잘못된 Animator에 연결했다면 설정 오류를 알린다.
        if (player == null)
        {
            Debug.LogError(
                "PlayerHitReactionState의 부모에 PlayerController가 없습니다.",
                animator);
            return;
        }

        // 실제 깜빡임 상태의 종료와 무적 해제 시점을 맞춘다.
        player.EndHitInvulnerability();
    }
}
using UnityEngine;

/// <summary>
/// 보스 Renderer의 Animation Event를 부모 BossEnemy에 전달한다.
/// Animator와 BossEnemy가 서로 다른 게임 오브젝트에 있기 때문에 사용한다.
/// </summary>
public class BossAnimationEventRelay : MonoBehaviour
{
    // 현재 Renderer가 속한 부모 보스
    BossEnemy _bossEnemy;

    private void Awake()
    {
        // Renderer의 부모에서 실제 보스 컴포넌트를 찾는다.
        _bossEnemy = GetComponentInParent<BossEnemy>();

        if (_bossEnemy == null)
        {
            throw new MissingComponentException(
                $"{name}의 부모에서 BossEnemy를 찾을 수 없습니다.");
        }
    }

    /// <summary>
    /// 사망 애니메이션 마지막 프레임의 Animation Event가 호출한다.
    /// </summary>
    public void NotifyDeathAnimationCompleted()
    {
        _bossEnemy.CompleteDefeat();
    }
}
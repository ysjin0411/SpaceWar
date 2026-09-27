using System.Collections.Generic;
using UnityEngine;

// 폭발 순간의 범위를 검사해 실드 또는 플레이어에 한 번 피해를 준다.
public static class BossAlienExplosionDamage
{
    /// <summary>
    /// Apply 처리에서 변경된 전투 수치와 상태를 반영한다.
    /// </summary>
    public static void Apply(Vector2 center, float radius, float damage)
    {
        // 피해가 없는 설정이면 시각 효과만 재생한다.
        if (damage <= 0)
        {
            return;
        }

        // 몸과 실드만 검사하고, Trigger 콜라이더도 포함한다.
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(LayerMask.GetMask("Player", "PlayerShield"));
        filter.useTriggers = true;

        // 같은 프레임에 이동한 플레이어 위치를 검사에 반영한다.
        Physics2D.SyncTransforms();

        List<Collider2D> hits = new List<Collider2D>();
        Physics2D.OverlapCircle(center, radius, filter, hits);

        // 몸과 실드가 모두 범위 안에 있으면 실드 방어를 우선한다.
        for (int i = 0; i < hits.Count; i++)
        {
            Collider2D hit = hits[i];

            if (!hit.TryGetComponent(out Shield shield))
            {
                continue;
            }

            // 폭발 중심에서 가장 가까운 실드 위치에 방어 효과를 표시한다.
            Vector2 hitPoint = hit.ClosestPoint(center);

            if (shield.TryBlockDamage(damage, hitPoint))
            {
                return;
            }
        }

        // 실드가 막지 못했다면 범위에 들어온 플레이어 몸을 검사한다.
        for (int i = 0; i < hits.Count; i++)
        {
            Collider2D hit = hits[i];

            // 실드 콜라이더를 플레이어 몸으로 해석하지 않는다.
            if (hit.gameObject.layer == LayerMask.NameToLayer("PlayerShield"))
            {
                continue;
            }

            PlayerController player =
                hit.GetComponentInParent<PlayerController>();

            if (player == null ||
                !player.isActiveAndEnabled ||
                player.IsDead)
            {
                continue;
            }

            // 기존 피격 무적과 체력 감소 처리를 사용한다.
            player.TakeHit(damage);

            // 현재 단일 플레이어 게임에서 여러 콜라이더의 중복 피해를 막는다.
            return;
        }
    }
}
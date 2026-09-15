using System;
using UnityEngine;

public static class HitEffectUtility
{
    // 두 콜라이더의 관계를 계산하여 상대 표면의 위치를 반환한다.
    // 두 콜라이더가 활성화된 상태에서 호출해야 한다.
    public static Vector2 GetSurfacePoint(
        Collider2D source,
        Collider2D target)
    {
        ColliderDistance2D separation = source.Distance(target);

        if (!separation.isValid)
        {
            throw new InvalidOperationException(
                "히트 위치 계산에는 활성화된 두 Collider2D가 필요합니다.");
        }

        // source가 A, target이 B이므로 상대 표면인 pointB를 사용한다.
        return separation.pointB;
    }

    // 이동 경로에서 발견한 충돌 위치를 반환한다.
    public static Vector2 GetCastPoint(
        Collider2D source,
        RaycastHit2D hit)
    {
        // 검사 시작부터 겹쳤다면 현재 상대 표면을 계산한다.
        if (hit.distance <= 0f)
        {
            return GetSurfacePoint(source, hit.collider);
        }

        // 앞으로 이동하다 만나는 대상은 실제 경로 검사 결과를 사용한다.
        return hit.point;
    }
}
using UnityEngine;

/// <summary>
/// 발사된 방향으로 직선 이동하며,
/// 적을 관통하는 레이저 투사체.
/// 공격력, 수명, 충돌 처리는 Projectile 부모가 담당한다.
/// </summary>
public class Lazer : Projectile
{
    /// <summary>
    /// 레이저는 적에게 명중해도 바로 제거되지 않고 관통한다.
    /// Projectile 부모의 Life Time이 지나면 자동으로 제거된다.
    /// </summary>
    protected override bool DestroyAfterHit => false;

    /// <summary>
    /// Projectile의 FixedUpdate에서 호출된다.
    /// Weapon이 전달한 발사 방향으로 레이저를 직선 이동시킨다.
    /// </summary>
    protected override void MoveProjectile()
    {
        // 현재 진행 방향으로 레이저를 일정한 속도로 이동시킨다.
        Body.linearVelocity = Direction * MoveSpeed;
    }
}
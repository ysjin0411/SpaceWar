using UnityEngine;

/// <summary>
/// 발사된 방향으로 직선 이동하는 기본 총알.
/// 공격력, 수명, 충돌 처리는 Projectile 부모가 담당한다.
/// </summary>
public class Bullet : Projectile
{
    /// <summary>
    /// Projectile의 FixedUpdate에서 호출된다.
    /// Weapon이 전달한 발사 방향으로 총알을 직선 이동시킨다.
    /// </summary>
    protected override void MoveProjectile()
    {
        // 현재 진행 방향에 이동속도를 곱해서
        // Rigidbody2D의 이동 속도로 적용한다.
        Body.linearVelocity = Direction * MoveSpeed;
    }
}
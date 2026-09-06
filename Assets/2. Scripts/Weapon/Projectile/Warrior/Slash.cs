using UnityEngine;

// 공격력, 수명, 적 명중 처리는 부모 Projectile을 사용한다.
public class Slash : Projectile
{
    // Weapon에서 전달받은 방향으로 일정한 속도로 이동한다.
    protected override void MoveProjectile()
    {
        Body.linearVelocity = Direction * MoveSpeed;
    }
}
using System;
using UnityEngine;

/// <summary>
/// 플레이어의 공격으로 피해를 받을 수 있는 대상이 구현한다.
/// Enemy와 BossEnemy 사이에 상속 관계를 만들지 않고
/// 투사체가 두 종류를 동일하게 공격할 수 있게 한다.
/// </summary>
public interface IDamageable
{
    // 대미지를 받아 체력이 변경되었을 때 발생한다.
    event Action<float> Damaged;

    // 현재 대상이 사망 처리됐는지 외부에서 확인한다.
    bool IsDead { get; }

    // 유도탄이 대상을 추적할 때 사용할 Transform이다.
    Transform TargetTransform { get; }

    // 플레이어 공격으로 전달받은 피해를 적용한다.
    void TakeHit(float damage);
}
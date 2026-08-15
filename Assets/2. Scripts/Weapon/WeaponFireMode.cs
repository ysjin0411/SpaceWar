using UnityEngine;

/// <summary>
/// 무기가 공격을 실행하는 방식을 구분한다.
/// 무기의 이름이나 종류가 아니라 실제 발사 로직을 결정하는 값이다.
/// </summary>
public enum WeaponFireMode
{
    // 총알, 미사일처럼 프리팹이 생성된 후 직접 이동하는 방식
    Projectile,

    // 발사 지점에서 일정 시간 유지되는 펄스형 레이저 방식
    PulseBeam
}


using UnityEngine;

// 로봇 페이즈의 균열, 방어 실드, 특수 패턴 발동 규칙을 저장한다.
[CreateAssetMenu(
    menuName = "Boss/Configs/Alien Phase One",
    fileName = "BossAlienPhaseOneConfig")]
public sealed class BossAlienPhaseOneConfig : ScriptableObject
{
    // 균열 하나가 생성된 순간부터 유지되는 시간이다.
    // 균열마다 독립적으로 계산한다.
    [SerializeField, Min(0.1f)]
    private float _crackLifetime = 40f;

    // 균열이 하나 이상 있을 때 받는 피해의 감소율이다.
    // 30이면 원래 피해의 70%를 받는다.
    // 균열 수에 따라 중복 적용하지 않는다.
    [SerializeField, Range(0f, 100f)]
    private float _shieldDamageReductionPercent = 30f;

    // 전투 시작부터 첫 미사일 포위 포격을 요청하기까지의 시간이다.
    [SerializeField, Min(0f)]
    private float _barrageFirstDelay = 20f;

    // 포위 포격이 끝난 뒤 다음 포위 포격까지 기다리는 시간이다.
    // 요청이 대기 중이거나 실행 중일 때는 중복 요청하지 않는다.
    [SerializeField, Min(0.1f)]
    private float _barrageCooldown = 30f;

    // 균열은 최대 3개이며, 3중첩에서 균열 공명을 요청한다.
    public const int MaxCrackCount = 3;

    // 실행 코드가 읽을 설정값이다.
    public float CrackLifetime => _crackLifetime;
    public float ShieldDamageReductionPercent =>
        _shieldDamageReductionPercent;
    public float BarrageFirstDelay => _barrageFirstDelay;
    public float BarrageCooldown => _barrageCooldown;
}
using UnityEngine;

// 균열 3스택에서 실행할 공명 공격의 수치를 보관한다.
[CreateAssetMenu(
    menuName = "Boss/Configs/Alien Resonance",
    fileName = "BossAlienResonanceConfig")]
public sealed class BossAlienResonanceConfig : ScriptableObject
{
    [Header("순차 충격파")]

    // 각 균열의 공격 경로를 보여준 뒤 발사하기까지의 시간이다.
    [SerializeField, Min(0.1f)]
    private float _warningDuration = 0.8f;

    // 앞 균열의 발사부터 다음 균열의 예고 시작까지의 간격이다.
    [SerializeField, Min(0f)]
    private float _waveInterval = 0.35f;

    // 충격파가 충돌 면의 안쪽 방향으로 이동하는 속도다.
    [SerializeField, Min(0.1f)]
    private float _waveSpeed = 10f;

    // 이동 방향에 수직인 충격파의 전체 너비다.
    // 예고와 실제 피해 판정이 같은 값을 사용한다.
    [SerializeField, Min(0.1f)]
    private float _waveWidth = 2f;

    // 이동 방향을 기준으로 한 충격파의 두께다.
    [SerializeField, Min(0.1f)]
    private float _waveThickness = 0.4f;

    // 충격파 하나에 맞았을 때의 피해다.
    // 같은 충격파는 플레이어에게 한 번만 피해를 시도한다.
    [SerializeField, Min(1)]
    private int _waveDamage = 10;

    [Header("마무리 공격")]

    // 안전 구역과 공격 범위를 미리 보여주는 시간이다.
    [SerializeField, Min(0.1f)]
    private float _finalWarningDuration = 1.2f;

    // 마무리 공격에서 피해를 받지 않는 세로 통로의 전체 너비다.
    // 통로는 실제 전투 경계 안에 배치한다.
    [SerializeField, Min(0.1f)]
    private float _safeLaneWidth = 3f;

    // 마무리 공격의 피해 판정이 유지되는 시간이다.
    [SerializeField, Min(0.1f)]
    private float _finalActiveDuration = 0.4f;

    // 안전 통로 밖에서 마무리 공격에 맞았을 때의 피해다.
    // 마무리 공격 전체에서 한 번만 피해를 시도한다.
    [SerializeField, Min(1)]
    private int _finalDamage = 20;

    [Header("회복")]

    // 마무리 공격 후 균열과 실드를 제거한 상태로 쉬는 시간이다.
    [SerializeField, Min(0f)]
    private float _recoveryDuration = 1.5f;

    [Header("카메라 흔들림")]

    // 순차 충격파 발사 시 카메라의 최대 이동 거리다.
    [SerializeField, Min(0f)]
    private float _waveShakeStrength = 0.06f;

    // 마무리 공격 시 카메라의 최대 이동 거리다.
    [SerializeField, Min(0f)]
    private float _finalShakeStrength = 0.18f;

    // 충격파 발사 시 흔들림이 유지되는 시간이다.
    [SerializeField, Min(0.01f)]
    private float _waveShakeDuration = 0.12f;

    // 마무리 공격 시 흔들림이 유지되는 시간이다.
    [SerializeField, Min(0.01f)]
    private float _finalShakeDuration = 0.3f;

    // 실행 코드가 읽을 공격 설정이다.
    public float WarningDuration => _warningDuration;
    public float WaveInterval => _waveInterval;
    public float WaveSpeed => _waveSpeed;
    public float WaveWidth => _waveWidth;
    public float WaveThickness => _waveThickness;
    public int WaveDamage => _waveDamage;

    // 실행 코드가 읽을 마무리 공격과 회복 설정이다.
    public float FinalWarningDuration => _finalWarningDuration;
    public float SafeLaneWidth => _safeLaneWidth;
    public float FinalActiveDuration => _finalActiveDuration;
    public int FinalDamage => _finalDamage;
    public float RecoveryDuration => _recoveryDuration;

    // 카메라 연출에만 사용하며 전투 경계에는 적용하지 않는다.
    public float WaveShakeStrength => _waveShakeStrength;
    public float FinalShakeStrength => _finalShakeStrength;
    public float WaveShakeDuration => _waveShakeDuration;
    public float FinalShakeDuration => _finalShakeDuration;
}
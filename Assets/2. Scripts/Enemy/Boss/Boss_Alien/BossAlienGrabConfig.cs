using UnityEngine;

// 잡기부터 던지기까지 사용하는 동작 수치를 저장한다.
[CreateAssetMenu(
    menuName = "Boss/Configs/Alien Grab",
    fileName = "BossAlienGrabConfig")]
public sealed class BossAlienGrabConfig : ScriptableObject
{
    // 준비·잡은 뒤 이동·던지기 준비 위치로 손목을 옮기는 속도다.
    [SerializeField, Min(0.01f)]
    private float _handMoveSpeed = 8f;

    // 어깨부터 손목까지 허용하는 최대 거리다.
    [SerializeField, Min(0.01f)]
    private float _maxArmLength = 18f;

    // 조준한 플레이어 위치를 지나 추가로 뻗을 거리다.
    [SerializeField, Min(0f)]
    private float _extraReach = 1.5f;

    // 플레이어를 향해 팔을 뻗는 속도다.
    [SerializeField, Min(0.01f)]
    private float _extendSpeed = 20f;

    // 공격 종료 후 손목을 기본 위치로 복귀시키는 속도다.
    [SerializeField, Min(0.01f)]
    private float _returnSpeed = 12f;

    // 접촉한 플레이어를 GrabPoint까지 부드럽게 붙이는 시간이다.
    [SerializeField, Min(0.01f)]
    private float _attachDuration = 0.2f;

    // HandHoldPoint에서 플레이어를 잡고 흔드는 시간이다.
    [SerializeField, Min(0f)]
    private float _shakeDuration = 1.2f;

    // 흔들기 중심에서 손목이 위아래로 움직이는 최대 거리다.
    [SerializeField, Min(0f)]
    private float _shakeAmplitude = 0.15f;

    // 1초 동안 위아래 흔들기를 반복하는 횟수다.
    [SerializeField, Min(0.01f)]
    private float _shakeFrequency = 8f;

    // 손을 놓은 플레이어가 왼쪽 벽으로 날아가는 속도다.
    [SerializeField, Min(0.01f)]
    private float _throwSpeed = 22f;

    // 벽 충돌 시 적용할 피해량이다. 0이면 충돌 피해는 없다.
    [SerializeField, Min(0)]
    private float _wallImpactDamage = 10;

    // 애니메이션 이벤트가 누락됐을 때 무한 대기를 막는 제한 시간이다.
    [SerializeField, Min(0.1f)]
    private float _animationTimeout = 3f;

    // 왼쪽 방향을 기준으로 던질 각도의 최소·최대값이다.
    // 0도는 왼쪽, 양수는 위쪽, 음수는 아래쪽이다.
    // 던질 때 범위 안에서 각도를 한 번 선택하고 직선으로 날린다.
    [SerializeField]
    private Vector2 _throwAngleRange = new Vector2(-60f, 60f);

    // 첫 벽 충돌 이후 조작이 돌아오기까지의 시간이다.
    // 그동안 다른 벽에 다시 닿아도 이 시간을 갱신하지 않는다.
    [SerializeField, Min(0.01f)]
    private float _reboundDuration = 1.2f;

    // 벽에 부딪힐 때 유지할 속도의 비율이다.
    // 0.6이면 충돌 직전 속도의 60%로 반사된다.
    [SerializeField, Range(0f, 1f)]
    private float _reboundSpeedMultiplier = 0.6f;

    // 첫 충돌 이후 플레이어 외형이 초당 회전할 각도다.
    // 조작 복구 또는 중단 시 원래 회전으로 복원한다.
    [SerializeField, Min(0f)]
    private float _spinSpeed = 720f;

    // 경계 누락 등으로 첫 충돌이 발생하지 않을 때의 비행 제한 시간이다.
    // 정상적인 회복 시간과 별개로 조작 잠금이 영구히 남는 것을 막는다.
    [SerializeField, Min(0.1f)]
    private float _maxFlightDuration = 3f;

    // 패턴이 읽을 설정값이다.
    public float HandMoveSpeed => _handMoveSpeed;
    public float MaxArmLength => _maxArmLength;
    public float ExtraReach => _extraReach;
    public float ExtendSpeed => _extendSpeed;
    public float ReturnSpeed => _returnSpeed;
    public float AttachDuration => _attachDuration;
    public float ShakeDuration => _shakeDuration;
    public float ShakeAmplitude => _shakeAmplitude;
    public float ShakeFrequency => _shakeFrequency;
    public float ThrowSpeed => _throwSpeed;
    public float WallImpactDamage => _wallImpactDamage;
    public float AnimationTimeout => _animationTimeout;

    // 던지기 실행 코드가 각도와 반사·회복 설정을 읽을 때 사용한다.
    public Vector2 ThrowAngleRange => _throwAngleRange;
    public float ReboundDuration => _reboundDuration;
    public float ReboundSpeedMultiplier => _reboundSpeedMultiplier;
    public float SpinSpeed => _spinSpeed;
    public float MaxFlightDuration => _maxFlightDuration;
}
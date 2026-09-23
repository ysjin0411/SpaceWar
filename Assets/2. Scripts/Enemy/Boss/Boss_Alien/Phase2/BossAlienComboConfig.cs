using System;
using UnityEngine;

// 콤보의 설정값만 보관한다.
// 현재 공격 순서, 조준 방향, 피격 기록 같은 실행 상태는 저장하지 않는다.
[CreateAssetMenu(
    menuName = "Boss/Alien/Phase2/Combo Config",
    fileName = "BossAlienComboConfig")]
public sealed class BossAlienComboConfig : ScriptableObject
{
    // 콤보 시작 시 플레이어에게 접근하는 속도다.
    [SerializeField, Min(0.01f)]
    private float _chaseSpeed = 3f;

    // 플레이어를 계속 쫓기만 하지 않도록 추적 시간을 제한한다.
    [SerializeField, Min(0.01f)]
    private float _maxChaseDuration = 2f;

    // 보스와 플레이어 루트 사이 거리가 이 값 이하면 추적을 끝낸다.
    [SerializeField, Min(0f)]
    private float _stopDistance = 3f;

    // 추적 시 보스 루트와 전투 영역 벽 사이에 확보할 간격이다.
    [SerializeField, Min(0f)]
    private float _wallPadding = 0.2f;

    // 첫 할퀴기, 두 번째 할퀴기, 내려찍기 순서로 등록한다.
    [SerializeField]
    private StrikeSettings[] _strikes = new StrikeSettings[3]
    {
        new StrikeSettings(),
        new StrikeSettings(),
        new StrikeSettings()
    };

    // 패턴 코드에서 읽을 추적 설정이다.
    public float ChaseSpeed => _chaseSpeed;
    public float MaxChaseDuration => _maxChaseDuration;
    public float StopDistance => _stopDistance;
    public float WallPadding => _wallPadding;

    // 배열 자체를 외부에 넘기지 않고 순서대로 설정을 읽게 한다.
    public int StrikeCount => _strikes.Length;

    public StrikeSettings GetStrike(int index)
    {
        return _strikes[index];
    }

    // 한 번의 공격에 필요한 자세, 시간, 판정 범위를 묶는다.
    [Serializable]
    public sealed class StrikeSettings
    {
        // Phase2_Combo에서 사용할 준비 자세의 Label이다.
        [SerializeField]
        private string _prepareLabel;

        // 실제 타격 순간에 표시할 Label이다.
        [SerializeField]
        private string _attackLabel;

        // 공격 후 회복 자세의 Label이다.
        [SerializeField]
        private string _recoverLabel;

        // 준비 자세에서 플레이어 방향을 갱신하는 시간이다.
        [SerializeField, Min(0.01f)]
        private float _aimDuration = 0.35f;

        // 방향을 확정한 뒤 타격 전까지 기다리는 시간이다.
        // 이 시간부터 해당 타격이 끝날 때까지 재조준하지 않는다.
        [SerializeField, Min(0.01f)]
        private float _lockDuration = 0.2f;

        // 타격 자세와 공격 판정을 유지할 시간이다.
        // 같은 타격 구간에서 피해가 중복 적용되지 않도록 실행 코드가 관리한다.
        [SerializeField, Min(0.01f)]
        private float _activeDuration = 0.15f;

        // 회복 자세를 유지하는 시간이다.
        [SerializeField, Min(0.01f)]
        private float _recoveryDuration = 0.25f;

        // 해당 타격의 피해량이다.
        [SerializeField, Min(0)]
        private int _damage = 10;

        // 공격 사각형의 크기다.
        // X는 공격 방향으로의 길이, Y는 그 방향에 수직인 폭이다.
        [SerializeField]
        private Vector2 _hitboxSize = new Vector2(3f, 2.5f);

        // 보스 루트에서 공격 방향으로 판정 중심을 옮길 거리다.
        [SerializeField, Min(0f)]
        private float _forwardOffset = 1.5f;

        // 패턴에서 사용할 자세 이름이다.
        public string PrepareLabel => _prepareLabel;
        public string AttackLabel => _attackLabel;
        public string RecoverLabel => _recoverLabel;

        // 각 공격 단계의 시간이다.
        public float AimDuration => _aimDuration;
        public float LockDuration => _lockDuration;
        public float ActiveDuration => _activeDuration;
        public float RecoveryDuration => _recoveryDuration;

        // 실제 피해 판정에 사용할 설정이다.
        public int Damage => _damage;
        public Vector2 HitboxSize => _hitboxSize;
        public float ForwardOffset => _forwardOffset;
    }
}
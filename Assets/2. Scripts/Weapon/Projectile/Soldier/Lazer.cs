using System;
using UnityEngine;

// 누르는 동안 유지되며 출력에 따라 색상과 틱 피해량이 증가하는 빔이다.
public class Lazer : MonoBehaviour
{
    // 빔의 시작, 반복 몸통, 끝 부분을 함께 재생하는 Animator다.
    [SerializeField] Animator _beamAnimator;
    [SerializeField] SpriteRenderer _bodyRenderer;
    [SerializeField] Transform _endCap;
    // 충돌한 적까지만 빔을 표시한다. 기존 Enemy 레이어를 유지한다.
    [SerializeField] LayerMask _enemyLayerMask;
    [SerializeField, Min(0.1f)] float _maxDistance = 20f;
    // 저출력, 중간 출력, 최대 출력 순서의 명중 이펙트다.
    [SerializeField] SpriteOneShotEffect[] _hitEffects;

    // 초당 타격 횟수의 역수로 피해 간격을 계산한다.
    float DamageInterval => 1f / _weapon.CurrentLazerHitsPerSecond;
    float _previousDamageInterval;
    const int StageCount = 17;
    readonly int[] _stageHashes = new int[StageCount];
    WeaponRuntimeState _weapon;
    Transform _firePoint;
    float _charge;
    float _tickTimer;
    int _stage = -1;
    bool _isInitialized;

    // 화면 표시와 검증에 사용할 현재 출력 및 틱 피해량이다.
    public float ChargeRatio => _charge;
    public float CurrentTickDamage => _weapon == null ? 0f :
        _weapon.CurrentDamage * Mathf.Lerp(1f, _weapon.Data.LazerMaxDamageMultiplier, _charge);

    /// <summary>
    /// 사용할 컴포넌트와 초기 참조를 준비한다.
    /// </summary>
    private void Awake()
    {

        // 사용할 컴포넌트와 초기 참조를 준비한다.
        if (_beamAnimator == null || _bodyRenderer == null || _endCap == null ||
            _hitEffects == null || _hitEffects.Length != 3)
            throw new InvalidOperationException("Lazer visual references are incomplete.");
        foreach (SpriteOneShotEffect effect in _hitEffects)
            if (effect == null) throw new InvalidOperationException("Lazer hit effect is missing.");
        for (int i = 0; i < StageCount; i++)
            _stageHashes[i] = Animator.StringToHash("Base Layer.Stage_" + i.ToString("00"));
    }

    /// <summary>
    /// 전달받은 설정으로 실행 상태를 초기화한다.
    /// </summary>
    public void Initialize(WeaponRuntimeState weapon, Transform firePoint)
    {

        // 전달받은 설정으로 실행 상태를 초기화한다.
        if (weapon == null || firePoint == null)
            throw new ArgumentException("Lazer requires weapon state and fire point.");
        if (weapon.Data.Type != WeaponType.Lazer || weapon.CurrentDamage <= 0 ||
            weapon.CurrentLazerHitsPerSecond <= 0f || weapon.Data.LazerChargeDuration <= 0f || weapon.Data.LazerMaxDamageMultiplier < 1f)
            throw new InvalidOperationException("Lazer output settings are invalid.");
        _weapon = weapon;
        _previousDamageInterval = DamageInterval;
        _firePoint = firePoint;
        _charge = 0f;
        _tickTimer = 0f;
        _stage = -1;
        _isInitialized = true;
        UpdateBeamPosition();
        UpdateStage();
    }

    /// <summary>
    /// LateUpdate 처리에서 변경된 전투 수치와 상태를 반영한다.
    /// </summary>
    private void LateUpdate()
    {

        // LateUpdate 처리에서 변경된 전투 수치와 상태를 반영한다.
        if (!_isInitialized || Time.deltaTime <= 0f) return;
        if (_firePoint == null) { StopBeam(); return; }
        RaycastHit2D hit = UpdateBeamPosition();
        AdvanceOutput(Time.deltaTime, hit);
        UpdateStage();
    }

    /// <summary>
    /// 충돌 위치에 맞춰 빔의 방향과 길이를 갱신한다.
    /// </summary>
    private RaycastHit2D UpdateBeamPosition()
    {

        // 충돌 위치에 맞춰 빔의 방향과 길이를 갱신한다.
        Vector2 origin = _firePoint.position;
        Vector2 direction = _firePoint.right.normalized;
        transform.SetPositionAndRotation(origin,
            Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg));
        RaycastHit2D hit = Physics2D.Raycast(origin, direction, _maxDistance, _enemyLayerMask);
        float length = hit.collider != null ? hit.distance : _maxDistance;
        _bodyRenderer.size = new Vector2(Mathf.Max(0.001f, length), _bodyRenderer.size.y);
        _endCap.localPosition = new Vector3(length, 0f, 0f);
        return hit;
    }

    // 느린 프레임에서도 각 피해 주기 경계의 출력으로 소수점 피해를 계산한다.
    /// <summary>
    /// 경과 시간에 따라 출력을 올리고 타격 주기마다 피해를 적용한다.
    /// </summary>
    private void AdvanceOutput(float deltaTime, RaycastHit2D hit)
    {
        // 충전 시간은 고정하며 연사 강화 시 진행 중인 틱의 비율은 유지한다.
        float chargeDuration = _weapon.Data.LazerChargeDuration;
        float interval = DamageInterval;
        if (!Mathf.Approximately(interval, _previousDamageInterval))
        {
            _tickTimer = Mathf.Clamp01(_tickTimer / _previousDamageInterval) * interval;
            _previousDamageInterval = interval;
        }
        float remaining = deltaTime;
        while (remaining > 0f)
        {
            float step = Mathf.Min(remaining, DamageInterval - _tickTimer);
            _charge = Mathf.Clamp01(_charge + step / chargeDuration);
            _tickTimer += step;
            remaining = Mathf.Max(0f, remaining - step);
            if (_tickTimer >= DamageInterval - 0.000001f)
            {
                _tickTimer = 0f;
                DamageTarget(hit);
            }
        }
    }

    /// <summary>
    /// 유효한 대상에 현재 출력의 소수점 피해를 전달한다.
    /// </summary>
    private void DamageTarget(RaycastHit2D hit)
    {
        if (hit.collider == null) return;
        IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
        if (target == null || target.IsDead) return;
        // 타격 직전 출력으로 색상과 피해를 함께 결정한다.
        int heat = _charge >= 0.99999f ? 2 : _charge >= 0.5f ? 1 : 0;
        Instantiate(_hitEffects[heat], hit.point, Quaternion.identity);
        target.TakeHit(CurrentTickDamage);
    }

    /// <summary>
    /// 현재 출력에 맞는 레이저 색상 단계를 표시한다.
    /// </summary>
    private void UpdateStage()
    {
        int stage = _charge >= 0.99999f ? StageCount - 1 :
            Mathf.Min(StageCount - 2, Mathf.FloorToInt(_charge * (StageCount - 1)));
        if (stage == _stage) return;
        // 색 단계가 바뀌어도 반복 애니메이션의 시간은 이어 간다.
        float phase = _stage < 0 ? 0f : Mathf.Repeat(_beamAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1f);
        _stage = stage;
        _beamAnimator.Play(_stageHashes[stage], 0, phase);
        _beamAnimator.Update(0f);
    }

    /// <summary>
    /// 지속 빔을 종료하고 출력과 타격 타이머를 초기화한다.
    /// </summary>
    public void StopBeam()
    {
        // Destroy 처리 전 같은 프레임에 피해가 들어가는 것을 막는다.
        _isInitialized = false;
        _charge = 0f;
        _tickTimer = 0f;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}

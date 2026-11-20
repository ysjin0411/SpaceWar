using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 모든 플레이어 캐릭터가 공통으로 사용하는 컨트롤러.
/// 모든 캐릭터가 이 스크립트를 사용한다.
/// 캐릭터별 체력과 이동속도는 CharacterData를 통해 전달받는다.
/// PlayerController - 이동, 체력, 피격
/// </summary>
public class PlayerController : MonoBehaviour, IDamageable
{
    [Header("런타임 이동 데이터")]
    [SerializeField] float _moveSpeed;      // 캐릭터 이동속도

    [Header("런타임 체력 데이터")]
    [SerializeField] float _maxHp;            // 최대 체력
    [SerializeField] float _currentHp;        // 현재 체력

    [Header("UI")]
    [SerializeField] HPUI _hpUI;

    // 잡기·던지기처럼 외부 코드가 플레이어 위치를 관리하는 동안 유지한다.
    private bool _hasForcedMovement;

    // 이동 권한을 얻은 객체만 위치 변경과 해제를 할 수 있다.
    private MonoBehaviour _forcedMovementOwner;

    // 피격 후 깜빡임이 끝날 때까지 추가 피해를 막는다.
    private bool _isHitInvulnerable;

    Vector2 _moveDirection;         // Update에서 입력받고 FixedUpdate에서 사용할 이동 방향

    bool _canControl = true;        // 이동 가능 여부

    // 흡인 등 외부 효과가 전달한 이동 속도를 효과별로 보관한다.
    readonly Dictionary<MonoBehaviour, Vector2> _externalVelocities =
        new Dictionary<MonoBehaviour, Vector2>();

    Rigidbody2D _rigidbody;
    // 무기를 바꿔도 누적 이동속도 증강은 유지한다.
    float _moveSpeedBonus;
    Weapon _weapon;
    // 무기가 없으면 CharacterData에서 읽은 기본 속도를 사용한다.
    public float CurrentMoveSpeed => Mathf.Max(0f,
        (_weapon != null && _weapon.CurrentWeapon != null
            ? _weapon.CurrentWeapon.Data.WeaponMoveSpeed : _moveSpeed) + _moveSpeedBonus);


    // 이동 가능한 화면 영역
    const float MinX = -8f;
    const float MaxX = 8f;
    const float MinY = -4f;
    const float MaxY = 4f;

    // 플레이어와 필드 아이템이 공유하는 월드 이동 범위다.
    public static Rect MovementBounds =>
        Rect.MinMaxRect(MinX, MinY, MaxX, MaxY);


    public float MaxHp => _maxHp;
    public float CurrentHp => _currentHp;

    // 기본 조작 허용 상태와 강제 이동 상태를 함께 반영한다.
    public bool CanControl =>
        _canControl && !_hasForcedMovement && !IsDead;
    public bool IsDead => _currentHp <= 0;

    // 조작 가능한 상태에서 이동 방향이 입력되고 있는지 알려준다.
    public bool HasMoveInput =>
        CanControl && _moveDirection.sqrMagnitude > 0.001f;
    public Transform TargetTransform => transform;  // 캐릭터의 위치 전달

    // 캐릭터가 데미지 입었을때 이벤트
    public event Action<float> Damaged;

    // 캐릭터가 사망했을 때 스테이지 시스템에 알린다.
    public event Action Died;

    // 이동 연출 등으로 조작 가능 상태가 바뀌면 무기에도 알린다.
    public event Action<bool> ControlStateChanged;



    /// <summary>
    /// 사용할 컴포넌트와 초기 참조를 준비한다.
    /// </summary>
    private void Awake()
    {
        // Player 프리팹에 있는 Rigidbody2D를 가져온다.
        _rigidbody = GetComponent<Rigidbody2D>();
        _weapon = GetComponent<Weapon>();

        // Rigidbody2D가 없다면 플레이어를 이동시킬 수 없다.
        if (_rigidbody == null)
        {
            throw new MissingComponentException($"{name}에 Rigidbody2D가 없습니다.");
        }
    }

    /// <summary>
    /// 초기화된 데이터를 화면과 실행 상태에 반영한다.
    /// </summary>
    private void Start()
    {
        // HpUI 시작 업데이트
        UpdateHpUI();
    }

    /// <summary>
    /// StageManager가 캐릭터 프리팹을 생성한 직후 호출
    /// 선택한 CharacterData의 기본 능력치를 실제 전투용 런타임 값으로 복사한다.
    /// </summary>
    /// <param name="characterData">선택한 캐릭터 원본 데이터</param><param name="hpUI">Stage에 존재하는 체력 UI</param>
    public void Initialize(CharacterData characterData, HPUI hpUI)
    {
        // 캐릭터 데이터 없음
        if (characterData == null)
        {
            throw new ArgumentNullException(nameof(characterData));
        }

        // 캐릭터 초기화 시 이전 피격 무적 상태를 초기화한다.
        _isHitInvulnerable = false;

        // 캐릭터 원본 데이터에서 기본 능력치를 가져온다.
        _maxHp = characterData.BaseHp;
        _currentHp = _maxHp;
        _moveSpeed = characterData.BaseMoveSpeed;
        _moveSpeedBonus = 0f;

        // 프리팹이 직접 참조할 수 없는 Stage의 UI를 전달받는다.
        _hpUI = hpUI;

        UpdateHpUI();
    }

    /// <summary>
    /// 매 프레임 입력과 경과 시간에 따른 상태를 갱신한다.
    /// </summary>
    private void Update()
    {
        // 이동을 맡은 객체가 사라지거나 비활성화되면 잠금을 정리한다.
        if (_hasForcedMovement &&
            (_forcedMovementOwner == null ||
             !_forcedMovementOwner.isActiveAndEnabled))
        {
            ReleaseForcedMovement();
        }

        // 강제 이동 중에는 새 이동 입력을 받지 않는다.
        if (!CanControl)
        {
            _moveDirection = Vector2.zero;
            return;
        }

        // 방향키 입력 받기
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        // 대각선 이동이 더 빨라지지 않도록 방향 벡터를 정규화한다.
        _moveDirection = new Vector2(horizontal, vertical).normalized;
    }

    /// <summary>
    /// FixedUpdate 처리에서 변경된 전투 수치와 상태를 반영한다.
    /// </summary>
    private void FixedUpdate()
    {
        // 강제 이동 중에는 일반 이동과 화면 경계 보정을 적용하지 않는다.
        if (!CanControl)
        {
            _rigidbody.linearVelocity = Vector2.zero;
            return;
        }

        // 플레이어가 직접 입력한 이동 속도다.
        Vector2 velocity = _moveDirection * CurrentMoveSpeed;

        // 활성 상태인 외부 효과의 이동 속도를 합산한다.
        foreach (KeyValuePair<MonoBehaviour, Vector2> entry
                 in _externalVelocities)
        {
            if (entry.Key != null && entry.Key.isActiveAndEnabled)
            {
                velocity += entry.Value;
            }
        }

        _rigidbody.linearVelocity = velocity;

        // 기존 화면 경계 제한을 유지한다.
        Vector2 clampedPosition = _rigidbody.position;
        clampedPosition.x = Mathf.Clamp(clampedPosition.x, MinX, MaxX);
        clampedPosition.y = Mathf.Clamp(clampedPosition.y, MinY, MaxY);
        _rigidbody.position = clampedPosition;
    }

    /// <summary>
    /// 외부 효과가 이번 물리 프레임에 적용할 이동 속도를 전달한다.
    /// 같은 효과는 기존 값을 갱신하므로 계속 누적되지 않는다.
    /// </summary>
    public void SetExternalVelocity(MonoBehaviour source, Vector2 velocity)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        _externalVelocities[source] = velocity;
    }

    /// <summary>
    /// 종료되거나 범위를 벗어난 외부 효과의 이동 속도를 제거한다.
    /// </summary>
    public void RemoveExternalVelocity(MonoBehaviour source)
    {
        if (source != null)
        {
            _externalVelocities.Remove(source);
        }
    }

    // 입장·구간 전환 등 기존 시스템의 조작 허용 상태를 변경한다.
    /// <summary>
    /// 입장·구간 전환 등 기존 시스템의 조작 허용 상태를 변경한다.
    /// </summary>
    public void SetControlEnabled(bool canControl)
    {
        _canControl = canControl;
        NotifyControlState();
    }

    // 최종 조작 가능 상태를 무기에도 전달한다.
    /// <summary>
    /// 최종 조작 가능 상태를 무기에도 전달한다.
    /// </summary>
    private void NotifyControlState()
    {
        _moveDirection = Vector2.zero;

        if (!CanControl)
        {
            _rigidbody.linearVelocity = Vector2.zero;
        }

        if (ControlStateChanged != null)
        {
            ControlStateChanged.Invoke(CanControl);
        }
    }

    // 일반 총알·몸박 등의 피해를 처리한다.
    // 잡기·던지기·반사 이동 중에는 CanControl이 false이므로 차단한다.
    /// <summary>
    /// 소수점 피해를 반영하고 피격 및 사망 처리를 실행한다.
    /// </summary>
    public void TakeHit(float damage)
    {
        // 기존 IDamageable 호출은 일반 피격 연출을 사용한다.
        TakeHit(damage, false);
    }

    /// <summary>기존 피격 무적 규칙을 유지하며 반복 공격의 화면 반응만 약하게 지정한다.</summary>
    public void TakeHit(float damage, bool continuousFeedback)
    {
        // 소수점 피해를 반영하고 피격 및 사망 처리를 실행한다.
        if (!CanControl || _isHitInvulnerable || damage <= 0)
        {
            return;
        }

        ApplyDamage(damage, true, continuousFeedback);
    }

    // 지속 피해도 조작이 복구되기 전까지 차단한다.
    // 조작 가능 상태에서는 기존처럼 일반 피격 무적을 무시한다.
    /// <summary>
    /// 조작 가능 상태에서는 기존처럼 일반 피격 무적을 무시한다.
    /// </summary>
    public void TakeContinuousDamage(float damage)
    {
        // 조작 가능 상태에서는 기존처럼 일반 피격 무적을 무시한다.
        if (!CanControl || damage <= 0)
        {
            return;
        }

        ApplyDamage(damage, false, true);
    }

    // 일반 피해와 지속 피해의 체력 감소, UI 갱신, 사망 처리를 공유한다.
    /// <summary>
    /// 일반 피해와 지속 피해의 체력 감소, UI 갱신, 사망 처리를 공유한다.
    /// </summary>
    private void ApplyDamage(float damage, bool playHitReaction, bool continuousFeedback = false)
    {
        // 체력이 음수가 되지 않도록 제한한다.
        float previousHp = _currentHp;
        _currentHp = Mathf.Max(0, _currentHp - damage);

        // 무적·조작 제한을 통과해 실제 체력이 줄어든 피해만 카메라에 전달한다.
        // 마지막 타격도 원래 피해량으로 비교해 남은 체력에 따라 분류가 바뀌지 않는다.
        if (_currentHp < previousHp)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                CameraController controller = mainCamera.GetComponent<CameraController>();
                if (controller != null) controller.CameraShake(damage, continuousFeedback);
            }
            // 반복 피격음의 최소 간격은 오디오 카탈로그에서 제한한다.
            if (_currentHp > 0) GameAudio.Play(GameSound.PlayerHurt);
        }

        // 일반 피해를 받고 살아남았을 때만 피격 애니메이션을 요청한다.
        // 지속 피해마다 애니메이션을 다시 시작하지 않도록 구분한다.
        if (_currentHp > 0 && playHitReaction)
        {
            // 이벤트 처리 중 다른 공격이 들어와도 중복 피격을 막는다.
            _isHitInvulnerable = true;

            if (Damaged != null)
            {
                Damaged.Invoke(damage);
            }
        }

        // 두 종류의 피해 모두 기존 체력 UI에 반영한다.
        UpdateHpUI();

        // 지속 피해로 체력이 소진되어도 기존 사망 처리를 실행한다.
        if (_currentHp <= 0)
        {
            Die();
        }
    }

    // 깜빡임 상태가 끝나면 다시 피해를 받을 수 있게 한다.
    /// <summary>
    /// 깜빡임 상태가 끝나면 다시 피해를 받을 수 있게 한다.
    /// </summary>
    public void EndHitInvulnerability()
    {
        _isHitInvulnerable = false;
    }

    /// <summary>
    /// 현재 체력 상태를 HP UI에 반영한다.
    /// </summary>
    private void UpdateHpUI()
    {
        if (_hpUI == null)
        {
            return;
        }

        _hpUI.UpdateHpUI();
    }

    /// <summary>
    /// 플레이어 조작을 잠그고 사망 사실을 스테이지 시스템에 전달한다.
    /// </summary>
    private void Die()
    {
        GameAudio.Play(GameSound.PlayerDeath); // 결과 일시정지에도 끝까지 재생.
        // 입력과 현재 이동 속도를 함께 초기화한다.
        SetControlEnabled(false);

        // 사망 후에는 잡기·던지기의 이동 권한도 남기지 않는다.
        ReleaseForcedMovement();

        // 사망 후에는 흡인 등 외부 이동 효과도 유지하지 않는다.
        _externalVelocities.Clear();

        // 사망한 순간 전투를 정지한다.
        Time.timeScale = 0f;

        // 결과창을 직접 참조하지 않고 사망 사실만 알린다.
        if (Died != null)
        {
            Died.Invoke();
        }
    }

    /// <summary>
    /// 전투 중 이동속도 증강을 적용한다.
    /// </summary><param name="value">추가할 이동속도</param>
    public void AddMoveSpeed(float value)
    {
        // 무기의 기본값을 변경하지 않고 증강분만 누적한다.
        _moveSpeedBonus += value;
    }

    /// <summary>
    /// 최대체력을 증가시키고 현재 체력을 새로운 최대체력까지 회복한다.
    /// </summary>
    /// <param name="amount">증가할 최대체력</param>
    public void IncreaseMaxHpAndFullHeal(float amount)
    {
        if (amount <= 0)
        {
            return;
        }

        // 최대체력을 먼저 증가시킨다.
        _maxHp += amount;

        // 증가된 최대체력까지 현재 체력을 완전히 회복한다.
        _currentHp = _maxHp;

        // 변경된 현재체력과 최대체력을 즉시 화면에 반영한다.
        UpdateHpUI();
    }

    // 잡기에 성공한 객체가 플레이어의 이동 권한을 요청한다.
    /// <summary>
    /// 잡기에 성공한 객체가 플레이어의 이동 권한을 요청한다.
    /// </summary>
    public bool TryBeginForcedMovement(MonoBehaviour owner)
    {
        if (owner == null)
        {
            throw new ArgumentNullException(nameof(owner));
        }

        // 다른 연출·잡기·사망 상태에서는 새 이동 권한을 주지 않는다.
        if (!isActiveAndEnabled ||
            !owner.isActiveAndEnabled ||
            !CanControl)
        {
            return false;
        }

        _forcedMovementOwner = owner;
        _hasForcedMovement = true;

        // 이동 입력과 진행 중인 무기 공격을 중단한다.
        NotifyControlState();
        return true;
    }

    // 권한을 가진 객체만 잡기·던지기의 월드 위치를 전달할 수 있다.
    /// <summary>
    /// 권한을 가진 객체만 잡기·던지기의 월드 위치를 전달할 수 있다.
    /// </summary>
    public bool TrySetForcedPosition(MonoBehaviour owner, Vector2 position)
    {
        if (!_hasForcedMovement ||
            owner == null ||
            owner != _forcedMovementOwner ||
            !owner.isActiveAndEnabled ||
            !isActiveAndEnabled ||
            !_canControl ||
            IsDead)
        {
            return false;
        }

        _rigidbody.linearVelocity = Vector2.zero;
        _rigidbody.position = position;

        // 같은 프레임의 표시 위치도 맞추고 기존 Z 좌표는 유지한다.
        transform.position = new Vector3(
            position.x, position.y, transform.position.z);

        return true;
    }

    // 정상 종료나 패턴 취소 시 권한을 가진 객체가 호출한다.
    /// <summary>
    /// 정상 종료나 패턴 취소 시 권한을 가진 객체가 호출한다.
    /// </summary>
    public void EndForcedMovement(MonoBehaviour owner)
    {
        if (!_hasForcedMovement ||
            owner == null ||
            owner != _forcedMovementOwner)
        {
            return;
        }

        ReleaseForcedMovement();
    }

    // 소유자 제거·사망·비활성화에서도 공통으로 사용하는 정리다.
    /// <summary>
    /// 소유자 제거·사망·비활성화에서도 공통으로 사용하는 정리다.
    /// </summary>
    private void ReleaseForcedMovement()
    {
        if (!_hasForcedMovement)
        {
            return;
        }

        _hasForcedMovement = false;
        _forcedMovementOwner = null;
        _rigidbody.linearVelocity = Vector2.zero;

        // 벽 충돌의 피격 무적이 남아 있어도 조작 복구 시에는 해제한다.
        // 이후 새로 받는 일반 공격은 기존 피격 무적 규칙을 따른다.
        _isHitInvulnerable = false;

        // 손이 화면 밖에 있는 순간 취소돼도 플레이어는 이동 영역으로 복귀한다.
        Vector2 position = _rigidbody.position;
        position.x = Mathf.Clamp(position.x, MinX, MaxX);
        position.y = Mathf.Clamp(position.y, MinY, MaxY);

        _rigidbody.position = position;
        transform.position = new Vector3(
            position.x, position.y, transform.position.z);

        // 기본 조작 허용 상태는 변경하지 않는다.
        NotifyControlState();
    }

    // 플레이어가 비활성화되면 남은 이동 권한을 정리한다.
    /// <summary>
    /// 플레이어가 비활성화되면 남은 이동 권한을 정리한다.
    /// </summary>
    private void OnDisable()
    {
        ReleaseForcedMovement();
    }

    // 강제 이동을 관리하는 객체가 해당 패턴의 피해를 적용한다.
    // 외부 피해 차단과 기존 피격 무적을 무시하지만,
    // 이동 권한이 없거나 플레이어가 사망한 경우에는 적용하지 않는다.
    /// <summary>
    /// 이동 권한이 없거나 플레이어가 사망한 경우에는 적용하지 않는다.
    /// </summary>
    public void ApplyForcedMovementDamage(MonoBehaviour owner, float damage)
    {
        if (!isActiveAndEnabled ||
            !_hasForcedMovement ||
            owner == null ||
            owner != _forcedMovementOwner ||
            !owner.isActiveAndEnabled ||
            !_canControl ||
            IsDead ||
            damage <= 0)
        {
            return;
        }

        // 기존 체력 감소·피격 연출·UI 갱신·사망 처리를 사용한다.
        ApplyDamage(damage, true);
    }
}

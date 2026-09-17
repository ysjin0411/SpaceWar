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
    [SerializeField] int _maxHp;            // 최대 체력
    [SerializeField] int _currentHp;        // 현재 체력

    [Header("UI")]
    [SerializeField] HPUI _hpUI;

    // 피격 후 깜빡임이 끝날 때까지 추가 피해를 막는다.
    private bool _isHitInvulnerable;

    Vector2 _moveDirection;         // Update에서 입력받고 FixedUpdate에서 사용할 이동 방향

    bool _canControl = true;        // 이동 가능 여부

    // 흡인 등 외부 효과가 전달한 이동 속도를 효과별로 보관한다.
    readonly Dictionary<MonoBehaviour, Vector2> _externalVelocities =
        new Dictionary<MonoBehaviour, Vector2>();

    Rigidbody2D _rigidbody;


    // 이동 가능한 화면 영역
    const float MinX = -8f;
    const float MaxX = 8f;
    const float MinY = -4f;
    const float MaxY = 4f;


    public int MaxHp => _maxHp;
    public int CurrentHp => _currentHp;
    public bool CanControl => _canControl;
    public bool IsDead => _currentHp <= 0;    
    public Transform TargetTransform => transform;  // 캐릭터의 위치 전달

    // 캐릭터가 데미지 입었을때 이벤트
    public event Action<int> Damaged;

    // 캐릭터가 사망했을 때 스테이지 시스템에 알린다.
    public event Action Died;

    // 이동 연출 등으로 조작 가능 상태가 바뀌면 무기에도 알린다.
    public event Action<bool> ControlStateChanged;



    private void Awake()
    {
        // Player 프리팹에 있는 Rigidbody2D를 가져온다.
        _rigidbody = GetComponent<Rigidbody2D>();

        // Rigidbody2D가 없다면 플레이어를 이동시킬 수 없다.
        if (_rigidbody == null)
        {
            throw new MissingComponentException($"{name}에 Rigidbody2D가 없습니다.");
        }
    }

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

        // 프리팹이 직접 참조할 수 없는 Stage의 UI를 전달받는다.
        _hpUI = hpUI;

        UpdateHpUI();
    }

    private void Update()
    {
        // 입장이나 구간 전환 연출 중에는 플레이어 입력을 받지 않는다.
        if (!_canControl)
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

    private void FixedUpdate()
    {
        // 이동 연출과 사망 중에는 입력과 외부 이동을 모두 적용하지 않는다.
        if (!_canControl || IsDead)
        {
            _rigidbody.linearVelocity = Vector2.zero;
            return;
        }

        // 플레이어가 직접 입력한 이동 속도다.
        Vector2 velocity = _moveDirection * _moveSpeed;

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

    // 이 아래에는 기존 SetControlEnabled 메서드가 이어진다.

    public void SetControlEnabled(bool canControl)
    {
        // 플레이어의 조작 허용 상태를 변경한다.
        _canControl = canControl;

        // 잠금 이전의 이동 입력이 남지 않도록 초기화한다.
        _moveDirection = Vector2.zero;

        if (!_canControl)
        {
            // 연출 중에는 플레이어 입력으로 움직이지 않는다.
            _rigidbody.linearVelocity = Vector2.zero;
        }

        // 무기가 진행 중인 공격을 즉시 중단할 수 있도록 알린다.
        if (ControlStateChanged != null)
        {
            ControlStateChanged.Invoke(_canControl);
        }
    }

    // 일반 공격의 피해를 처리한다.
    // 피격 애니메이션 중에는 추가 일반 피해를 받지 않는다.
    public void TakeHit(int damage)
    {
        if (!_canControl || IsDead || _isHitInvulnerable || damage <= 0)
        {
            return;
        }

        ApplyDamage(damage, true);
    }

    // 투사체에 접촉한 시간에 따라 발생하는 지속 피해를 처리한다.
    // 일반 피격 무적 시간의 영향을 받지 않으며,
    // 이동 연출 중이거나 이미 사망했다면 피해를 받지 않는다.
    public void TakeContinuousDamage(int damage)
    {
        if (!_canControl || IsDead || damage <= 0)
        {
            return;
        }

        ApplyDamage(damage, false);
    }

    // 일반 피해와 지속 피해의 체력 감소, UI 갱신, 사망 처리를 공유한다.
    private void ApplyDamage(int damage, bool playHitReaction)
    {
        // 체력이 음수가 되지 않도록 제한한다.
        _currentHp = Mathf.Max(0, _currentHp - damage);

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
        // 입력과 현재 이동 속도를 함께 초기화한다.
        SetControlEnabled(false);

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
        _moveSpeed += value;
    }

    /// <summary>
    /// 최대체력을 증가시키고 현재 체력을 새로운 최대체력까지 회복한다.
    /// </summary>
    /// <param name="amount">증가할 최대체력</param>
    public void IncreaseMaxHpAndFullHeal(int amount)
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
}
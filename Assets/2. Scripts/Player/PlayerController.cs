using System;
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

    Vector2 _moveDirection;         // Update에서 입력받고 FixedUpdate에서 사용할 이동 방향

    bool _canControl = true;        // 이동 가능 여부

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
        // 연출 중에는 PlayerController가 플레이어 위치에 간섭하지 않는다.
        // 위치 제한도 건너뛰어야 플레이어가 화면 밖으로 이동할 수 있다.
        if (!_canControl)
        {
            _rigidbody.linearVelocity = Vector2.zero;
            return;
        }

        // 물리 프레임에서 플레이어를 이동시킨다.
        _rigidbody.linearVelocity = _moveDirection * _moveSpeed;

        // 이동 후 Stage 범위를 벗어나지 않도록 위치를 제한한다.
        Vector2 clampedPosition = _rigidbody.position;
        clampedPosition.x = Mathf.Clamp(clampedPosition.x, MinX, MaxX);
        clampedPosition.y = Mathf.Clamp(clampedPosition.y, MinY, MaxY);

        // 실제 내 위치
        _rigidbody.position = clampedPosition;
    }

    /// <summary>
    /// 플레이어의 직접 이동 조작 가능 여부를 변경한다.
    /// 입장·퇴장 연출에서는 false, 전투 시작 시에는 true를 전달한다.
    /// </summary>
    public void SetControlEnabled(bool canControl)
    {
        _canControl = canControl;

        // 이전 프레임의 입력 방향이 연출 종료 후 다시 적용되지 않게 초기화한다.
        _moveDirection = Vector2.zero;

        // 조작을 잠글 때 현재 이동 속도도 즉시 제거한다.
        if (!_canControl)
        {
            _rigidbody.linearVelocity = Vector2.zero;
        }
    }

    /// <summary>
    /// 적 또는 적 투사체로부터 피해를 받는다.
    /// </summary>
    /// <param name="damage">받을 피해량</param>
    public void TakeHit(int damage)
    {
        // 0 이하의 피해는 처리하지 않는다.
        if (damage <= 0)
        {
            return;
        }

        // 현재 체력이 음수가 되지 않도록 제한한다.
        _currentHp = Mathf.Max(0, _currentHp - damage);

        // 살아남은 피격일 때만 피격 애니메이션 이벤트를 발생시킨다.
        // 사망 시 Time.timeScale이 0이 되므로 사망 순간 점멸은 재생하지 않는다.
        if (_currentHp > 0)
        {
            Damaged?.Invoke(damage);
        }

        // HPUI 업데이트
        UpdateHpUI();

        Debug.Log($"입은 데미지: {damage}");

        // 사망처리
        if (_currentHp <= 0)
        {
            Die();
        }
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
    /// 플레이어 사망 처리.
    /// 현재는 게임 시간을 정지한다.
    /// </summary>
    private void Die()
    {
        // 사망한 플레이어가 계속 움직이지 않도록 속도를 제거한다.
        _rigidbody.linearVelocity = Vector2.zero;

        Debug.Log("사망");

        Time.timeScale = 0f;
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
using System;
using UnityEngine;

/// <summary>
/// 모든 플레이어 캐릭터가 공통으로 사용하는 컨트롤러.
/// 모든 캐릭터가 이 스크립트를 사용한다.
/// 캐릭터별 체력과 이동속도는 CharacterData를 통해 전달받는다.
/// 무기와 투사체 생성은 Weapon 컴포넌트가 담당하므로 PlayerController는 이동, 체력, 피격만 담당한다.
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("런타임 이동 데이터")]
    [Tooltip("현재 캐릭터의 이동속도")]
    [SerializeField] float _moveSpeed;

    [Header("런타임 체력 데이터")]
    [Tooltip("현재 캐릭터의 최대 체력")]
    [SerializeField] int _maxHp;
    [Tooltip("현재 캐릭터의 체력")]
    [SerializeField] int _currentHp;

    [Header("Stage UI")]
    [Tooltip("현재 캐릭터의 체력을 표시할 Stage HP UI")]
    [SerializeField] HPUI _hpUI;

    // 외부 UI가 현재 체력 정보를 읽을 수 있게 제공한다.
    public int MaxHp => _maxHp;
    public int CurrentHp => _currentHp;

    // 이동 가능한 Stage 영역
    const float MinX = -8f;
    const float MaxX = 8f;
    const float MinY = -4f;
    const float MaxY = 4f;

    // Update에서 입력받고 FixedUpdate에서 사용할 이동 방향
    Vector2 _moveDirection;

    Rigidbody2D _rigidbody;

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
        // 아직 StageManager에서 초기화하지 않은 테스트 상황을 위해
        // Inspector의 최대 체력으로 현재 체력을 초기화한다.
        if (_currentHp <= 0)
        {
            _currentHp = _maxHp;
        }

        UpdateHpUI();
    }

    /// <summary>
    /// StageManager가 캐릭터 프리팹을 생성한 직후 호출한다.
    /// 선택한 CharacterData의 기본 능력치를 실제 전투용 런타임 값으로 복사한다.
    /// </summary>
    /// <param name="characterData">
    /// 유저가 선택한 캐릭터 원본 데이터
    /// </param>
    /// <param name="hpUI">
    /// Stage에 존재하는 체력 UI
    /// </param>
    public void Initialize(CharacterData characterData, HPUI hpUI)
    {
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
        // 방향키 입력을 받는다.
        float horizontal = Input.GetAxisRaw("Horizontal");

        float vertical = Input.GetAxisRaw("Vertical");

        // 대각선 이동이 더 빨라지지 않도록 방향 벡터를 정규화한다.
        _moveDirection = new Vector2(horizontal, vertical).normalized;
    }

    private void FixedUpdate()
    {
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

        // HPUI 업데이트
        UpdateHpUI();

        Debug.Log($"입은 데미지: {damage}");

        // 체력이 정확히 0이 되어도 사망 처리해야 한다.
        if (_currentHp <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// 현재 체력 상태를 HP UI에 반영한다.
    /// 아직 UI가 연결되지 않았다면 건너뛴다.
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
    /// </summary>
    /// <param name="value">추가할 이동속도</param>
    public void AddMoveSpeed(float value)
    {
        _moveSpeed += value;
    }
}
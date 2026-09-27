using System;
using UnityEngine;

public class Tower : MonoBehaviour, IDamageable
{
    // 타워마다 Inspector에서 서로 다른 최대 체력을 설정한다.
    [SerializeField, Min(1)] private float _maxHp = 100;

    // 자신이 속한 보스의 전투 시작 여부를 확인한다.
    private BossEnemy _boss;

    // 현재 체력을 보관한다.
    private float _currentHp;

    // 다른 컴포넌트에서 먼저 초기화하더라도 체력을 중복 초기화하지 않는다.
    private bool _isInitialized;

    // 피격 연출을 위한 기존 IDamageable 이벤트다.
    public event Action<float> Damaged;

    // 체력이 변하면 합산 실드 컴포넌트에 알린다.
    public event Action HealthChanged;

    // 타워가 파괴되면 공격 중단 등의 처리를 연결할 수 있다.
    public event Action<Tower> Destroyed;

    // 외부에서는 체력 값을 읽기만 할 수 있다.
    public float MaxHp => _maxHp;

    public float CurrentHp
    {
        get
        {
            InitializeHealth();
            return _currentHp;
        }
    }

    // 기존 무기에서 사망 여부와 타겟 위치를 확인할 때 사용한다.
    public bool IsDead => CurrentHp <= 0;
    public Transform TargetTransform => transform;

    /// <summary>
    /// 컴포넌트 참조와 초기 실행 상태를 준비한다.
    /// </summary>
    private void Awake()
    {
        // 타워가 속한 최상단 보스를 찾는다.
        _boss = GetComponentInParent<BossEnemy>();

        if (_boss == null)
        {
            throw new InvalidOperationException(
                $"{name}: 부모에 BossEnemy가 필요합니다.");
        }

        InitializeHealth();
    }

    // 부모와 자식의 Awake 실행 순서에 관계없이 한 번만 초기화한다.
    /// <summary>
    /// 부모와 자식의 Awake 실행 순서에 관계없이 한 번만 초기화한다.
    /// </summary>
    public void InitializeHealth()
    {
        if (_isInitialized)
            return;

        _maxHp = Mathf.Max(1, _maxHp);
        _currentHp = _maxHp;
        _isInitialized = true;
    }

    // 기존 플레이어 무기가 호출하는 피해 처리 함수다.
    /// <summary>
    /// 소수점 피해를 반영하고 피격 및 사망 처리를 실행한다.
    /// </summary>
    public void TakeHit(float damage)
    {
        InitializeHealth();

        // 보스가 입장 중이면 타워도 피해를 받지 않는다.
        if (!isActiveAndEnabled ||
            !_boss.HasBattleStarted ||
            IsDead ||
            damage <= 0)
        {
            return;
        }

        // 남은 체력보다 큰 피해가 들어와도 0 아래로 내려가지 않는다.
        float appliedDamage = Mathf.Min(damage, _currentHp);
        _currentHp -= appliedDamage;

        // 마지막 타격도 합산 체력에 반영한다.
        HealthChanged?.Invoke();

        if (IsDead)
        {
            // HP 데이터를 유지하기 위해 여기서는 오브젝트를 삭제하지 않는다.
            Destroyed?.Invoke(this);
        }
        else
        {
            // 살아 있을 때만 기존 피격 연출을 실행한다.
            Damaged?.Invoke(appliedDamage);
        }
    }
}
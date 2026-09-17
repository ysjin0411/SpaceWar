using System;
using UnityEngine;

public class Tower : MonoBehaviour, IDamageable
{
    // 타워마다 Inspector에서 서로 다른 최대 체력을 설정한다.
    [SerializeField, Min(1)] private int _maxHp = 100;

    // 현재 체력을 보관한다.
    private int _currentHp;

    // 다른 컴포넌트에서 먼저 초기화하더라도 체력을 중복 초기화하지 않는다.
    private bool _isInitialized;

    // 피격 연출을 위한 기존 IDamageable 이벤트다.
    public event Action<int> Damaged;

    // 체력이 변하면 합산 실드 컴포넌트에 알린다.
    public event Action HealthChanged;

    // 타워가 파괴되면 공격 중단 등의 처리를 연결할 수 있다.
    public event Action<Tower> Destroyed;

    // 외부에서는 체력 값을 읽기만 할 수 있다.
    public int MaxHp => _maxHp;

    public int CurrentHp
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

    private void Awake()
    {
        InitializeHealth();
    }

    // 부모와 자식의 Awake 실행 순서에 관계없이 한 번만 초기화한다.
    public void InitializeHealth()
    {
        if (_isInitialized)
            return;

        _maxHp = Mathf.Max(1, _maxHp);
        _currentHp = _maxHp;
        _isInitialized = true;
    }

    // 기존 플레이어 무기가 호출하는 피해 처리 함수다.
    public void TakeHit(int damage)
    {
        InitializeHealth();

        if (!isActiveAndEnabled || IsDead || damage <= 0)
            return;

        // 남은 체력보다 큰 피해가 들어와도 0 아래로 내려가지 않는다.
        int appliedDamage = Mathf.Min(damage, _currentHp);
        _currentHp -= appliedDamage;

        if (IsDead)
        {
            // TowerRoot에 있는 피격 콜라이더를 꺼서 추가 충돌을 막는다.
            foreach (Collider2D hitCollider in GetComponents<Collider2D>())
                hitCollider.enabled = false;
        }

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
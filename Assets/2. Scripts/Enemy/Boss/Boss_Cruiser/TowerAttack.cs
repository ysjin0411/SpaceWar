using System;
using System.Collections.Generic;
using UnityEngine;

// SpriteResolver가 스프라이트를 갱신한 뒤 피격 모양을 반영한다.
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
public abstract class TowerAttack : MonoBehaviour
{
    // 이 공격을 사용하는 타워의 체력 컴포넌트다.
    [SerializeField] private Tower _tower;

    // TowerRoot에 있는 피격용 PolygonCollider2D다.
    [SerializeField] private PolygonCollider2D _hitCollider;

    // 보스 전투 시작 후 첫 공격까지 기다리는 시간이다.
    [SerializeField, Min(0f)] private float _firstDelay = 2f;

    // 공격 종료 후 다음 공격까지 기다리는 시간이다.
    [SerializeField, Min(0.1f)] private float _cooldown = 3f;

    // 공격 요청을 전달하는 최상단 보스다.
    private Boss_Cruiser _boss;

    // 이 Renderer의 애니메이션과 스프라이트를 관리한다.
    private Animator _animator;
    private SpriteRenderer _spriteRenderer;

    // 이번 공격에서 사용할 플레이어 참조다.
    private PlayerController _targetPlayer;

    // 애니메이션 이벤트가 중복으로 발사하지 않도록 기록한다.
    private bool _hasFired;

    // Physics Shape 좌표를 변환할 때 재사용한다.
    private readonly List<Vector2> _shapePoints = new List<Vector2>();

    // Animator의 상태 이름이다.
    private static readonly int IdleState =
        Animator.StringToHash("Base Layer.Idle");

    private static readonly int AttackState =
        Animator.StringToHash("Base Layer.Attack");

    // 타워 체력이 0이 되었을 때 재생할 Animator 상태다.
    private static readonly int DieState =
        Animator.StringToHash("Base Layer.Die");

    // 최상단 보스가 공격 가능 여부와 시간을 확인한다.
    public Tower Owner => _tower;
    public float FirstDelay => _firstDelay;
    public float Cooldown => _cooldown;
    public bool IsAttacking { get; private set; }

    protected virtual void Awake()
    {
        _boss = GetComponentInParent<Boss_Cruiser>();
        _animator = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();

        // 잘못된 연결을 실행 시작 시 확인한다.
        if (_boss == null || _tower == null || _hitCollider == null)
        {
            throw new InvalidOperationException(
                $"{name}: Boss_Cruiser, Tower, 피격 Collider 연결을 확인하세요.");
        }

        // 기존 Tower의 사망 처리가 같은 오브젝트의 Collider를 끈다.
        if (_hitCollider.gameObject != _tower.gameObject)
        {
            throw new InvalidOperationException(
                $"{name}: 피격 Collider는 Tower와 같은 오브젝트에 두세요.");
        }

        // 대기·공격·사망 상태가 모두 연결되어 있는지 확인한다.
        if (!_animator.HasState(0, IdleState) ||
            !_animator.HasState(0, AttackState) ||
            !_animator.HasState(0, DieState))
        {
            throw new InvalidOperationException(
                $"{name}: Animator에 Idle, Attack, Die 상태가 필요합니다.");
        }

        // 모양 좌표에 위치를 직접 반영하므로 추가 Offset은 사용하지 않는다.
        _hitCollider.offset = Vector2.zero;
        _hitCollider.isTrigger = true;
    }

    protected virtual void OnEnable()
    {
        // 최상단의 공격 요청과 해당 타워의 파괴 알림을 받는다.
        _boss.TowerAttackRequested += HandleAttackRequested;
        _tower.Destroyed += HandleDestroyed;

        IsAttacking = false;
        _hasFired = false;

        // 파괴된 타워가 재활성화되어도 다시 나타나지 않는다.
        bool isAlive = !_tower.IsDead;
        _animator.enabled = isAlive;
        _spriteRenderer.enabled = isAlive;

        if (isAlive)
        {
            _animator.Play(IdleState, 0, 0f);
        }
        else
        {
            _hitCollider.enabled = false;
        }
    }

    private void HandleAttackRequested(
        TowerAttack requestedAttack,
        PlayerController targetPlayer)
    {
        // 이벤트는 모두 받지만 지정된 타워만 공격한다.
        if (requestedAttack != this ||
            !isActiveAndEnabled ||
            !_tower.isActiveAndEnabled ||
            _tower.IsDead ||
            IsAttacking ||
            targetPlayer == null)
        {
            return;
        }

        _targetPlayer = targetPlayer;
        _hasFired = false;
        IsAttacking = true;

        // 매번 공격 애니메이션의 처음부터 재생한다.
        _animator.Play(AttackState, 0, 0f);
    }

    // 공격 클립의 발사 프레임에서 Animation Event로 호출한다.
    public void FireAttack()
    {
        if (!IsAttacking ||
            _hasFired ||
            _tower.IsDead ||
            _boss.IsDead ||
            _boss.CurrentPhase != Boss_Cruiser.CruiserPhase.Towers ||
            _targetPlayer == null)
        {
            return;
        }

        // 한 번의 공격 애니메이션에서 실제 공격은 한 번만 발생한다.
        _hasFired = true;
        ExecuteAttack(_targetPlayer);
    }

    // 공격 클립의 마지막에서 Animation Event로 호출한다.
    public void CompleteAttack()
    {
        if (!IsAttacking)
        {
            return;
        }

        IsAttacking = false;

        // 살아 있는 타워만 대기 상태로 돌아간다.
        if (!_tower.IsDead)
        {
            _animator.Play(IdleState, 0, 0f);
        }
    }

    private void LateUpdate()
    {
        // 비활성화된 타워의 피격 영역은 끈다.
        if (!_tower.isActiveAndEnabled)
        {
            _hitCollider.enabled = false;
            return;
        }

        // 사망 애니메이션 중에는 마지막 피격 모양을 유지한다.
        // 콜라이더를 끄는 시점은 사망 애니메이션 종료 이벤트가 담당한다.
        if (_tower.IsDead)
        {
            return;
        }

        // 현재 표시 중인 스프라이트의 피격 모양을 적용한다.
        Sprite sprite = _spriteRenderer.sprite;

        if (sprite == null || sprite.GetPhysicsShapeCount() == 0)
        {
            _hitCollider.enabled = false;
            return;
        }

        // Polygon 콜라이더의 path갯수는 스프라이트의 피직스쉐이프갯수다.
        _hitCollider.pathCount = sprite.GetPhysicsShapeCount();

        for (int pathIndex = 0;
             pathIndex < _hitCollider.pathCount;
             pathIndex++)
        {
            // 반복사용을 위한 벡터2값 초기화
            _shapePoints.Clear();

            // 스프라이트.
            sprite.GetPhysicsShape(pathIndex, _shapePoints);

            for (int i = 0; i < _shapePoints.Count; i++)
            {
                Vector2 point = _shapePoints[i];

                // Renderer의 좌우·상하 반전도 피격 모양에 반영한다.
                if (_spriteRenderer.flipX)
                    point.x = -point.x;

                if (_spriteRenderer.flipY)
                    point.y = -point.y;

                // 자식 Renderer와 TowerRoot의 크기·위치 차이를 반영한다.
                Vector3 worldPoint =
                    _spriteRenderer.transform.TransformPoint(point);

                Vector3 localPoint =
                    _hitCollider.transform.InverseTransformPoint(worldPoint);

                _shapePoints[i] = new Vector2(
                    localPoint.x,
                    localPoint.y);
            }

            _hitCollider.SetPath(pathIndex, _shapePoints);
        }

        _hitCollider.enabled = true;
    }

    private void HandleDestroyed(Tower destroyedTower)
    {
        // 자신이 담당하는 타워의 사망 이벤트만 처리한다.
        if (destroyedTower != _tower)
        {
            return;
        }

        // 진행 중이던 공격을 취소한다.
        // 이후 공격 클립의 발사 이벤트가 들어와도 발사하지 않는다.
        IsAttacking = false;

        // 사망 애니메이션이 보이도록 Animator와 Renderer를 유지한다.
        _animator.enabled = true;
        _spriteRenderer.enabled = true;

        // 공격 중이었어도 사망 상태의 처음부터 재생한다.
        _animator.Play(DieState, 0, 0f);
    }

    protected virtual void OnDisable()
    {
        // 재활성화 시 이벤트가 중복 구독되지 않도록 정리한다.
        if (_boss != null)
            _boss.TowerAttackRequested -= HandleAttackRequested;

        if (_tower != null)
            _tower.Destroyed -= HandleDestroyed;

        IsAttacking = false;

        if (_hitCollider != null)
            _hitCollider.enabled = false;
    }

    // Die 클립 마지막의 Animation Event에서 호출한다.
    public void CompleteDeath()
    {
        // 살아 있는 타워에는 잘못된 종료 이벤트를 적용하지 않는다.
        if (!_tower.IsDead || !_tower.gameObject.activeSelf)
        {
            return;
        }

        // 이 타워에 속한 모든 콜라이더를 끈다.
        foreach (Collider2D towerCollider in
                 _tower.GetComponentsInChildren<Collider2D>(true))
        {
            towerCollider.enabled = false;
        }

        // Renderer만이 아니라 해당 TowerRoot 전체를 비활성화한다.
        // 오브젝트를 삭제하지 않으므로 합산 체력 참조는 유지된다.
        _tower.gameObject.SetActive(false);
    }

    // 타워 종류마다 실제 공격 방식만 다르게 구현한다.
    protected abstract void ExecuteAttack(PlayerController targetPlayer);
}
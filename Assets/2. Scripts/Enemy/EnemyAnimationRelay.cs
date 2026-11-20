using System;
using UnityEngine;

/// <summary>
/// 공통 공격 애니메이션의 진행률을 확인하고 부모 적에게 알린다.
/// </summary>
[RequireComponent(typeof(Animator))]
public sealed class EnemyAnimationRelay : MonoBehaviour
{
    // 공격 준비 시간 계산과 실제 발사 판정이 공유하는 정규화 시점.
    public const float FireNormalizedTime = 0.5f;

    // 같은 Renderer 오브젝트의 Animator다.
    Animator _animator;

    // 공격을 실제로 처리하는 부모 적이다.
    Enemy _enemy;

    // 이전 확인 시 공격 상태였는지 기록한다.
    bool _wasAttacking;

    // 현재 공격에서 실제 공격을 전달했는지 기록한다.
    bool _fireSent;

    // 현재 공격에서 종료를 전달했는지 기록한다.
    bool _finishSent;

    // 같은 상태가 처음부터 다시 재생되는지 확인할 진행률이다.
    float _previousProgress;

    // 현재 컨트롤러에 있는 실제 상태 이름이다.
    static readonly int IdleHash =
        Animator.StringToHash("Base Layer.Idle");

    static readonly int AttackHash =
        Animator.StringToHash("Base Layer.Attack_4Frame");

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _enemy = GetComponentInParent<Enemy>();

        if (_enemy == null)
        {
            throw new InvalidOperationException(
                $"{name}: 부모에 Enemy가 필요합니다.");
        }

        if (!_animator.HasState(0, IdleHash) ||
            !_animator.HasState(0, AttackHash))
        {
            throw new InvalidOperationException(
                $"{name}: Idle과 Attack_4Frame 상태가 필요합니다.");
        }
    }

    /// <summary>
    /// Animator가 갱신된 뒤 현재 공격의 진행률을 확인한다.
    /// </summary>
    private void LateUpdate()
    {
        if (_enemy == null ||
            _enemy.IsDead ||
            !_enemy.isActiveAndEnabled ||
            !_animator.isActiveAndEnabled ||
            Time.deltaTime <= 0f)
        {
            return;
        }

        AnimatorStateInfo state =
            _animator.GetCurrentAnimatorStateInfo(0);

        // Idle 등 다른 상태라면 다음 공격을 기다린다.
        if (state.fullPathHash != AttackHash)
        {
            ResetTracking();
            return;
        }

        float progress = state.normalizedTime;

        // 공격에 새로 진입하거나 같은 공격이 처음부터 재생됐다.
        if (!_wasAttacking || progress < _previousProgress)
        {
            _fireSent = false;
            _finishSent = false;
        }

        _wasAttacking = true;
        _previousProgress = progress;

        // 현재 4컷 클립의 세 번째 컷이 시작되는 시점이다.
        // 프레임이 건너뛰어도 공격을 누락하지 않도록 이상 조건을 사용한다.
        if (!_fireSent && progress >= FireNormalizedTime)
        {
            _fireSent = true;
            _enemy.FireFromAnimation();
        }

        // 공격 처리 과정에서 적이 제거됐다면 종료 처리를 하지 않는다.
        if (_enemy == null || _enemy.IsDead)
        {
            return;
        }

        // 비반복 공격을 한 번 재생했으면 Idle로 돌아간다.
        if (!_finishSent && progress >= 1f)
        {
            _finishSent = true;

            _animator.Play(IdleHash, 0, 0f);
            _enemy.FinishAttackFromAnimation();
        }
    }

    /// <summary>
    /// 다음 공격에서 다시 한 번씩 알릴 수 있도록 초기화한다.
    /// </summary>
    private void ResetTracking()
    {
        _wasAttacking = false;
        _fireSent = false;
        _finishSent = false;
        _previousProgress = 0f;
    }

    private void OnDisable()
    {
        ResetTracking();
    }
}
using System;
using UnityEngine;
using UnityEngine.U2D.Animation;

// 2페이즈 공격 자세를 표시하고 공격 종료 시 Idle로 복귀시킨다.
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteResolver))]
[RequireComponent(typeof(SpriteLibrary))]
public sealed class BossAlienPhaseTwoVisual : MonoBehaviour
{
    // 공격 가능 상태를 확인할 2페이즈 본체다.
    private Boss_AlienBody _boss;

    // Idle과 공격용 빈 상태를 전환한다.
    private Animator _animator;

    // Sprite Library의 Category와 Label로 자세를 변경한다.
    private SpriteResolver _resolver;

    // 요청한 자세의 스프라이트가 등록됐는지 확인한다.
    private SpriteLibrary _library;

    // 이 컴포넌트가 공격 자세를 표시하고 있는지 저장한다.
    private bool _isActionPlaying;

    // Idle 애니메이션이 자세를 덮어쓰지 않도록 진입할 빈 상태다.
    private static readonly int ActionState =
        Animator.StringToHash("Base Layer.Alien_Phase2_Action");

    // 공격 종료 후 복귀할 대기 상태다.
    private static readonly int IdleState =
        Animator.StringToHash("Base Layer.Alien_Phase2_Idle");

    private void Awake()
    {
        // 부모 오브젝트에서 본체 전용 보스 컴포넌트를 찾는다.
        _boss = GetComponentInParent<Boss_AlienBody>();

        // 현재 BodyVisual에 붙어 있는 표현 컴포넌트들을 가져온다.
        _animator = GetComponent<Animator>();
        _resolver = GetComponent<SpriteResolver>();
        _library = GetComponent<SpriteLibrary>();

        // 로봇 등 잘못된 오브젝트에 붙어 있으면 설정 오류를 알린다.
        if (_boss == null)
        {
            throw new InvalidOperationException(
                $"{name}: 부모에서 Boss_AlienBody를 찾을 수 없습니다.");
        }

        // 본체 Animator에 공격용 빈 상태와 Idle 상태가 있는지 검사한다.
        if (!_animator.HasState(0, ActionState) ||
            !_animator.HasState(0, IdleState))
        {
            throw new InvalidOperationException(
                $"{name}: Alien_Phase2_Action과 " +
                "Alien_Phase2_Idle 상태를 확인하세요.");
        }
    }

    // 공격 표현을 시작하면서 첫 자세를 표시한다.
    // 전투가 중단됐거나 이미 공격 표현 중이면 시작하지 않는다.
    public bool BeginAction(string category, string label)
    {
        if (!CanShowPose() || _isActionPlaying)
            return false;

        ValidatePose(category, label);

        // 빈 상태로 즉시 전환해 Idle의 스프라이트 변경을 멈춘다.
        _animator.Play(ActionState, 0, 0f);
        _animator.Update(0f);

        _isActionPlaying = true;
        _resolver.SetCategoryAndLabel(category, label);
        return true;
    }

    // 진행 중인 공격의 자세만 변경한다.
    // 예: Crouch → Takeoff → Airborne → Landing.
    public bool ShowPose(string category, string label)
    {
        if (!_isActionPlaying || !CanShowPose())
            return false;

        // 다른 연출이 Animator를 가져갔다면 자세를 덮어쓰지 않는다.
        if (_animator.GetCurrentAnimatorStateInfo(0).fullPathHash
            != ActionState)
        {
            return false;
        }

        ValidatePose(category, label);
        _resolver.SetCategoryAndLabel(category, label);
        return true;
    }

    // 정상 종료와 패턴 취소 시 공통으로 호출한다.
    public void EndAction()
    {
        if (!_isActionPlaying)
            return;

        _isActionPlaying = false;

        // 사망이나 비활성화 중에는 Idle로 강제 전환하지 않는다.
        if (!CanShowPose())
            return;

        // 다른 연출로 이미 전환됐다면 그 연출을 유지한다.
        if (_animator.GetCurrentAnimatorStateInfo(0).fullPathHash
            != ActionState)
        {
            return;
        }

        _animator.Play(IdleState, 0, 0f);
        _animator.Update(0f);
    }

    // 전투 조건과 표현 컴포넌트의 활성 상태를 함께 확인한다.
    private bool CanShowPose()
    {
        return isActiveAndEnabled &&
            _boss != null &&
            _boss.CanRunPhaseTwoPattern &&
            _animator.isActiveAndEnabled &&
            _resolver.isActiveAndEnabled &&
            _library.isActiveAndEnabled;
    }

    // 오타나 누락된 Label을 이전 자세 유지로 숨기지 않고 알린다.
    private void ValidatePose(string category, string label)
    {
        if (_library.GetSprite(category, label) == null)
        {
            throw new InvalidOperationException(
                $"{name}: 스프라이트를 찾을 수 없습니다. " +
                $"Category={category}, Label={label}");
        }
    }

    private void OnDisable()
    {
        // 비활성화 과정에서는 애니메이션을 재생하지 않는다.
        _isActionPlaying = false;
    }
}
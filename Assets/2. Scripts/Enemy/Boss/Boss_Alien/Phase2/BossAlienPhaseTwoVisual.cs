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

    // 본체의 좌우·상하 반전 상태를 읽는 Renderer다.
    private SpriteRenderer _bodyRenderer;

    // 양손을 박은 위치에 표시할 균열 Renderer다.
    [SerializeField]
    private SpriteRenderer _traceWristRims;

    // 이 컴포넌트가 공격 자세를 표시하고 있는지 저장한다.
    private bool _isActionPlaying;

    // Idle 애니메이션이 자세를 덮어쓰지 않도록 진입할 빈 상태다.
    private static readonly int ActionState =
        Animator.StringToHash("Base Layer.Alien_Phase2_Action");

    // 공격 종료 후 복귀할 대기 상태다.
    private static readonly int IdleState =
        Animator.StringToHash("Base Layer.Alien_Phase2_Idle");

    // 현재 공격 표시를 담당하는 Animator 상태를 기억한다.
    // 정지 포즈 공격과 클립 재생 공격 모두 종료 후 Idle로 복귀시키기 위해 사용한다.
    private int _activeActionState;

    // 새 내려차기 애니메이션 상태의 전체 경로다.
    private static readonly int LeapState =
        Animator.StringToHash("Base Layer.Alien_Phase2_Leap");

    // Leap 상태에만 적용할 애니메이션 재생 배속이다.
    private static readonly int LeapSpeedParameter =
        Animator.StringToHash("LeapSpeed");

    // 플레이어를 추격하는 동안 반복 재생할 Animator 상태다.
    private static readonly int ComboChaseState =
        Animator.StringToHash("Base Layer.Alien_Phase2_ComboChase");

    // ComboChase 상태의 재생 배속을 조절하는 Animator 파라미터다.
    private static readonly int ComboChaseSpeedParameter =
        Animator.StringToHash("ComboChaseSpeed");

    // 추격 후 연속 공격을 재생할 Animator 상태다.
    private static readonly int ComboAttackState =
        Animator.StringToHash("Base Layer.Alien_Phase2_ComboAttack");

    // 관찰 자세 이후 양손을 땅에 박는 상태다.
    private static readonly int TraceInsertState =
        Animator.StringToHash("Base Layer.Alien_Phase2_TraceInsert");

    // 손을 땅에 박은 채 지면 공격이 끝날 때까지 유지하는 상태다.
    private static readonly int TraceHoldState =
        Animator.StringToHash("Base Layer.Alien_Phase2_TraceHold");

    // 지면 공격이 끝나면 손을 빼고 일어나는 상태다.
    private static readonly int TraceWithdrawState =
        Animator.StringToHash("Base Layer.Alien_Phase2_TraceWithdraw");

    private void Awake()
    {
        // 부모 오브젝트에서 본체 전용 보스 컴포넌트를 찾는다.
        _boss = GetComponentInParent<Boss_AlienBody>();

        // 현재 BodyVisual에 붙어 있는 표현 컴포넌트들을 가져온다.
        _animator = GetComponent<Animator>();
        _resolver = GetComponent<SpriteResolver>();
        _library = GetComponent<SpriteLibrary>();
        // 본체 이미지의 반전 상태를 이펙트에도 적용하기 위해 가져온다.
        _bodyRenderer = GetComponent<SpriteRenderer>();

        // 필요한 Renderer가 빠졌다면 설정 오류를 알린다.
        if (_bodyRenderer == null || _traceWristRims == null)
        {
            throw new MissingComponentException(
                $"{name}: 본체 Sprite Renderer와 Trace Wrist Rims 연결을 확인하세요.");
        }

        // 생성 직후에는 손목 균열을 표시하지 않는다.
        _traceWristRims.enabled = false;

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

        // 이번 공격은 기존의 정지 포즈용 상태에서 시작한다.
        _activeActionState = ActionState;

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

        // 공격을 시작할 때 기록한 상태에 있을 때만 Idle로 복귀한다.
        // 사망 상태 등으로 전환됐다면 해당 상태를 유지한다.
        if (_animator.GetCurrentAnimatorStateInfo(0).fullPathHash
            != _activeActionState)
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
        // 비활성화 중에는 진행 중인 동작을 종료 처리한다.
        _isActionPlaying = false;

        // 이 컴포넌트만 꺼지는 경우에도 자식 균열이 남지 않게 숨긴다.
        if (_traceWristRims != null)
            _traceWristRims.enabled = false;
    }

    // 첫 추적을 시작하거나, 직전 콤보 공격에서 다음 추적으로 전환한다.
    public bool BeginComboChase(float animationSpeed)
    {
        if (!CanShowPose())
            return false;

        // 이미 동작 중이라면 현재 콤보 공격에서만 추적으로 돌아갈 수 있다.
        // 다른 패턴이나 사망 상태는 덮어쓰지 않는다.
        if (_isActionPlaying)
        {
            if (_activeActionState != ComboAttackState ||
                _animator.GetCurrentAnimatorStateInfo(0).fullPathHash
                    != ComboAttackState)
            {
                return false;
            }
        }

        // 잘못된 배속으로 애니메이션이 멈추거나 역재생되는 것을 막는다.
        if (float.IsNaN(animationSpeed) ||
            float.IsInfinity(animationSpeed) ||
            animationSpeed <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(animationSpeed));
        }

        // 추적 상태가 실제로 등록되어 있는지 검사한다.
        if (!_animator.HasState(0, ComboChaseState))
        {
            throw new InvalidOperationException(
                $"{name}: Alien_Phase2_ComboChase 상태를 확인하세요.");
        }

        // 이번 타격에 설정한 달리기 배속을 적용한다.
        _animator.SetFloat(ComboChaseSpeedParameter, animationSpeed);

        // 종료 시 EndAction이 현재 상태를 정리할 수 있도록 기록한다.
        _isActionPlaying = true;
        _activeActionState = ComboChaseState;

        // 달리기 클립을 처음부터 재생하고 상태 변경을 즉시 반영한다.
        _animator.Play(ComboChaseState, 0, 0f);
        _animator.Update(0f);

        return true;
    }

    // 추적을 끝내고, 전체 공격 클립에서 이번 타격의 시작 위치로 전환한다.
    // startNormalizedTime은 클립 전체 길이를 기준으로 한 0 이상 1 미만의 값이다.
    public bool PlayComboAttack(float startNormalizedTime)
    {
        if (!_isActionPlaying || !CanShowPose())
            return false;

        // 현재 콤보가 추적 상태를 유지하고 있을 때만 공격을 시작한다.
        if (_activeActionState != ComboChaseState ||
            _animator.GetCurrentAnimatorStateInfo(0).fullPathHash
                != ComboChaseState)
        {
            return false;
        }

        // 공격 상태가 실제로 등록되어 있는지 검사한다.
        if (!_animator.HasState(0, ComboAttackState))
        {
            throw new InvalidOperationException(
                $"{name}: Alien_Phase2_ComboAttack 상태를 확인하세요.");
        }

        // 클립 범위를 벗어난 위치에서 재생하지 않도록 검사한다.
        if (float.IsNaN(startNormalizedTime) ||
            float.IsInfinity(startNormalizedTime) ||
            startNormalizedTime < 0f ||
            startNormalizedTime >= 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(startNormalizedTime));
        }

        // 종료 시 정리할 현재 공격 상태를 기록한다.
        _activeActionState = ComboAttackState;

        // 지정한 타격의 준비 동작부터 재생한다.
        _animator.Play(ComboAttackState, 0, startNormalizedTime);
        _animator.Update(0f);

        return true;
    }

    // 공격 클립의 진행률을 읽어 할퀴기 이미지와 피해 시점을 맞추는 데 사용한다.
    public bool TryGetComboAttackProgress(out float progress)
    {
        progress = 0f;

        if (!_isActionPlaying || !CanShowPose())
            return false;

        AnimatorStateInfo state =
            _animator.GetCurrentAnimatorStateInfo(0);

        // 사망 등으로 공격 상태를 벗어났다면 진행률을 사용하지 않는다.
        if (_activeActionState != ComboAttackState ||
            state.fullPathHash != ComboAttackState)
        {
            return false;
        }

        // 시작은 0, 클립 끝은 1로 반환한다.
        progress = Mathf.Clamp01(state.normalizedTime);
        return true;
    }

    // 계산된 배속으로 도약 클립을 시작한다.
    // 이동과 착지 판정은 이 클립의 진행률에 맞춰 함께 진행된다.
    public bool PlayLeapAnimation(float playbackSpeed)
    {
        if (!_isActionPlaying || !CanShowPose())
            return false;

        // 다른 상태가 현재 동작을 중단했다면 덮어쓰지 않는다.
        if (_animator.GetCurrentAnimatorStateInfo(0).fullPathHash
            != _activeActionState)
        {
            return false;
        }

        // 사용할 도약 상태가 등록되어 있는지 확인한다.
        if (!_animator.HasState(0, LeapState))
        {
            throw new InvalidOperationException(
                $"{name}: Alien_Phase2_Leap 상태를 확인하세요.");
        }

        // 유효하지 않은 배속으로 멈추거나 역재생되는 것을 막는다.
        if (float.IsNaN(playbackSpeed) ||
            float.IsInfinity(playbackSpeed) ||
            playbackSpeed <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(playbackSpeed));
        }

        // 도약 상태에만 배속을 적용한다.
        _animator.SetFloat(LeapSpeedParameter, playbackSpeed);
        _activeActionState = LeapState;

        // 클립을 처음부터 재생하고 상태 변경을 즉시 반영한다.
        _animator.Play(LeapState, 0, 0f);
        _animator.Update(0f);

        return true;
    }

    // Leap 클립의 진행률을 가져온다.
    // 0은 시작, 0.75는 내려찍기, 1은 애니메이션 완료 지점이다.
    public bool TryGetLeapProgress(out float progress)
    {
        progress = 0f;

        if (!_isActionPlaying || !CanShowPose())
            return false;

        AnimatorStateInfo state =
            _animator.GetCurrentAnimatorStateInfo(0);

        // 사망 등으로 상태가 바뀌면 이동과 공격 판정도 중단한다.
        if (_activeActionState != LeapState ||
            state.fullPathHash != LeapState)
        {
            return false;
        }

        progress = Mathf.Clamp01(state.normalizedTime);
        return true;
    }

    // 관찰용 정지 자세에서 손을 박는 클립으로 전환한다.
    public bool PlayTraceInsert()
    {
        return TryPlayTraceState(ActionState, TraceInsertState);
    }

    // 손을 박는 클립에서 유지용 반복 클립으로 전환한다.
    public bool PlayTraceHold()
    {
        return TryPlayTraceState(TraceInsertState, TraceHoldState);
    }

    // 유지 동작에서 손을 빼는 클립으로 전환한다.
    public bool PlayTraceWithdraw()
    {
        return TryPlayTraceState(TraceHoldState, TraceWithdrawState);
    }

    // 예상한 이전 상태일 때만 다음 Trace 클립을 처음부터 재생한다.
    private bool TryPlayTraceState(int expectedState, int nextState)
    {
        // 전투가 중단됐거나 표시 기능이 비활성화되면 전환하지 않는다.
        if (!_isActionPlaying || !CanShowPose())
            return false;

        // 사망 등 다른 동작으로 바뀐 상태를 덮어쓰지 않는다.
        if (_activeActionState != expectedState ||
            _animator.GetCurrentAnimatorStateInfo(0).fullPathHash
            != expectedState)
        {
            return false;
        }

        // 필요한 상태가 Controller에 없으면 설정 오류를 알린다.
        if (!_animator.HasState(0, nextState))
        {
            throw new InvalidOperationException(
                $"{name}: Trace의 Insert, Hold, Withdraw 상태를 확인하세요.");
        }

        // 종료 시 EndAction이 이 상태에서 Idle로 복귀하도록 기록한다.
        _activeActionState = nextState;

        // 다음 클립을 처음부터 재생하고 첫 프레임을 즉시 반영한다.
        _animator.Play(nextState, 0, 0f);
        _animator.Update(0f);

        return true;
    }

    // 손을 박는 클립의 진행률을 읽는다. 1이면 끝까지 재생된 상태다.
    public bool TryGetTraceInsertProgress(out float progress)
    {
        return TryGetTraceProgress(TraceInsertState, out progress);
    }

    // 손을 빼는 클립의 진행률을 읽는다. 1이면 끝까지 재생된 상태다.
    public bool TryGetTraceWithdrawProgress(out float progress)
    {
        return TryGetTraceProgress(TraceWithdrawState, out progress);
    }

    // 요청한 Trace 상태가 실제로 재생 중일 때만 진행률을 반환한다.
    private bool TryGetTraceProgress(int expectedState, out float progress)
    {
        progress = 0f;

        // 전투 중단이나 비활성화 상태에서는 진행률을 사용하지 않는다.
        if (!_isActionPlaying || !CanShowPose())
            return false;

        AnimatorStateInfo state =
            _animator.GetCurrentAnimatorStateInfo(0);

        // 사망 등 다른 상태로 전환됐다면 현재 Trace 동작은 중단된 것이다.
        if (_activeActionState != expectedState ||
            state.fullPathHash != expectedState)
        {
            return false;
        }

        // 시작은 0, 재생 완료는 1로 반환한다.
        progress = Mathf.Clamp01(state.normalizedTime);
        return true;
    }

    // 본체 애니메이션이 반영된 뒤 손목 균열의 표시와 반전을 맞춘다.
    private void LateUpdate()
    {
        // 참조가 사라졌다면 더 이상 이펙트를 갱신하지 않는다.
        if (_traceWristRims == null)
            return;

        // 실제로 Trace 유지 상태를 재생하는 동안에만 표시한다.
        bool showRims =
            _isActionPlaying &&
            CanShowPose() &&
            _bodyRenderer != null &&
            _bodyRenderer.enabled &&
            _activeActionState == TraceHoldState &&
            _animator.GetCurrentAnimatorStateInfo(0).fullPathHash
                == TraceHoldState;

        _traceWristRims.enabled = showRims;

        if (!showRims)
            return;

        // SpriteRenderer의 반전은 자식에게 상속되지 않으므로 직접 맞춘다.
        _traceWristRims.flipX = _bodyRenderer.flipX;
        _traceWristRims.flipY = _bodyRenderer.flipY;
    }
}
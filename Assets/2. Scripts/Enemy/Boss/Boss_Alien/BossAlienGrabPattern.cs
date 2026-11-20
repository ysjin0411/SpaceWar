using System;
using System.Collections;
using UnityEngine;

// Grab의 순서를 관리한다.
// 플레이어 붙이기와 반사 이동은 각각의 전용 컴포넌트가 담당한다.
[CreateAssetMenu(
    menuName = "Boss/Patterns/Alien Grab",
    fileName = "BossAlienGrabPattern")]
public sealed class BossAlienGrabPattern : BossPattern
{
    // 잡기·흔들기·던지기에 사용할 설정 에셋이다.
    [SerializeField] private BossAlienGrabConfig _config;

    // BodyVisual Animator에서 직접 재생할 상태다.
    private static readonly int PrepareState =
        Animator.StringToHash("Base Layer.Alien_Grab_Prepare");
    private static readonly int ThrowState =
        Animator.StringToHash("Base Layer.Alien_Grab_Throw");
    private static readonly int IdleState =
        Animator.StringToHash("Base Layer.Alien_Idle4");

    public override IEnumerator Execute(BossPatternContext context)
    {
        Boss_Alien boss = context.Boss as Boss_Alien;

        if (boss == null || _config == null || !PauseRandomMovement)
        {
            Debug.LogError(
                $"{name}: Boss_Alien, Config, Pause Random Movement를 확인하세요.");
            yield break;
        }

        ValidateConfig();

        BossAlienArmRig rig = boss.ArmRig;
        BossAlienGrabSensor sensor = boss.GrabSensor;
        BossAlienGrabController grab = boss.GrabController;
        BossAlienAnimationEventRelay relay = boss.AnimationEventRelay;
        Animator animator = context.VisualTransform.GetComponent<Animator>();
        PlayerThrownMotion thrown =
            context.TargetPlayer.GetComponent<PlayerThrownMotion>();

        // 보스 프리팹에는 씬 오브젝트를 저장할 수 없으므로 실행 시 찾는다.
        // 여러 경계가 있으면 임의로 선택하지 않고 설정 오류로 처리한다.
        BossAlienGrabArena[] arenas =
            UnityEngine.Object.FindObjectsByType<BossAlienGrabArena>(
                FindObjectsSortMode.None);

        if (rig == null || sensor == null || grab == null ||
            relay == null || animator == null || thrown == null ||
            !thrown.isActiveAndEnabled ||
            arenas.Length != 1 || !arenas[0].isActiveAndEnabled)
        {
            Debug.LogError(
                $"{name}: Grab 연결, PlayerThrownMotion, 씬의 Arena 하나를 확인하세요.");
            yield break;
        }

        BossAlienGrabArena arena = arenas[0];

        if (!animator.HasState(0, PrepareState) ||
            !animator.HasState(0, ThrowState) ||
            !animator.HasState(0, IdleState))
        {
            Debug.LogError($"{name}: Animator의 Grab·Idle 상태를 확인하세요.");
            yield break;
        }

        // 다른 실행이 가진 플레이어 이동 권한을 건드리지 않는다.
        if (grab.HasCapturedPlayer || thrown.IsRunning ||
            !context.TargetPlayer.CanControl)
        {
            yield break;
        }

        // 실행 상태는 에셋 필드가 아니라 이번 실행의 지역 변수로 관리한다.
        bool aborted = false;
        bool captured = false;
        bool throwStarted = false;
        bool extendRequested = false;
        bool throwRequested = false;

        // 0: 이벤트 대기 아님, 1: 팔 뻗기 대기, 2: 던지기 대기.
        int eventStage = 0;

        bool CanContinue()
        {
            return boss != null && boss.CanRunArmPattern &&
                rig != null && rig.isActiveAndEnabled &&
                sensor != null && sensor.isActiveAndEnabled &&
                grab != null && grab.isActiveAndEnabled &&
                animator != null && animator.isActiveAndEnabled &&
                arena != null && arena.isActiveAndEnabled;
        }

        // 다른 단계에서 들어온 이벤트는 무시한다.
        void HandleExtend()
        {
            if (eventStage == 1 && CanContinue())
            {
                extendRequested = true;
            }
        }

        void HandleThrow()
        {
            if (eventStage == 2 && CanContinue())
            {
                throwRequested = true;
            }
        }

        // 지정 위치까지 손목을 이동한다.
        // 잡은 뒤 이동할 때는 플레이어가 해제됐는지도 검사한다.
        IEnumerator MoveHand(Vector3 destination, float speed, bool requireCapture)
        {
            while ((rig.HandRoot.position - destination).sqrMagnitude > 0.0001f)
            {
                if (!CanContinue() ||
                    (requireCapture && !grab.HasCapturedPlayer))
                {
                    aborted = true;
                    yield break;
                }

                rig.SetHandPosition(Vector3.MoveTowards(
                    rig.HandRoot.position, destination, speed * Time.deltaTime));

                yield return null;
            }
        }

        // 애니메이션 이벤트 누락으로 무한 대기하지 않도록 제한한다.
        IEnumerator WaitForEvent(Func<bool> received, bool requireCapture)
        {
            float deadline = Time.time + _config.AnimationTimeout;

            while (!received())
            {
                if (!CanContinue() ||
                    (requireCapture && !grab.HasCapturedPlayer))
                {
                    aborted = true;
                    yield break;
                }

                if (Time.time >= deadline)
                {
                    Debug.LogError($"{name}: Grab 애니메이션 이벤트가 오지 않았습니다.");
                    aborted = true;
                    yield break;
                }

                yield return null;
            }
        }

        if (!CanContinue())
        {
            yield break;
        }

        relay.GrabExtendRequested += HandleExtend;
        relay.GrabThrowRequested += HandleThrow;

        try
        {
            sensor.SetDetectionEnabled(false);
            rig.SetHandClosed(false);

            // 1. 손을 준비 위치로 옮긴다.
            yield return MoveHand(
                boss.PrepareHandPosition, _config.HandMoveSpeed, false);

            if (aborted || !CanContinue())
            {
                yield break;
            }

            // 2. 준비 애니메이션의 팔 뻗기 이벤트를 기다린다.
            eventStage = 1;
            animator.Play(PrepareState, 0, 0f);
            GameAudio.Play(GameSound.AlienGrabPrepare);
            yield return WaitForEvent(() => extendRequested, false);
            eventStage = 0;

            if (aborted || !CanContinue())
            {
                yield break;
            }

            // 이벤트를 받은 순간에만 플레이어 위치로 조준한다.
            Vector3 origin = boss.ArmSocket.position;
            Vector3 target = context.TargetPlayer.transform.position;
            target.z = origin.z;

            Vector3 toTarget = target - origin;

            if (toTarget.sqrMagnitude < 0.0001f)
            {
                yield break;
            }

            Vector3 direction = toTarget.normalized;
            float reach = Mathf.Min(
                toTarget.magnitude + _config.ExtraReach,
                _config.MaxArmLength);
            Vector3 endPosition = origin + direction * reach;

            // 3. 손을 뻗으면서 현재 겹침과 이동 경로를 검사한다.
            sensor.SetDetectionEnabled(true);

            while (CanContinue())
            {
                if (Time.deltaTime <= 0f)
                {
                    yield return null;
                    continue;
                }

                Vector3 current = rig.HandRoot.position;
                Vector3 next = Vector3.MoveTowards(
                    current, endPosition, _config.ExtendSpeed * Time.deltaTime);
                Vector2 movement = next - current;

                if (sensor.TryDetectPlayer(
                    movement, out PlayerController player, out float hitDistance))
                {
                    // 경로에서 접촉했다면 접촉 위치까지 손을 옮긴다.
                    if (movement.sqrMagnitude > 0f)
                    {
                        float distance = Mathf.Min(hitDistance, movement.magnitude);
                        rig.SetHandPosition(
                            current + (Vector3)(movement.normalized * distance));
                    }

                    captured = grab.TryCapture(player, _config.AttachDuration);

                    if (captured)
                    {
                        break;
                    }
                }

                rig.SetHandPosition(next);

                // 이동 후 손의 회전 때문에 겹친 경우도 확인한다.
                if (sensor.TryDetectPlayer(
                    Vector2.zero, out player, out hitDistance))
                {
                    captured = grab.TryCapture(player, _config.AttachDuration);

                    if (captured)
                    {
                        break;
                    }
                }

                if ((next - endPosition).sqrMagnitude <= 0.0001f)
                {
                    break;
                }

                yield return null;
            }

            sensor.SetDetectionEnabled(false);

            if (!CanContinue())
            {
                yield break;
            }

            // 빗나가면 잡기 이후 동작 없이 기본 위치로 복귀한다.
            if (!captured)
            {
                yield return MoveHand(
                    boss.RestHandPosition, _config.ReturnSpeed, false);
                yield break;
            }

            // 4. 플레이어가 손 안에 완전히 붙을 때까지 기다린다.
            float attachDeadline =
                Time.time + _config.AttachDuration + _config.AnimationTimeout;

            while (!grab.IsAttached)
            {
                if (!CanContinue() || !grab.HasCapturedPlayer)
                {
                    yield break;
                }

                if (Time.time >= attachDeadline)
                {
                    Debug.LogError($"{name}: 플레이어를 손에 붙이지 못했습니다.");
                    yield break;
                }

                yield return null;
            }

            // 5. 유지 위치로 이동한 뒤 설정 시간 동안 위아래로 흔든다.
            yield return MoveHand(
                boss.HoldHandPosition, _config.HandMoveSpeed, true);

            if (aborted || !CanContinue() || !grab.HasCapturedPlayer)
            {
                yield break;
            }

            float shakeElapsed = 0f;

            while (shakeElapsed < _config.ShakeDuration)
            {
                if (!CanContinue() || !grab.HasCapturedPlayer)
                {
                    yield break;
                }

                shakeElapsed += Time.deltaTime;
                float offset = Mathf.Sin(
                    shakeElapsed * _config.ShakeFrequency * Mathf.PI * 2f) *
                    _config.ShakeAmplitude;

                rig.SetHandPosition(
                    boss.HoldHandPosition + Vector3.up * offset);

                yield return null;
            }

            // 6. 손을 당긴 뒤 던지기 애니메이션 이벤트를 기다린다.
            yield return MoveHand(
                boss.ThrowWindupHandPosition, _config.HandMoveSpeed, true);

            if (aborted || !CanContinue() || !grab.HasCapturedPlayer)
            {
                yield break;
            }

            eventStage = 2;
            animator.Play(ThrowState, 0, 0f);
            yield return WaitForEvent(() => throwRequested, true);
            eventStage = 0;

            if (aborted || !CanContinue() || !grab.HasCapturedPlayer)
            {
                yield break;
            }

            // 7. 전용 컴포넌트가 비행·반사·회전·조작 복구를 실행한다.
            throwStarted = thrown.TryBegin(grab, boss, arena, _config);

            if (!throwStarted)
            {
                Debug.LogError($"{name}: 플레이어 던지기를 시작하지 못했습니다.");
                yield break;
            }

            // 플레이어가 날아가는 동안 손은 기본 위치로 돌아온다.
            yield return MoveHand(
                boss.RestHandPosition, _config.ReturnSpeed, false);

            if (aborted)
            {
                yield break;
            }

            // 플레이어의 반사 이동과 조작 복구까지 기다린다.
            while (thrown.IsRunning)
            {
                if (!CanContinue())
                {
                    yield break;
                }

                yield return null;
            }
        }
        finally
        {
            // 정상 종료와 Runner의 강제 취소 모두 이곳에서 정리한다.
            eventStage = 0;

            if (relay != null)
            {
                relay.GrabExtendRequested -= HandleExtend;
                relay.GrabThrowRequested -= HandleThrow;
            }

            if (sensor != null)
            {
                sensor.SetDetectionEnabled(false);
            }

            if (throwStarted && thrown != null)
            {
                thrown.Stop();
            }

            if (captured && grab != null)
            {
                grab.Release();
            }

            if (boss != null)
            {
                boss.ResetArm();

                // 사망 애니메이션은 Idle로 덮어쓰지 않는다.
                if (!boss.IsDead && animator != null &&
                    animator.isActiveAndEnabled)
                {
                    animator.Play(IdleState, 0, 0f);
                }
            }
        }
    }

    // 손 이동 속도와 시간 설정이 유효한지 실행 전에 확인한다.
    private void ValidateConfig()
    {
        if (!Positive(_config.HandMoveSpeed) ||
            !Positive(_config.MaxArmLength) ||
            !Positive(_config.ExtendSpeed) ||
            !Positive(_config.ReturnSpeed) ||
            !Positive(_config.AttachDuration) ||
            !Positive(_config.ShakeFrequency) ||
            !Positive(_config.AnimationTimeout) ||
            !NonNegative(_config.ExtraReach) ||
            !NonNegative(_config.ShakeDuration) ||
            !NonNegative(_config.ShakeAmplitude))
        {
            throw new InvalidOperationException(
                $"{name}: Grab Config의 속도·시간·거리 설정을 확인하세요.");
        }
    }

    private static bool Positive(float value)
    {
        return NonNegative(value) && value > 0f;
    }

    private static bool NonNegative(float value)
    {
        return !float.IsNaN(value) &&
            !float.IsInfinity(value) && value >= 0f;
    }
}
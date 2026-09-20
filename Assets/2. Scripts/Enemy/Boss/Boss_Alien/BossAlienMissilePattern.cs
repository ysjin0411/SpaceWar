using System.Collections;
using UnityEngine;

// 설정은 에셋에 보관하고, 발사 진행 상태는 실행마다 따로 관리한다.
[CreateAssetMenu(
    menuName = "Boss/Patterns/Alien Missile",
    fileName = "BossAlienMissilePattern")]
public sealed class BossAlienMissilePattern : BossPattern
{
    // 소켓별 발사 일정과 미사일 정보를 가진 설정 에셋이다.
    [SerializeField] private BossAlienMissileConfig _config;

    // 이벤트 누락이나 잘못된 Animator 연결로 무한 대기하지 않게 한다.
    [SerializeField, Min(0.1f)]
    private float _animationTimeout = 3f;

    // 해치 개방 상태를 제어하는 Animator 파라미터다.
    private static readonly int MissileAttacking =
        Animator.StringToHash("IsMissileAttacking");

    public override IEnumerator Execute(BossPatternContext context)
    {
        Boss_Alien boss = context.Boss as Boss_Alien;

        // 필요한 설정과 실행 대상을 확인한다.
        if (boss == null || _config == null ||
            _config.MissilePrefab == null ||
            _config.SocketCount != 4 ||
            !PauseRandomMovement ||
            !IsFinite(_animationTimeout) || _animationTimeout <= 0f ||
            !IsFinite(_config.FlightDuration) ||
            _config.FlightDuration <= 0f)
        {
            Debug.LogError($"{name}: 보스, Config, 소켓 수, 시간 설정과 " +
                "Pause Random Movement를 확인하세요.");
            yield break;
        }

        Animator animator =
            context.VisualTransform.GetComponent<Animator>();

        BossAlienAnimationEventRelay relay = boss.AnimationEventRelay;

        if (animator == null || relay == null)
        {
            Debug.LogError($"{name}: Animator와 이벤트 전달 연결이 필요합니다.");
            yield break;
        }

        // 잘못된 발사 일정이 실행 도중 문제를 일으키지 않게 검사한다.
        for (int i = 0; i < _config.SocketCount; i++)
        {
            BossAlienMissileConfig.SocketSettings settings =
                _config.GetSocketSettings(i);

            if (settings == null ||
                !IsFinite(settings.FirstDelay) ||
                !IsFinite(settings.Interval) ||
                settings.FirstDelay < 0f ||
                settings.Interval <= 0f ||
                settings.ShotCount < 0)
            {
                Debug.LogError($"{name}: {i}번 소켓의 발사 설정을 확인하세요.");
                yield break;
            }

            // 실제 보스에도 대응하는 소켓이 연결되어 있는지 확인한다.
            boss.GetMissileSpawnPosition(i);
        }

        // 이 실행에서만 사용하는 소켓별 발사 횟수다.
        int[] firedCounts = new int[_config.SocketCount];

        bool sequenceStarted = false;
        float sequenceStartTime = 0f;

        // 보스 또는 플레이어가 전투 불가능한 상태면 실행을 중단한다.
        bool CanContinue()
        {
            return boss != null &&
                boss.isActiveAndEnabled &&
                boss.HasBattleStarted &&
                !boss.IsDead &&
                context.TargetPlayer != null &&
                context.TargetPlayer.isActiveAndEnabled &&
                !context.TargetPlayer.IsDead;
        }

        // 해치 개방 이벤트가 발생한 시각을 한 번만 기록한다.
        void HandleSequenceStart()
        {
            if (sequenceStarted || !CanContinue())
            {
                return;
            }

            sequenceStarted = true;
            sequenceStartTime = Time.time;
        }

        if (!CanContinue())
        {
            yield break;
        }

        // 애니메이션을 시작하기 전에 구독하여 이벤트 누락을 막는다.
        relay.MissileSequenceStartRequested += HandleSequenceStart;

        try
        {
            animator.SetBool(MissileAttacking, true);

            // 해치가 실제로 열린다는 이벤트를 기다린다.
            float deadline = Time.time + _animationTimeout;

            while (!sequenceStarted)
            {
                if (!CanContinue())
                {
                    yield break;
                }

                if (Time.time >= deadline)
                {
                    Debug.LogError($"{name}: 미사일 시작 이벤트가 오지 않았습니다.");
                    yield break;
                }

                yield return null;
            }

            // 네 소켓의 일정을 같은 시작 시각 기준으로 진행한다.
            while (CanContinue())
            {
                float elapsed = Time.time - sequenceStartTime;
                bool allFired = true;

                for (int i = 0; i < _config.SocketCount; i++)
                {
                    BossAlienMissileConfig.SocketSettings settings =
                        _config.GetSocketSettings(i);

                    // 프레임이 늦어져도 예정된 발사 횟수가 누락되지 않게 한다.
                    while (firedCounts[i] < settings.ShotCount &&
                        elapsed >= settings.FirstDelay +
                        firedCounts[i] * settings.Interval)
                    {
                        Vector3 start = boss.GetMissileSpawnPosition(i);

                        // 매 발마다 현재 플레이어 위치를 새로 받아온다.
                        Vector3 target =
                            context.TargetPlayer.transform.position;
                        target.z = start.z;

                        BossAlienMissile missile = Instantiate(
                            _config.MissilePrefab,
                            start,
                            Quaternion.identity);

                        // 발사자와 공격 대상도 전달하여 전투 종료 시 정리할 수 있게 한다.
                        missile.Launch(
                            start,
                            target,
                            settings.StartControlOffset,
                            settings.EndControlOffset,
                            _config.FlightDuration,
                            _config.ExplosionPrefab,
                            _config.ExplosionRadius,
                            _config.Damage,
                            boss,
                            context.TargetPlayer);

                        firedCounts[i]++;
                    }

                    if (firedCounts[i] < settings.ShotCount)
                    {
                        allFired = false;
                    }
                }

                if (allFired)
                {
                    break;
                }

                yield return null;
            }

            if (!CanContinue())
            {
                yield break;
            }

            // 마지막 발사 후 해치를 닫고 대기 상태 복귀까지 기다린다.
            animator.SetBool(MissileAttacking, false);
            deadline = Time.time + _animationTimeout;

            do
            {
                yield return null;

                if (!CanContinue())
                {
                    yield break;
                }

                if (Time.time >= deadline)
                {
                    Debug.LogError($"{name}: 해치가 대기 상태로 복귀하지 않았습니다.");
                    yield break;
                }
            }
            while (animator.IsInTransition(0) ||
                !animator.GetCurrentAnimatorStateInfo(0)
                    .IsName("Alien_Idle4"));
        }
        finally
        {
            // 정상 종료와 강제 중단 모두 이벤트 구독과 공격 플래그를 정리한다.
            if (relay != null)
            {
                relay.MissileSequenceStartRequested -= HandleSequenceStart;
            }

            if (animator != null)
            {
                animator.SetBool(MissileAttacking, false);
            }
        }
    }

    // NaN이나 무한대가 발사 시간 계산에 들어오는 것을 막는다.
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
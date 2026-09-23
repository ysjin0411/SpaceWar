using System;
using System.Collections;
using UnityEngine;

// 예측 조준과 착지점 확정을 담당한다.
// 실제 도약 이동과 착지 피해는 다음 단계에서 연결한다.
[CreateAssetMenu(
    menuName = "Boss/Alien/Phase2/Leap Pattern",
    fileName = "BossAlienLeapPattern")]
public sealed class BossAlienLeapPattern : BossPattern
{
    // 예상 착지 범위를 표시할 경고 프리팹이다.
    [SerializeField]
    private BossAlienLeapWarning _warningPrefab;

    // 착지 순간 재생할 시각 효과다. 피해 판정은 포함하지 않는다.
    [SerializeField]
    private BossAlienLeapImpact _impactPrefab;

    // 플레이어 이동을 관측하며 경고 위치를 갱신하는 시간이다.
    [SerializeField, Min(0.01f)]
    private float _aimDuration = 0.8f;

    // 착지점 확정 후 도약 전까지 제공할 회피 시간이다.
    [SerializeField, Min(0.01f)]
    private float _lockDuration = 0.4f;

    // 현재 이동 속도를 기준으로 몇 초 앞을 조준할지 정한다.
    [SerializeField, Min(0f)]
    private float _predictionSeconds = 0.4f;

    // 플레이어 현재 위치에서 앞서 조준할 최대 거리다.
    [SerializeField, Min(0f)]
    private float _maxPredictionDistance = 3f;

    // 이후 착지 피해에도 동일하게 사용할 원형 범위의 반경이다.
    [SerializeField, Min(0.01f)]
    private float _landingRadius = 3f;

    // 몸체 콜라이더와 전투 영역 벽 사이에 확보할 간격이다.
    [SerializeField, Min(0f)]
    private float _wallPadding = 0.2f;

    // 이륙 자세를 보여주는 시간이다.
    [SerializeField, Min(0.01f)]
    private float _takeoffDuration = 0.12f;

    // 출발점에서 확정된 착지점까지 이동하는 시간이다.
    [SerializeField, Min(0.01f)]
    private float _flightDuration = 0.6f;

    // 공중에서 외형을 위로 띄우는 최대 높이다.
    // BodyVisual의 로컬 좌표 기준이며 몸체 콜라이더는 따라 올라가지 않는다.
    [SerializeField, Min(0f)]
    private float _jumpHeight = 2.5f;

    // 착지 자세를 유지하며 다음 행동을 쉬는 시간이다.
    [SerializeField, Min(0.01f)]
    private float _recoveryDuration = 0.6f;

    // 착지 순간 원형 범위에 한 번 요청할 피해량이다.
    [SerializeField, Min(0)]
    private int _landingDamage = 10;

    public override IEnumerator Execute(BossPatternContext context)
    {
        // 이 패턴은 에일리언 본체에서만 실행한다.
        Boss_AlienBody boss = context.Boss as Boss_AlienBody;

        if (boss == null)
        {
            throw new InvalidOperationException(
                $"{name}: Boss_AlienBody 전용 패턴입니다.");
        }

        if (!boss.CanRunPhaseTwoPattern)
            yield break;

        // 실행 전에 필요한 두 프리팹이 연결됐는지 검사한다.
        if (_warningPrefab == null || _impactPrefab == null)
        {
            throw new InvalidOperationException(
                $"{name}: Warning Prefab과 Impact Prefab을 연결하세요.");
        }

        // 현재 본체의 자세를 변경할 컴포넌트를 찾는다.
        BossAlienPhaseTwoVisual visual =
            context.VisualTransform.GetComponent<BossAlienPhaseTwoVisual>();

        if (visual == null)
        {
            throw new MissingComponentException(
                $"{name}: BodyVisual에 BossAlienPhaseTwoVisual이 필요합니다.");
        }

        // 최초 경고를 표시할 유효한 착지 좌표를 계산한다.
        if (!boss.TryGetLeapLandingPosition(
            _predictionSeconds,
            _maxPredictionDistance,
            _wallPadding,
            out Vector2 landingPosition))
        {
            yield break;
        }

        // 실행 상태는 공유 에셋의 필드가 아닌 지역 변수로 보관한다.
        BossAlienLeapWarning warning = null;
        bool beganAction = false;

        // 도약 종료나 취소 시 복원할 외형의 원래 로컬 위치다.
        Vector3 originalVisualPosition = context.VisualTransform.localPosition;

        try
        {
            // 웅크린 자세로 도약을 준비한다.
            beganAction = visual.BeginAction("Phase2_Leap", "Crouch");

            if (!beganAction)
                yield break;

            // 부모 없이 생성해 보스의 이동이나 Scale에 영향을 받지 않는다.
            warning = UnityEngine.Object.Instantiate(_warningPrefab);
            warning.Initialize(boss, landingPosition, _landingRadius);

            float elapsed = 0f;

            // 조준 중에는 플레이어의 이동에 맞춰 예상 착지점을 갱신한다.
            while (elapsed < _aimDuration)
            {
                if (boss == null ||
                    !boss.CanRunPhaseTwoPattern ||
                    warning == null)
                {
                    yield break;
                }

                if (!boss.TryGetLeapLandingPosition(
                    _predictionSeconds,
                    _maxPredictionDistance,
                    _wallPadding,
                    out landingPosition))
                {
                    yield break;
                }

                warning.SetCenter(landingPosition);

                elapsed += Time.deltaTime;
                yield return null;
            }

            // 마지막 대기 프레임 사이에 전투가 중단됐는지 확인한다.
            if (boss == null ||
                !boss.CanRunPhaseTwoPattern ||
                warning == null)
            {
                yield break;
            }

            // 여기부터는 landingPosition을 다시 계산하지 않는다.
            warning.LockPosition(landingPosition);

            elapsed = 0f;

            // 확정된 경고를 유지하여 플레이어에게 회피 시간을 준다.
            while (elapsed < _lockDuration)
            {
                if (boss == null ||
                    !boss.CanRunPhaseTwoPattern ||
                    warning == null)
                {
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // 위치 확정 대기 중 전투가 끝났으면 이륙하지 않는다.
            if (boss == null ||
                !boss.CanRunPhaseTwoPattern ||
                warning == null)
            {
                yield break;
            }

            // 이륙 자세를 잠시 보여준다.
            if (!visual.ShowPose("Phase2_Leap", "Takeoff"))
            {
                yield break;
            }

            elapsed = 0f;

            while (elapsed < _takeoffDuration)
            {
                if (boss == null ||
                    !boss.CanRunPhaseTwoPattern ||
                    warning == null)
                {
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // 이륙 자세를 보여주는 동안 전투가 중단됐는지 다시 확인한다.
            if (boss == null ||
                !boss.CanRunPhaseTwoPattern ||
                warning == null)
            {
                yield break;
            }

            if (!visual.ShowPose("Phase2_Leap", "Airborne"))
            {
                yield break;
            }

            // 출발점과 착지점은 여기부터 변경하지 않는다.
            Vector2 startPosition = context.Rigidbody.position;
            elapsed = 0f;

            // 물리 이동 요청을 일정한 간격으로 실행한다.
            WaitForFixedUpdate waitForPhysics = new WaitForFixedUpdate();

            while (elapsed < _flightDuration)
            {
                yield return waitForPhysics;

                if (boss == null ||
                    !boss.CanRunPhaseTwoPattern ||
                    warning == null)
                {
                    yield break;
                }

                elapsed += Time.fixedDeltaTime;
                float progress = Mathf.Clamp01(elapsed / _flightDuration);

                // 루트는 출발점과 착지점 사이를 직선으로 이동한다.
                Vector2 groundPosition = Vector2.Lerp(
                    startPosition,
                    landingPosition,
                    progress);

                context.Rigidbody.MovePosition(groundPosition);

                // 외형만 포물선 모양으로 올렸다 내린다.
                // 진행도 0과 1에서는 높이가 0, 0.5에서는 최대 높이가 된다.
                float height = 4f * _jumpHeight * progress * (1f - progress);

                context.VisualTransform.localPosition =
                    originalVisualPosition + Vector3.up * height;
            }

            // 마지막 이동 요청이 다음 물리 계산에 적용될 시간을 준다.
            yield return waitForPhysics;

            if (boss == null ||
                !boss.CanRunPhaseTwoPattern ||
                warning == null)
            {
                yield break;
            }

            // 물리 이동의 작은 오차를 제거하고 확정한 좌표에 착지한다.
            context.Rigidbody.position = landingPosition;
            context.Rigidbody.linearVelocity = Vector2.zero;
            context.VisualTransform.localPosition = originalVisualPosition;

            if (!visual.ShowPose("Phase2_Leap", "Landing"))
            {
                yield break;
            }

            // 착지 순간 경고를 제거한다.
            warning.Remove();
            warning = null;

            // 착지한 위치에 시각 효과를 생성한다.
            // 짧은 연출을 마치면 이펙트 스스로 제거된다.
            BossAlienLeapImpact impact =
                UnityEngine.Object.Instantiate(_impactPrefab);

            impact.Play(landingPosition, _landingRadius);

            // 기존 원형 피해 함수를 재사용한다.
            // 실드 방어를 먼저 검사하고 플레이어에게 중복 피해를 주지 않는다.
            BossAlienExplosionDamage.Apply(
                landingPosition,
                _landingRadius,
                _landingDamage);

            elapsed = 0f;

            // 착지 후 빈틈을 제공한다. 이 구간에서는 피해를 반복하지 않는다.
            while (elapsed < _recoveryDuration)
            {
                if (boss == null || !boss.CanRunPhaseTwoPattern)
                {
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        finally
        {
            // 정상 종료와 취소 모두 남아 있는 경고를 제거한다.
            if (warning != null)
            {
                warning.Remove();
            }

            // 공중에서 사망하거나 패턴이 취소돼도 외형 높이를 복원한다.
            // 루트 위치는 현재 위치를 유지하므로 출발점으로 순간이동하지 않는다.
            if (beganAction && context.VisualTransform != null)
            {
                context.VisualTransform.localPosition = originalVisualPosition;
            }

            // 남아 있는 도약 이동 속도를 제거한다.
            if (beganAction && context.Rigidbody != null)
            {
                context.Rigidbody.linearVelocity = Vector2.zero;
            }

            // 정상 종료 시 Idle로 돌아간다.
            // 사망 중에는 표현 컴포넌트가 Idle 복귀를 막는다.
            if (beganAction && visual != null)
            {
                visual.EndAction();
            }
        }
    }
}
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

    // 공중에서 외형을 위로 띄우는 최대 높이다.
    // BodyVisual의 로컬 좌표 기준이며 몸체 콜라이더는 따라 올라가지 않는다.
    [SerializeField, Min(0f)]
    private float _jumpHeight = 2.5f;

    // 실제 이륙부터 착지 피해가 발생하기까지 걸리는 게임 시간이다.
    // 이 시간에 맞춰 Leap 클립의 재생 배속을 계산한다.
    [SerializeField, Min(0.05f)]
    private float _flightDuration = 22f / 24f;

    // 착지 순간 원형 범위에 한 번 요청할 피해량이다.
    [SerializeField, Min(0)]
    private float _landingDamage = 10;

    // Alien_AxeKick_028의 발 접촉 기준점이다.
    // 2048 캔버스, 중앙 Pivot, PPU 100 기준의 SpriteRenderer 로컬 좌표다.
    // 보스 Scale 0.43은 Transform 계산에서 적용하므로 여기에 곱하지 않는다.
    [SerializeField]
    private Vector2 _landingContactLocal = new Vector2(-2.144f, -5.5896f);

    /// <summary>
    /// Execute 동작에 필요한 상태와 처리 흐름을 실행한다.
    /// </summary>
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

        // 실제 착지 이미지의 접촉점을 보스 원점 기준 월드 오프셋으로 환산한다.
        SpriteRenderer bodyRenderer =
            context.VisualTransform.GetComponent<SpriteRenderer>();

        if (bodyRenderer == null)
        {
            throw new MissingComponentException(
                $"{name}: BodyVisual에 SpriteRenderer가 필요합니다.");
        }

        // SpriteRenderer의 좌우·상하 반전도 접촉점에 반영한다.
        Vector3 contactLocal = new Vector3(
            bodyRenderer.flipX ? -_landingContactLocal.x : _landingContactLocal.x,
            bodyRenderer.flipY ? -_landingContactLocal.y : _landingContactLocal.y,
            0f);

        // 부모 Scale과 BodyVisual의 기본 위치를 포함해 계산한다.
        // 아직 점프 높이를 적용하기 전이므로 기본 자세 기준 오프셋이 된다.
        Vector2 landingContactOffset =
            (Vector2)context.VisualTransform.TransformPoint(contactLocal)
            - context.Rigidbody.position;

        // 경고 중심이 실제 발 접촉 위치가 되도록 계산한다.
        if (!boss.TryGetLeapLandingPosition(
            _predictionSeconds,
            _maxPredictionDistance,
            _wallPadding,
            landingContactOffset,
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
            // 플레이어를 관찰하고 착지점을 고정하는 동안 새 웅크리기 자세를 표시한다.
            beganAction = visual.BeginAction(
                "Phase2_Leap_AxeKick",
                "Alien_AxeKick_004");

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

                // 플레이어를 관찰하는 동안에도 같은 접촉점 기준으로 갱신한다.
                if (!boss.TryGetLeapLandingPosition(
                    _predictionSeconds,
                    _maxPredictionDistance,
                    _wallPadding,
                    landingContactOffset,
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

            // 현재 도약 클립은 24 FPS, 전체 36프레임 길이다.
            const float clipDuration = 36f / 24f;

            // 5프레임 위치에서 이륙하고, 27프레임 위치에서 착지 공격한다.
            const float takeoffProgress = 5f / 36f;
            const float impactProgress = 27f / 36f;

            // Inspector 제한과 별개로 실행 시에도 시간값을 검사한다.
            if (float.IsNaN(_flightDuration) ||
                float.IsInfinity(_flightDuration) ||
                _flightDuration < 0.05f)
            {
                throw new InvalidOperationException(
                    $"{name}: Flight Duration은 0.05초 이상이어야 합니다.");
            }

            // 원본 클립에서 이륙부터 착지까지 걸리는 시간이다.
            // 1.5초 × (27/36 - 5/36) = 약 0.917초.
            float originalFlightDuration =
                clipDuration * (impactProgress - takeoffProgress);

            // 목표 시간이 짧으면 빠르게, 길면 느리게 재생한다.
            float playbackSpeed =
                originalFlightDuration / _flightDuration;

            // 계산한 배속으로 도약 애니메이션을 시작한다.
            if (!visual.PlayLeapAnimation(playbackSpeed))
                yield break;

            // 실제 이동 시작 위치는 착지점을 고정한 뒤의 보스 위치다.
            Vector2 startPosition = context.Rigidbody.position;

            // 경고 중심에 발을 놓으려면 보스 원점은 접촉 오프셋만큼 반대쪽에 있어야 한다.
            Vector2 landingRootPosition =
                landingPosition - landingContactOffset;

            // 착지 피해는 클립 진행률이 착지 지점을 넘는 순간 한 번만 적용한다.
            bool hasLanded = false;
            bool leapSoundPlayed = false; // 실제 이륙 프레임을 한 번 감지.

            // Rigidbody 이동은 물리 갱신 주기에 맞춘다.
            WaitForFixedUpdate waitForPhysics = new WaitForFixedUpdate();

            while (true)
            {
                yield return waitForPhysics;

                // 사망·전투 중단 시 finally에서 경고와 표시 위치를 정리한다.
                if (boss == null || !boss.CanRunPhaseTwoPattern)
                    yield break;

                // 애니메이션이 다른 상태로 바뀌었다면 공격도 중단한다.
                if (!visual.TryGetLeapProgress(out float progress))
                    yield break;
                if (!leapSoundPlayed && progress >= takeoffProgress)
                {
                    leapSoundPlayed = true;
                    GameAudio.Play(GameSound.AlienP2Leap);
                }

                if (!hasLanded)
                {
                    if (warning == null)
                        yield break;

                    if (progress >= impactProgress)
                    {
                        // 발 접촉점이 경고 중심에 닿도록 보정된 보스 위치를 적용한다.
                        context.Rigidbody.position = landingRootPosition;
                        context.Rigidbody.linearVelocity = Vector2.zero;
                        context.VisualTransform.localPosition =
                            originalVisualPosition;

                        // 먼저 착지 완료를 기록하여 중복 피해를 방지한다.
                        hasLanded = true;

                        // 착지 순간 경고를 제거한다.
                        warning.Remove();
                        warning = null;

                        // 기존 착지 이펙트를 유지한다.
                        // 새 이펙트와 화면 연출은 이후 교체 단계에서 연결한다.
                        BossAlienLeapImpact impact =
                            UnityEngine.Object.Instantiate(_impactPrefab);

                        impact.Play(landingPosition, _landingRadius);

                        // 내려찍는 이미지에 맞춰 범위 피해를 한 번 적용한다.
                        BossAlienExplosionDamage.Apply(
                            landingPosition,
                            _landingRadius,
                            _landingDamage);
                    }
                    else if (progress >= takeoffProgress)
                    {
                        // 도약 시작부터 착지까지의 구간을 0~1로 환산한다.
                        float flightProgress = Mathf.InverseLerp(
                            takeoffProgress,
                            impactProgress,
                            progress);

                        // 공중 이동도 보정된 목적지를 향하게 해서 착지 순간 위치가 튀지 않도록 한다.
                        Vector2 groundPosition = Vector2.Lerp(
                            startPosition,
                            landingRootPosition,
                            flightProgress);

                        context.Rigidbody.MovePosition(groundPosition);

                        // 기존 방식대로 BodyVisual만 포물선 높이를 적용한다.
                        // 회전은 스프라이트에 포함되어 있으므로 Transform은 회전하지 않는다.
                        float height =
                            4f * _jumpHeight *
                            flightProgress * (1f - flightProgress);

                        context.VisualTransform.localPosition =
                            originalVisualPosition + Vector3.up * height;
                    }
                }

                // 착지 이후에도 클립 끝까지 기다려 회복 자세를 보여준다.
                // 완료 후 기존 finally의 EndAction이 Idle로 복귀시킨다.
                if (progress >= 1f)
                    break;
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
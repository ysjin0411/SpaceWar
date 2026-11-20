using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 경고 후 회전 레이저를 발사하고 최초 스폰 위치로 복귀한다.
/// 실행 중 상태는 지역 변수에 보관한다.
/// </summary>
[CreateAssetMenu(
    fileName = "BossHeadLaserSweepPattern",
    menuName = "BossPattern/Head/Laser Sweep")]
public sealed class BossHeadLaserSweepPattern : BossPattern
{
    // 시스템 다이얼로그에 표시할 경고 문구다.
    [TextArea]
    [SerializeField] string _systemMessage;

    // 시스템 경고 후 보스가 이동할 월드 좌표다.
    [SerializeField] Vector2 _attackPosition = new Vector2(5f, 0f);

    // 공격 위치까지 이동하는 시간이다.
    [Min(0.01f)]
    [SerializeField] float _moveDuration = 0.5f;

    // 최초 발사 방향을 정하는 월드 조준 좌표다.
    // 실제 레이저 끝은 이 점을 넘어 화면 밖까지 연장된다.
    [SerializeField] Vector2 _aimPoint = new Vector2(-5f, 3f);

    // 최초 방향에서 회전할 각도다.
    // 양수는 반시계, 음수는 시계 방향이다.
    [Range(-150f, 150f)]
    [SerializeField] float _sweepAngle = 40f;

    // 레이저 방향이 전체 각도를 회전하는 시간이다.
    [Min(0.01f)]
    [SerializeField] float _sweepDuration = 3f;

    // 보스 이미지의 기본 정면 방향을 보정한다.
    // 원본 이미지가 왼쪽을 바라보면 180을 사용한다.
    [SerializeField] float _visualAngleOffset = 180f;

    // 생성할 레이저 프리팹이다.
    [SerializeField] BossLaserBeam _laserPrefab;

    // 레이저의 피해 간격마다 적용할 피해량이다.
    [Min(1)]
    [SerializeField] float _damage = 20;

    // 전체 위험 범위를 표시할 경고 프리팹이다.
    [SerializeField] BossLaserWarning _warningPrefab;

    // 경고가 한 번 밝아졌다 어두워지는 시간이다.
    [Min(0.01f)]
    [SerializeField] float _warningBlinkDuration = 0.3f;

    // 위험 범위 경고를 점멸하는 횟수다.
    [Min(1)]
    [SerializeField] int _warningBlinkCount = 3;

    // 레이저 소멸 후 최초 스폰 위치로 복귀하는 시간이다.
    [Min(0.01f)]
    [SerializeField] float _returnDuration = 0.35f;

    /// <summary>
    /// 경고부터 복귀까지 하나의 특수패턴으로 실행한다.
    /// </summary>
    public override IEnumerator Execute(BossPatternContext context)
    {
        Camera battleCamera = Camera.main;
        ValidateSettings(context, battleCamera);

        BossEnemy boss = context.Boss;
        Transform visual = context.VisualTransform;

        // 정상 종료 후 복원할 외형 회전이다.
        Quaternion originalRotation = visual.localRotation;

        // 복귀 위치는 공격 시작 위치가 아니라 최초 스폰 위치다.
        Vector2 returnPosition = boss.MovementCenter;

        BossLaserWarning warning = null;
        BossLaserBeam laser = null;

        bool dialogueCompleted = false;
        GameAudio.Handle chargeAudio = default; // 패턴 취소 시 정리할 준비음.

        // 시스템 다이얼로그 완료 신호를 받을 함수다.
        Action onDialogueCompleted = () => dialogueCompleted = true;

        boss.SystemDialogueCompleted += onDialogueCompleted;

        try
        {
            // Runner가 이미 특수패턴 시작 이벤트를 보낸 상태다.
            boss.RequestSystemDialogue(_systemMessage);

            while (IsAlive(boss) && !dialogueCompleted)
            {
                yield return null;
            }

            if (!IsAlive(boss))
            {
                yield break;
            }

            // 경고 문구가 끝난 뒤 공격 위치로 이동한다.
            yield return MoveAndRotate(
                context, _attackPosition,
                originalRotation, _moveDuration);

            if (!IsAlive(boss))
            {
                yield break;
            }

            Vector2 aimDirection =
                _aimPoint - (Vector2)boss.FirePoint.position;

            if (aimDirection.sqrMagnitude < 0.000001f)
            {
                throw new InvalidOperationException(
                    $"{name}: Aim Point와 FirePoint가 같은 위치입니다.");
            }

            float startAngle = Mathf.Atan2(
                aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;

            // 레이저 발사 전부터 최초 발사 방향을 바라본다.
            SetVisualAngle(visual, startAngle);

            warning = Instantiate(_warningPrefab);

            warning.Initialize(
                boss,
                battleCamera,
                boss.FirePoint.position,
                startAngle,
                _sweepAngle,
                _laserPrefab.DamageWidth);
            chargeAudio = GameAudio.PlayOwned(GameSound.HeadLaserCharge, boss);

            // 레이저가 지나갈 전체 범위를 먼저 점멸한다.
            float warningDuration =
                _warningBlinkDuration * _warningBlinkCount;

            float elapsed = 0f;

            while (IsAlive(boss) && elapsed < warningDuration)
            {
                elapsed += Time.deltaTime;

                // 한 주기마다 0 → 1 → 0으로 부드럽게 점멸한다.
                float phase = elapsed / _warningBlinkDuration;
                float opacity = 0.5f -
                    0.5f * Mathf.Cos(phase * Mathf.PI * 2f);

                warning.SetOpacity(opacity);
                yield return null;
            }

            if (warning != null)
            {
                Destroy(warning.gameObject);
                warning = null;
            }

            if (!IsAlive(boss))
            {
                yield break;
            }

            // 위험 범위 경고가 끝난 뒤 실제 레이저를 생성한다.
            GameAudio.Stop(chargeAudio); // 발사로 전환.
            laser = Instantiate(_laserPrefab);

            laser.Initialize(
                boss,
                boss.FirePoint,
                battleCamera,
                Direction(startAngle),
                _damage);

            elapsed = 0f;

            while (IsAlive(boss) && laser != null &&
                   elapsed < _sweepDuration)
            {
                elapsed += Time.deltaTime;
                float ratio = Mathf.Clamp01(elapsed / _sweepDuration);

                float angle = startAngle + _sweepAngle * ratio;

                // 보스 외형과 레이저에 같은 회전 각도를 적용한다.
                SetVisualAngle(visual, angle);
                laser.SetDirection(Direction(angle));

                // 마지막 각도가 LateUpdate에서 선에 적용될 시간도 확보한다.
                yield return null;
            }

            if (!IsAlive(boss))
            {
                yield break;
            }

            if (laser != null)
            {
                // 피해는 즉시 끄고 DOTween 소멸 완료까지 기다린다.
                yield return laser.FadeOutAndWait();
                laser = null;
            }

            if (!IsAlive(boss))
            {
                yield break;
            }

            // 레이저 공격 종료 후 복귀하는 동안 몸체 충돌을 무시한다.
            boss.BeginReturnCollisionIgnore();

            // 스폰 위치와 원래 회전으로 복귀한다.
            yield return MoveAndRotate(
                context, returnPosition,
                originalRotation, _returnDuration);

            // 여기까지 완료되어야 Runner가 특수패턴 종료를 알린다.
        }
        finally
        {
            GameAudio.Stop(chargeAudio); // 예외·취소 경로도 정리.
            // 정상 종료와 중단 시 남은 참조를 정리한다.
            if (boss != null)
            {
                boss.SystemDialogueCompleted -= onDialogueCompleted;
            }

            if (warning != null)
            {
                Destroy(warning.gameObject);
            }

            if (laser != null)
            {
                laser.BeginFadeOut();
            }

            if (visual != null && IsAlive(boss))
            {
                visual.localRotation = originalRotation;
            }
        }
    }

    /// <summary>
    /// 지정 위치로 이동하면서 외형의 로컬 회전도 복원한다.
    /// </summary>
    private IEnumerator MoveAndRotate(
        BossPatternContext context,
        Vector2 destination,
        Quaternion targetRotation,
        float duration)
    {
        Vector2 start = context.Rigidbody.position;
        Quaternion startRotation = context.VisualTransform.localRotation;
        float elapsed = 0f;

        while (IsAlive(context.Boss) && elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float ratio = Mathf.SmoothStep(
                0f, 1f, Mathf.Clamp01(elapsed / duration));

            // 현재 보스는 Kinematic Rigidbody2D를 사용한다.
            context.Rigidbody.position =
                Vector2.Lerp(start, destination, ratio);

            context.VisualTransform.localRotation =
                Quaternion.Slerp(startRotation, targetRotation, ratio);

            yield return null;
        }
    }

    /// <summary>
    /// 외형의 정면 방향을 보정하여 레이저 방향으로 회전한다.
    /// </summary>
    private void SetVisualAngle(Transform visual, float angle)
    {
        visual.rotation = Quaternion.Euler(
            0f, 0f, angle + _visualAngleOffset);
    }

    /// <summary>
    /// Z축 회전 각도를 XY 방향으로 변환한다.
    /// </summary>
    private static Vector2 Direction(float angle)
    {
        float radians = angle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    /// <summary>
    /// 사망하거나 비활성화된 보스는 다음 행동을 실행하지 않는다.
    /// </summary>
    private static bool IsAlive(BossEnemy boss)
    {
        return boss != null &&
               !boss.IsDead &&
               boss.isActiveAndEnabled;
    }

    /// <summary>
    /// 패턴 실행 전에 필수 연결과 수치를 검사한다.
    /// </summary>
    private void ValidateSettings(
        BossPatternContext context,
        Camera battleCamera)
    {
        if (context == null || battleCamera == null ||
            _laserPrefab == null || _warningPrefab == null)
        {
            throw new InvalidOperationException(
                $"{name}: Context, MainCamera, 레이저와 경고 프리팹이 필요합니다.");
        }

        if (!PauseRandomMovement)
        {
            throw new InvalidOperationException(
                $"{name}: Pause Random Movement를 켜세요.");
        }

        if (!battleCamera.orthographic ||
            Mathf.Abs(Vector3.Dot(
                battleCamera.transform.forward,
                Vector3.forward)) < 0.9999f)
        {
            throw new InvalidOperationException(
                $"{name}: XY 평면을 바라보는 직교 카메라가 필요합니다.");
        }

        if (string.IsNullOrWhiteSpace(_systemMessage) ||
            Mathf.Abs(_sweepAngle) < 1f ||
            Mathf.Abs(_sweepAngle) > 150f ||
            _moveDuration <= 0f || _sweepDuration <= 0f ||
            _returnDuration <= 0f || _warningBlinkDuration <= 0f ||
            _warningBlinkCount < 1 || _damage <= 0)
        {
            throw new InvalidOperationException(
                $"{name}: 경고 문구와 패턴 수치를 확인하세요.");
        }

        // 현재 구조에서는 총구를 보스 루트 아래에 두어
        // 외형 회전으로 경고의 중심점이 움직이지 않도록 한다.
        if (context.Boss.FirePoint.IsChildOf(context.VisualTransform))
        {
            throw new InvalidOperationException(
                $"{name}: FirePoint를 Renderer 밖의 보스 루트 아래에 두세요.");
        }
    }
}
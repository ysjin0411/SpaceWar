using System;
using System.Collections;
using UnityEngine;

// 추적 후 각 타격마다 조준, 방향 고정, 공격, 회복을 실행한다.
[CreateAssetMenu(
    menuName = "Boss/Alien/Phase2/Combo Pattern",
    fileName = "BossAlienComboPattern")]
public sealed class BossAlienComboPattern : BossPattern
{
    // 추적 및 세 타격의 자세, 시간, 피해 설정이다.
    [SerializeField]
    private BossAlienComboConfig _config;

    // 조준과 실제 공격 범위를 표시할 경고 프리팹이다.
    [SerializeField]
    private BossAlienComboWarning _warningPrefab;

    // 타격 순간에 생성할 시각 효과 프리팹이다.
    [SerializeField]
    private BossAlienComboEffect _effectPrefab;

    // 첫 번째 할퀴기 이펙트의 이미지 9장을 번호 순서대로 등록한다.
    [SerializeField]
    private Sprite[] _slashAFrames = Array.Empty<Sprite>();

    // 두 번째 할퀴기 이펙트의 이미지 9장을 번호 순서대로 등록한다.
    [SerializeField]
    private Sprite[] _slashBFrames = Array.Empty<Sprite>();

    // 마지막 내려찍기 이펙트의 이미지 12장을 번호 순서대로 등록한다.
    [SerializeField]
    private Sprite[] _slamFrames = Array.Empty<Sprite>();

    // 공격 중 표시되는 자세에 맞춰 손의 로컬 위치를 반환한다.
    // 제작 자료의 2048×2048 이미지 좌표를 기준으로 한다.
    private static Vector2 GetComboHandLocalPosition(
        int strikeIndex,
        float hitProgress)
    {
        Vector2 pixelPosition;

        switch (strikeIndex)
        {
            case 0:
                // 첫 0.05초는 손을 뻗고, 이후에는 할퀴는 자세다.
                pixelPosition = hitProgress < 1f / 3f
                    ? new Vector2(380f, 950f)
                    : new Vector2(590f, 1190f);
                break;

            case 1:
                // 첫 0.05초는 할퀴기, 이후에는 반대손 올려치기다.
                pixelPosition = hitProgress < 1f / 3f
                    ? new Vector2(660f, 1080f)
                    : new Vector2(500f, 700f);
                break;

            case 2:
                // 첫 0.05초는 내려찍는 중간 자세, 이후에는 접촉 자세다.
                pixelPosition = hitProgress < 0.25f
                    ? new Vector2(640f, 1150f)
                    : new Vector2(620f, 1490f);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(strikeIndex));
        }

        // 이미지 왼쪽 위 기준 좌표를 중앙 Pivot, PPU 100 기준으로 변환한다.
        return new Vector2(
            (pixelPosition.x - 1024f) / 100f,
            (1024f - pixelPosition.y) / 100f);
    }

    public override IEnumerator Execute(BossPatternContext context)
    {
        // 본체 전용 패턴이며 실행 전에 설정을 검사한다.
        Boss_AlienBody boss = context.Boss as Boss_AlienBody;

        if (boss == null)
        {
            throw new InvalidOperationException(
                $"{name}: Boss_AlienBody 전용 패턴입니다.");
        }

        ValidateConfig();

        if (!CanContinue(boss))
            yield break;

        BossAlienPhaseTwoVisual visual =
            context.VisualTransform.GetComponent<BossAlienPhaseTwoVisual>();

        SpriteRenderer renderer =
            context.VisualTransform.GetComponent<SpriteRenderer>();

        if (visual == null || renderer == null)
        {
            throw new MissingComponentException(
                $"{name}: BodyVisual의 표현 컴포넌트를 확인하세요.");
        }

        // 실행 상태는 공유 설정 에셋에 저장하지 않는다.
        BossAlienComboWarning warning = null;
        // 현재 타격의 효과를 보관하여 정상 종료와 중단 시 제거한다.
        BossAlienComboEffect effect = null;
        bool beganAction = false;
        bool originalFlipX = renderer.flipX;

        // 플레이어와 위치가 겹치면 마지막 유효한 방향을 사용한다.
        Vector2 direction = originalFlipX ? Vector2.right : Vector2.left;

        try
        {
            // 추적 이동도 보스 루트가 전투 영역 밖으로 나가지 않게 한다.
            Rect area = boss.GetBattleArea();
            float padding = _config.WallPadding;

            if (area.width < padding * 2f ||
                area.height < padding * 2f)
            {
                throw new InvalidOperationException(
                    $"{name}: 전투 영역에 비해 Wall Padding이 큽니다.");
            }

            // 각 추적에서 물리 이동이 반영된 뒤 현재 거리를 확인한다.
            WaitForFixedUpdate waitForPhysics = new WaitForFixedUpdate();

            // 현재 3연격 공격 클립의 전체 길이다.
            const float comboClipDuration = 69f / 20f;

            // 전체 공격 클립에서 이번 타격의 준비 동작이 시작되는 시각이다.
            // 추적 시간은 공격 클립 시간에 포함하지 않는다.
            float strikeStartTime = 0f;

            // 각 타격은 별도의 경고와 피해 기록을 사용한다.
            for (int i = 0; i < _config.StrikeCount; i++)
            {
                // 전투가 종료됐다면 다음 타격을 시작하지 않는다.
                if (!CanContinue(boss))
                    yield break;

                // 이번 타격의 이동 속도, 달리기 배속, 공격 설정을 가져온다.
                BossAlienComboConfig.StrikeSettings strike =
                    _config.GetStrike(i);

                // 첫 타격뿐 아니라 두 번째와 세 번째 타격도 추적으로 시작한다.
                if (!visual.BeginComboChase(strike.ChaseAnimationSpeed))
                    yield break;

                // 중간에 종료되더라도 finally에서 애니메이션을 정리한다.
                beganAction = true;

                // 추적 제한 시간은 타격마다 새로 계산한다.
                float chaseElapsed = 0f;

                while (true)
                {
                    // 이전 MovePosition이 물리에 반영된 위치에서 판단한다.
                    yield return waitForPhysics;

                    if (!CanContinue(boss))
                        yield break;

                    Vector2 position = context.Rigidbody.position;
                    Vector2 target = context.TargetPlayer.transform.position;
                    Vector2 delta = target - position;

                    float distance = delta.magnitude;

                    // 매 물리 주기마다 현재 플레이어 위치를 향해 추적한다.
                    // 위치가 겹치면 마지막으로 사용한 방향을 유지한다.
                    if (delta.sqrMagnitude > 0.0001f)
                    {
                        direction = delta.normalized;
                        FaceDirection(renderer, direction);
                    }

                    // R 거리 안에 실제로 접근했을 때만 공격 준비로 넘어간다.
                    // 작은 허용 오차로 거리 경계에서의 소수점 오차를 처리한다.
                    if (distance <= _config.StopDistance + 0.001f)
                        break;

                    // 제한 시간 동안 접근하지 못했다면 콤보를 종료한다.
                    // 멀리 떨어진 상태에서 허공을 공격하지 않는다.
                    if (chaseElapsed >= _config.MaxChaseDuration)
                        yield break;

                    // 각 타격에 설정한 N, M, Z 속도로 이동한다.
                    // 이번 이동으로 R 거리보다 안쪽까지 지나치지 않게 제한한다.
                    float travel = Mathf.Min(
                        strike.ChaseSpeed * Time.fixedDeltaTime,
                        distance - _config.StopDistance);

                    Vector2 next = position + direction * travel;

                    // 보스 루트가 전투 영역 밖으로 나가지 않게 제한한다.
                    next.x = Mathf.Clamp(
                        next.x,
                        area.xMin + padding,
                        area.xMax - padding);

                    next.y = Mathf.Clamp(
                        next.y,
                        area.yMin + padding,
                        area.yMax - padding);

                    context.Rigidbody.MovePosition(next);
                    chaseElapsed += Time.fixedDeltaTime;
                }

                // 접근 완료 후에는 제자리에서 경고와 공격 동작을 진행한다.
                context.Rigidbody.linearVelocity = Vector2.zero;

                // 이번 타격의 시작 위치를 먼저 보관한다.
                // 다음 타격 시간을 계산한 뒤에도 이 값으로 올바른 자세를 재생한다.
                float attackClipStart = strikeStartTime / comboClipDuration;

                // 기존 공격 클립의 준비·타격·회복 시점을 유지한다.
                // 이제 준비 시간 동안에는 이동하거나 재조준하지 않는다.
                float attackStartTime =
                    strikeStartTime + strike.AimDuration + strike.LockDuration;

                float attackEndTime =
                    attackStartTime + strike.ActiveDuration;

                float recoveryEndTime =
                    attackEndTime + strike.RecoveryDuration;

                // 다음 반복에서 사용할 다음 타격의 시작 시각이다.
                strikeStartTime = recoveryEndTime;

                // 1타는 1타 준비, 2타는 2타 준비, 3타는 3타 준비부터 재생한다.
                if (!visual.PlayComboAttack(attackClipStart))
                    yield break;

                // 접근 완료 시점의 플레이어 위치를 이번 공격 중심으로 고정한다.
                // 경고 이후 플레이어가 이동해도 다시 따라가지 않는다.
                Vector2 center = context.TargetPlayer.transform.position;

                // 본체와 같은 회전 기준으로 사각형 경고를 표시한다.
                float angle = context.VisualTransform.eulerAngles.z;

                // 경고와 실제 피해 판정이 동일한 고정 좌표를 사용한다.
                warning = UnityEngine.Object.Instantiate(_warningPrefab);
                warning.Initialize(boss, center, strike.HitboxSize, angle);
                warning.LockAim();

                // 방향을 고정한 채 실제 공격 이미지가 시작되는 시점까지 기다린다.
                while (true)
                {
                    if (!CanContinue(boss) || warning == null)
                        yield break;

                    if (!visual.TryGetComboAttackProgress(out float progress))
                        yield break;

                    float animationTime = progress * comboClipDuration;

                    if (animationTime >= attackStartTime)
                        break;

                    // 이 구간에서는 방향과 경고 위치를 갱신하지 않는다.
                    yield return null;
                }

                if (!CanContinue(boss) || warning == null)
                    yield break;

                warning.ShowAttack();

                // 현재 타격에 사용할 이펙트 이미지 목록을 선택한다.
                Sprite[] effectFrames;

                switch (i)
                {
                    case 0:
                        effectFrames = _slashAFrames;
                        break;

                    case 1:
                        effectFrames = _slashBFrames;
                        break;

                    case 2:
                        effectFrames = _slamFrames;
                        break;

                    default:
                        throw new InvalidOperationException(
                            $"{name}: 새 Combo 이펙트는 3타 구성입니다.");
                }

                // 제작 미리보기의 본체 대비 이펙트 크기를 참고한 초기 비율이다.
                // 보스의 실제 Scale은 SetHandPose에서 별도로 반영한다.
                // 경고가 끝나고 실제 타격이 시작될 때 종류별로 선택한다.
                GameAudio.Play((GameSound)((int)GameSound.AlienP2Claw01 + i));
                const float effectScaleRatio = 0.85f;

                // 새 이펙트를 만들고 첫 공격 자세의 손 앞에 배치한다.
                if (_effectPrefab != null)
                {
                    effect = UnityEngine.Object.Instantiate(_effectPrefab);
                    effect.Initialize(boss, effectFrames);

                    effect.SetHandPose(
                        context.VisualTransform,
                        GetComboHandLocalPosition(i, 0f),
                        renderer.flipX,
                        renderer.flipY,
                        effectScaleRatio);

                    // 손동작 위치를 설정한 직후, 이번 경고의 고정 중심으로 보정한다.
                    AlignEffectToAttackCenter(
                        effect,
                        context.VisualTransform,
                        renderer,
                        i,
                        warning.Center);
                }

                // 경고가 확정한 좌표, 크기, 각도로 실제 판정을 만든다.
                BossAlienComboHitbox hitbox = new BossAlienComboHitbox(
                    context.TargetPlayer,
                    warning.Center,
                    warning.Size,
                    warning.Angle,
                    strike.Damage);

                // 공격 이미지가 재생되는 구간에서만 피해를 검사한다.
                while (true)
                {
                    if (!CanContinue(boss) || warning == null)
                        yield break;

                    if (!visual.TryGetComboAttackProgress(out float progress))
                        yield break;

                    float animationTime = progress * comboClipDuration;

                    // 애니메이션이 회수 동작에 들어가면 피해 검사를 종료한다.
                    if (animationTime >= attackEndTime)
                        break;

                    // 일시정지 중에는 피해 검사와 효과 진행을 멈춘다.
                    if (Time.deltaTime > 0f)
                    {
                        // 이번 타격의 공격 구간 안에서 진행률을 계산한다.
                        float hitProgress = Mathf.InverseLerp(
                            attackStartTime,
                            attackEndTime,
                            animationTime);

                        if (effect != null)
                        {
                            // 공격 진행률에 맞는 손 위치를 선택한다.
                            Vector2 handLocal =
                                GetComboHandLocalPosition(i, hitProgress);

                            // 본체의 손동작과 좌우 반전을 따라 이펙트를 배치한다.
                            // 플레이어 위치를 다시 조준하는 처리는 하지 않는다.
                            effect.SetHandPose(
                                context.VisualTransform,
                                handLocal,
                                renderer.flipX,
                                renderer.flipY,
                                effectScaleRatio);

                            // 손동작 위치를 설정한 직후, 이번 경고의 고정 중심으로 보정한다.
                            AlignEffectToAttackCenter(
                                effect,
                                context.VisualTransform,
                                renderer,
                                i,
                                warning.Center);

                            // 같은 진행률로 이펙트 이미지도 교체한다.
                            effect.SetProgress(hitProgress);
                        }

                        // 기존 Hitbox가 같은 타격의 중복 피해를 막는다.
                        hitbox.Tick();
                    }

                    yield return null;
                }

                if (!CanContinue(boss) || warning == null)
                    yield break;

                // 공격 구간이 끝나면 범위 표시와 피해 검사를 종료한다.
                warning.Remove();
                warning = null;
                // 이번 타격의 효과를 지우고 다음 타격을 준비한다.
                if (effect != null)
                {
                    effect.Remove();
                    effect = null;
                }

                // 회수 동작이 끝나는 애니메이션 시점까지 기다린다.
                // 회수 중에는 공격 방향을 갱신하지 않는다.
                while (true)
                {
                    if (!CanContinue(boss))
                        yield break;

                    if (!visual.TryGetComboAttackProgress(out float progress))
                        yield break;

                    float animationTime = progress * comboClipDuration;

                    // 클립 종료도 함께 확인해 마지막 타격의 소수점 오차로
                    // 회수 대기가 끝나지 않는 상황을 방지한다.
                    if (progress >= 1f || animationTime >= recoveryEndTime)
                        break;

                    yield return null;
                }
            }
        }

        finally
        {
            // 공격 도중 사망하거나 비활성화돼도 효과가 남지 않게 한다.
            if (effect != null)
            {
                effect.Remove();
                effect = null;
            }

            // 정상 종료, 사망, 패턴 취소 모두 남은 경고를 제거한다.
            if (warning != null)
            {
                warning.Remove();
            }

            // 추적 이동의 속도가 남지 않게 한다.
            if (context.Rigidbody != null)
            {
                context.Rigidbody.linearVelocity = Vector2.zero;
            }

            // 패턴 진입 전의 좌우 표시 상태를 복원한다.
            if (renderer != null)
            {
                renderer.flipX = originalFlipX;
            }

            // 살아 있는 경우 Idle로 복귀하고, 사망 연출은 덮어쓰지 않는다.
            if (beganAction && visual != null)
            {
                visual.EndAction();
            }
        }
    }

    // 기존 이펙트의 휘두르는 움직임은 유지하면서,
    // 그 움직임의 기준 중심을 고정된 공격 중심으로 옮긴다.
    private static void AlignEffectToAttackCenter(
        BossAlienComboEffect effect,
        Transform bodyVisual,
        SpriteRenderer renderer,
        int strikeIndex,
        Vector2 attackCenter)
    {
        // 기존 손동작이 기준으로 삼던 공격 중심이다.
        Vector2 originalCenter = GetComboAttackCenter(
            bodyVisual, renderer, strikeIndex);

        // 새 공격 중심과 기존 중심 사이의 차이만큼 보정한다.
        Vector2 offset = attackCenter - originalCenter;

        // SetHandPose 직후 호출해야 위치 보정이 매번 누적되지 않는다.
        effect.transform.position +=
            new Vector3(offset.x, offset.y, 0f);
    }

    // 이번 타격의 손동작 시작점과 끝점 사이를 경고·피해 영역의 중심으로 사용한다.
    private static Vector2 GetComboAttackCenter(
        Transform bodyVisual,
        SpriteRenderer renderer,
        int strikeIndex)
    {
        Vector2 startHand = GetComboHandLocalPosition(strikeIndex, 0f);
        Vector2 endHand = GetComboHandLocalPosition(strikeIndex, 1f);

        // 손동작의 중간 위치에 이펙트와 동일한 앞쪽 여유 거리를 적용한다.
        Vector2 localCenter = (startHand + endHand) * 0.5f;
        localCenter.x -= 1.5f;

        // 본체가 반전되면 경고와 피해 중심도 함께 반전한다.
        if (renderer.flipX)
            localCenter.x = -localCenter.x;

        if (renderer.flipY)
            localCenter.y = -localCenter.y;

        // 본체 위치와 Scale을 반영한 월드 좌표를 반환한다.
        return bodyVisual.TransformPoint(
            new Vector3(localCenter.x, localCenter.y, 0f));
    }

    // 플레이어와 보스가 겹치면 이전 방향을 유지한다.
    private static Vector2 GetDirection(
        BossPatternContext context,
        Vector2 fallback)
    {
        Vector2 delta = (Vector2)context.TargetPlayer.transform.position
            - context.Rigidbody.position;

        return delta.sqrMagnitude > 0.0001f
            ? delta.normalized
            : fallback;
    }

    // 현재 스프라이트의 기본 방향은 왼쪽이다.
    // 상하 공격에서도 캐릭터를 눕히지 않고 좌우 반전만 사용한다.
    private static void FaceDirection(
        SpriteRenderer renderer,
        Vector2 direction)
    {
        if (Mathf.Abs(direction.x) > 0.01f)
        {
            renderer.flipX = direction.x > 0f;
        }
    }

    // 플레이어 사망과 본체 비활성화를 포함한 실행 조건이다.
    private static bool CanContinue(Boss_AlienBody boss)
    {
        return boss != null && boss.CanRunPhaseTwoPattern;
    }
    // 공통 설정과 각 타격의 설정을 실행 전에 검사한다.
    private void ValidateConfig()
    {
        // 패턴 실행에 필요한 에셋 연결을 확인한다.
        if (_config == null || _warningPrefab == null)
        {
            throw new InvalidOperationException(
                $"{name}: Config와 Warning Prefab을 연결하세요.");
        }

        // 추적 시간, 정지 거리, 벽 여백은 공통 설정에서 검사한다.
        // 이동 속도는 타격별 설정으로 옮겼으므로 여기서 검사하지 않는다.
        if (_config.StrikeCount != 3 ||
            !Valid(_config.MaxChaseDuration, 0.01f) ||
            !Valid(_config.StopDistance, 0f) ||
            !Valid(_config.WallPadding, 0f))
        {
            throw new InvalidOperationException(
                $"{name}: 추적 설정과 Strikes 3개를 확인하세요.");
        }

        // 세 타격 각각의 이동 속도와 공격 설정을 검사한다.
        for (int i = 0; i < _config.StrikeCount; i++)
        {
            BossAlienComboConfig.StrikeSettings strike =
                _config.GetStrike(i);

            // 설정이 없으면 뒤의 프로퍼티에 접근하지 않는다.
            if (strike == null ||
                !Valid(strike.ChaseSpeed, 0.01f) ||
                !Valid(strike.ChaseAnimationSpeed, 0.01f) ||
                !Valid(strike.AimDuration, 0.01f) ||
                !Valid(strike.LockDuration, 0.01f) ||
                !Valid(strike.ActiveDuration, 0.01f) ||
                !Valid(strike.RecoveryDuration, 0.01f) ||
                !Valid(strike.HitboxSize.x, 0.01f) ||
                !Valid(strike.HitboxSize.y, 0.01f) ||
                !Valid(strike.Damage, 0f))
            {
                throw new InvalidOperationException(
                    $"{name}: Strikes의 {i + 1}번째 설정을 확인하세요.");
            }
        }
    }

    // 유한한 숫자이며 지정한 최솟값 이상인지 확인한다.
    private static bool Valid(float value, float minimum)
    {
        return !float.IsNaN(value) &&
            !float.IsInfinity(value) &&
            value >= minimum;
    }
}
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

    // 0번은 첫 할퀴기, 1번은 두 번째 할퀴기, 2번은 내려찍기다.
    // 이미지가 없는 타격은 효과 표시만 생략하고 기존 공격은 실행한다.
    [SerializeField]
    private Sprite[] _strikeEffectSprites = new Sprite[3];

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

            float elapsed = 0f;
            WaitForFixedUpdate waitForPhysics = new WaitForFixedUpdate();

            // 처음에만 접근한다. 공격 사이에는 제자리에서 재조준한다.
            while (elapsed < _config.MaxChaseDuration)
            {
                yield return waitForPhysics;

                if (!CanContinue(boss))
                    yield break;

                Vector2 position = context.Rigidbody.position;
                Vector2 target = context.TargetPlayer.transform.position;
                Vector2 delta = target - position;

                if (delta.sqrMagnitude > 0.0001f)
                {
                    direction = delta.normalized;
                    FaceDirection(renderer, direction);
                }

                float distance = delta.magnitude;

                if (distance <= _config.StopDistance)
                    break;

                // 한 번의 이동으로 정지 거리를 지나치지 않게 한다.
                float travel = Mathf.Min(
                    _config.ChaseSpeed * Time.fixedDeltaTime,
                    distance - _config.StopDistance);

                Vector2 next = position + direction * travel;

                next.x = Mathf.Clamp(
                    next.x, area.xMin + padding, area.xMax - padding);

                next.y = Mathf.Clamp(
                    next.y, area.yMin + padding, area.yMax - padding);

                context.Rigidbody.MovePosition(next);
                elapsed += Time.fixedDeltaTime;
            }

            // 마지막 이동 요청이 물리에 반영된 뒤 공격을 시작한다.
            yield return waitForPhysics;

            if (!CanContinue(boss))
                yield break;

            context.Rigidbody.linearVelocity = Vector2.zero;

            // 각 타격은 별도의 경고와 피해 기록을 사용한다.
            for (int i = 0; i < _config.StrikeCount; i++)
            {
                if (!CanContinue(boss))
                    yield break;

                BossAlienComboConfig.StrikeSettings strike =
                    _config.GetStrike(i);

                // 첫 타격에서 공격 표현을 시작하고 이후에는 자세만 바꾼다.
                if (!beganAction)
                {
                    beganAction = visual.BeginAction(
                        "Phase2_Combo", strike.PrepareLabel);

                    if (!beganAction)
                        yield break;
                }
                else if (!visual.ShowPose(
                    "Phase2_Combo", strike.PrepareLabel))
                {
                    yield break;
                }

                // 최초 조준값으로 경고를 생성한다.
                direction = GetDirection(context, direction);
                FaceDirection(renderer, direction);

                Vector2 center = context.Rigidbody.position
                    + direction * strike.ForwardOffset;

                float angle = Mathf.Atan2(
                    direction.y, direction.x) * Mathf.Rad2Deg;

                warning = UnityEngine.Object.Instantiate(_warningPrefab);
                warning.Initialize(boss, center, strike.HitboxSize, angle);

                elapsed = 0f;

                // 준비 중에만 플레이어를 향해 경고 방향을 갱신한다.
                while (elapsed < strike.AimDuration)
                {
                    if (!CanContinue(boss) || warning == null)
                        yield break;

                    direction = GetDirection(context, direction);
                    FaceDirection(renderer, direction);

                    center = context.Rigidbody.position
                        + direction * strike.ForwardOffset;

                    angle = Mathf.Atan2(
                        direction.y, direction.x) * Mathf.Rad2Deg;

                    warning.SetAim(center, angle);

                    elapsed += Time.deltaTime;
                    yield return null;
                }

                if (!CanContinue(boss) || warning == null)
                    yield break;

                // 이 순간부터 경고의 위치와 방향을 변경하지 않는다.
                warning.LockAim();

                elapsed = 0f;

                // 고정된 범위를 보고 피할 시간을 제공한다.
                while (elapsed < strike.LockDuration)
                {
                    if (!CanContinue(boss) || warning == null)
                        yield break;

                    elapsed += Time.deltaTime;
                    yield return null;
                }

                if (!CanContinue(boss) || warning == null)
                    yield break;

                if (!visual.ShowPose("Phase2_Combo", strike.AttackLabel))
                    yield break;

                warning.ShowAttack();

                // 준비된 이미지가 있는 타격에만 전용 효과를 생성한다.
                // 피해 판정과 같은 확정 좌표·크기·각도를 사용한다.
                if (_effectPrefab != null &&
                    _strikeEffectSprites != null &&
                    i < _strikeEffectSprites.Length &&
                    _strikeEffectSprites[i] != null)
                {
                    effect = UnityEngine.Object.Instantiate(_effectPrefab);

                    effect.Initialize(
                        boss,
                        _strikeEffectSprites[i],
                        warning.Center,
                        warning.Size,
                        warning.Angle);
                }

                // 경고가 확정한 좌표, 크기, 각도로 실제 판정을 만든다.
                BossAlienComboHitbox hitbox = new BossAlienComboHitbox(
                    context.TargetPlayer,
                    warning.Center,
                    warning.Size,
                    warning.Angle,
                    strike.Damage);

                elapsed = 0f;

                // 공격 중 들어온 플레이어도 검사하되 타격당 한 번만 요청한다.
                while (elapsed < strike.ActiveDuration)
                {
                    if (!CanContinue(boss) || warning == null)
                        yield break;

                    // 일시정지 중에는 효과 진행과 피해 판정을 멈춘다.
                    if (Time.deltaTime > 0f)
                    {
                        // 피해가 발생하는 공격 시간과 동일한 시간축으로 표시한다.
                        if (effect != null)
                        {
                            effect.SetProgress(elapsed / strike.ActiveDuration);
                        }

                        // 실제 피해는 기존 판정 객체가 타격당 한 번 처리한다.
                        hitbox.Tick();
                    }

                    elapsed += Time.deltaTime;
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

                if (!visual.ShowPose("Phase2_Combo", strike.RecoverLabel))
                    yield break;

                elapsed = 0f;

                // 회복 중에는 재조준하지 않고 다음 준비 단계까지 기다린다.
                while (elapsed < strike.RecoveryDuration)
                {
                    if (!CanContinue(boss))
                        yield break;

                    elapsed += Time.deltaTime;
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

    // 실행 전에 설정 누락과 잘못된 시간·범위를 검사한다.
    private void ValidateConfig()
    {
        if (_config == null || _warningPrefab == null)
        {
            throw new InvalidOperationException(
                $"{name}: Config와 Warning Prefab을 연결하세요.");
        }

        if (_config.StrikeCount != 3 ||
            !Valid(_config.ChaseSpeed, 0.01f) ||
            !Valid(_config.MaxChaseDuration, 0.01f) ||
            !Valid(_config.StopDistance, 0f) ||
            !Valid(_config.WallPadding, 0f))
        {
            throw new InvalidOperationException(
                $"{name}: 추적 설정과 Strikes 3개를 확인하세요.");
        }

        for (int i = 0; i < _config.StrikeCount; i++)
        {
            BossAlienComboConfig.StrikeSettings strike =
                _config.GetStrike(i);

            if (strike == null ||
                string.IsNullOrWhiteSpace(strike.PrepareLabel) ||
                string.IsNullOrWhiteSpace(strike.AttackLabel) ||
                string.IsNullOrWhiteSpace(strike.RecoverLabel) ||
                !Valid(strike.AimDuration, 0.01f) ||
                !Valid(strike.LockDuration, 0.01f) ||
                !Valid(strike.ActiveDuration, 0.01f) ||
                !Valid(strike.RecoveryDuration, 0.01f) ||
                !Valid(strike.HitboxSize.x, 0.01f) ||
                !Valid(strike.HitboxSize.y, 0.01f) ||
                !Valid(strike.ForwardOffset, 0f) ||
                strike.Damage < 0)
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
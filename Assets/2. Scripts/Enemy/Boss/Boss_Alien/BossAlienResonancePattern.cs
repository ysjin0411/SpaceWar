using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 균열 공명의 실행 순서를 담당한다.
// 실행 중인 상태는 지역 변수로 보관해 설정 에셋에 남기지 않는다.
[CreateAssetMenu(
    menuName = "Boss/Patterns/Alien Resonance",
    fileName = "BossAlienResonancePattern")]
public sealed class BossAlienResonancePattern : BossPattern
{
    // 공격 시간, 피해량, 충격파 크기를 읽을 설정이다.
    [SerializeField] private BossAlienResonanceConfig _config;

    // 공격 경로와 마무리 공격 범위를 표시할 예고 프리팹이다.
    [SerializeField] private BossAlienAttackWarning _warningPrefab;

    // 각 균열에서 발사할 충격파 프리팹이다.
    [SerializeField] private BossAlienResonanceWave _wavePrefab;

    public override IEnumerator Execute(BossPatternContext context)
    {
        Boss_Alien boss = context.Boss as Boss_Alien;

        // 누락된 설정으로 패턴 실행기가 무한 대기하지 않게 한다.
        if (boss == null || _config == null ||
            _warningPrefab == null || _wavePrefab == null ||
            !PauseRandomMovement)
        {
            Debug.LogError(
                $"{name}: Config, 두 프리팹, Pause Random Movement를 확인하세요.");
            yield break;
        }

        BossAlienCrackController cracks =
            boss.GetComponent<BossAlienCrackController>();

        BossAlienGrabArena[] arenas =
            UnityEngine.Object.FindObjectsByType<BossAlienGrabArena>(
                FindObjectsSortMode.None);

        if (cracks == null ||
            !cracks.isActiveAndEnabled ||
            cracks.Count != BossAlienPhaseOneConfig.MaxCrackCount ||
            arenas.Length != 1 ||
            !arenas[0].isActiveAndEnabled)
        {
            Debug.LogError(
                $"{name}: 활성 균열 3개와 Arena 하나가 필요합니다.");
            yield break;
        }

        BossAlienGrabArena arena = arenas[0];
        Rect area = arena.GetInteriorRect();
        // 현재 씬의 카메라 연출 컴포넌트를 가져온다.
        Camera battleCamera = Camera.main;
        BattleCameraShake cameraShake = battleCamera != null
            ? battleCamera.GetComponent<BattleCameraShake>()
            : null;

        // 카메라 연출 누락이 공격 자체를 막지는 않지만 설정 오류는 알려준다.
        if (cameraShake == null)
        {
            Debug.LogWarning(
                $"{name}: Main Camera에 BattleCameraShake를 추가하세요.");
        }

        if (!ValidSettings(area))
        {
            Debug.LogError(
                $"{name}: 공명 Config의 시간·크기·피해 설정을 확인하세요.");
            yield break;
        }

        // 생성 순서를 유지한 채 이번 공격에서 사용할 위치를 고정한다.
        var sources =
            new List<BossAlienCrackController.Crack>(cracks.Cracks);

        // 정상 종료와 강제 취소에서 제거할 생성물 목록이다.
        var warnings = new List<BossAlienAttackWarning>();
        var waves = new List<BossAlienResonanceWave>();

        // 마무리 공격의 물리 검사 결과를 재사용한다.
        var hits = new List<Collider2D>();
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(
            LayerMask.GetMask("Player", "PlayerShield"));
        filter.useTriggers = true;

        bool CanContinue()
        {
            return boss != null && boss.CanRunArmPattern &&
                arena != null && arena.isActiveAndEnabled &&
                cracks != null && cracks.isActiveAndEnabled;
        }

        // 대기 중에도 사망·비활성화를 검사한다.
        IEnumerator Wait(float duration, Action<float> onProgress = null)
        {
            float elapsed = 0f;

            while (elapsed < duration && CanContinue())
            {
                onProgress?.Invoke(Mathf.Clamp01(elapsed / duration));
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (CanContinue())
            {
                onProgress?.Invoke(1f);
            }
        }

        // 생성 즉시 목록에 기록해 이후 초기화가 실패해도 정리한다.
        BossAlienAttackWarning CreateWarning(
            Vector2 center, Vector2 size, float angle)
        {
            BossAlienAttackWarning warning =
                Instantiate(_warningPrefab);

            warnings.Add(warning);
            warning.Initialize(boss, center, size, angle);
            return warning;
        }

        try
        {
            // 1. 균열 생성 순서대로 경로를 예고하고 충격파를 발사한다.
            foreach (BossAlienCrackController.Crack source in sources)
            {
                if (!CanContinue())
                {
                    yield break;
                }

                Vector2 direction = source.Normal.normalized;
                float distance = arena.GetTravelDistance(
                    source.Position, direction);
                float angle =
                    Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

                // 이동하는 사각형의 전체 경로다.
                // 시작·도착 지점에서 돌출되는 충격파의 반 두께도 포함한다.
                Vector2 center =
                    source.Position + direction * (distance * 0.5f);

                Vector2 size = new Vector2(
                    distance + _config.WaveThickness,
                    _config.WaveWidth);

                BossAlienAttackWarning warning =
                    CreateWarning(center, size, angle);

                yield return Wait(_config.WarningDuration, progress =>
                {
                    if (warning != null)
                    {
                        warning.SetProgress(progress);
                    }
                });

                if (!CanContinue())
                {
                    yield break;
                }

                Destroy(warning.gameObject);

                BossAlienResonanceWave wave = Instantiate(_wavePrefab);
                waves.Add(wave);
                wave.Initialize(
                    boss, _config, source.Position, direction, distance);

                // 충격파를 발사하는 순간 카메라를 약하게 흔든다.
                if (cameraShake != null)
                {
                    cameraShake.Shake(
                        boss,
                        _config.WaveShakeStrength,
                        _config.WaveShakeDuration);
                }

                yield return Wait(_config.WaveInterval);
            }

            // 순차 충격파가 모두 사라진 뒤 마무리 공격을 시작한다.
            while (CanContinue() && waves.Exists(wave => wave != null))
            {
                yield return null;
            }

            if (!CanContinue())
            {
                yield break;
            }

            // 2. 예고 시작 순간 플레이어 X를 기준으로 안전 통로를 고정한다.
            // 이후 플레이어를 따라 움직이지 않아 예고대로 피할 수 있다.
            float halfSafe = _config.SafeLaneWidth * 0.5f;
            const float edgeMargin = 0.05f;

            float safeCenter = Mathf.Clamp(
                context.TargetPlayer.transform.position.x,
                area.xMin + halfSafe + edgeMargin,
                area.xMax - halfSafe - edgeMargin);

            float safeLeft = safeCenter - halfSafe;
            float safeRight = safeCenter + halfSafe;

            // 안전 통로를 제외한 왼쪽·오른쪽 영역만 공격한다.
            Rect leftArea = Rect.MinMaxRect(
                area.xMin, area.yMin, safeLeft, area.yMax);

            Rect rightArea = Rect.MinMaxRect(
                safeRight, area.yMin, area.xMax, area.yMax);

            BossAlienAttackWarning leftWarning =
                CreateWarning(leftArea.center, leftArea.size, 0f);

            BossAlienAttackWarning rightWarning =
                CreateWarning(rightArea.center, rightArea.size, 0f);

            yield return Wait(_config.FinalWarningDuration, progress =>
            {
                if (leftWarning != null)
                {
                    leftWarning.SetProgress(progress);
                }

                if (rightWarning != null)
                {
                    rightWarning.SetProgress(progress);
                }
            });

            if (!CanContinue())
            {
                yield break;
            }

            // 3. 예고 범위를 밝게 바꿔 실제 공격 시작을 구분한다.
            ShowAttack(leftWarning);
            ShowAttack(rightWarning);

            // 마무리 공격이 시작되는 순간 카메라를 강하게 흔든다.
            if (cameraShake != null)
            {
                cameraShake.Shake(
                    boss,
                    _config.FinalShakeStrength,
                    _config.FinalShakeDuration);
            }

            bool hitConsumed = false;
            float activeElapsed = 0f;

            while (activeElapsed < _config.FinalActiveDuration &&
                   CanContinue())
            {
                if (!hitConsumed)
                {
                    Physics2D.SyncTransforms();

                    // 양쪽 영역을 합쳐 마무리 공격 전체에서 한 번만 처리한다.
                    hitConsumed =
                        TryHitArea(leftArea, context.TargetPlayer, filter, hits) ||
                        TryHitArea(rightArea, context.TargetPlayer, filter, hits);
                }

                activeElapsed += Time.deltaTime;
                yield return null;
            }

            if (!CanContinue())
            {
                yield break;
            }

            Destroy(leftWarning.gameObject);
            Destroy(rightWarning.gameObject);

            // 4. 공격이 끝나면 균열과 방어 실드를 제거하고 회복한다.
            cracks.ClearAll();
            yield return Wait(_config.RecoveryDuration);
        }
        finally
        {
            // 패턴이 종료되거나 취소되면 카메라를 원래 위치로 복구한다.
            if (cameraShake != null)
            {
                cameraShake.StopShake(boss);
            }

            // 사망·패턴 취소 때도 예고와 공격 판정을 남기지 않는다.
            foreach (BossAlienAttackWarning warning in warnings)
            {
                if (warning != null)
                {
                    warning.gameObject.SetActive(false);
                    Destroy(warning.gameObject);
                }
            }

            foreach (BossAlienResonanceWave wave in waves)
            {
                if (wave != null)
                {
                    wave.gameObject.SetActive(false);
                    Destroy(wave.gameObject);
                }
            }

            if (cracks != null)
            {
                cracks.ClearAll();
            }
        }
    }

    // 공격 시작 시 표시 변경은 예고 컴포넌트가 담당한다.
    private static void ShowAttack(BossAlienAttackWarning warning)
    {
        warning.ShowAttack();
    }

    // 마무리 공격 범위 안의 방패 또는 플레이어에게 피해를 시도한다.
    private bool TryHitArea(
        Rect area,
        PlayerController target,
        ContactFilter2D filter,
        List<Collider2D> hits)
    {
        hits.Clear();
        Physics2D.OverlapBox(area.center, area.size, 0f, filter, hits);

        // 범위 공격은 기존 폭발 공격과 동일하게 방패를 먼저 검사한다.
        foreach (Collider2D hit in hits)
        {
            if (hit.TryGetComponent(out Shield shield) &&
                shield.TryBlockDamage(
                    _config.FinalDamage, hit.ClosestPoint(area.center)))
            {
                return true;
            }
        }

        int shieldLayer = LayerMask.NameToLayer("PlayerShield");

        foreach (Collider2D hit in hits)
        {
            if (hit.gameObject.layer == shieldLayer)
            {
                continue;
            }

            if (hit.GetComponentInParent<PlayerController>() == target)
            {
                // 기존 피격 무적과 잡기 보호 규칙을 따른다.
                target.TakeHit(_config.FinalDamage);
                return true;
            }
        }

        return false;
    }

    // 안전 통로가 전장을 전부 덮거나 잘못된 시간으로 대기하지 않게 한다.
    private bool ValidSettings(Rect area)
    {
        return Positive(_config.WarningDuration) &&
            NonNegative(_config.WaveInterval) &&
            Positive(_config.WaveSpeed) &&
            Positive(_config.WaveWidth) &&
            Positive(_config.WaveThickness) &&
            _config.WaveDamage > 0 &&
            Positive(_config.FinalWarningDuration) &&
            Positive(_config.FinalActiveDuration) &&
            Positive(_config.SafeLaneWidth) &&
            _config.SafeLaneWidth < area.width - 0.1f &&
            _config.FinalDamage > 0 &&
            NonNegative(_config.RecoveryDuration);
    }

    // 설정 검증에 사용할 유효한 숫자 검사다.
    private static bool NonNegative(float value)
    {
        return !float.IsNaN(value) &&
            !float.IsInfinity(value) && value >= 0f;
    }

    private static bool Positive(float value)
    {
        return NonNegative(value) && value > 0f;
    }
}
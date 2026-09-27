using System;
using System.Collections;
using UnityEngine;
using System.Collections.Generic;

// 회차별 포격 설정을 순서대로 보관하는 체력 특수패턴이다.
// 실행 큐, 타이머, 생성된 오브젝트는 필드에 저장하지 않는다.
[CreateAssetMenu(
    menuName = "Boss/Patterns/Alien Barrage",
    fileName = "BossAlienBarragePattern")]
public sealed class BossAlienBarragePattern : BossPattern
{
    // 패턴이 실제 시작됐을 때 시스템 다이어로그에 표시할 문구다.
    [SerializeField, TextArea(2, 3)]
    private string _warningMessage =
        "경고! 미사일 포격 감지. 안전지대의 순서를 기억하십시오.";

    // 등록 순서대로 모든 회차를 예고한 뒤,
    // 같은 순서대로 실제 포격을 진행한다.
    // 배열의 항목 수가 전체 포격 회차 수다.
    [SerializeField]
    private BossAlienBarrageRoundConfig[] _rounds =
        Array.Empty<BossAlienBarrageRoundConfig>();

    // 마지막 회차의 Preview Duration까지 끝난 뒤,
    // 첫 회차 미사일의 낙하를 시작하기 전 기다리는 시간이다.
    // 개별 장판이 생성된 시점부터 계산하지 않는다.
    [SerializeField, Min(0f)]
    private float _firstDelay = 1f;

    // 한 회차의 모든 폭발 연출과 페이드아웃이 끝나고
    // 해당 회차 장판을 제거한 뒤, 다음 낙하까지 기다리는 시간이다.
    // 마지막 회차 이후에는 적용하지 않는다.
    [SerializeField, Min(0f)]
    private float _roundInterval = 0.8f;

    // 마지막 회차의 연출과 장판 정리가 끝난 뒤,
    // 보스가 다음 패턴으로 넘어가기 전 쉬는 시간이다.
    [SerializeField, Min(0f)]
    private float _recoveryDuration = 1f;

    [Header("포격 프리팹")]

    // 미사일 한 발의 위치와 피해 반경을 표시할 원형 장판이다.
    [SerializeField]
    private BossAlienBarrageWarning _warningPrefab;

    // 회차 Config의 미사일 이미지를 받아 낙하할 프리팹이다.
    [SerializeField]
    private BossAlienBarrageMissile _missilePrefab;

    // 도착 지점에서 재생하고 페이드아웃할 폭발 프리팹이다.
    [SerializeField]
    private BossAlienBarrageExplosion _explosionPrefab;

    [Header("낙하 연출")]

    // 경기장 내부의 위쪽 경계보다 얼마나 높은 곳에서 출발할지 정한다.
    // 월드 유닛이며, 미사일의 X좌표는 해당 폭발 위치의 X좌표를 사용한다.
    // 실제 출발 Y = 경기장 위쪽 경계 Y + 이 값이다.
    [SerializeField, Min(0.1f)]
    private float _spawnHeightAboveArena = 2f;

    // 미사일이 출발점에서 설정된 폭발 위치까지 낙하하는 시간이다.
    // 같은 회차의 미사일들은 함께 출발하고 같은 시간에 도착하도록 한다.
    [SerializeField, Min(0.01f)]
    private float _fallDuration = 0.6f;

    [Header("폭발 연출")]

    // 폭발 애니메이션이 끝난 뒤 잔광의 알파를 0으로 낮추는 시간이다.
    // 이 시간이 끝날 때까지 해당 회차의 경고 장판을 유지한다.
    [SerializeField, Min(0.01f)]
    private float _explosionFadeDuration = 0.2f;

    // 시스템 경고가 끝나면 모든 회차의 위험지대를 순서대로 표시한다.
    // 실행 데이터와 이벤트 대기 상태는 이번 호출 안에서만 보관한다.
    /// <summary>
    /// 실행 데이터와 이벤트 대기 상태는 이번 호출 안에서만 보관한다.
    /// </summary>
    public override IEnumerator Execute(BossPatternContext context)
    {

        // 이 패턴은 Alien 보스에서만 실행할 수 있다.
        if (context == null || !(context.Boss is Boss_Alien boss))
        {
            throw new InvalidOperationException(
                $"{name}: Alien 보스의 실행 Context가 필요합니다.");
        }

        // 전투가 종료된 상태에서는 시작하지 않는다.
        if (!boss.CanRunArmPattern)
        {
            yield break;
        }

        // 장판을 생성하기 전에 전체 설정을 검사하고 복사한다.
        ValidateSettings();
        RoundRuntime[] rounds = CreateRuntimeRounds();

        // 이번 실행에서 사용할 경고 문구와 첫 낙하 대기 시간을 저장한다.
        string warningMessage = _warningMessage;
        float firstDelay = _firstDelay;

        // 이번 실행 도중 Inspector 값이 바뀌어도 같은 시간을 사용한다.
        float fallDuration = _fallDuration;
        float fadeDuration = _explosionFadeDuration;
        float roundInterval = _roundInterval;
        float recoveryDuration = _recoveryDuration;

        // 기존 경기장 경계에서 미사일의 공통 출발 높이를 구한다.
        BossAlienGrabArena[] arenas =
            FindObjectsByType<BossAlienGrabArena>(
                FindObjectsSortMode.None);

        if (arenas.Length != 1 || !arenas[0].isActiveAndEnabled)
        {
            throw new InvalidOperationException(
                $"{name}: 활성화된 BossAlienGrabArena가 하나 필요합니다.");
        }

        float spawnY =
            arenas[0].GetInteriorRect().yMax + _spawnHeightAboveArena;

        // 아래로 낙하할 수 있도록 출발점이 모든 도착점보다 높은지 검사한다.
        // 입력한 공격 좌표를 자동으로 변경하지 않는다.
        if (!IsFinite(spawnY))
        {
            throw new InvalidOperationException(
                $"{name}: 미사일 출발 높이가 유효하지 않습니다.");
        }

        foreach (RoundRuntime round in rounds)
        {
            foreach (MissileRuntime missile in round.Missiles)
            {
                if (missile.Position.y >= spawnY)
                {
                    throw new InvalidOperationException(
                        $"{name}: 미사일 출발 높이는 폭발 위치보다 높아야 합니다.");
                }
            }
        }

        // 완료 이벤트를 받았는지 이번 실행 안에서만 기록한다.
        bool dialogueCompleted = false;
        Action onDialogueCompleted = () => dialogueCompleted = true;

        try
        {
            // 앞서 요청된 공명 경고가 남아 있으면 먼저 종료를 기다린다.
            // 기존 UI는 메시지 중복 요청 시 예외를 발생시킨다.
            while (boss.IsResonanceWarningPending)
            {
                if (!boss.CanRunArmPattern)
                {
                    yield break;
                }

                yield return null;
            }

            if (!boss.CanRunArmPattern)
            {
                yield break;
            }

            // 요청 도중 완료 신호가 발생해도 놓치지 않도록 먼저 구독한다.
            boss.SystemDialogueCompleted += onDialogueCompleted;
            boss.RequestSystemDialogue(warningMessage);

            // UI에서 실제 완료 신호를 받을 때까지 기다린다.
            while (!dialogueCompleted)
            {
                if (!boss.CanRunArmPattern)
                {
                    yield break;
                }

                yield return null;
            }

            // 이번 경고가 끝났으므로 이후 다른 메시지의 완료 신호는 받지 않는다.
            boss.SystemDialogueCompleted -= onDialogueCompleted;

            // 회차마다 새 장판을 추가하고 이전 회차의 장판은 유지한다.
            for (int roundIndex = 0;
                 roundIndex < rounds.Length;
                 roundIndex++)
            {
                if (!boss.CanRunArmPattern)
                {
                    yield break;
                }

                RoundRuntime round = rounds[roundIndex];

                for (int missileIndex = 0;
                     missileIndex < round.Missiles.Length;
                     missileIndex++)
                {
                    MissileRuntime missile = round.Missiles[missileIndex];

                    // 부모 없이 생성하여 보스 이동과 Scale의 영향을 받지 않는다.
                    // 먼저 참조를 저장하므로 초기화 오류 시에도 finally에서 정리한다.
                    missile.Warning = Instantiate(_warningPrefab);

                    // 실제 폭격도 이 실행 데이터의 위치와 반경을 사용한다.
                    missile.Warning.Initialize(
                        boss,
                        missile.Position,
                        missile.Radius);
                }

                // 이 회차의 예고 시간을 기다린다.
                // 시간이 끝나도 장판을 제거하지 않는다.
                float elapsed = 0f;

                while (elapsed < round.PreviewDuration)
                {
                    if (!boss.CanRunArmPattern)
                    {
                        yield break;
                    }

                    yield return null;
                    elapsed += Time.deltaTime;
                }
            }

            // 마지막 회차의 예고 시간까지 끝난 뒤에만 First Delay를 시작한다.
            float firstDelayElapsed = 0f;

            while (firstDelayElapsed < firstDelay)
            {
                if (!boss.CanRunArmPattern)
                {
                    yield break;
                }

                yield return null;
                firstDelayElapsed += Time.deltaTime;
            }

            // 전체 예고가 끝난 뒤, 처음 예고한 회차부터 순서대로 공격한다.
            for (int roundIndex = 0;
                 roundIndex < rounds.Length;
                 roundIndex++)
            {
                if (boss == null || !boss.CanRunArmPattern)
                {
                    yield break;
                }

                RoundRuntime round = rounds[roundIndex];

                // 해당 회차의 미사일을 함께 낙하시킨다.
                LaunchRound(boss, round, spawnY, fallDuration);

                while (true)
                {
                    // 사망이나 취소 여부를 미사일 참조 검사보다 먼저 확인한다.
                    if (boss == null || !boss.CanRunArmPattern)
                    {
                        yield break;
                    }

                    // 일시정지 중에는 도착했더라도 새 폭발을 시작하지 않는다.
                    if (Time.deltaTime > 0f &&
                        HaveAllMissilesArrived(round))
                    {
                        break;
                    }

                    yield return null;
                }

                // 모든 폭발 외형을 생성한 뒤 회차 피해를 한 번만 처리한다.
                CreateRoundExplosions(boss, round, fadeDuration);
                ApplyRoundDamage(round, context.TargetPlayer);

                // 피해 처리로 플레이어가 사망하면 즉시 종료한다.
                // 이후 finally가 남은 장판과 폭발을 정리한다.
                if (boss == null || !boss.CanRunArmPattern)
                {
                    yield break;
                }

                while (true)
                {
                    if (boss == null || !boss.CanRunArmPattern)
                    {
                        yield break;
                    }

                    // 모든 폭발의 애니메이션과 페이드아웃까지 기다린다.
                    if (HaveAllExplosionsFinished(round))
                    {
                        break;
                    }

                    yield return null;
                }

                // 완료한 회차의 장판만 제거한다.
                // 다음 회차들의 장판은 각자 다른 참조이므로 그대로 남는다.
                round.Clear();

                // 마지막 회차 뒤에는 회차 간격을 적용하지 않는다.
                if (roundIndex < rounds.Length - 1)
                {
                    float intervalElapsed = 0f;

                    while (intervalElapsed < roundInterval)
                    {
                        if (boss == null || !boss.CanRunArmPattern)
                        {
                            yield break;
                        }

                        yield return null;
                        intervalElapsed += Time.deltaTime;
                    }
                }
            }

            // 마지막 회차까지 정리한 뒤 설정된 회복 시간을 기다린다.
            float recoveryElapsed = 0f;

            while (recoveryElapsed < recoveryDuration)
            {
                if (boss == null || !boss.CanRunArmPattern)
                {
                    yield break;
                }

                yield return null;
                recoveryElapsed += Time.deltaTime;
            }
        }
        finally
        {
            // 정상 종료, 중단, 오류 모두 이벤트 구독을 해제한다.
            boss.SystemDialogueCompleted -= onDialogueCompleted;

            // 아직 남아 있는 모든 회차의 장판과 연출을 정리한다.
            // 정상 폭격에서 이미 정리한 참조는 Clear가 건너뛴다.
            for (int i = 0; i < rounds.Length; i++)
            {
                rounds[i].Clear();
            }
        }
    }



    // 실행 전에 필요한 참조와 수치를 검사한다.
    // 실제 Execute를 구현할 때 예고 시작 전에 호출한다.
    /// <summary>
    /// 실제 Execute를 구현할 때 예고 시작 전에 호출한다.
    /// </summary>
    private void ValidateSettings()
    {
        if (string.IsNullOrWhiteSpace(_warningMessage))
        {
            throw new InvalidOperationException(
                $"{name}: 시스템 경고 문구를 입력하세요.");
        }

        if (_warningPrefab == null ||
            _missilePrefab == null ||
            _explosionPrefab == null)
        {
            throw new InvalidOperationException(
                $"{name}: 장판, 미사일, 폭발 프리팹을 연결하세요.");
        }

        if (_rounds == null || _rounds.Length == 0)
        {
            throw new InvalidOperationException(
                $"{name}: 포격 회차를 하나 이상 등록하세요.");
        }

        // 대기 시간은 0을 허용하지만, 낙하·페이드 시간은 양수여야 한다.
        ValidateNumber(_firstDelay, "First Delay", true);
        ValidateNumber(_roundInterval, "Round Interval", true);
        ValidateNumber(_recoveryDuration, "Recovery Duration", true);
        ValidateNumber(_spawnHeightAboveArena, "Spawn Height Above Arena", false);
        ValidateNumber(_fallDuration, "Fall Duration", false);
        ValidateNumber(_explosionFadeDuration, "Explosion Fade Duration", false);

        // 모든 회차를 먼저 검사하여 예고 도중 설정 오류가 나지 않게 한다.
        for (int roundIndex = 0; roundIndex < _rounds.Length; roundIndex++)
        {
            BossAlienBarrageRoundConfig round = _rounds[roundIndex];
            string roundLabel = $"{roundIndex + 1}회차";

            if (round == null || round.MissileCount == 0)
            {
                throw new InvalidOperationException(
                    $"{name}: {roundLabel} Config와 미사일 목록을 확인하세요.");
            }

            ValidateNumber(
                round.PreviewDuration,
                $"{roundLabel} Preview Duration",
                false);

            // 오류 메시지에 회차와 미사일 번호를 함께 표시한다.
            for (int missileIndex = 0;
                 missileIndex < round.MissileCount;
                 missileIndex++)
            {
                BossAlienBarrageRoundConfig.MissileSettings missile =
                    round.GetMissileSettings(missileIndex);

                string label =
                    $"{roundLabel} / {missileIndex + 1}번 미사일";

                if (missile == null || missile.MissileSprite == null)
                {
                    throw new InvalidOperationException(
                        $"{name}: {label} 설정과 스프라이트를 확인하세요.");
                }

                // 월드 좌표는 음수도 허용한다.
                Vector2 position = missile.WorldPosition;

                if (!IsFinite(position.x) || !IsFinite(position.y))
                {
                    throw new InvalidOperationException(
                        $"{name}: {label}의 월드 좌표가 유효하지 않습니다.");
                }

                ValidateNumber(missile.Radius, $"{label} Radius", false);
                ValidateNumber(
                    missile.ExplosionVisualScale,
                    $"{label} Explosion Visual Scale",
                    false);

                // 피해량 0은 연출 확인용으로 허용한다.
                if (missile.Damage < 0)
                {
                    throw new InvalidOperationException(
                        $"{name}: {label}의 Damage는 0 이상이어야 합니다.");
                }
            }
        }
    }

    // Inspector 메뉴에서 실행하지 않고 설정만 검사할 수 있게 한다.
    [ContextMenu("Validate Barrage Settings")]
    /// <summary>
    /// ValidateFromInspector 동작에 필요한 상태와 처리 흐름을 실행한다.
    /// </summary>
    private void ValidateFromInspector()
    {
        ValidateSettings();
        Debug.Log($"{name}: 포격 설정 검사 완료.", this);
    }

    // 숫자가 유효한지와 0 허용 여부를 함께 검사한다.
    /// <summary>
    /// 숫자가 유효한지와 0 허용 여부를 함께 검사한다.
    /// </summary>
    private void ValidateNumber(float value, string label, bool allowZero)
    {
        if (!IsFinite(value) ||
            (allowZero ? value < 0f : value <= 0f))
        {
            string condition = allowZero ? "0 이상" : "0보다 큰 값";

            throw new InvalidOperationException(
                $"{name}: {label}은 유효한 {condition}이어야 합니다.");
        }
    }

    // NaN과 무한대는 좌표·시간·반경으로 사용하지 않는다.
    /// <summary>
    /// NaN과 무한대는 좌표·시간·반경으로 사용하지 않는다.
    /// </summary>
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    // 이번 패턴 실행에서 사용할 미사일 한 발의 데이터다.
    // Config의 값을 복사하고, 생성한 오브젝트도 이곳에서 추적한다.
    private sealed class MissileRuntime
    {
        // 예고와 폭격이 함께 사용할 확정된 공격 설정이다.
        public readonly Vector2 Position;
        public readonly float Radius;
        public readonly float Damage;
        public readonly float VisualScale;
        public readonly Sprite Sprite;

        // 이번 실행에서 생성한 오브젝트만 보관한다.
        // 같은 위치의 미사일이라도 서로 다른 참조를 가진다.
        public BossAlienBarrageWarning Warning;
        public BossAlienBarrageMissile Missile;
        public BossAlienBarrageExplosion Explosion;

        // 도착한 미사일의 폭발을 중복 처리하지 않도록 기록한다.
        public bool ImpactHandled;

        // 원본 Config를 보관하지 않고 필요한 값을 복사한다.
        /// <summary>
        /// 원본 Config를 보관하지 않고 필요한 값을 복사한다.
        /// </summary>
        public MissileRuntime(
            BossAlienBarrageRoundConfig.MissileSettings settings)
        {
            Position = settings.WorldPosition;
            Radius = settings.Radius;
            Damage = settings.Damage;
            VisualScale = settings.ExplosionVisualScale;
            Sprite = settings.MissileSprite;
        }

        // 이 미사일에 속한 장판과 연출만 정리한다.
        // 정상 종료와 패턴 취소 시 모두 호출할 수 있다.
        /// <summary>
        /// 정상 종료와 패턴 취소 시 모두 호출할 수 있다.
        /// </summary>
        public void Clear()
        {
            if (Warning != null)
            {
                Warning.Remove();
                Warning = null;
            }

            if (Missile != null)
            {
                Missile.Remove();
                Missile = null;
            }

            if (Explosion != null)
            {
                Explosion.Cancel();
                Explosion = null;
            }
        }
    }

    // 한 회차의 예고 시간과 미사일 실행 데이터를 묶는다.
    private sealed class RoundRuntime
    {
        // 실행 시작 시 복사한 회차 예고 시간이다.
        public readonly float PreviewDuration;

        // 이 회차에 속한 미사일들이다.
        public readonly MissileRuntime[] Missiles;

        /// <summary>
        /// RoundRuntime 동작에 필요한 상태와 처리 흐름을 실행한다.
        /// </summary>
        public RoundRuntime(BossAlienBarrageRoundConfig config)
        {
            PreviewDuration = config.PreviewDuration;
            Missiles = new MissileRuntime[config.MissileCount];

            // 같은 Config가 여러 회차에 등록되어도 실행 데이터는 따로 만든다.
            for (int i = 0; i < Missiles.Length; i++)
            {
                Missiles[i] = new MissileRuntime(
                    config.GetMissileSettings(i));
            }
        }

        // 완료한 회차 또는 취소된 회차의 오브젝트를 정리한다.
        /// <summary>
        /// 완료한 회차 또는 취소된 회차의 오브젝트를 정리한다.
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < Missiles.Length; i++)
            {
                Missiles[i].Clear();
            }
        }
    }

    // 설정 검사를 통과한 뒤, 이번 실행 전용 데이터를 생성한다.
    // 반환한 배열은 Execute의 지역 변수로 보관한다.
    /// <summary>
    /// 반환한 배열은 Execute의 지역 변수로 보관한다.
    /// </summary>
    private RoundRuntime[] CreateRuntimeRounds()
    {
        RoundRuntime[] result = new RoundRuntime[_rounds.Length];

        // Inspector에 등록한 회차 순서를 그대로 유지한다.
        for (int i = 0; i < _rounds.Length; i++)
        {
            result[i] = new RoundRuntime(_rounds[i]);
        }

        return result;
    }

    // 한 회차의 미사일을 같은 프레임에 모두 생성하고 낙하시킨다.
    // spawnY는 패턴 실행 시작 시 계산할 공통 출발 높이다.
    /// <summary>
    /// spawnY는 패턴 실행 시작 시 계산할 공통 출발 높이다.
    /// </summary>
    private void LaunchRound(
        Boss_Alien boss,
        RoundRuntime round,
        float spawnY,
        float fallDuration)
    {
        for (int i = 0; i < round.Missiles.Length; i++)
        {
            MissileRuntime data = round.Missiles[i];

            // 예고한 X좌표의 위쪽에서 수직으로 내려온다.
            Vector2 startPosition = new Vector2(
                data.Position.x, spawnY);

            // 초기화 오류나 취소 때 정리할 수 있도록 먼저 참조를 저장한다.
            data.Missile = Instantiate(_missilePrefab);

            // 예고에 사용한 위치를 실제 도착점으로 그대로 전달한다.
            data.Missile.Launch(
                boss,
                data.Sprite,
                startPosition,
                data.Position,
                fallDuration);
        }
    }

    // 해당 회차의 모든 미사일이 목표에 도착했는지 확인한다.
    /// <summary>
    /// 해당 회차의 모든 미사일이 목표에 도착했는지 확인한다.
    /// </summary>
    private bool HaveAllMissilesArrived(RoundRuntime round)
    {
        bool allArrived = true;

        for (int i = 0; i < round.Missiles.Length; i++)
        {
            BossAlienBarrageMissile missile = round.Missiles[i].Missile;

            // 도착 확인 전에 미사일이 사라졌다면 무한히 기다리지 않고 알린다.
            // 정상적인 전투 취소 여부는 호출하는 실행부에서 먼저 확인한다.
            if (missile == null || !missile.isActiveAndEnabled)
            {
                throw new InvalidOperationException(
                    $"{name}: 도착 확인 전에 포격 미사일이 제거되거나 비활성화됐습니다.");
            }

            if (!missile.HasArrived)
            {
                allArrived = false;
            }
        }

        return allArrived;
    }

    // 모든 미사일이 도착한 회차의 폭발 외형을 한꺼번에 생성한다.
    // 회차당 최대 피해 한 번 적용은 별도 메서드에서 처리한다.
    /// <summary>
    /// 회차당 최대 피해 한 번 적용은 별도 메서드에서 처리한다.
    /// </summary>
    private void CreateRoundExplosions(
        Boss_Alien boss,
        RoundRuntime round,
        float fadeDuration)
    {
        for (int i = 0; i < round.Missiles.Length; i++)
        {
            MissileRuntime data = round.Missiles[i];

            // 같은 미사일의 폭발 외형을 중복 생성하지 않는다.
            if (data.ImpactHandled)
            {
                continue;
            }

            Vector3 position = new Vector3(
                data.Position.x, data.Position.y, 0f);

            // 부모 없이 생성하여 월드 위치와 크기를 유지한다.
            data.Explosion = Instantiate(
                _explosionPrefab,
                position,
                Quaternion.identity);

            data.Explosion.Initialize(
                boss,
                data.Radius,
                data.VisualScale,
                fadeDuration);

            data.ImpactHandled = true;

            // 도착한 미사일은 폭발 외형으로 교체됐으므로 제거한다.
            data.Missile.Remove();
            data.Missile = null;
        }
    }

    // 해당 회차의 폭발 애니메이션과 페이드아웃이 모두 끝났는지 확인한다.
    /// <summary>
    /// 해당 회차의 폭발 애니메이션과 페이드아웃이 모두 끝났는지 확인한다.
    /// </summary>
    private bool HaveAllExplosionsFinished(RoundRuntime round)
    {
        for (int i = 0; i < round.Missiles.Length; i++)
        {
            MissileRuntime data = round.Missiles[i];

            // 아직 폭발을 생성하지 않은 미사일은 완료 상태가 아니다.
            if (!data.ImpactHandled)
            {
                return false;
            }

            // 폭발은 정상 완료 시 자기 오브젝트를 제거한다.
            // 참조가 남아 있으면 완료 상태를 직접 확인한다.
            if (data.Explosion != null && !data.Explosion.IsFinished)
            {
                return false;
            }
        }

        return true;
    }

    // 플레이어 몸과 겹친 폭발 중 최대 피해를 선택해 한 번만 요청한다.
    // 회차의 폭발 생성 직후 한 번 호출하며, 잔광에는 피해가 없다.
    /// <summary>
    /// 회차의 폭발 생성 직후 한 번 호출하며, 잔광에는 피해가 없다.
    /// </summary>
    private void ApplyRoundDamage(
        RoundRuntime round,
        PlayerController player)
    {
        if (player == null ||
            !player.isActiveAndEnabled ||
            player.IsDead)
        {
            return;
        }

        // 최대 피해를 고를 때는 플레이어 몸만 검사한다.
        // 선택된 폭발의 실드 방어 여부는 기존 피해 함수에서 처리한다.
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(LayerMask.GetMask("Player"));
        filter.useTriggers = true;

        // 같은 프레임의 이동을 범위 검사에 반영한다.
        Physics2D.SyncTransforms();

        // 한 회차 안에서는 결과 목록 하나를 재사용한다.
        List<Collider2D> hits = new List<Collider2D>();
        MissileRuntime strongest = null;

        for (int i = 0; i < round.Missiles.Length; i++)
        {
            MissileRuntime missile = round.Missiles[i];

            // 피해가 없거나 현재 최댓값 이하이면 추가 검사가 필요 없다.
            // 피해가 같으면 먼저 등록된 미사일을 유지한다.
            if (missile.Damage <= 0 ||
                (strongest != null &&
                 missile.Damage <= strongest.Damage))
            {
                continue;
            }

            hits.Clear();
            Physics2D.OverlapCircle(
                missile.Position,
                missile.Radius,
                filter,
                hits);

            for (int hitIndex = 0; hitIndex < hits.Count; hitIndex++)
            {
                // 공격 대상 플레이어의 실제 몸 콜라이더인지 확인한다.
                PlayerController hitPlayer =
                    hits[hitIndex].GetComponentInParent<PlayerController>();

                if (hitPlayer == player)
                {
                    strongest = missile;
                    break;
                }
            }
        }

        if (strongest == null)
        {
            return;
        }

        // 선택한 폭발에 대해 실드 방어를 먼저 시도하고,
        // 방어하지 못하면 기존 TakeHit으로 피해를 한 번 요청한다.
        BossAlienExplosionDamage.Apply(
            strongest.Position,
            strongest.Radius,
            strongest.Damage);
    }

}
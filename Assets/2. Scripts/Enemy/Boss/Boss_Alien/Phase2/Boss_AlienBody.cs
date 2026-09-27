using System;
using System.Collections.Generic;
using UnityEngine;

// 로봇에서 나온 에일리언 본체의 전투 상태와 공통 참조를 관리한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class Boss_AlienBody : BossEnemy
{
    // 도약 조준 시 플레이어의 현재 위치와 이동 속도를 읽는다.
    private Rigidbody2D _targetBody;

    // 기존 로봇 패턴과 동일한 전투 영역을 사용한다.
    private BossAlienGrabArena _arena;

    // 본체 체력으로 발동할 특수패턴 목록이다.
    // 높은 체력 퍼센트부터 등록하며 로봇의 목록과 별도로 사용한다.
    [SerializeField]
    private List<HealthBossPatternEntry> _healthSpecialPatternEntries =
        new List<HealthBossPatternEntry>();

    // 각 항목을 이미 예약했는지 본체 인스턴스별로 기록한다.
    private bool[] _hasRequestedHealthPatterns = Array.Empty<bool>();

    // 초기화가 끝나기 전에는 체력 조건을 검사하지 않는다.
    private bool _isHealthPatternReady;

    // 플레이어 사망에 따른 패턴 정리를 한 번만 실행한다.
    private bool _hasStoppedForPlayerDeath;

    // 로봇과 본체로 구성된 보스전이다.
    public override int TotalPhaseCount => 2;

    // 이 프리팹은 본체인 두 번째 페이즈를 담당한다.
    public override int CurrentPhaseNumber => 2;

    // 본체 체력은 빨간 체력바에 표시한다.
    public override bool UsesShieldHealthBar => false;

    // 초기화가 끝나고 보스와 플레이어가 모두 살아 있어야 공격한다.
    public bool CanRunPhaseTwoPattern =>
        isActiveAndEnabled &&
        HasBattleStarted &&
        !IsDead &&
        TargetPlayer != null &&
        TargetPlayer.isActiveAndEnabled &&
        !TargetPlayer.IsDead;

    // 전투가 중단되면 일반 패턴 타이머도 멈춘다.
    protected override bool CanUseNormalPatterns =>
        CanRunPhaseTwoPattern;

    // 게임 시간이 멈춰도 실행되어 물리 프레임을 기다리는 패턴을 정리한다.
    /// <summary>
    /// 게임 시간이 멈춰도 실행되어 물리 프레임을 기다리는 패턴을 정리한다.
    /// </summary>
    private void LateUpdate()
    {
        if (!HasBattleStarted ||
            _hasStoppedForPlayerDeath ||
            TargetPlayer == null ||
            !TargetPlayer.IsDead)
        {
            return;
        }

        // 정리 과정에서 다른 처리가 발생해도 중복 실행하지 않는다.
        _hasStoppedForPlayerDeath = true;

        // 실행 중인 패턴과 대기열을 제거하고 finally 정리 코드를 실행한다.
        StopPatternsForPhaseChange();

        // 중단 과정에서 이동이 재개되더라도 최종적으로 정지시킨다.
        SetControllable(false);
    }

    // 본체가 비활성화되면 실행 중인 공격과 대기 중인 패턴을 정리한다.
    /// <summary>
    /// 본체가 비활성화되면 실행 중인 공격과 대기 중인 패턴을 정리한다.
    /// </summary>
    protected override void OnDisable()
    {
        // 기존 실행기를 중단하고 패턴의 finally 정리 코드를 실행한다.
        // 도약 외형 위치 복원과 경고·발톱 제거도 이 과정에서 처리된다.
        StopPatternsForPhaseChange();

        // 패턴 중단 과정에서 이동이 재개되더라도 최종적으로 정지시킨다.
        SetControllable(false);

        // 부모가 담당하는 충돌 상태 복원도 유지한다.
        base.OnDisable();
    }

    /// <summary>
    /// OnBossInitialized 동작에 필요한 상태와 처리 흐름을 실행한다.
    /// </summary>
    protected override void OnBossInitialized()
    {
        base.OnBossInitialized();

        // 이번 전투의 플레이어 사망 처리 상태를 초기화한다.
        _hasStoppedForPlayerDeath = false;

        // 본체의 이동은 각 공격 패턴이 직접 제어한다.
        SetControllable(false);

        // 예측 조준에 사용할 플레이어의 물리 참조를 보관한다.
        _targetBody = TargetPlayer.GetComponent<Rigidbody2D>();

        if (_targetBody == null)
        {
            throw new MissingComponentException(
                $"{name}: 플레이어 루트에 Rigidbody2D가 필요합니다.");
        }

        // 현재 프로젝트는 활성 전투 영역 하나를 사용하는 구조다.
        // 패턴마다 검색하지 않고 본체 초기화 때 한 번 찾는다.
        BossAlienGrabArena[] arenas =
            UnityEngine.Object.FindObjectsByType<BossAlienGrabArena>(
                FindObjectsSortMode.None);

        // 영역이 없거나 여러 개면 임의의 영역을 선택하지 않는다.
        if (arenas.Length != 1 || !arenas[0].isActiveAndEnabled)
        {
            throw new InvalidOperationException(
                $"{name}: 활성화된 BossAlienGrabArena가 하나 필요합니다.");
        }

        _arena = arenas[0];

        // 벽 참조와 전투 영역 설정이 올바른지 미리 검사한다.
        _arena.GetInteriorRect();

        // 본체 전투에 사용할 체력 조건과 발동 기록을 준비한다.
        InitializeHealthPatterns();
    }

    // 체력 특수패턴 설정을 검사하고 이번 본체의 발동 기록을 초기화한다.
    /// <summary>
    /// 체력 특수패턴 설정을 검사하고 이번 본체의 발동 기록을 초기화한다.
    /// </summary>
    private void InitializeHealthPatterns()
    {
        _isHealthPatternReady = false;

        float previousPercent = 100f;

        for (int i = 0; i < _healthSpecialPatternEntries.Count; i++)
        {
            HealthBossPatternEntry entry = _healthSpecialPatternEntries[i];

            // 빈 항목은 실행 시점까지 숨기지 않고 초기화에서 알린다.
            if (entry == null || entry.Pattern == null)
            {
                throw new InvalidOperationException(
                    $"{name}: 본체 체력 특수패턴 {i + 1}번을 연결하세요.");
            }

            // 0%, 100%, NaN, 무한대는 발동 기준으로 허용하지 않는다.
            if (!(entry.HealthPercent > 0f && entry.HealthPercent < 100f))
            {
                throw new InvalidOperationException(
                    $"{name}: 체력 기준은 0보다 크고 100보다 작아야 합니다.");
            }

            // 큰 피해로 여러 기준을 통과해도 높은 체력 조건부터 예약한다.
            if (entry.HealthPercent > previousPercent)
            {
                throw new InvalidOperationException(
                    $"{name}: 체력 특수패턴을 높은 퍼센트부터 등록하세요.");
            }

            previousPercent = entry.HealthPercent;
        }

        // ScriptableObject가 아닌 현재 본체에 발동 기록을 저장한다.
        _hasRequestedHealthPatterns =
            new bool[_healthSpecialPatternEntries.Count];

        _isHealthPatternReady = true;
        CheckHealthPatterns();
    }

    // 부모가 실제 피해를 적용한 다음 본체의 체력 조건을 확인한다.
    /// <summary>
    /// 보스 피격 이후 상태와 패턴을 갱신한다.
    /// </summary>
    protected override void OnBossDamaged(float damage)
    {
        // 보스 피격 이후 상태와 패턴을 갱신한다.
        base.OnBossDamaged(damage);
        CheckHealthPatterns();
    }

    // 조건을 만족한 항목을 전투 중 한 번씩 특수패턴 대기열에 넣는다.
    /// <summary>
    /// 조건을 만족한 항목을 전투 중 한 번씩 특수패턴 대기열에 넣는다.
    /// </summary>
    private void CheckHealthPatterns()
    {
        // 치명타로 체력이 0이 됐으면 공격 예약보다 사망 처리를 우선한다.
        if (!_isHealthPatternReady ||
            !CanRunPhaseTwoPattern ||
            CurrentHp <= 0 ||
            MaxHp <= 0)
        {
            return;
        }

        // 로봇 체력을 합산하지 않고 현재 본체 체력만 사용한다.
        float healthPercent = (float)CurrentHp / MaxHp * 100f;

        for (int i = 0; i < _healthSpecialPatternEntries.Count; i++)
        {
            if (_hasRequestedHealthPatterns[i])
                continue;

            HealthBossPatternEntry entry = _healthSpecialPatternEntries[i];

            if (healthPercent > entry.HealthPercent)
                continue;

            // 예약 과정에서 추가 피격이 발생해도 중복 요청하지 않도록 먼저 기록한다.
            _hasRequestedHealthPatterns[i] = true;

            // 진행 중인 공격을 끊지 않고 기존 특수패턴 대기열에 예약한다.
            RequestSpecialPattern(entry.Pattern);
        }
    }

    // 본체 패턴들이 동일한 전투 영역을 사용하도록 제공한다.
    /// <summary>
    /// 본체 패턴들이 동일한 전투 영역을 사용하도록 제공한다.
    /// </summary>
    public Rect GetBattleArea()
    {
        if (_arena == null || !_arena.isActiveAndEnabled)
        {
            throw new InvalidOperationException(
                $"{name}: 사용할 수 있는 전투 영역이 없습니다.");
        }

        return _arena.GetInteriorRect();
    }

    // 현재 이동을 기준으로 플레이어의 미래 위치를 계산한다.
    // 이 함수는 위치 계산만 하며 보스를 움직이거나 피해를 주지 않는다.
    /// <summary>
    /// 이 함수는 위치 계산만 하며 보스를 움직이거나 피해를 주지 않는다.
    /// </summary>
    public bool TryGetPredictedTargetPosition(
        float predictionSeconds,
        float maxPredictionDistance,
        out Vector2 predictedPosition)
    {
        predictedPosition = Vector2.zero;

        // 전투가 끝났거나 플레이어 참조가 사라졌으면 조준하지 않는다.
        if (!CanRunPhaseTwoPattern || _targetBody == null)
        {
            return false;
        }

        // 음수나 유효하지 않은 설정은 오류로 알린다.
        if (float.IsNaN(predictionSeconds) ||
            float.IsInfinity(predictionSeconds) ||
            predictionSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(predictionSeconds));
        }

        if (float.IsNaN(maxPredictionDistance) ||
            float.IsInfinity(maxPredictionDistance) ||
            maxPredictionDistance < 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxPredictionDistance));
        }

        // 현재 속도가 유지된다고 가정한 예상 이동 거리다.
        Vector2 predictedOffset =
            _targetBody.linearVelocity * predictionSeconds;

        // 예측으로 앞서 조준하는 거리에 상한을 둔다.
        predictedOffset = Vector2.ClampMagnitude(
            predictedOffset,
            maxPredictionDistance);

        predictedPosition = _targetBody.position + predictedOffset;
        return true;
    }

    // 예상 착지점인 보스 루트 좌표를 전투 영역 안으로 제한한다.
    // 피격 콜라이더의 크기는 착지점 제한에 사용하지 않는다.
    /// <summary>
    /// 피격 콜라이더의 크기는 착지점 제한에 사용하지 않는다.
    /// </summary>
    public bool TryGetLeapLandingPosition(
    float predictionSeconds,
    float maxPredictionDistance,
    float wallPadding,
    // 보스 원점에서 착지할 발 접촉점까지의 월드 좌표 차이다.
    Vector2 landingContactOffset,
    out Vector2 landingPosition)
    {
        landingPosition = Vector2.zero;

        // 전투가 중단되거나 영역이 사라졌으면 조준하지 않는다.
        if (_arena == null ||
            !_arena.isActiveAndEnabled ||
            !CanRunPhaseTwoPattern)
        {
            return false;
        }

        // 여기서 wallPadding은 벽과 착지 중심 사이의 최소 간격이다.
        if (float.IsNaN(wallPadding) ||
            float.IsInfinity(wallPadding) ||
            wallPadding < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(wallPadding));
        }

        // 플레이어의 현재 이동을 기준으로 예상 위치를 구한다.
        if (!TryGetPredictedTargetPosition(
            predictionSeconds,
            maxPredictionDistance,
            out Vector2 predictedPosition))
        {
            return false;
        }

        Rect area = _arena.GetInteriorRect();

        // 큰 피격 콜라이더 대신 착지 중심에 필요한 여유만 확보한다.
        float minX = area.xMin + wallPadding;
        float maxX = area.xMax - wallPadding;
        float minY = area.yMin + wallPadding;
        float maxY = area.yMax - wallPadding;

        // 발 접촉점과 보스 원점이 모두 허용 영역 안에 있도록 범위를 좁힌다.
        // 발 위치를 보정하면서 보스 원점이 외벽 밖으로 나가는 것을 방지한다.
        minX = Mathf.Max(minX, minX + landingContactOffset.x);
        maxX = Mathf.Min(maxX, maxX + landingContactOffset.x);
        minY = Mathf.Max(minY, minY + landingContactOffset.y);
        maxY = Mathf.Min(maxY, maxY + landingContactOffset.y);

        // 여유 간격이 전투 영역보다 크면 설정 오류를 알린다.
        if (minX > maxX || minY > maxY)
        {
            throw new InvalidOperationException(
                $"{name}: 전투 영역에 비해 착지점의 벽 여유 간격이 큽니다.");
        }

        // X와 Y 모두 동일하게 경계 안으로 보정한다.
        landingPosition = new Vector2(
            Mathf.Clamp(predictedPosition.x, minX, maxX),
            Mathf.Clamp(predictedPosition.y, minY, maxY));

        return true;
    }
}
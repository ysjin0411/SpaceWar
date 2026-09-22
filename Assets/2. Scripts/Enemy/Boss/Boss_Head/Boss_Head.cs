using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 체력 조건에 따라 헤드 보스의 특수패턴을 요청한다.
/// 이동과 레이저 공격의 구체적인 설정은 패턴 Config가 담당한다.
/// </summary>
public class Boss_Head : BossEnemy
{

    // 높은 체력 기준부터 등록한다.
    // 여러 조건을 한 번에 넘으면 등록 순서대로 요청한다.
    [SerializeField]
    List<HealthBossPatternEntry> _healthSpecialPatternEntries =
        new List<HealthBossPatternEntry>();

    // 각 체력 조건을 이미 요청했는지 기록한다.
    bool[] _hasRequestedPatterns = Array.Empty<bool>();

    // 보스 초기화가 완료된 뒤에만 체력 조건을 검사한다.
    bool _isHealthPatternReady;

    private void OnEnable()
    {
        // 체력이 변경될 때 특수패턴 발동 조건을 확인한다.
        HealthChanged += HandleHealthChanged;
    }

    protected override void OnDisable()
    {
        // 기존 체력 이벤트 구독을 해제한다.
        HealthChanged -= HandleHealthChanged;

        // 부모에서 복귀 충돌 상태를 정리한다.
        base.OnDisable();
    }

    protected override void OnBossInitialized()
    {
        base.OnBossInitialized();

        _isHealthPatternReady = false;

        // 잘못된 Config와 체력 기준을 초기화 시 검사한다.
        for (int i = 0; i < _healthSpecialPatternEntries.Count; i++)
        {
            HealthBossPatternEntry entry =
                _healthSpecialPatternEntries[i];

            if (entry == null || entry.Pattern == null)
            {
                throw new InvalidOperationException(
                    $"{name}: 체력 패턴 {i + 1}번에 Config를 연결하세요.");
            }

            if (!(entry.HealthPercent > 0f &&
                  entry.HealthPercent < 100f))
            {
                throw new InvalidOperationException(
                    $"{name}: 체력 패턴 {i + 1}번 기준은 " +
                    "0보다 크고 100보다 작아야 합니다.");
            }
        }

        // 보스가 생성될 때 모든 조건을 미발동 상태로 시작한다.
        _hasRequestedPatterns =
            new bool[_healthSpecialPatternEntries.Count];

        _isHealthPatternReady = true;

        // 초기화 시점의 체력도 확인한다.
        HandleHealthChanged(CurrentHp, MaxHp);
    }

    /// <summary>
    /// 조건을 만족한 항목마다 특수패턴을 한 번만 요청한다.
    /// </summary>
    private void HandleHealthChanged(int currentHp, int maxHp)
    {
        // 초기화 전이거나 사망에 이르는 피해라면 요청하지 않는다.
        if (!_isHealthPatternReady ||
            IsDead || currentHp <= 0 || maxHp <= 0)
        {
            return;
        }

        float healthPercent = (float)currentHp / maxHp * 100f;

        for (int i = 0; i < _healthSpecialPatternEntries.Count; i++)
        {
            if (_hasRequestedPatterns[i])
            {
                continue;
            }

            HealthBossPatternEntry entry =
                _healthSpecialPatternEntries[i];

            if (healthPercent > entry.HealthPercent)
            {
                continue;
            }

            // 요청 전에 기록하여 같은 조건의 중복 발동을 막는다.
            _hasRequestedPatterns[i] = true;

            // 기존 Runner가 현재 패턴 종료 후 우선 실행한다.
            RequestSpecialPattern(entry.Pattern);
        }
    }
}
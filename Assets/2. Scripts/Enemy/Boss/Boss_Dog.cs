using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 체력 조건에 따라 Boss Dog의 특수 패턴을 요청한다.
/// 실제 실행 순서는 기존 BossPatternRunner가 관리한다.
/// </summary>
public class Boss_Dog : BossEnemy
{
    [Header("체력 특수 패턴")]

    // 높은 체력 기준부터 등록한다.
    // 여러 조건을 한 번에 만족하면 리스트 위에서부터 요청한다.
    [SerializeField]
    List<HealthBossPatternEntry> _healthSpecialPatternEntries =
        new List<HealthBossPatternEntry>();

    // 각 Entry가 이미 요청됐는지를 인덱스별로 기록한다.
    // 같은 Config를 여러 슬롯에 넣어도 슬롯별로 한 번씩 실행한다.
    bool[] _hasRequestedPatterns = Array.Empty<bool>();

    // 초기화가 끝나기 전에는 체력 이벤트를 처리하지 않는다.
    bool _isHealthPatternReady;

    private void OnEnable()
    {
        // 체력 변화 이벤트를 구독한다.
        HealthChanged += HandleHealthChanged;
    }

    private void OnDisable()
    {
        // 비활성화될 때 이벤트 구독을 해제한다.
        HealthChanged -= HandleHealthChanged;
    }

    protected override void OnBossInitialized()
    {
        base.OnBossInitialized();

        // 설정 검사가 끝날 때까지 패턴 요청을 막는다.
        _isHealthPatternReady = false;

        // Inspector에 등록된 모든 항목을 검사한다.
        for (int i = 0; i < _healthSpecialPatternEntries.Count; i++)
        {
            HealthBossPatternEntry entry =
                _healthSpecialPatternEntries[i];

            // Config가 빠진 항목은 잘못된 설정으로 알려준다.
            if (entry == null || entry.Pattern == null)
            {
                throw new InvalidOperationException(
                    $"{name}의 체력 패턴 {i + 1}번 항목에 Config가 없습니다.");
            }

            // 슬라이더 대신 직접 입력하므로 유효한 퍼센트인지 검사한다.
            if (!(entry.HealthPercent > 0f &&
                  entry.HealthPercent < 100f))
            {
                throw new InvalidOperationException(
                    $"{name}의 체력 패턴 {i + 1}번 체력은 " +
                    "0보다 크고 100보다 작아야 합니다.");
            }
        }

        // 리스트 개수만큼 발동 기록을 만든다.
        // 새 bool 배열의 모든 값은 false로 시작한다.
        _hasRequestedPatterns =
            new bool[_healthSpecialPatternEntries.Count];

        // 이제 체력 이벤트를 처리할 수 있다.
        _isHealthPatternReady = true;

        // 초기화 시점의 현재 체력도 확인한다.
        HandleHealthChanged(CurrentHp, MaxHp);
    }

    /// <summary>
    /// 아직 요청하지 않은 항목 중 체력 조건을 만족한 패턴을 요청한다.
    /// 체력이 회복됐다 다시 내려가도 이미 요청한 항목은 반복하지 않는다.
    /// </summary>
    private void HandleHealthChanged(int currentHp, int maxHp)
    {
        // BossEnemy가 초기화 중 먼저 보내는 이벤트는 건너뛴다.
        if (!_isHealthPatternReady)
        {
            return;
        }

        // 사망했거나 유효하지 않은 체력이라면 요청하지 않는다.
        if (IsDead || currentHp <= 0 || maxHp <= 0)
        {
            return;
        }

        // 현재 체력을 퍼센트로 계산한다.
        float currentHealthPercent =
            (float)currentHp / maxHp * 100f;

        for (int i = 0; i < _healthSpecialPatternEntries.Count; i++)
        {
            // 이미 요청한 항목은 건너뛴다.
            if (_hasRequestedPatterns[i])
            {
                continue;
            }

            HealthBossPatternEntry entry =
                _healthSpecialPatternEntries[i];

            // 아직 발동 기준보다 체력이 높으면 건너뛴다.
            if (currentHealthPercent > entry.HealthPercent)
            {
                continue;
            }

            // 요청 전에 기록하여 중복 요청을 방지한다.
            _hasRequestedPatterns[i] = true;

            // 진행 중인 패턴을 끊지 않고 특수 패턴 대기열에 넣는다.
            RequestSpecialPattern(entry.Pattern);
        }
    }
}
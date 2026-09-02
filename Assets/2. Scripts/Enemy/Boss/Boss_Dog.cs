using System;
using UnityEngine;

/// <summary>
/// Boss Dog만의 고유한 상태와 특수 패턴 진입 조건을 관리한다.
/// 공통 체력, 이동, 패턴 실행 기능은 BossEnemy에서 상속받는다.
/// </summary>
public class Boss_Dog : BossEnemy
{
    [Header("체력 특수 패턴")]

    // 해당 체력 비율에 도달했을 때 실행할 특수 패턴 설정이다.
    // Inspector에서 Health Percent를 70으로 설정한다.
    [SerializeField]
    HealthBossPatternEntry _healthSpecialPatternEntry;

    // 같은 체력 특수 패턴이 여러 번 요청되는 것을 방지한다.
    bool _hasRequestedHealthSpecialPattern;

    private void OnEnable()
    {
        // BossEnemy의 체력이 변경될 때마다 현재 체력 비율을 확인한다.
        HealthChanged += HandleHealthChanged;
    }

    private void OnDisable()
    {
        // 오브젝트가 비활성화되거나 제거될 때 이벤트 구독을 해제한다.
        HealthChanged -= HandleHealthChanged;
    }

    /// <summary>
    /// Boss Dog 초기화 시 특수 패턴 설정이 정상적으로 들어왔는지 확인한다.
    /// </summary>
    protected override void OnBossInitialized()
    {
        base.OnBossInitialized();

        if (_healthSpecialPatternEntry == null)
        {
            throw new InvalidOperationException(
                $"{name}에 체력 특수 패턴 Entry가 설정되지 않았습니다.");
        }

        if (_healthSpecialPatternEntry.Pattern == null)
        {
            throw new InvalidOperationException(
                $"{name}의 체력 특수 패턴이 설정되지 않았습니다.");
        }
    }

    /// <summary>
    /// 체력이 변경될 때 설정된 체력 비율에 도달했는지 확인한다.
    /// 조건을 만족하면 특수 패턴을 한 번만 요청한다.
    /// </summary>
    private void HandleHealthChanged(
        int currentHp,
        int maxHp)
    {
        // 이미 요청한 특수 패턴은 다시 요청하지 않는다.
        if (_hasRequestedHealthSpecialPattern)
        {
            return;
        }

        // 죽게 만든 공격에서는 새로운 특수 패턴을 요청하지 않는다.
        if (currentHp <= 0)
        {
            return;
        }

        if (_healthSpecialPatternEntry == null ||
            maxHp <= 0)
        {
            return;
        }

        // 현재 체력을 0~100 범위의 퍼센트로 계산한다.
        float currentHealthPercent =
            (float)currentHp / maxHp * 100f;

        // 아직 설정된 체력 비율보다 높다면 기다린다.
        if (currentHealthPercent >
            _healthSpecialPatternEntry.HealthPercent)
        {
            return;
        }

        // 이벤트가 다시 들어와도 중복 요청되지 않도록 먼저 기록한다.
        _hasRequestedHealthSpecialPattern = true;

        // 일반 패턴 실행 중이면 Runner가 대기시켰다가
        // 현재 일반 패턴이 끝난 직후 이 특수 패턴을 실행한다.
        RequestSpecialPattern(
            _healthSpecialPatternEntry.Pattern);
    }
}
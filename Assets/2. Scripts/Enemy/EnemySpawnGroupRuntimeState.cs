using System;

// 묶음 하나의 타이머와 패턴 선택기를 관리한다.
public sealed class EnemySpawnGroupRuntimeState
{
    // Inspector에서 설정한 묶음 데이터다.
    readonly EnemySpawnGroup _group;

    // 이 묶음의 패턴들만 대상으로 가중치와 개별 쿨타임을 관리한다.
    readonly EnemySpawnPatternSelector _selector;

    // 이 묶음이 마지막으로 소환한 뒤 흐른 시간이다.
    float _timer;

    // 묶음마다 별도의 선택기를 만들어 선택 범위를 분리한다.
    public EnemySpawnGroupRuntimeState(EnemySpawnGroup group)
    {
        if (group == null)
        {
            throw new ArgumentNullException(nameof(group));
        }

        _group = group;
        _selector = new EnemySpawnPatternSelector(group.Patterns);
    }

    // 시간이 충분히 지났으면 이 묶음에서 패턴 하나를 실행한다.
    public void Tick(
        float deltaTime,
        bool isBossBattle,
        EnemySpawnContext context)
    {
        if (deltaTime <= 0f)
        {
            return;
        }

        // 이 묶음에 속한 패턴들의 개별 쿨타임을 감소시킨다.
        _selector.UpdateCooldowns(deltaTime);

        // 묶음 소환 타이머는 다른 묶음과 독립적으로 증가한다.
        _timer += deltaTime;

        if (_timer < _group.GetInterval(isBossBattle))
        {
            return;
        }

        // 선택할 패턴이 없으면 타이머를 유지하고 다음 프레임에 재시도한다.
        if (!_selector.TrySelect(
            out EnemySpawnPatternRuntimeState selectedState))
        {
            return;
        }

        // 소환 간격은 패턴 실행을 시작한 시점부터 다시 계산한다.
        _timer = 0f;

        // 선택한 패턴의 개별 쿨타임도 시작한다.
        selectedState.StartCooldown();
        selectedState.Pattern.Execute(context);
    }

    // 소환 중단 또는 재개 시 묶음의 소환 간격만 초기화한다.
    public void ResetTimer()
    {
        _timer = 0f;
    }

    // 스테이지 단계가 바뀌면 묶음 타이머와 개별 쿨타임을 모두 초기화한다.
    public void ResetAll()
    {
        _timer = 0f;
        _selector.ResetCooldowns();
    }
}
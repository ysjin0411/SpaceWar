using UnityEngine;

/// <summary>
/// Boss Dog의 이동, 공격, 피격, 사망 패턴을 담당한다.
/// 체력과 공통 사망 처리는 BossEnemy가 담당한다.
/// </summary>
public class Boss_Dog : BossEnemy
{
    [Header("Boss Dog 이동 설정")]
    [Tooltip("Boss Dog의 이동속도")]
    [Min(0f)]
    [SerializeField] float _moveSpeed = 2f;

    [Header("Boss Dog 런타임 상태")]
    [Tooltip("현재 Boss Dog가 이동과 공격을 실행할 수 있는지 확인")]
    [SerializeField] bool _canAct;

    private void Update()
    {
        // BossEncounterManager의 초기화가 끝나기 전에는 행동하지 않는다.
        if (!_canAct)
        {
            return;
        }

        // 사망 처리된 이후에는 이동과 공격 패턴을 실행하지 않는다.
        if (IsDead)
        {
            return;
        }

        // 공격 대상을 전달받지 못했다면 패턴을 실행할 수 없다.
        if (TargetPlayer == null)
        {
            return;
        }

        UpdateMovement();
        UpdateAttack();
    }

    /// <summary>
    /// BossEncounterManager가 Initialize()를 호출한 뒤 실행된다.
    /// 플레이어 참조까지 전달된 이후에만 보스 행동을 시작한다.
    /// </summary>
    protected override void OnBossInitialized()
    {
        _canAct = true;

        // 이후 Boss Dog의 패턴 타이머 등을 초기화한다.
    }

    /// <summary>
    /// Boss Dog의 전용 이동 패턴을 처리한다.
    /// </summary>
    private void UpdateMovement()
    {
        // 이후 등장 위치로 이동하거나
        // 플레이어를 기준으로 위아래 이동하는 패턴을 구현한다.
    }

    /// <summary>
    /// Boss Dog의 전용 공격 패턴을 처리한다.
    /// </summary>
    private void UpdateAttack()
    {
        // 이후 탄막 발사나 돌진 공격 패턴을 구현한다.
    }

    /// <summary>
    /// Boss Dog가 피해를 받은 직후 실행된다.
    /// 체력 감소와 UI 갱신은 BossEnemy가 이미 처리한 상태다.
    /// </summary>
    protected override void OnBossDamaged(int damage)
    {
        // 이후 피격 색상 변경, 흔들림 등의 연출을 실행한다.
    }

    /// <summary>
    /// Boss Dog의 체력이 0이 됐을 때 실행된다.
    /// </summary>
    protected override void OnBossDefeated()
    {
        // 사망 이후에는 이동과 공격을 중지한다.
        _canAct = false;

        // 이후 Animator 사망 트리거, 폭발 효과, 사운드를 실행한다.
    }
}
using UnityEngine;

/// <summary>
/// 모든 일반 적 생성 패턴이 상속받는 전략 클래스다.
/// 변하지 않는 패턴 설정만 보관하고 실행 중 상태는 저장하지 않는다.
/// </summary>
public abstract class EnemySpawnPattern : ScriptableObject
{
    [Header("패턴 기본 설정")]

    [SerializeField] string _patternId; // 로그와 패턴 구분에 사용할 고유 ID

    [Min(1)]
    [SerializeField] int _weight = 10; // 선택 가능한 상태일 때 사용할 기본 가중치

    [Min(0f)]
    [SerializeField] float _cooldown = 5f; // 선택된 후 다시 선택될 수 있을 때까지의 시간

    public string PatternId => _patternId;
    public int Weight => _weight;
    public float Cooldown => _cooldown;

    /// <summary>
    /// 자식 패턴이 자신만의 적 생성 규칙을 실행한다.
    /// </summary>
    public abstract void Execute(EnemySpawnContext context);
}
using UnityEngine;

public enum SmallEnemyMoveType
{
    Suicide,
    Twin
}
public class SmallEnemy : Enemy
{
    [Header("발사위치")]
    [SerializeField] Transform _firePoint;

    [Header("패턴타입")]
    [SerializeField] SmallEnemyMoveType _moveType;

    protected override void Awake()
    {
        base.Awake();
    }

    private void Update()
    {
        // 죽으면 정지
        if (IsDead)
            return;
    }

    /// <summary>
    /// EnemySpawner가 생성한 FastEnemy의 이동 패턴을 지정한다.
    /// </summary>
    public void SetMoveType(SmallEnemyMoveType moveType)
    {
        // 전달받은 이동 패턴을 현재 FastEnemy에 저장한다.
        _moveType = moveType;
    }

    public void SmallEnemyPatternA()
    {

    }
}

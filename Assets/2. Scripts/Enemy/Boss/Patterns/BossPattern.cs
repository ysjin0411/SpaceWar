using System.Collections;
using UnityEngine;

/// <summary>
/// 모든 보스 패턴이 상속받는 기반 클래스다.
/// 패턴 설정만 저장하고 실행 중인 상태는 저장하지 않는다.
/// </summary>
public abstract class BossPattern : ScriptableObject
{
    [Header("보스 패턴 기본 설정")]
    [SerializeField] bool _pauseRandomMovement = true; // 패턴 실행 중 공통 랜덤 이동을 정지할지 확인하는 값

    public bool PauseRandomMovement =>
        _pauseRandomMovement;

    /// <summary>
    /// 전달받은 Context를 사용하여 보스 패턴을 실행한다.
    /// </summary>
    public abstract IEnumerator Execute(
        BossPatternContext context);
}
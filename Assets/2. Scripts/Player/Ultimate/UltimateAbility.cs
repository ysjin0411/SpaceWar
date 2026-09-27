using UnityEngine;

// 캐릭터별 필살기가 제공해야 하는 공통 기능이다.
public abstract class UltimateAbility : MonoBehaviour
{
    // 공격과 남은 연출까지 진행 중인지 확인한다.
    public abstract bool IsRunning { get; }

    // 실제 사용 전에 프리팹과 수치 설정을 검사한다.
    public abstract void ValidateConfiguration();

    // 실행에 성공했을 때만 true를 반환한다.
    public abstract bool TryActivate(Camera worldCamera);

    // 스테이지 종료 시 남은 공격과 연출을 정리한다.
    public abstract void Cancel();
}
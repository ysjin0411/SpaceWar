// 퀘스트의 진행 상태.
// 저장되는 숫자가 바뀌지 않도록 값을 명시한다.
public enum QuestState
{
    Locked = 0,          // 시작 조건 미충족
    Available = 1,       // 시작 가능
    InProgress = 2,      // 수락 후 진행 중
    ReadyToTurnIn = 3,   // 목표 달성, 보상 미수령
    RewardClaimed = 4    // 보상 수령 완료
}
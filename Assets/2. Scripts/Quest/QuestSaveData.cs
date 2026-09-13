using System;

// 사용자별 퀘스트 진행 기록.
// 퀘스트 이름과 대사는 QuestData에서 가져온다.
[Serializable]
public class QuestSaveData
{
    // 어떤 퀘스트의 기록인지 구분한다.
    public string questId;

    // 수락 이후의 진행 상태를 저장한다.
    public QuestState state;
}
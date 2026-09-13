using System.Collections.Generic;
using UnityEngine;

// 게임에 존재하는 전체 퀘스트 설정을 모아둔다.
[CreateAssetMenu(
    fileName = "QuestDatabase",
    menuName = "GameSettings/Quest Database")]
public class QuestDatabase : ScriptableObject
{
    // 같은 퀘스트를 중복 등록하지 않는다.
    [SerializeField] private QuestData[] _quests = new QuestData[0];

    // 외부에서는 등록 목록을 읽기만 한다.
    public IReadOnlyList<QuestData> Quests => _quests;
}
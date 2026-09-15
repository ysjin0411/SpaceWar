using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;

// 퀘스트의 공통 정보와 상태별 표시 문구를 저장한다.
// 플레이어의 현재 진행 상태는 QuestService와 저장 데이터에서 관리한다.
[CreateAssetMenu(
    fileName = "QuestData",
    menuName = "GameSettings/Quest")]
public class QuestData : ScriptableObject
{
    // 저장 데이터에서 퀘스트를 구분하는 고유 ID.
    [SerializeField] private string _id;

    // 슬롯과 상세 패널에서 공통으로 사용하는 제목.
    [SerializeField] private string _questName;

    // 시작가능 상태에서 상세 패널에 표시할 설명.
    // 기존 _description에 입력한 내용을 이어받는다.
    [FormerlySerializedAs("_description")]
    [SerializeField, TextArea(3, 8)]
    private string _availableDescription;

    // 수락 후 진행중 상태에서 표시할 설명.
    [SerializeField, TextArea(3, 8)]
    private string _inProgressDescription;

    // 보상까지 수령하여 완료된 상태에서 표시할 설명.
    [SerializeField, TextArea(3, 8)]
    private string _completedDescription;

    // 수락과 저장이 성공한 뒤 NPC가 말할 한 줄 대사.
    [SerializeField] private string _acceptResponse;

    // 거절 버튼을 누른 뒤 NPC가 말할 한 줄 대사.
    [SerializeField] private string _declineResponse;

    // 퀘스트를 수락하는 데 필요한 User 레벨.
    [SerializeField, Min(1)]
    private int _requiredUserLevel = 1;

    // 수락하기 전에 보여주는 퀘스트 소개 대화.
    [SerializeField] private DialogueData _offerDialogue;

    // 진행중에 NPC와 대화할 때 사용하는 대화 데이터.
    [SerializeField] private DialogueData _inProgressDialogue;

    // 목표 달성 후 NPC와 대화할 때 사용하는 대화 데이터.
    [SerializeField] private DialogueData _completionDialogue;

    // 이 퀘스트를 달성하기 위해 충족해야 하는 목표 조건.
    [SerializeField]
    private QuestObjectiveData _objective = new QuestObjectiveData();

    // 이 퀘스트에서 지급할 보상 목록.
    // 비어 있으면 보상 없이 수령 완료 상태로 전환한다.
    [SerializeField]
    private List<QuestRewardData> _rewards = new List<QuestRewardData>();

    // 보상 지급 코드가 목록을 읽을 수 있도록 제공한다.
    public IReadOnlyList<QuestRewardData> Rewards => _rewards;

    // 외부에서 공통 정보와 대화 데이터를 읽는다.
    public string Id => _id;
    public string QuestName => _questName;
    public int RequiredUserLevel => _requiredUserLevel;
    public string AcceptResponse => _acceptResponse;
    public string DeclineResponse => _declineResponse;
    public DialogueData OfferDialogue => _offerDialogue;
    public DialogueData InProgressDialogue => _inProgressDialogue;
    public DialogueData CompletionDialogue => _completionDialogue;
    // 퀘스트 서비스가 검사할 목표 설정을 제공한다.
    public QuestObjectiveData Objective => _objective;

    // 플레이어의 현재 상태에 맞는 상세 설명을 반환한다.
    public string GetDescription(QuestState state)
    {
        switch (state)
        {
            case QuestState.Available:
                return _availableDescription;

            case QuestState.InProgress:
            case QuestState.ReadyToTurnIn:
                // 보상 미수령 상태는 기존처럼 진행중 목록에 포함한다.
                return _inProgressDescription;

            case QuestState.RewardClaimed:
                return _completedDescription;

            default:
                return string.Empty;
        }
    }
}
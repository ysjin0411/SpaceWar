using UnityEngine;

// 퀘스트의 고정 정보와 상태별 대화를 저장한다.
// 플레이어의 수락 여부와 진행도는 별도 저장 데이터에서 관리한다.
[CreateAssetMenu(
    fileName = "QuestData",
    menuName = "GameSettings/Quest")]
public class QuestData : ScriptableObject
{
    // 저장 데이터에서 퀘스트를 구분하는 고유 ID.
    [SerializeField] private string _id;

    // 대화창의 퀘스트 텍스트 버튼과 퀘스트창에 표시할 이름.
    [SerializeField] private string _questName;

    // 퀘스트창에서 표시할 설명.
    [SerializeField, TextArea(3, 8)]
    private string _description;

    // 퀘스트를 시작하는 데 필요한 User 레벨.
    // 솔져나 워리어의 캐릭터 레벨과는 관계없다.
    [SerializeField, Min(1)]
    private int _requiredUserLevel = 1;

    // 시작 가능한 퀘스트 이름을 눌렀을 때 재생할 설명.
    // 마지막 대사 출력이 끝나면 수락·거절을 표시한다.
    [SerializeField] private DialogueData _offerDialogue;

    // 퀘스트를 수락했지만 목표를 아직 달성하지 못했을 때의 대화.
    [SerializeField] private DialogueData _inProgressDialogue;

    // 목표를 달성했지만 보상을 아직 받지 않았을 때의 대화.
    [SerializeField] private DialogueData _completionDialogue;

    // 외부에서는 설정 내용을 읽기만 한다.
    public string Id => _id;
    public string QuestName => _questName;
    public string Description => _description;
    public int RequiredUserLevel => _requiredUserLevel;
    public DialogueData OfferDialogue => _offerDialogue;
    public DialogueData InProgressDialogue => _inProgressDialogue;
    public DialogueData CompletionDialogue => _completionDialogue;
}
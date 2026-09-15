using System;
using UnityEngine;

// 저장되는 숫자가 바뀌지 않도록 값을 명시한다.
// 새 보상 종류는 기존 값을 바꾸지 않고 뒤에 추가한다.
public enum QuestRewardType
{
    UserExp = 0, // 계정의 유저 경험치.
    Gold = 1    // 계정의 보유 골드.
}

// 퀘스트가 지급하는 보상 한 항목.
[Serializable]
public class QuestRewardData
{
    // 지급할 보상의 종류.
    [SerializeField] private QuestRewardType _type;

    // 지급할 경험치 또는 골드의 양.
    [SerializeField, Min(1)] private int _amount = 1;

    // 보상 목록에서 이 보상을 나타낼 아이콘.
    [SerializeField] private Sprite _icon;

    // UI에서 보상 아이콘을 읽는다.
    public Sprite Icon => _icon;

    // 아이콘이 없어도 어떤 보상인지 알 수 있도록 이름을 제공한다.
    // 나중에 아이템 보상을 추가하면 아이템 데이터의 이름을 반환한다.
    public string DisplayName
    {
        get
        {
            switch (_type)
            {
                case QuestRewardType.UserExp:
                    return "유저 경험치";

                case QuestRewardType.Gold:
                    return "골드";

                default:
                    return "알 수 없는 보상";
            }
        }
    }

    // 외부에서는 설정된 보상 정보를 읽는다.
    public QuestRewardType Type => _type;
    public int Amount => _amount;
}
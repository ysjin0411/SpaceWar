using UnityEngine;

// 아이템의 분류를 나타낸다.
public enum ItemType
{
    Material = 0,   // 일반 재료
    Quest = 1,      // 퀘스트 아이템
    Consumable = 2  // 소비 아이템
}

// 아이템 프리팹에 붙이는 기본 정보다.
// 획득과 저장은 이 컴포넌트에서 자동 실행하지 않는다.
public class Item : MonoBehaviour
{
    // 저장 데이터와 퀘스트에서 사용하는 고유 ID다.
    // 같은 아이템은 항상 같은 ID를 사용한다.
    [SerializeField] private string _id;

    // 이 아이템이 속하는 분류다.
    [SerializeField] private ItemType _type;

    // 인벤토리 화면에 표시할 이름이다.
    [SerializeField] private string _displayName;

    // 인벤토리 슬롯에 표시할 이미지다.
    [SerializeField] private Sprite _icon;

    // 외부에서는 아이템 설정을 읽기만 한다.
    public string Id => _id;
    public ItemType Type => _type;
    public string DisplayName => _displayName;
    public Sprite Icon => _icon;
}
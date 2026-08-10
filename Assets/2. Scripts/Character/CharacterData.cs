using UnityEngine;

/// <summary>
/// 캐릭터의 변하지 않는 원본 데이터를 보관한다.
///
/// 이 데이터는 ScriptableObject 에셋으로 만들어 사용한다.
/// Soldier와 Vampire는 각각 서로 다른 CharacterData 에셋을 가진다.
///
/// 주의:
/// 캐릭터의 현재 레벨, 경험치, 장착 무기처럼
/// 플레이하면서 변경되는 값은 이 클래스에 저장하지 않는다.
/// 변경되는 값은 나중에 JSON 세이브 데이터에 저장한다.
/// </summary>
[CreateAssetMenu(fileName = "CharacterData", menuName = "GameData/CharacterData")]
public class CharacterData : ScriptableObject
{
    [Header("기본 정보")]

    [Tooltip("세이브 데이터에서 캐릭터를 구분할 고유 ID")]
    [SerializeField] string _id;

    [Tooltip("UI에 표시할 캐릭터 이름")]
    [SerializeField] string _name;

    [Tooltip("캐릭터 선택 화면 등에 표시할 아이콘")]
    [SerializeField] Sprite _icon;

    [Tooltip("Stage에서 실제로 생성할 캐릭터 프리팹")]
    [SerializeField] GameObject _characterPrefab;

    [Header("기본 스탯")]

    [Tooltip("캐릭터의 기본 최대 체력")]
    [SerializeField] int _baseHp;

    [Tooltip("캐릭터의 기본 이동속도")]
    [SerializeField] float _baseMoveSpeed;

    [Header("사용 가능한 무기")]

    [Tooltip("이 캐릭터가 구매하고 장착할 수 있는 무기 목록")]
    [SerializeField] WeaponData[] _availableWeapons;

    public string Id => _id;
    public string CharacterName => _name;
    public Sprite Icon => _icon;
    public GameObject CharacterPrefab => _characterPrefab;
    public int BaseHp => _baseHp;
    public float BaseMoveSpeed => _baseMoveSpeed;
    public WeaponData[] AvailableWeapons => _availableWeapons;
}
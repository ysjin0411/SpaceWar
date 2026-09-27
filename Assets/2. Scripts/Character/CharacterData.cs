using UnityEngine;

/// <summary>
/// 캐릭터의 변하지 않는 원본 데이터를 보관한다.
///
/// 이 데이터는 ScriptableObject 에셋으로 만들어 사용한다.
/// Soldier와 Warrior는 각각 서로 다른 CharacterData 에셋을 가진다.
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
    [SerializeField] string _id;    // 캐릭터 고유 ID
    [SerializeField] string _name;  // UI에 표시할 캐릭터 이름
    [SerializeField] Sprite _icon;  // 캐릭터 아이콘 스프라이트
    [SerializeField] GameObject _characterPrefab;   // 캐릭터 프리팹

    [Header("기본 스탯")]
    [SerializeField] float _baseHp;   // 캐릭터 기본 최대 체력
    [SerializeField] float _baseMoveSpeed;  // 캐릭터 기본 이속

    [Header("사용 가능한 무기")]
    [SerializeField] WeaponData[] _availableWeapons;    // 해당 캐릭터의 무기 종류

    [Header("기본 장착 무기")]
    [SerializeField] WeaponData[] _initialEquippedWeapons = new WeaponData[3];  // 기본 베이스 무기 3종.

    // 장착 무기의 Sprite Library에서 미리보기에 사용할 Idle 카테고리.
    [SerializeField] private string _previewIdleCategory;

    // 이 캐릭터를 선택할 수 있는 최소 유저 레벨이다.
    [SerializeField, Min(1)] private int _requiredUserLevel = 1;

    // 잠금 안내 문구에서도 같은 조건을 사용한다.
    public int RequiredUserLevel => _requiredUserLevel;

    // 캐릭터 레벨이 아닌 계정의 UserLevel로 해금 여부를 판단한다.
    public bool IsUnlocked(int userLevel)
    {
        return userLevel >= _requiredUserLevel;
    }

    // 캐릭터 선택과 상점이 같은 Idle 카테고리를 사용한다.
    public string PreviewIdleCategory => _previewIdleCategory;

    public string Id => _id;
    public string CharacterName => _name;
    public Sprite Icon => _icon;
    public GameObject CharacterPrefab => _characterPrefab;
    public float BaseHp => _baseHp;
    public float BaseMoveSpeed => _baseMoveSpeed;
    public WeaponData[] AvailableWeapons => _availableWeapons;
    public WeaponData[] InitialEquippedWeapons => _initialEquippedWeapons;
}
using UnityEngine;

/// <summary>
/// 캐릭터의 변하지 않는 고정 데이터
/// </summary>
[CreateAssetMenu(fileName = "CharacterData", menuName = "GameData/CharacterData")]
public class CharacterData : ScriptableObject
{
    [SerializeField] string _id;                    // 캐릭터 넘버
    [SerializeField] string _name;                  // 캐릭터 이름
    [SerializeField] Sprite _icon;                  // 캐릭터 아이콘
    [SerializeField] GameObject _characterPrefab;   // 캐릭터 프리팹 게임오브젝트

    [Header("기본 스탯")]
    [SerializeField] int _baseHp;                   // 캐릭터 기본 체력
    [SerializeField] float _baseMoveSpeed;          // 캐릭터 기본 이동속도

    public string Id => _id;
    public string CharacterName => _name;
    public Sprite Icon => _icon;
    public GameObject CharacterPrefab => _characterPrefab;
    public int BaseHp => _baseHp;
    public float BaseMoveSpeed => _baseMoveSpeed;
}
using UnityEngine;

/// <summary>
/// 무기의 변하지 않는 원본 데이터
/// </summary>
[CreateAssetMenu(fileName = "WeaponData", menuName = "GameData/WeaponData")]
public class WeaponData : ScriptableObject
{
    [SerializeField] string _id;                      // 무기 넘버
    [SerializeField] string _name;                    // 무기 이름
    [SerializeField] WeaponType _type;                // 무기 종류
    [SerializeField] Sprite _icon;                    // 무기 아이콘
    [SerializeField] GameObject _projectilePrefab;    // 총알프리팹 게임오브젝트

    [Header("기본 스탯")]
    [SerializeField] int _baseDamage;                 // 기본 데미지
    [SerializeField] float _baseCooltime;             // 기본 쿨타임

    [Header("증강 설정")]
    [SerializeField] float _fireRateUpgradeValue;     // 발사속도 증강 수치
    [SerializeField] float _minimumCooltime = 0.05f;  // 최소 쿨타임

    public string Id => _id;
    public string WeaponName => _name;
    public WeaponType Type => _type;
    public Sprite Icon => _icon;
    public GameObject ProjectilePrefab => _projectilePrefab;
    public int BaseDamage => _baseDamage;
    public float BaseCooltime => _baseCooltime;
    public float FireRateUpgradeValue => _fireRateUpgradeValue;
    public float MinimumCooltime => _minimumCooltime;
}
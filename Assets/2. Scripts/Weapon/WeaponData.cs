using UnityEngine;
using UnityEngine.U2D.Animation;

/// <summary>
/// 무기의 변하지 않는 원본 데이터를 보관한다.
/// WeaponData에는 모든 유저에게 동일한 정보만 들어간다.
/// 예:
/// - 무기 가격, 요구 레벨, 기본 공격력, 투사체 프리팹
/// 보유 여부와 장착 슬롯처럼 유저마다 달라지는 정보는 WeaponData가 아니라 JSON 세이브 데이터에 저장한다.
/// </summary>
[CreateAssetMenu(fileName = "WeaponData",menuName = "GameData/WeaponData")]
public class WeaponData : ScriptableObject
{
    [Header("기본 정보")]

    [Tooltip("세이브 데이터에서 무기를 구분할 고유 ID")]
    [SerializeField] string _id;

    [Tooltip("UI에 표시할 무기 이름")]
    [SerializeField] string _name;

    [Tooltip("무기의 공격 방식")]
    [SerializeField] WeaponType _type;

    [Tooltip("이 무기가 실제 공격을 실행하는 방식")]
    [SerializeField] WeaponFireMode _fireMode = WeaponFireMode.Projectile;

    [Tooltip("EquipManage 및 전투 UI에 표시할 무기 아이콘")]
    [SerializeField] Sprite _icon;

    [Tooltip("이 무기로 공격할 때 생성할 투사체 프리팹")]
    [SerializeField] GameObject _projectilePrefab;
    // 이 무기를 들었을 때 캐릭터가 사용할 전용 Sprite Library다.
    // 전용 외형이 없는 무기는 비워두면 캐릭터의 기본 라이브러리를 사용한다.
    [SerializeField] SpriteLibraryAsset _playerSpriteLibrary;

    [Header("기본 전투 스탯")]

    [Tooltip("전투 시작 시 사용할 기본 공격력")]
    [Min(0)][SerializeField] int _baseDamage;

    [Tooltip("공격과 다음 공격 사이의 기본 대기시간")]
    [Min(0.01f)][SerializeField] float _baseCooltime;

    [Header("구매 조건")]

    [Tooltip("EquipManage에서 이 무기를 구매할 때 필요한 재화")]
    [Min(0)][SerializeField] int _price;

    [Tooltip("구매 가능 상태가 되기 위해 필요한 캐릭터 레벨")]
    [Min(1)][SerializeField] int _requiredCharacterLevel = 1;

    [Tooltip("새로운 세이브 데이터에서 처음부터 보유할 기본 무기인지")]
    [SerializeField] bool _isInitiallyOwned;

    [Header("전투 중 증강 설정")]

    [Tooltip("연사력 증강 한 번당 감소하는 쿨타임")]
    [Min(0f)][SerializeField] float _fireRateUpgradeValue;

    [Tooltip("연사력 증강으로 감소할 수 있는 최소 쿨타임")]
    [Min(0.01f)][SerializeField] float _minimumCooltime = 0.05f;

    [Header("UI 표시 정보")]
    [TextArea(2, 5)][SerializeField] string _info; // 장비 상세 화면과 툴팁에 표시할 무기 설명
                                                   // 2차 증강 대상 선택 화면에서 표시할 전용 설명
    [TextArea(2, 5)]
    [SerializeField] private string _upgradeSelectionInfo;


    // 무기 기본 정보
    public string Id => _id;
    public string WeaponName => _name;
    public WeaponType Type => _type;
    public WeaponFireMode FireMode => _fireMode;
    public Sprite Icon => _icon;
    public GameObject ProjectilePrefab => _projectilePrefab;

    // 무기 변경 시 Weapon이 적용할 캐릭터 전용 Sprite Library다.
    // null이면 캐릭터 프리팹에 연결된 기본 라이브러리를 사용한다.
    public SpriteLibraryAsset PlayerSpriteLibrary =>
        _playerSpriteLibrary;

    // 무기 기본 전투 스탯
    public int BaseDamage => _baseDamage;
    public float BaseCooltime => _baseCooltime;

    // EquipManage 구매 조건
    public int Price => _price;
    public int RequiredCharacterLevel => _requiredCharacterLevel;
    public bool IsInitiallyOwned => _isInitiallyOwned;

    // 전투 중 증강 수치
    public float FireRateUpgradeValue => _fireRateUpgradeValue;
    public float MinimumCooltime => _minimumCooltime;

    // UI
    public string Info => _info;
    // 2차 증강 대상 선택 슬롯에 표시할 전용 설명
    public string UpgradeSelectionInfo =>
        _upgradeSelectionInfo;
}
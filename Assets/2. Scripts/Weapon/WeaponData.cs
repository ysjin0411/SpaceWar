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
    [Header("상점 캐릭터 미리보기")]

    // 상점에서 이 무기를 장착한 캐릭터 이미지의 표시 배율.
    [SerializeField, Min(0.01f)]
    private float _equipmentPreviewScale = 1f;

    // 상점 미리보기 이미지의 기준 위치에서 이동할 거리.
    [SerializeField]
    private Vector2 _equipmentPreviewOffset = Vector2.zero;

    // 상점 화면에서 읽을 크기와 위치 보정값.
    public float EquipmentPreviewScale => _equipmentPreviewScale;
    public Vector2 EquipmentPreviewOffset => _equipmentPreviewOffset;

    [Header("기본 정보")]

    [Tooltip("세이브 데이터에서 무기를 구분할 고유 ID")]
    [SerializeField] string _id;

    [Tooltip("UI에 표시할 무기 이름")]
    [SerializeField] string _name;

    [Tooltip("무기의 공격 방식")]
    [SerializeField] WeaponType _type;

    [Tooltip("EquipManage 및 전투 UI에 표시할 무기 아이콘")]
    [SerializeField] Sprite _icon;

    [Tooltip("이 무기로 공격할 때 생성할 투사체 프리팹")]
    [SerializeField] GameObject _projectilePrefab;

    // 발사에 성공했을 때 총구에서 재생할 이펙트.
    // 총구 이펙트가 없는 무기는 비워둔다.
    [SerializeField] private SpriteOneShotEffect _muzzleEffectPrefab;

    // 연사 강화가 없는 상태에서 최대 출력까지 걸리는 시간이다.
    [SerializeField, Min(0.1f)]
    private float _lazerChargeDuration = 3f;

    // 이전 설정의 호환성을 위해 보존한다. 현재 연사 강화는 이 값을 사용하지 않는다.
    [SerializeField, Min(0.1f)]
    private float _lazerMinimumChargeDuration = 0.5f;

    // 최대 출력일 때 기본 틱 피해량에 적용하는 배율이다.
    [SerializeField, Min(1f)]
    private float _lazerMaxDamageMultiplier = 3f;

    // 이 무기를 들었을 때 캐릭터가 사용할 전용 Sprite Library다.
    // 전용 외형이 없는 무기는 비워두면 캐릭터의 기본 라이브러리를 사용한다.
    [SerializeField] SpriteLibraryAsset _playerSpriteLibrary;

    [Header("캐릭터 선택 미리보기")]

    // 이 무기를 첫 번째로 장착했을 때 미리보기 이미지에 적용할 배율.
    // 게임 안의 캐릭터 크기나 공격 범위에는 영향을 주지 않는다.
    [SerializeField, Min(0.01f)]
    float _characterSelectPreviewScale = 1f;

    // 미리보기 영역의 중심을 기준으로 이미지를 이동할 위치.
    // 이미지 안의 투명 여백 때문에 그림이 치우친 경우 조절한다.
    [SerializeField]
    Vector2 _characterSelectPreviewOffset = Vector2.zero;

    [Header("기본 전투 스탯")]

    [Tooltip("전투 시작 시 사용할 기본 공격력")]
    [Min(0)][SerializeField] float _baseDamage;

    [Tooltip("공격과 다음 공격 사이의 기본 대기시간")]
    [Min(0.01f)][SerializeField] float _baseCooltime;

    [Header("충전형 무기 설정")]

    // 0보다 크면 시간에 따라 보유량이 회복되는 충전형 무기로 사용한다.
    // Grenade의 최대 소지 개수를 설정한다.
    [Min(0)]
    [SerializeField] int _maximumChargeCount;

    // 수류탄 보유량이 1개 회복되는 데 필요한 시간이다.
    // 현재 무기로 선택하지 않아도 장착 슬롯에 있으면 이 시간이 흐른다.
    [Min(0.01f)]
    [SerializeField] float _chargeRecoveryTime = 5f;

    // Grenade가 연사 강화 한 번을 받을 때 감소하는 충전 시간이다.
    [Min(0f)]
    [SerializeField] float _chargeRecoveryUpgradeValue = 0.5f;

    // 연사 강화를 여러 번 받아도 이 시간보다 충전 시간이 짧아지지 않는다.
    [Min(0.01f)]
    [SerializeField] float _minimumChargeRecoveryTime = 1f;

    [Header("실드 설정")]

    // 실드의 최대 체력. 전투 시작 시 이 값까지 채워진다.
    [Min(1f)]
    [SerializeField] float _shieldMaxHealth = 100f;

    // 실드가 자연회복하는 초당 체력량.
    [Min(0f)]
    [SerializeField] float _shieldRecoveryPerSecond = 5f;

    // 공격력 증강에서 실드를 선택했을 때 즉시 회복하는 체력량.
    // 최대 체력을 늘리는 값이 아니며, 회복은 최대 체력에서 제한된다.
    [Min(0f)]
    [SerializeField] float _shieldRepairAmount = 100f;

    // 연사 증강에서 실드를 선택할 때 추가되는 초당 자연회복량.
    // 예: 기존 5에 1을 더하면 초당 6씩 회복한다.
    [Min(0f)]
    [SerializeField] float _shieldRecoveryUpgradeValue = 1f;

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

    // 자이언트 소드와 해머의 1타 기본 데미지.
    [SerializeField, Min(0f)]
    private float _meleeFirstDamage = 30f;

    // 자이언트 소드와 해머의 2타 기본 데미지.
    [SerializeField, Min(0f)]
    private float _meleeSecondDamage = 30f;

    // 자이언트 소드와 해머의 3타 기본 데미지.
    // 두 번 타격하는 동작이라면 각 타격에 이 값이 적용된다.
    [SerializeField, Min(0f)]
    private float _meleeThirdDamage = 30f;

    // 연격 인덱스에 해당하는 기본 데미지를 반환한다.
    // 인덱스 0은 1타, 1은 2타, 2는 3타다.
    public float GetMeleeDamage(int comboIndex)
    {
        switch (comboIndex)
        {
            case 0:
                return _meleeFirstDamage;
            case 1:
                return _meleeSecondDamage;
            case 2:
                return _meleeThirdDamage;
            default:
                throw new System.ArgumentOutOfRangeException(
                    nameof(comboIndex));
        }
    }


    // 무기 기본 정보
    public string Id => _id;
    public string WeaponName => _name;
    public WeaponType Type => _type;
    public Sprite Icon => _icon;
    public GameObject ProjectilePrefab => _projectilePrefab;

    // 발사 시 사용할 총구 이펙트를 제공한다.
    public SpriteOneShotEffect MuzzleEffectPrefab => _muzzleEffectPrefab;

    // 레이저의 기본 충전 시간을 제공한다.
    public float LazerChargeDuration => _lazerChargeDuration;

    // 레이저 충전 시간의 하한을 제공한다.
    public float LazerMinimumChargeDuration => _lazerMinimumChargeDuration;

    // 최대 출력의 피해 배율을 제공한다.
    public float LazerMaxDamageMultiplier => _lazerMaxDamageMultiplier;

    // 무기 변경 시 Weapon이 적용할 캐릭터 전용 Sprite Library다.
    // null이면 캐릭터 프리팹에 연결된 기본 라이브러리를 사용한다.
    public SpriteLibraryAsset PlayerSpriteLibrary =>
        _playerSpriteLibrary;

    // 캐릭터 선택 화면에서 사용할 표시 배율.
    public float CharacterSelectPreviewScale =>
        _characterSelectPreviewScale;

    // 캐릭터 선택 화면에서 사용할 표시 위치.
    public Vector2 CharacterSelectPreviewOffset =>
        _characterSelectPreviewOffset;

    // 무기 기본 전투 스탯
    // 선택한 무기의 기본 이동속도이며 증강값은 PlayerController에서 더한다.
    [SerializeField, Min(0f)] float _weaponMoveSpeed = 5f;
    // 레이저 연사 강화는 충전 시간이 아닌 초당 타격 빈도에 적용한다.
    [SerializeField, Min(0.1f)] float _lazerHitsPerSecond = 10f;
    [SerializeField, Min(0f)] float _lazerHitsPerSecondUpgrade = 1f;
    public float WeaponMoveSpeed => _weaponMoveSpeed;
    public float LazerHitsPerSecond => _lazerHitsPerSecond;
    public float LazerHitsPerSecondUpgrade => _lazerHitsPerSecondUpgrade;

    public float BaseDamage => _baseDamage;
    public float BaseCooltime => _baseCooltime;

    // 수류탄만 충전형 무기로 사용한다.
    // 다른 타입으로 바꾼 뒤 숨겨진 충전량 값이 남아 있어도
    // 해당 무기에 충전 동작이 적용되지 않게 한다.
    public bool UsesCharges =>
        _type == WeaponType.Grenade &&
        _maximumChargeCount > 0;

    // 전투 중 보유할 수 있는 최대 수류탄 개수다.
    public int MaximumChargeCount => _maximumChargeCount;

    // 수류탄이 1개 회복되는 데 필요한 시간이다.
    public float ChargeRecoveryTime => _chargeRecoveryTime;

    // Grenade가 연사 강화 한 번을 받을 때 감소하는 충전 시간이다.
    public float ChargeRecoveryUpgradeValue =>
        _chargeRecoveryUpgradeValue;

    // 강화된 수류탄 충전 시간의 최솟값이다.
    public float MinimumChargeRecoveryTime =>
        _minimumChargeRecoveryTime;

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

    // 실드 전용 동작을 사용하는 무기인지 확인한다.
    public bool IsShield => _type == WeaponType.Shield;

    // 실드의 최대 체력.
    public float ShieldMaxHealth => _shieldMaxHealth;

    // 증강을 받기 전 기본 초당 자연회복량.
    public float ShieldRecoveryPerSecond => _shieldRecoveryPerSecond;

    // 공격력 증강을 실드에 적용할 때의 즉시 회복량.
    public float ShieldRepairAmount => _shieldRepairAmount;

    // 연사 증강 한 번당 증가하는 초당 자연회복량.
    public float ShieldRecoveryUpgradeValue => _shieldRecoveryUpgradeValue;
}
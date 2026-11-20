using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D.Animation;

public enum WeaponType
{
    Bullet,
    Lazer,
    Missile,
    Grenade,
    Sniper,
    GiantSword,
    Shield,
    Slash,
    Hammer,
}

/// <summary>
/// 현재 캐릭터의 장착 무기, 무기 변경, 발사를 관리한다.
/// WeaponUI와 WeaponSlotPanel은 Stage 씬에 있으므로 StageManager가 InitializeUI()를 통해 전달한다.
/// </summary>
public class Weapon : MonoBehaviour
{
    [Header("컴포넌트")]
    [SerializeField] Animator _animator;
    // 캐릭터 Renderer에 붙어 있는 SpriteLibrary다.
    // 무기가 바뀔 때 이 컴포넌트의 Sprite Library Asset을 교체한다.
    [SerializeField] SpriteLibrary _spriteLibrary;


    [Header("장착 무기 런타임 데이터")]
    [SerializeField] List<WeaponRuntimeState> _equippedWeapons = new();

    [Header("현재 무기 슬롯")]
    // -1이면 현재 사용할 수 있는 무기가 없는 상태
    [SerializeField] int _currentSlotIndex = -1;

    [Header("캐릭터 프리팹 내부 참조")]
    // 투사체가 생성될 캐릭터 프리팹 내부 위치
    [SerializeField] Transform _firePoint;

    // Soldier가 Grenade를 선택했을 때 표시할 포물선 조준선이다.
    // Grenade가 없는 캐릭터에서는 비워둘 수 있다.
    [SerializeField] GrenadeAimGuide _grenadeAimGuide;

    // 워리어 Renderer 아래의 자이언트 소드 공격 판정.
    // 솔져에서는 비워둔다.
    [SerializeField] SpriteMeleeAttack _giantSword;

    // 해머 공격 판정 오브젝트.
    [SerializeField] SpriteMeleeAttack _hammer;

    // 워리어 Renderer 아래 ShieldGuard에 붙인 실드 컴포넌트.
    // 솔져에서는 비워둔다.
    [SerializeField] Shield _shield;

    // 워리어가 Slash 공격에 사용하는 Attack4 클립.
    // 원본 재생 시간을 가져와 현재 공격 간격에 맞게 속도를 계산한다.
    [SerializeField] AnimationClip _slashAttackClip;

    // Slash의 한 번 휘두르기가 진행 중인지 기록한다.
    bool _isSlashAttacking;

    // Attack 상태의 속도 파라미터.
    static readonly int AttackSpeedHash =
        Animator.StringToHash("AttackSpeed");

    // 워리어 기본 레이어의 공격·대기 상태.
    static readonly int SlashAttackStateHash =
        Animator.StringToHash("Base Layer.SlashAttack");

    static readonly int SlashIdleStateHash =
        Animator.StringToHash("Base Layer.Idle");

    // StageManager가 런타임에 전달하는 Stage UI
    WeaponUI _weaponUI;
    GameObject _weaponSlotPanel;

    // 무기 선택 패널이 현재 열려 있는지 저장한다.
    bool _isWeaponSlotPanelOpen;

    // 마지막 발사 이후 흐른 시간
    float _timer;

    // 유지 중인 레이저 하나만 관리한다.
    Lazer _activeLazer;

    // 같은 플레이어 오브젝트의 조작 가능 상태를 확인한다.
    private PlayerController _playerController;

    // 조작이 허용되고 살아 있을 때만 무기를 사용할 수 있다.
    private bool CanUseWeapon =>
        _playerController.CanControl && !_playerController.IsDead;

    // 캐릭터 프리팹에 처음부터 연결되어 있던 기본 Sprite Library다.
    // 전용 라이브러리가 없는 무기로 돌아왔을 때 복구하기 위해 저장한다.
    SpriteLibraryAsset _defaultSpriteLibraryAsset;

    // 문자열을 매 프레임 검색하지 않도록 파라미터 ID를 미리 저장한다.
    static readonly int AttackHash = Animator.StringToHash("Attack");

    // 현재 런타임에 구성된 장착 무기 슬롯 개수
    public int EquippedSlotCount => _equippedWeapons.Count;

    /// <summary>
    /// 현재 선택한 슬롯의 무기 상태를 반환한다. 장착 무기가 없다면 null을 반환한다.
    /// </summary>
    public WeaponRuntimeState CurrentWeapon
    {
        get
        {
            if (_currentSlotIndex < 0 || _currentSlotIndex >= _equippedWeapons.Count)
            {
                return null;
            }

            return _equippedWeapons[_currentSlotIndex];
        }
    }

    /// <summary>
    /// 컴포넌트 참조와 초기 실행 상태를 준비한다.
    /// </summary>
    private void Awake()
    {
        // 현재 프로젝트는 플레이어와 Weapon이 같은 오브젝트에 붙어 있다.
        _playerController = GetComponent<PlayerController>();

        if (_playerController == null)
        {
            throw new MissingComponentException(
                $"{name}: Weapon과 같은 오브젝트에 PlayerController가 필요합니다.");
        }

        // Animator 연결 누락을 개발 단계에서 바로 확인한다.
        if (_animator == null)
        {
            throw new MissingComponentException(
                $"{name}의 Weapon에 Animator가 연결되지 않았습니다.");
        }

        // 무기별 캐릭터 외형을 교체하려면 SpriteLibrary가 필요하다.
        if (_spriteLibrary == null)
        {
            throw new MissingComponentException(
                $"{name}의 Weapon에 SpriteLibrary가 연결되지 않았습니다.");
        }

        // Soldier 프리팹의 경우 현재 연결된 Soldier.spriteLib가 저장된다.
        // 나중에 Bullet, Lazer처럼 전용 라이브러리가 없는 무기로 바꾸면
        // 이 기본 라이브러리로 다시 복구한다.
        _defaultSpriteLibraryAsset =
            _spriteLibrary.spriteLibraryAsset;

        // FirePoint는 캐릭터 프리팹 내부 참조이므로 프리팹에 반드시 연결되어 있어야 한다.
        if (_firePoint == null)
        {
            throw new InvalidOperationException($"{name}의 Weapon에 FirePoint가 연결되지 않았습니다.");
        }

    }

    /// <summary>
    /// StageManager가 Stage의 무기 UI를 전달한다.
    /// </summary>
    /// <param name="weaponUI">현재 무기 아이콘을 표시할 WeaponUI</param>
    /// <param name="weaponSlotPanel">Tab 키로 열고 닫을 무기 슬롯 패널</param>
    public void InitializeUI(WeaponUI weaponUI, GameObject weaponSlotPanel)
    {
        if (weaponUI == null)
        {
            throw new ArgumentNullException(nameof(weaponUI));
        }

        if (weaponSlotPanel == null)
        {
            throw new ArgumentNullException(nameof(weaponSlotPanel));
        }

        _weaponUI = weaponUI;
        _weaponSlotPanel = weaponSlotPanel;

        // Stage 시작 시 무기 선택 패널을 닫는다.
        _isWeaponSlotPanelOpen = false;
        _weaponSlotPanel.SetActive(false);

        // 현재 장착 무기 상태를 UI에 반영한다.
        UpdateWeaponUI();
    }

    /// <summary>
    /// 슬롯 순서대로 전달받은 WeaponData를
    /// 전투용 WeaponRuntimeState로 변환한다.
    /// </summary>
    public void InitializeWeapons(WeaponData[] equippedWeaponDatas)
    {
        if (equippedWeaponDatas == null)
        {
            throw new ArgumentNullException(nameof(equippedWeaponDatas), "장착 무기 데이터 배열이 전달되지 않았습니다.");
        }

        // 이전 전투에서 사용하던 런타임 무기를 제거한다.
        _equippedWeapons.Clear();

        _currentSlotIndex = -1;

        for (int slotIndex = 0; slotIndex < equippedWeaponDatas.Length; slotIndex++)
        {
            WeaponData weaponData = equippedWeaponDatas[slotIndex];

            // 빈 슬롯도 null로 추가하여 슬롯 위치를 보존한다.
            if (weaponData == null)
            {
                _equippedWeapons.Add(null);
                continue;
            }

            WeaponRuntimeState runtimeState = new WeaponRuntimeState(weaponData);

            _equippedWeapons.Add(runtimeState);

            // 처음 발견한 장착 무기를 현재 무기로 선택한다.
            if (_currentSlotIndex == -1)
            {
                _currentSlotIndex = slotIndex;
            }
        }

        // 장착 무기 초기화 직후에는 첫 공격을 즉시 사용할 수 있게 한다.
        _timer = CurrentWeapon != null
            ? CurrentWeapon.CurrentCooltime
            : 0f;

        // 스테이지 시작 시 첫 번째로 선택된 무기의 외형을 적용한다.
        // Grenade 또는 Sniper가 첫 슬롯이라면 시작부터 해당 외형이 나온다.
        ApplyCurrentWeaponSpriteLibrary();

        // StageManager가 UI를 전달한 상태라면
        // JSON 기반 장착 무기 3개의 아이콘을 Tab 패널에 표시한다.
        if (_weaponUI != null)
        {
            _weaponUI.UpdateEquippedWeapons(
                equippedWeaponDatas);
        }

        UpdateWeaponUI();
    }

    /// <summary>
    /// 매 프레임 입력과 경과 시간에 따른 상태를 갱신한다.
    /// </summary>
    private void Update()
    {
        // 일시정지 중에는 입력과 쿨타임을 진행하지 않는다.
        // 진행 중인 검 공격의 Animator 파라미터는 유지한다.
        if (Time.timeScale <= 0f)
        {
            return;
        }

        // 현재 선택 여부와 관계없이 장착 슬롯에 들어 있는
        // 모든 충전형 무기의 회복 시간을 진행한다.
        UpdateEquippedWeaponCharges(
            Time.deltaTime);

        // 현재 무기 아이콘과 Tab 장착 아이콘에
        // Grenade 충전 진행도와 보유량을 표시한다.
        UpdateWeaponChargeUI();

        _timer += Time.deltaTime;

        // 쿨타임과 충전 회복은 유지하되,
        // 이동 연출이나 사망 중에는 공격·무기 변경 입력을 받지 않는다.
        if (!CanUseWeapon)
        {
            // 제어를 잃으면 저장된 Z 유지 상태까지 지워 자동 연격을 중단한다.
            CancelCurrentAttack();
            return;
        }

        // 현재 선택된 무기를 한 번만 가져와
        // 이번 프레임의 입력 처리에 사용한다.
        WeaponRuntimeState currentWeapon =
            CurrentWeapon;

        // 실제 Z 유지 입력을 읽는다.
        bool isHeld = Input.GetKey(KeyCode.Z);

        // 근접공격은 키 해제도 전달해야 다음 공격을 1타부터 시작한다.
        // 누르고 있는 동안의 실제 공격 요청은 기존 Fire() 흐름에서 처리한다.
        if (currentWeapon != null &&
            (currentWeapon.Data.Type == WeaponType.GiantSword ||
             currentWeapon.Data.Type == WeaponType.Hammer))
        {
            SpriteMeleeAttack melee = currentWeapon.Data.Type == WeaponType.GiantSword
                ? _giantSword
                : _hammer;

            if (melee == null || !melee.isActiveAndEnabled)
            {
                throw new InvalidOperationException(
                    $"{name}: 현재 무기의 활성화된 SpriteMeleeAttack을 연결하세요.");
            }

            melee.SetAttackHeld(isHeld);
        }


        // 실드는 일반 발사와 별도로 자세를 제어한다.
        bool isShield =
            currentWeapon != null &&
            currentWeapon.Data.Type == WeaponType.Shield;

        // 검과 해머도 각 공격 컴포넌트가 애니메이션을 제어한다.
        bool isSpriteMelee =
            currentWeapon != null &&
            (currentWeapon.Data.Type == WeaponType.GiantSword ||
             currentWeapon.Data.Type == WeaponType.Hammer);

        // Slash는 실제 발사 시점에 애니메이션을 시작한다.
        bool isSlash =
            currentWeapon != null &&
            currentWeapon.Data.Type == WeaponType.Slash;


        bool isAttackHeld =
            isHeld &&
            currentWeapon != null &&
            currentWeapon.HasAvailableCharge;

        // 입력 해제 또는 다른 무기로 변경되면 출력을 초기화한다.
        if (!isAttackHeld || currentWeapon.Data.Type != WeaponType.Lazer)
            StopLazer();

        if (isShield)
        {
            // 연결 누락은 바로 확인할 수 있도록 오류로 알린다.
            if (_shield == null || !_shield.isActiveAndEnabled)
            {
                throw new InvalidOperationException(
                    $"{name}: 활성화된 ShieldGuard의 Shield를 연결해야 합니다.");
            }

            // 선택된 실드의 체력 데이터와 입력을 전달한다.
            _shield.Tick(
                currentWeapon,
                isHeld,
                Time.deltaTime);
        }
        else
        {
            // 검·해머·Slash는 각 공격 시작과 종료가 애니메이션을 제어한다.
            if (!isSpriteMelee && !isSlash)
            {
                _animator.SetBool(AttackHash, isAttackHeld);
            }

            if (currentWeapon != null)
            {
                if (currentWeapon.Data.Type == WeaponType.Grenade)
                {
                    // 수류탄은 누른 순간 한 번만 던진다.
                    if (Input.GetKeyDown(KeyCode.Z) &&
                        currentWeapon.HasAvailableCharge)
                    {
                        Fire();
                    }
                }
                else if (isAttackHeld)
                {
                    // 나머지 공격 무기는 기존 발사 처리를 사용한다.
                    Fire();
                }
            }
        }

        // Tab 입력 시 무기 선택 패널을 연다.
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleWeaponSlotPanel();
        }

        // 패널이 닫혀 있으면 슬롯 선택 입력을 처리하지 않는다.
        if (!_isWeaponSlotPanelOpen)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SelectWeaponSlot(0);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SelectWeaponSlot(1);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            SelectWeaponSlot(2);
        }
    }


    /// <summary>
    /// 현재 장착된 모든 무기의 충전 시간을 갱신한다.
    /// 현재 선택한 슬롯뿐 아니라 장착된 세 슬롯 전체를 검사한다.
    /// </summary>
    private void UpdateEquippedWeaponCharges(
        float deltaTime)
    {
        for (int slotIndex = 0;
             slotIndex < _equippedWeapons.Count;
             slotIndex++)
        {
            WeaponRuntimeState weapon =
                _equippedWeapons[slotIndex];

            // 비어 있는 장착 슬롯은 건너뛴다.
            if (weapon == null)
            {
                continue;
            }

            // 일반 무기에서는 내부 검사를 통해 바로 반환하고,
            // Grenade 같은 충전형 무기만 시간이 진행된다.
            weapon.UpdateChargeRecovery(
                deltaTime);

            // 실드는 자연회복을 진행한다.
            // 현재 선택한 무기가 아니어도 장착 슬롯에 있으면 회복한다.
            float previousShield = weapon.ShieldHealthRatio;
            weapon.UpdateShieldRecovery(deltaTime);
            // 방패 내구도가 완전히 회복되는 순간만 알린다.
            if (weapon.Data.Type == WeaponType.Shield && previousShield < 1f && weapon.ShieldHealthRatio >= 1f)
                GameAudio.Play(GameSound.ShieldRecover);
        }
    }

    /// <summary>
    /// 현재 무기와 장착된 세 무기의 충전 상태를
    /// Stage WeaponUI에 전달한다.
    /// </summary>
    private void UpdateWeaponChargeUI()
    {
        // StageManager가 아직 UI를 연결하지 않았다면 건너뛴다.
        if (_weaponUI == null)
        {
            return;
        }

        _weaponUI.UpdateChargeDisplays(
            CurrentWeapon,
            _equippedWeapons);
    }

    /// <summary>
    /// Stage의 무기 슬롯 패널을 열거나 닫는다.
    /// </summary>
    private void ToggleWeaponSlotPanel()
    {
        // StageManager가 아직 UI를 전달하지 않았다면 패널 입력을 처리하지 않는다.
        if (_weaponSlotPanel == null)
        {
            return;
        }

        _isWeaponSlotPanelOpen = !_isWeaponSlotPanelOpen;

        _weaponSlotPanel.SetActive(_isWeaponSlotPanelOpen);
    }

    /// <summary>
    /// 지정한 장착 슬롯의 무기를 현재 무기로 선택한다.
    /// </summary>
    public void SelectWeaponSlot(int slotIndex)
    {
        WeaponRuntimeState weapon = GetWeaponAtSlot(slotIndex);

        // 빈 슬롯이거나 잘못된 슬롯이면 변경하지 않는다.
        if (weapon == null)
        {
            return;
        }

        if (_currentSlotIndex != slotIndex) GameAudio.Play(GameSound.WeaponSwitch); // 실제 교체만 알린다.
        _currentSlotIndex = slotIndex;

        // 새로 선택된 무기에 맞는 Idle/Attack 스프라이트 세트로 바꾼다.
        ApplyCurrentWeaponSpriteLibrary();

        UpdateWeaponUI();

        // 무기 선택을 완료했으므로 패널을 닫는다.
        _isWeaponSlotPanelOpen = false;

        if (_weaponSlotPanel != null)
        {
            _weaponSlotPanel.SetActive(false);
        }
    }

    /// <summary>
    /// 현재 무기에 맞는 캐릭터 Sprite Library를 적용하고
    /// Animator를 Idle 상태로 초기화한다.
    /// </summary>
    private void ApplyCurrentWeaponSpriteLibrary()
    {
        StopLazer();
        // 다른 무기 외형으로 변경하기 전에 Slash 공격을 종료한다.
        CancelSlashAnimation();

        // 무기 외형이 바뀌기 전에 진행 중인 검 공격을 종료한다.
        if (_giantSword != null)
        {
            _giantSword.CancelAttack();
        }

        // 해머 공격도 종료해서 이전 무기의 판정이 남지 않게 한다.
        if (_hammer != null)
        {
            _hammer.CancelAttack();
        }

        // 외형이 바뀌기 전에 실드 자세와 AttackSpeed를 복구한다.
        if (_shield != null)
        {
            _shield.CancelGuard();
        }

        WeaponRuntimeState currentWeapon = CurrentWeapon;

        // 현재 선택된 무기가 없다면 외형을 적용할 수 없다.
        if (currentWeapon == null)
        {
            return;
        }

        // 현재 무기에 전용 라이브러리가 연결되어 있다면 가져온다.
        SpriteLibraryAsset nextLibrary =
            currentWeapon.Data.PlayerSpriteLibrary;

        // 전용 라이브러리가 없다면
        // Soldier 프리팹에 처음 연결되어 있던 기본 라이브러리를 사용한다.
        if (nextLibrary == null)
        {
            nextLibrary = _defaultSpriteLibraryAsset;
        }

        // 전용 라이브러리와 기본 라이브러리가 모두 없다면
        // 캐릭터 외형을 표시할 수 없다.
        if (nextLibrary == null)
        {
            throw new InvalidOperationException(
                $"{name}에 적용할 Sprite Library가 없습니다.");
        }

        // 라이브러리가 같더라도 무기를 변경할 때는
        // 공격 파라미터와 Idle 상태를 초기화해야 한다.
        _animator.SetBool(AttackHash, false);

        // 현재 라이브러리와 새 라이브러리가 다를 때만 교체한다.
        // 같은 에셋을 다시 넣는 불필요한 갱신만 방지한다.
        if (_spriteLibrary.spriteLibraryAsset != nextLibrary)
        {
            _spriteLibrary.spriteLibraryAsset = nextLibrary;
        }

        // 라이브러리가 같든 다르든 항상 Idle 첫 프레임부터 재생한다.
        _animator.Play("Idle", 0, 0f);

        // 현재 무기가 Grenade인지 확인하여
        // 포물선 조준선을 표시하거나 숨긴다.
        UpdateGrenadeAimGuide(
            currentWeapon);
    }

    /// <summary>
    /// 현재 무기가 Grenade라면 포물선 조준선을 표시하고,
    /// 다른 무기라면 기존 조준선을 숨긴다.
    /// </summary>
    private void UpdateGrenadeAimGuide(
        WeaponRuntimeState currentWeapon)
    {
        if (currentWeapon.Data.Type != WeaponType.Grenade)
        {
            // Warrior처럼 GrenadeAimGuide가 없는 캐릭터도 허용한다.
            if (_grenadeAimGuide != null)
            {
                _grenadeAimGuide.Hide();
            }

            return;
        }

        if (_grenadeAimGuide == null)
        {
            throw new InvalidOperationException(
                $"{name}의 Weapon에 Grenade Aim Guide가 연결되지 않았습니다.");
        }

        GameObject projectilePrefab =
            currentWeapon.Data.ProjectilePrefab;

        if (projectilePrefab == null ||
            !projectilePrefab.TryGetComponent(
                out Grenade grenadePrefab))
        {
            throw new MissingComponentException(
                $"무기 '{currentWeapon.Data.WeaponName}'의 프리팹에 Grenade 컴포넌트가 없습니다.");
        }

        // Grenade 프리팹의 실제 속도, 중력, 신관 시간으로
        // 조준선 계산을 시작한다.
        _grenadeAimGuide.Show(
            grenadePrefab);
    }

    /// <summary>
    /// 현재 선택한 무기의 FireMode에 맞는 공격을 실행한다.
    /// Projectile은 이동형 투사체를 생성하고,
    /// PulseBeam은 발사 지점에서 펄스 레이저를 생성한다.
    /// </summary>
    public void Fire()
    {
        // 다른 코드에서 Fire를 직접 호출해도 잠금 중에는 발사하지 않는다.
        if (!isActiveAndEnabled || !CanUseWeapon)
        {
            return;
        }

        // 현재 선택된 슬롯의 런타임 무기를 가져온다.
        WeaponRuntimeState weapon = CurrentWeapon;

        // 실드는 Tick으로 제어하므로 공격 프리팹을 생성하지 않는다.
        if (weapon == null ||
            weapon.Data.Type == WeaponType.Shield)
        {
            return;
        }

        // Slash는 시작한 휘두르기가 끝나기 전에 다음 발을 생성하지 않는다.
        // 지속 레이저는 일반 발사 쿨타임과 단발 총구 이펙트를 사용하지 않는다.
        if (weapon.Data.Type == WeaponType.Lazer)
        {
            StartLazer(weapon);
            return;
        }

        if (weapon.Data.Type == WeaponType.Slash)
        {
            if (_isSlashAttacking)
            {
                return;
            }

            // 프리팹을 생성하기 전에 애니메이션 연결을 검사한다.
            if (_slashAttackClip == null || _slashAttackClip.length <= 0f)
            {
                throw new InvalidOperationException(
                    $"{name}: Slash Attack Clip에 Attack4를 연결해야 합니다.");
            }
        }

        // 충전형 무기는 일반 공격 쿨타임 대신
        // 현재 사용할 수 있는 충전량이 있는지 검사한다.
        // 근접공격은 자체 대기시간을 사용하므로 공용 발사 타이머보다 먼저 처리한다.
        if (weapon.Data.Type == WeaponType.GiantSword ||
            weapon.Data.Type == WeaponType.Hammer)
        {
            // 현재 무기에 맞는 공격 판정 오브젝트를 선택한다.
            SpriteMeleeAttack meleeAttack =
                weapon.Data.Type == WeaponType.GiantSword
                    ? _giantSword
                    : _hammer;

            // 연결 누락이나 오브젝트 비활성화를 확인한다.
            if (meleeAttack == null ||
                !meleeAttack.isActiveAndEnabled)
            {
                throw new InvalidOperationException(
                    $"{name}: {weapon.Data.WeaponName}의 " +
                    "활성화된 근접 공격 오브젝트를 연결해야 합니다.");
            }

            // 강화로 줄어든 쿨타임을 대기시간이 아닌 휘두르기 속도 배율로 사용한다.
            float baseCooltime = weapon.Data.BaseCooltime;
            float currentCooltime = weapon.CurrentCooltime;
            if (baseCooltime <= 0f || currentCooltime <= 0f)
                throw new InvalidOperationException("근접 무기의 쿨타임은 0보다 커야 합니다.");

            float attackSpeedMultiplier = baseCooltime / currentCooltime;

            // 현재 무기의 Config, 강화가 반영된 공격력, 공격속도를 전달한다.
            meleeAttack.BeginAttack(
                weapon.Data,
                weapon.CurrentDamage,
                attackSpeedMultiplier);

            return;
        }

        if (weapon.Data.UsesCharges)
        {
            if (!weapon.HasAvailableCharge)
            {
                return;
            }
        }
        else
        {
            // Bullet, Missile, Sniper 등의 일반 무기는
            // 기존 공격 쿨타임이 지나야 발사할 수 있다.
            if (_timer < weapon.CurrentCooltime)
            {
                return;
            }
        }

        // 스프라이트 기반 근접 공격은 캐릭터의 자식 판정을 사용한다.


        // 현재 무기에 공격 프리팹이 연결되어 있는지 확인한다.
        // 이 아래 기존 프리팹 생성 코드는 그대로 유지한다.

        // 현재 무기에 공격 프리팹이 연결되어 있는지 확인한다.
        if (weapon.Data.ProjectilePrefab == null)
        {
            throw new InvalidOperationException(
                $"무기 '{weapon.Data.WeaponName}'에 공격 프리팹이 연결되지 않았습니다.");
        }

        // 현재 무기의 프리팹에 대응하는 풀에서 투사체를 꺼낸다.
        GameObject attackObject = ProjectilePoolManager.Instance.Spawn(
            weapon.Data.ProjectilePrefab,
            _firePoint.position,
            _firePoint.rotation);

        // 이번 발사의 공격력과 방향을 적용한다.
        InitializeProjectileAttack(attackObject, weapon);

        // 프리팹 생성과 초기화가 정상적으로 끝난 다음
        // 충전형 무기의 보유량을 한 개 소비한다.
        if (weapon.Data.UsesCharges &&
            !weapon.TryConsumeCharge())
        {
            // 발사를 완료하지 못한 투사체도 파괴하지 않고 반납한다.
            attackObject.GetComponent<PoolObject>().ReturnToPool();
            return;
        }

        // 탄환 생성과 충전 횟수 차감이 성공한 뒤 총구 섬광을 재생한다.
        PlayProjectileFireSound(weapon.Data.Type); // 발사와 탄약 소비가 승인된 뒤 한 번 재생.
        SpriteOneShotEffect muzzlePrefab = weapon.Data.MuzzleEffectPrefab;

        if (muzzlePrefab != null)
        {
            // 다음 발사 전에 꺼지도록 발사 간격의 60%만 사용한다.
            // 느린 연사에서도 섬광이 오래 남지 않도록 최대 0.2초로 제한한다.
            float muzzleDuration = Mathf.Min(
                0.2f,
                weapon.CurrentCooltime * 0.6f);

            // 탄환이 생성된 총구 위치와 방향에 이펙트를 생성한다.
            SpriteOneShotEffect muzzleEffect = Instantiate(
                muzzlePrefab,
                _firePoint.position,
                _firePoint.rotation);

            // 현재 연사 간격에 맞춰 애니메이션과 제거 시점을 조정한다.
            muzzleEffect.SetPlaybackDuration(muzzleDuration);

            // 재생 중에는 총구를 따라가되 현재 월드 크기를 유지한다.
            // 재생 시간 설정 후 부모를 연결해야 크기 초기화의 영향을 피한다.
            muzzleEffect.transform.SetParent(_firePoint, true);
        }

        // 생성과 초기화가 성공한 Slash 한 발에 휘두르기 한 번을 대응시킨다.
        if (weapon.Data.Type == WeaponType.Slash)
        {
            BeginSlashAnimation(weapon.CurrentCooltime);
        }

        // 공격이 정상적으로 실행됐으므로 쿨타임을 처음부터 측정한다.
        _timer = 0f;
    }

    /// <summary>
    /// 생성한 공격 오브젝트를 이동형 투사체로 초기화한다.
    /// Bullet과 Missile 등이 이 방식을 사용한다.
    /// </summary>
    private void InitializeProjectileAttack(
        GameObject attackObject,
        WeaponRuntimeState weapon)
    {
        // 생성한 프리팹에서 Projectile 부모 컴포넌트를 찾는다.
        if (!attackObject.TryGetComponent(out Projectile projectile))
        {
            // 발사를 완료하지 못한 투사체도 파괴하지 않고 반납한다.
            attackObject.GetComponent<PoolObject>().ReturnToPool();

            throw new MissingComponentException(
                $"무기 '{weapon.Data.WeaponName}'은 Projectile 방식이지만 " +
                "공격 프리팹에 Projectile을 상속한 컴포넌트가 없습니다.");
        }

        // 기본 투사체는 기존처럼 FirePoint의 오른쪽으로 발사한다.
        Vector2 fireDirection =
            _firePoint.right;

        if (weapon.Data.Type == WeaponType.Grenade)
        {
            if (_grenadeAimGuide == null)
            {
                // 발사를 완료하지 못한 투사체도 파괴하지 않고 반납한다.
                attackObject.GetComponent<PoolObject>().ReturnToPool();

                throw new InvalidOperationException(
                    $"{name}의 Weapon에 Grenade Aim Guide가 연결되지 않았습니다.");
            }

            // Z키를 누른 바로 그 순간 조준선이 가리키던
            // 포물선 최초 방향을 Grenade에 전달한다.
            fireDirection =
                _grenadeAimGuide.CurrentDirection;
        }

        projectile.Initialize(
            weapon.CurrentDamage,
            fireDirection);
    }

    // 생성된 레이저에 현재 공격력과 발사 위치를 전달한다.
    /// <summary>
    /// 생성된 레이저에 현재 공격력과 발사 위치를 전달한다.
    /// </summary>
    private void StartLazer(WeaponRuntimeState weapon)
    {
        if (_activeLazer != null) return;
        if (weapon.Data.ProjectilePrefab == null ||
            !weapon.Data.ProjectilePrefab.TryGetComponent(out Lazer prefab))
            throw new InvalidOperationException("Lazer prefab is missing.");
        _activeLazer = Instantiate(prefab, _firePoint.position, _firePoint.rotation);
        _activeLazer.Initialize(weapon, _firePoint);
    }

    // 입력 해제, 무기 변경, 사망 및 조작 불가 상태에서 공통으로 호출한다.
    /// <summary>
    /// 입력 해제, 무기 변경, 사망 및 조작 불가 상태에서 공통으로 호출한다.
    /// </summary>
    private void StopLazer()
    {
        if (_activeLazer == null) return;
        _activeLazer.StopBeam();
        _activeLazer = null;
    }

    // 선택한 슬롯에 공격력 증강 효과를 전달한다.
    /// <summary>
    /// 지정한 슬롯 무기에 기본 피해 기준 비율 증강을 적용합니다.
    /// </summary>
    public void AddDamageRatio(int slotIndex, float value)
    {
        WeaponRuntimeState weapon = GetWeaponAtSlot(slotIndex);

        if (weapon == null)
        {
            return;
        }

        // 일반 무기의 공격력 증가와 실드의 체력 회복은
        // WeaponRuntimeState에서 무기 종류에 따라 처리한다.
        weapon.AddDamageRatio(value);
    }

    /// <summary>
    /// 지정한 슬롯 무기의 연사력을 증가시킨다.
    /// </summary>
    public void AddFireRate(int slotIndex)
    {
        WeaponRuntimeState weapon = GetWeaponAtSlot(slotIndex);

        if (weapon == null)
        {
            return;
        }

        weapon.AddFireRate();
    }

    /// <summary>
    /// 지정한 슬롯의 런타임 무기를 안전하게 반환한다.
    /// </summary>
    private WeaponRuntimeState GetWeaponAtSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _equippedWeapons.Count)
        {
            return null;
        }

        return _equippedWeapons[slotIndex];
    }

    /// <summary>
    /// 지정한 슬롯에 장착된 WeaponData를 반환한다.
    /// Upgrader가 무기 증강 대상을 확인할 때 사용한다.
    /// </summary>
    public WeaponData GetWeaponDataAtSlot(int slotIndex)
    {
        WeaponRuntimeState weapon = GetWeaponAtSlot(slotIndex);

        return weapon != null ? weapon.Data : null;
    }

    /// <summary>
    /// 현재 무기 아이콘을 Stage의 WeaponUI에 표시한다.
    /// </summary>
    private void UpdateWeaponUI()
    {
        // StageManager가 아직 WeaponUI를 전달하지 않았다면
        // 런타임 무기 데이터만 갱신하고 UI는 건너뛴다.
        if (_weaponUI == null)
        {
            return;
        }

        WeaponRuntimeState weapon = CurrentWeapon;

        Sprite currentIcon = weapon != null ? weapon.Data.Icon : null;

        _weaponUI.UpdateCurrentWeapon(currentIcon);

        // Stage 시작 및 무기 교체 직후에도
        // 기다리지 않고 충전 상태를 바로 표시한다.
        UpdateWeaponChargeUI();
    }

    /// <summary>
    /// TAB으로 열리는 장착 무기 패널을 닫는다.
    /// 증강이나 일시정지처럼 다른 모달 UI가 열릴 때 사용한다.
    /// </summary>
    public void CloseWeaponSlotPanel()
    {
        _isWeaponSlotPanelOpen = false;

        if (_weaponSlotPanel != null)
        {
            _weaponSlotPanel.SetActive(false);
        }
    }

    // 현재 공격 간격 안에 Attack 클립을 한 번 재생한다.
    /// <summary>
    /// 현재 공격 간격 안에 Attack 클립을 한 번 재생한다.
    /// </summary>
    private void BeginSlashAnimation(float attackInterval)
    {
        _isSlashAttacking = true;

        // 예: 원본 1초 / 공격 간격 0.2초 = 재생 속도 5배.
        // 증강으로 간격이 줄면 다음 공격부터 재생 속도도 증가한다.
        float duration = Mathf.Max(0.01f, attackInterval);
        float animationSpeed = _slashAttackClip.length / duration;

        _animator.SetFloat(AttackSpeedHash, animationSpeed);
        _animator.SetBool(AttackHash, true);
        _animator.Play(SlashAttackStateHash, 0, 0f);
        // 같은 프레임의 LateUpdate에서도 새 Slash 상태를 읽도록 반영한다.
        _animator.Update(0f);
    }

    /// <summary>
    /// 프레임의 이동 결과에 맞춰 후속 상태를 갱신한다.
    /// </summary>
    private void LateUpdate()
    {
        // 일시정지 중에는 현재 공격 상태를 유지한다.
        if (!_isSlashAttacking || Time.timeScale <= 0f)
        {
            return;
        }

        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);

        // 다른 코드에서 상태가 변경됐다면 진행 중 표시도 정리한다.
        if (state.fullPathHash != SlashAttackStateHash)
        {
            CancelSlashAnimation();
            return;
        }

        // normalizedTime 1은 클립 한 번의 재생이 끝났다는 뜻이다.
        if (state.normalizedTime >= 1f)
        {
            CancelSlashAnimation();
        }
    }

    // 공격 완료, 무기 변경, 비활성화 시 Slash 애니메이션을 정리한다.
    /// <summary>
    /// 공격 완료, 무기 변경, 비활성화 시 Slash 애니메이션을 정리한다.
    /// </summary>
    private void CancelSlashAnimation()
    {
        // Slash가 제어 중일 때만 Animator를 변경한다.
        if (!_isSlashAttacking)
        {
            return;
        }

        _isSlashAttacking = false;

        _animator.SetFloat(AttackSpeedHash, 1f);
        _animator.SetBool(AttackHash, false);
        _animator.Play(SlashIdleStateHash, 0, 0f);
    }

    /// <summary>
    /// 비활성화 시 이벤트 연결과 실행 상태를 정리한다.
    /// </summary>
    private void OnDisable()
    {
        // 재활성화할 때 이벤트가 중복으로 연결되지 않도록 해제한다.
        if (_playerController != null)
        {
            _playerController.ControlStateChanged -= HandleControlStateChanged;
        }

        // 비활성화할 때도 진행 중인 공격과 방어를 정리한다.
        CancelCurrentAttack();
    }

    /// <summary>
    /// 활성화 시 초기 상태와 이벤트 연결을 준비한다.
    /// </summary>
    private void OnEnable()
    {
        // 이동 연출이 시작되는 순간 공격을 중단한다.
        _playerController.ControlStateChanged += HandleControlStateChanged;

        // 조작이 이미 잠겨 있는 상태에서 활성화된 경우도 반영한다.
        HandleControlStateChanged(_playerController.CanControl);
    }

    /// <summary>
    /// HandleControlStateChanged 동작에 필요한 상태와 처리 흐름을 실행한다.
    /// </summary>
    private void HandleControlStateChanged(bool canControl)
    {
        if (canControl)
        {
            return;
        }

        // 진행 중인 공격과 방어 자세를 정리한다.
        CancelCurrentAttack();

        // 연출 중 무기 선택 패널도 닫는다.
        CloseWeaponSlotPanel();
    }

    /// <summary>
    /// CancelCurrentAttack 동작에 필요한 상태와 처리 흐름을 실행한다.
    /// </summary>
    private void CancelCurrentAttack()
    {
        StopLazer();
        // Slash의 공격 중 상태와 애니메이션 속도를 정리한다.
        CancelSlashAnimation();

        // 검과 망치의 활성 공격 판정을 즉시 중단한다.
        if (_giantSword != null)
        {
            _giantSword.CancelAttack();
        }

        if (_hammer != null)
        {
            _hammer.CancelAttack();
        }

        // 방어 중이었다면 방어 자세와 판정도 중단한다.
        if (_shield != null)
        {
            _shield.CancelGuard();
        }

        // 일반 사격의 공격 애니메이션 요청도 해제한다.
        _animator.SetBool(AttackHash, false);
    }

    /// <summary>실제 투사체 발사에 해당하는 무기음을 선택합니다.</summary>
    private void PlayProjectileFireSound(WeaponType type)
    {
        // 연사 총알은 세 변형을 사용하고 지속 레이저는 자신의 수명에서 처리한다.
        switch (type)
        {
            case WeaponType.Bullet:
                GameAudio.Play((GameSound)((int)GameSound.BulletFire01 + UnityEngine.Random.Range(0, 3))); break;
            case WeaponType.Missile: GameAudio.Play(GameSound.MissileLaunch); break;
            case WeaponType.Grenade: GameAudio.Play(GameSound.GrenadeThrow); break;
            case WeaponType.Sniper: GameAudio.Play(GameSound.SniperFire); break;
            case WeaponType.Slash: GameAudio.Play(GameSound.SlashWaveLaunch); break;
        }
    }

}
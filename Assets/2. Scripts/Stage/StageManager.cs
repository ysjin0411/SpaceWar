using System;
using UnityEngine;

/// <summary>
/// Stage가 시작될 때 유저가 선택한 캐릭터 프리팹을 생성한다.
/// 처리 순서:
/// 1. GameSessionManager에서 선택한 캐릭터 ID를 가져온다.
/// 2. CharacterDatabase에서 ID에 해당하는 CharacterData를 찾는다.
/// 3. CharacterData에 등록된 캐릭터 프리팹을 생성한다.
/// </summary>
[RequireComponent(typeof(GameManager))]
[RequireComponent(typeof(Upgrader))]
[RequireComponent(typeof(CharacterProgression))]
public class StageManager : MonoBehaviour
{
    [Header("캐릭터 데이터")]
    [Tooltip("캐릭터 ID를 CharacterData로 변환할 데이터베이스")]
    [SerializeField] CharacterDatabase _characterDatabase;
    [Tooltip("저장된 무기 ID를 WeaponData로 변환할 데이터베이스")]
    [SerializeField] WeaponDatabase _weaponDatabase;

    [Header("캐릭터 생성 위치")]
    [Tooltip("선택한 캐릭터가 생성될 위치. 비어 있으면 월드 원점에 생성한다.")]
    [SerializeField] Transform _playerSpawnPoint;

    [Header("Stage UI")]
    [Tooltip("생성된 플레이어의 체력을 표시할 HP UI")]
    [SerializeField] HPUI _hpUI;
    [Tooltip("현재 선택한 무기 아이콘을 표시할 UI")]
    [SerializeField] WeaponUI _weaponUI;
    [Tooltip("Tab 키로 열고 닫을 무기 선택 패널")]
    [SerializeField] GameObject _weaponSlotPanel;

    [Header("Stage 시스템")]
    [Tooltip("생성된 플레이어를 적들에게 전달할 EnemySpawner")]
    [SerializeField] EnemySpawner _enemySpawner;

    // 이번 Stage에 생성된 캐릭터의 원본 데이터
    CharacterData _selectedCharacterData;

    // Stage에 실제로 생성한 캐릭터를 보관한다.
    GameObject _spawnedPlayer;

    // 생성한 캐릭터에서 가져온 PlayerController
    PlayerController _spawnedPlayerController;
    Weapon _spawnedWeapon;

    // StageManager와 같은 오브젝트에 있는 GameManager
    GameManager _gameManager;
    Upgrader _upgrader;
    CharacterProgression _characterProgression;

    /// <summary>
    /// 이번 Stage에서 선택한 캐릭터의 원본 데이터.
    /// </summary>
    public CharacterData SelectedCharacterData => _selectedCharacterData;

    /// <summary>
    /// Stage에 생성된 현재 플레이어를 외부에서 확인할 수 있게 제공한다.
    /// </summary>
    public GameObject SpawnedPlayer => _spawnedPlayer;

    /// <summary>
    /// Stage에 생성된 플레이어 컨트롤러.
    /// 이후 EnemySpawner와 Upgrader에 전달할 때 사용한다.
    /// </summary>
    public PlayerController SpawnedPlayerController => _spawnedPlayerController;

    public Weapon SpawnedWeapon => _spawnedWeapon;

    private void Awake()
    {
        // 게임매니저 가져오기
        _gameManager = GetComponent<GameManager>();

        // 업그레이더 가져오기
        _upgrader = GetComponent<Upgrader>();

        // 캐릭터프로그레션 가져오기
        _characterProgression = GetComponent<CharacterProgression>();

        if (_gameManager == null)
        {
            throw new MissingComponentException("StageManager와 같은 오브젝트에 GameManager가 없습니다.");
        }
        if (_upgrader == null)
        {
            throw new MissingComponentException("StageManager와 같은 오브젝트에 Upgrader가 없습니다.");
        }
        if (_characterProgression == null)
        {
            throw new MissingComponentException("StageManager와 같은 오브젝트에 CharacterProgression이 없습니다.");
        }
    }

    private void Start()
    {
        SpawnSelectedCharacter();   // 캐릭터 셀렉한거 스폰
        InitializeStageSystems();   // 스테이지 시스템 이니셜라이즈
    }

    /// <summary>
    /// 캐릭터 선택 화면에서 선택한 캐릭터를 Stage에 생성한다.
    /// </summary>
    private void SpawnSelectedCharacter()
    {
        // Bootstrap 씬을 거치지 않았다면 세션 매니저가 존재하지 않는다.
        if (GameSessionManager.Instance == null)
        {
            throw new InvalidOperationException("GameSessionManager가 없습니다. Bootstrap 씬부터 실행해야 합니다.");
        }

        // CharacterDatabase가 연결되어 있는지 확인한다.
        if (_characterDatabase == null)
        {
            throw new InvalidOperationException("StageManager에 CharacterDatabase가 연결되지 않았습니다.");
        }

        // Stage의 HP UI가 연결되어 있는지 확인한다.
        if (_hpUI == null)
        {
            throw new InvalidOperationException("StageManager에 HPUI가 연결되지 않았습니다.");
        }

        // 캐릭터 선택 화면에서 저장한 캐릭터 ID를 가져온다.
        string selectedCharacterId = GameSessionManager.Instance.SelectedCharacterId;

        // 선택한 캐릭터 ID가 없다면 정상적인 캐릭터 선택 과정을 거치지 않은 것이다.
        if (string.IsNullOrWhiteSpace(selectedCharacterId))
        {
            throw new InvalidOperationException("선택된 캐릭터 ID가 없습니다.");
        }

        // 선택한 ID를 실제 CharacterData로 변환한다.
        _selectedCharacterData = _characterDatabase.GetCharacterData(selectedCharacterId);

        // CharacterData에 캐릭터 프리팹이 있는지 확인한다.
        if (_selectedCharacterData.CharacterPrefab == null)
        {
            throw new InvalidOperationException($"캐릭터 '{selectedCharacterId}'의 프리팹이 비어 있습니다.");
        }

        // SpawnPoint가 연결되어 있다면 그 위치를 사용하고, 연결되어 있지 않다면 월드 원점에 생성한다.
        Vector3 spawnPosition = _playerSpawnPoint != null ? _playerSpawnPoint.position : Vector3.zero;

        Quaternion spawnRotation = _playerSpawnPoint != null ? _playerSpawnPoint.rotation : Quaternion.identity;

        // 선택한 캐릭터 프리팹을 생성한다.
        _spawnedPlayer = Instantiate(_selectedCharacterData.CharacterPrefab,spawnPosition,spawnRotation);

        // 생성한 캐릭터 프리팹에서 공통 PlayerController를 찾는다.
        _spawnedPlayerController = _spawnedPlayer.GetComponent<PlayerController>();

        // 웨폰 컴포넌트 가져오기
        _spawnedWeapon = _spawnedPlayer.GetComponent<Weapon>();

        // 캐릭터 프리팹에 PlayerController가 없다면 공통 플레이어 기능을 사용할 수 없다.
        if (_spawnedPlayerController == null)
        {
            throw new MissingComponentException(
                $"캐릭터 프리팹 '{_selectedCharacterData.CharacterName}'에 PlayerController가 없습니다.");
        }

        if (_spawnedWeapon == null)
        {
            throw new MissingComponentException($"캐릭터 프리팹 '{_selectedCharacterData.CharacterName}'에 Weapon이 없습니다.");
        }

        // HP UI가 새로 생성한 PlayerController를 바라보게 한다.
        _hpUI.Initialize(_spawnedPlayerController);

        // CharacterData의 체력과 이동속도를 적용한다.
        _spawnedPlayerController.Initialize(_selectedCharacterData, _hpUI);

        Debug.Log($"선택한 캐릭터를 생성했습니다: {_selectedCharacterData.CharacterName}");
    }

    /// <summary>
    /// 선택한 캐릭터의 JSON 장착정보를 읽어서
    /// 생성된 캐릭터의 Weapon에 전달한다.
    /// </summary>
    private void InitializeEquippedWeapons()
    {
        // Bootstrap 씬의 SaveManager가 유지되고 있는지 확인한다.
        if (SaveManager.Instance == null)
        {
            throw new InvalidOperationException("SaveManager가 없습니다. Bootstrap 씬부터 실행해야 합니다.");
        }

        // 저장된 무기 ID를 WeaponData로 바꾸려면 데이터베이스가 필요하다.
        if (_weaponDatabase == null)
        {
            throw new InvalidOperationException("StageManager에 WeaponDatabase가 연결되지 않았습니다.");
        }

        // 현재 선택한 캐릭터의 장착정보를 가져온다.
        // 아직 장착정보가 없는 캐릭터라면 빈 슬롯 3개가 새로 만들어진다.
        CharacterLoadoutSaveData loadout = SaveManager.Instance.GetOrCreateCharacterLoadout(_selectedCharacterData.Id);

        if (loadout.equippedWeaponIds == null)
        {
            throw new InvalidOperationException($"캐릭터 '{_selectedCharacterData.Id}'의 장착 무기 배열이 없습니다.");
        }

        // Weapon은 ID 문자열이 아니라 WeaponData를 사용하므로 장착 슬롯과 같은 크기의 배열을 만든다.
        WeaponData[] equippedWeaponDatas = new WeaponData[loadout.equippedWeaponIds.Length];

        for (int slotIndex = 0; slotIndex < loadout.equippedWeaponIds.Length; slotIndex++)
        {
            string weaponId = loadout.equippedWeaponIds[slotIndex];

            // 저장된 ID가 없다면 장착되지 않은 빈 슬롯이다.
            if (string.IsNullOrWhiteSpace(weaponId))
            {
                equippedWeaponDatas[slotIndex] = null;
                continue;
            }

            // 저장된 무기 ID를 실제 WeaponData로 변환한다.
            equippedWeaponDatas[slotIndex] = _weaponDatabase.GetWeaponData(weaponId);
        }

        // 변환된 장착 무기를 생성된 캐릭터의 Weapon에 전달한다.
        _spawnedWeapon.InitializeWeapons(equippedWeaponDatas);
    }

    /// <summary>
    /// 캐릭터 생성이 끝난 후 Stage의 다른 시스템에 새 플레이어를 전달한다.
    /// </summary>
    private void InitializeStageSystems()
    {
        // null 검사들
        if (_enemySpawner == null)
        {
            throw new InvalidOperationException("StageManager에 EnemySpawner가 연결되지 않았습니다.");
        }
        if (_weaponUI == null)
        {
            throw new InvalidOperationException("StageManager에 WeaponUI가 연결되지 않았습니다.");
        }

        if (_weaponSlotPanel == null)
        {
            throw new InvalidOperationException("StageManager에 WeaponSlotPanel이 연결되지 않았습니다.");
        }

        // 생성된 캐릭터의 Weapon에 Stage 씬의 UI를 전달한다. 캐릭터 프리팹은 Stage UI를 직접 참조하지 않는다.
        _spawnedWeapon.InitializeUI(_weaponUI, _weaponSlotPanel);

        // 선택한 캐릭터의 저장된 장착 무기를 불러온다. InitializeWeapons()가 호출되면서 현재 무기 아이콘도 갱신된다.
        InitializeEquippedWeapons();

        // 선택한 캐릭터의 저장된 레벨과 경험치를 불러온다.
        _characterProgression.Initialize(_selectedCharacterData.Id);

        // 적과 적 투사체가 새로 생성된 플레이어를 사용하도록 연결한다.
        _enemySpawner.Initialize(_spawnedPlayerController,_gameManager);

        // 증강 시스템이 새 플레이어와 해당 플레이어 프리팹의 Weapon을 사용하도록 한다.
        _upgrader.Initialize(_gameManager, _spawnedPlayerController, _spawnedWeapon);
    }
}
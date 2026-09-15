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
    [SerializeField] CharacterDatabase _characterDatabase;      // 캐릭터 ID를 CharacterData로 변환할 데이터베이스
    [SerializeField] WeaponDatabase _weaponDatabase;            // 저장된 무기 ID를 WeaponData로 변환할 데이터베이스

    [Header("캐릭터 생성 위치")]
    [SerializeField] Transform _playerSpawnPoint;

    [Header("UI")]
    [SerializeField] HPUI _hpUI;        // HP UI
    [SerializeField] WeaponUI _weaponUI;    // 현재 선택한 무기 아이콘을 표시할 UI

    [Header("무기 슬롯 활성화 오브젝트(Tab)")]
    [SerializeField] GameObject _weaponSlotOpen;

    [Header("Stage 시스템")]
    [SerializeField] EnemySpawner _enemySpawner;                          // 생성된 플레이어를 적들에게 전달할 EnemySpawner
    [SerializeField] StageTransitionManager _stageTransitionManager;      // 플레이어 입장과 구간 이동 연출을 담당하는 매니저
    [SerializeField] BossEncounterManager _bossEncounterManager;          // 스테이지 페이즈에 맞춰 보스를 생성할 BossEncounterManager

    // 플레이어 사망을 스테이지 실패 상태로 전달할 매니저
    [SerializeField] StageFlowManager _stageFlowManager;

    // 편집 중 꺼뒀더라도 실행 시 활성화할 보스 체력바 프레임.
    // 보스 등장 전에는 가로 크기를 0으로 두어 숨긴다.
    [SerializeField] private RectTransform _bossHpFrame;

    // 편집 중 꺼뒀더라도 실행 시 활성화할 시스템 알림 프레임.
    // 평소에는 CanvasGroup의 Alpha를 0으로 두어 숨긴다.
    [SerializeField] private CanvasGroup _systemDialogueFrame;

    // 이번 Stage에 생성된 캐릭터의 원본 데이터
    CharacterData _selectedCharacterData;

    // Stage에 실제로 생성한 캐릭터를 보관한다.
    GameObject _spawnedPlayer;

    // 생성한 캐릭터에서 가져온 PlayerController
    PlayerController _spawnedPlayerController;
    // 생성한 캐릭터에서 가져온 Weapon
    Weapon _spawnedWeapon;

    // GetComponent 할것들.
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
        // 사망 후 실패 상태로 전환할 연결이 반드시 필요하다.
        if (_stageFlowManager == null)
        {
            throw new InvalidOperationException(
                "StageManager에 StageFlowManager가 연결되지 않았습니다.");
        }

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
        // 캐릭터 생성과 전투 시스템 초기화 전에 필수 UI를 준비한다.
        PrepareStageUI();

        // 선택한 캐릭터를 생성한다.
        SpawnSelectedCharacter();

        // 생성된 캐릭터를 스테이지의 각 시스템에 연결한다.
        InitializeStageSystems();
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
        _spawnedPlayer = Instantiate(_selectedCharacterData.CharacterPrefab, spawnPosition, spawnRotation);

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

        // CharacterData의 체력과 이동속도를 적용한다.
        _spawnedPlayerController.Initialize(_selectedCharacterData, _hpUI);

        // 생성한 플레이어가 사망하면 스테이지 실패를 처리한다.
        _spawnedPlayerController.Died += HandlePlayerDied;

        // HP UI가 새로 생성한 PlayerController를 바라보게 한다.
        _hpUI.Initialize(_spawnedPlayerController);

        Debug.Log($"선택한 캐릭터를 생성했습니다: {_selectedCharacterData.CharacterName}");
    }

    /// <summary>
    /// 현재 선택한 캐릭터의 장착 무기 ID를 세이브 데이터에서 가져온다.
    /// 장착 무기가 하나도 없다면 기본 무기를 0번 슬롯에 자동 장착한다.
    /// 이후 무기 ID를 실제 WeaponData로 변환하여 플레이어의 Weapon에 전달한다.
    /// </summary>
    private void InitializeEquippedWeapons()
    {
        // Bootstrap 씬에서 생성되는 SaveManager가 현재 존재하는지 확인한다.
        if (SaveManager.Instance == null)
        {
            // SaveManager가 없으면 세이브 데이터를 읽을 수 없으므로 실행을 중단한다.
            throw new InvalidOperationException(
                "SaveManager가 없습니다. Bootstrap 씬부터 실행해야 합니다.");
        }

        // StageManager의 Inspector에 WeaponDatabase가 연결되어 있는지 확인한다.
        if (_weaponDatabase == null)
        {
            // WeaponDatabase가 없으면 저장된 무기 ID를 WeaponData로 변환할 수 없다.
            throw new InvalidOperationException(
                "StageManager에 WeaponDatabase가 연결되지 않았습니다.");
        }

        // 현재 선택한 캐릭터의 고유 ID를 사용해서 해당 캐릭터의 장착 정보를 가져온다.
        // 기존 장착 정보가 없다면 SaveManager가 빈 무기 슬롯 3개를 가진 정보를 새로 만든다.
        CharacterLoadoutSaveData loadout =
            SaveManager.Instance.GetOrCreateCharacterLoadout(
                _selectedCharacterData.Id);

        // 장착 무기 ID 배열 자체가 생성되어 있는지 확인한다.
        if (loadout.equippedWeaponIds == null)
        {
            // 배열이 null이면 정상적인 장착 정보를 처리할 수 없으므로 실행을 중단한다.
            throw new InvalidOperationException(
                $"캐릭터 '{_selectedCharacterData.Id}'의 장착 무기 배열이 없습니다.");
        }

        // 장착 무기 슬롯이 하나 이상 존재하는지 확인한다.
        if (loadout.equippedWeaponIds.Length == 0)
        {
            // 슬롯 개수가 0이면 기본 무기를 넣을 위치가 없으므로 실행을 중단한다.
            throw new InvalidOperationException(
                $"캐릭터 '{_selectedCharacterData.Id}'의 장착 무기 슬롯이 없습니다.");
        }

        

        // 세이브 데이터에는 무기 ID 문자열만 저장되어 있다.
        // Weapon 컴포넌트는 WeaponData를 사용하므로 같은 슬롯 개수의 배열을 새로 만든다.
        WeaponData[] equippedWeaponDatas =
            new WeaponData[loadout.equippedWeaponIds.Length];

        // 저장된 모든 장착 슬롯을 순서대로 WeaponData로 변환한다.
        for (int slotIndex = 0;
             slotIndex < loadout.equippedWeaponIds.Length;
             slotIndex++)
        {
            // 현재 슬롯에 저장된 무기 ID를 가져온다.
            string weaponId =
                loadout.equippedWeaponIds[slotIndex];

            // 현재 슬롯의 무기 ID가 비어 있는지 확인한다.
            if (string.IsNullOrWhiteSpace(weaponId))
            {
                // 무기 ID가 없으므로 WeaponData 배열에서도 빈 슬롯을 null로 표시한다.
                equippedWeaponDatas[slotIndex] = null;

                // 현재 슬롯 처리를 끝내고 다음 슬롯으로 넘어간다.
                continue;
            }

            // WeaponDatabase에서 현재 무기 ID와 일치하는 WeaponData를 찾는다.
            WeaponData weaponData =
                _weaponDatabase.GetWeaponData(weaponId);

            // 찾은 WeaponData를 동일한 슬롯 인덱스에 저장한다.
            equippedWeaponDatas[slotIndex] = weaponData;
        }

        // 완성된 장착 무기 배열을 현재 생성된 플레이어의 Weapon 컴포넌트에 전달한다.
        // Weapon은 각 WeaponData를 전투 중 증강 가능한 WeaponRuntimeState로 변환한다.
        _spawnedWeapon.InitializeWeapons(equippedWeaponDatas);
    }

    /// <summary>
    /// 캐릭터 생성이 끝난 후 Stage의 다른 시스템에 새 플레이어를 전달한다.
    /// </summary>
    private void InitializeStageSystems()
    {
        // null 검사들
        if (_stageTransitionManager == null)
        {
            throw new InvalidOperationException(
                "StageManager의 StageTransitionManager가 연결되지 않았습니다.");
        }
        if (_bossEncounterManager == null)
        {
            throw new InvalidOperationException(
                "StageManager에 BossEncounterManager가 연결되지 않았습니다.");
        }
        if (_enemySpawner == null)
        {
            throw new InvalidOperationException("StageManager에 EnemySpawner가 연결되지 않았습니다.");
        }
        if (_weaponUI == null)
        {
            throw new InvalidOperationException("StageManager에 WeaponUI가 연결되지 않았습니다.");
        }

        if (_weaponSlotOpen == null)
        {
            throw new InvalidOperationException("StageManager에 WeaponSlotPanel이 연결되지 않았습니다.");
        }

        // 생성된 캐릭터의 Weapon에 Stage 씬의 UI를 전달한다. 캐릭터 프리팹은 Stage UI를 직접 참조하지 않는다.
        _spawnedWeapon.InitializeUI(_weaponUI, _weaponSlotOpen);

        // 선택한 캐릭터의 저장된 장착 무기를 불러온다. InitializeWeapons()가 호출되면서 현재 무기 아이콘도 갱신된다.
        InitializeEquippedWeapons();

        // 선택한 캐릭터의 저장된 레벨과 경험치를 불러온다.
        _characterProgression.Initialize(_selectedCharacterData.Id);

        // 적과 적 투사체가 새로 생성된 플레이어를 사용하도록 연결한다.
        _enemySpawner.Initialize(_spawnedPlayerController,_gameManager);

        // 보스 생성 시스템에도 현재 플레이어를 전달한다.
        _bossEncounterManager.Initialize(_spawnedPlayerController, _gameManager);

        // 증강 시스템이 새 플레이어와 해당 플레이어 프리팹의 Weapon을 사용하도록 한다.
        _upgrader.Initialize(_gameManager, _spawnedPlayerController, _spawnedWeapon);

        // 입장과 구간 전환 연출에서 사용할 플레이어를 전달한다.
        _stageTransitionManager.Initialize(_spawnedPlayerController);
    }

    /// <summary>
    /// 플레이어 사망을 스테이지 진행 시스템에 전달한다.
    /// </summary>
    private void HandlePlayerDied()
    {
        _stageFlowManager.FailStage();
    }

    /// <summary>
    /// 씬을 나갈 때 생성한 플레이어의 이벤트 연결을 정리한다.
    /// </summary>
    private void OnDestroy()
    {
        if (_spawnedPlayerController != null)
        {
            _spawnedPlayerController.Died -= HandlePlayerDied;
        }
    }

    // 편집 중의 활성화 상태와 관계없이 필수 UI를 실행 가능한 상태로 준비한다.
    private void PrepareStageUI()
    {
        // 이름으로 검색하지 않고 Inspector에 연결한 오브젝트를 사용한다.
        if (_bossHpFrame == null || _systemDialogueFrame == null)
        {
            throw new MissingReferenceException(
                "StageManager의 Boss Hp Frame과 System Dialogue Frame을 연결해주세요.");
        }

        // 보스 등장 전 체력바가 보이지 않도록 가로 크기만 접는다.
        // 세로와 깊이의 기존 크기는 유지한다.
        Vector3 scale = _bossHpFrame.localScale;
        scale.x = 0f;
        _bossHpFrame.localScale = scale;

        // 프레임과 자식 UI가 초기화될 수 있도록 활성화한다.
        _bossHpFrame.gameObject.SetActive(true);

        // 시스템 알림은 활성화하되 메시지가 오기 전까지 투명하게 유지한다.
        _systemDialogueFrame.alpha = 0f;

        // SystemDialogueUI의 초기화와 보스 이벤트 구독이 실행되도록 한다.
        _systemDialogueFrame.gameObject.SetActive(true);
    }
}
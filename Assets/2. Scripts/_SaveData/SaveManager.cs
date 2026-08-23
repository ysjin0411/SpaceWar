using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 플레이어의 세이브 데이터를 메모리에서 관리한다.
/// 캐릭터별 장착 무기 정보를 저장한다.
/// 실제 파일 저장과 불러오기는 이후 단계에서 추가한다.
/// </summary>
public class SaveManager : MonoBehaviour
{
    // 인스펙터에서 초기화할 스테이지를 선택하기 위한 테스트용 목록
    private enum StageClearRecordResetTarget
    {
        Stage1 = 0,
        Stage2 = 1
    }

    [Header("테스트용 클리어 기록 초기화")]

    // ContextMenu 실행 시 기록을 초기화할 스테이지
    [SerializeField]
    StageClearRecordResetTarget _resetTargetStage =
        StageClearRecordResetTarget.Stage1;

    // 다른 씬에서도 SaveManager에 접근하기 위한 현재 인스턴스
    public static SaveManager Instance { get; private set; }

    // 로컬에 생성할 세이브 파일 이름
    const string SaveFileName = "player_save.json";

    // Soldier 저장 데이터를 찾을 때 사용하는 고정 캐릭터 ID
    const string SoldierCharacterId = "soldier";

    // Warrior 저장 데이터를 찾을 때 사용하는 고정 캐릭터 ID
    const string WarriorCharacterId = "warrior";

    // 실행 중 사용할 세이브 파일의 전체 경로
    string _savePath;

    [Header("현재 플레이어 세이브 데이터")]
    [SerializeField] PlayerSaveData _playerSaveData = new();

    /// <summary>
    /// 현재 총 골드가 변경됐을 때 변경된 골드량을 전달한다.
    /// </summary>
    public event Action<int> GoldChanged;

    /// <summary>
    /// JSON에서 불러온 현재 플레이어의 총 골드.
    /// 외부에서는 읽기만 할 수 있다.
    /// </summary>
    public int CurrentGold => _playerSaveData.gold;

    // 다른 클래스에는 읽기 전용으로 현재 세이브 데이터를 공개한다.
    public PlayerSaveData PlayerSaveData => _playerSaveData;

    private void Awake()
    {
        _savePath = $"{Application.persistentDataPath}/GameSaveData.json";

        // 이미 SaveManager가 있는데 또 생성됐다면 새로 생성된 중복 SaveManager만 제거한다.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // 현재 SaveManager를 전역 접근 인스턴스로 등록한다.
        Instance = this;

        // 운영체제에서 이 게임의 저장 데이터에 적합한 폴더와 저장 파일 이름을 합쳐 전체 경로를 만든다.
        _savePath = Path.Combine(Application.persistentDataPath, SaveFileName);

        // 게임이 시작될 때 로컬 저장 데이터를 불러온다.
        LoadFromFile();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha9))
        {
            Application.OpenURL(Application.persistentDataPath);
        }
    }

    private void OnDestroy()
    {
        // 현재 등록된 인스턴스 자신이 파괴되는 경우에만 정적 참조를 비운다.
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 현재 메모리에 있는 플레이어 데이터를 JSON 문자열로 변환해 로컬 파일에 저장한다.
    /// </summary>
    public void SaveToFile()
    {
        // json(문자열) = _playerSaveData를 JSON으로 변환한 결과
        string json = JsonUtility.ToJson(_playerSaveData, true);

        // 만들어진 JSON 문자열을 로컬 파일에 기록한다. 같은 이름의 파일이 있으면 최신 내용으로 덮어쓴다.
        // _savePath에  json문자열을 기록
        File.WriteAllText(_savePath, json);
    }

    /// <summary>
    /// 로컬 JSON 파일을 읽어 플레이어 세이브 데이터로 복원한다. 
    /// 첫 실행이라 파일이 없다면 새로운 빈 세이브 파일을 만든다.
    /// </summary>
    private void LoadFromFile()
    {
        // 아직 세이브 파일이 없는 첫 실행인지 확인한다.
        if (!File.Exists(_savePath))
        {
            // 새로운 빈 플레이어 데이터를 만든다.
            _playerSaveData = new PlayerSaveData();

            // 첫 실행용 빈 세이브 파일을 실제 로컬에 생성한다.
            SaveToFile();

            // 새 데이터를 만들었으므로 더 읽을 파일이 없다.
            return;
        }

        // 기존 로컬 세이브 파일의 JSON 문자열을 읽는다.
        // _savePath에 있는 저장데이터 json문자열을 읽어서 json에 받아오기.
        string json = File.ReadAllText(_savePath);

        // 파일이 존재하지만 내용이 비어 있으면 정상적인 세이브 파일이 아니므로 오류를 발생시킨다.
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException($"세이브 파일이 비어 있습니다. 경로: {_savePath}");
        }

        // JSON 문자열을 PlayerSaveData 객체로 변환한다.
        // PlayerSaveData로 사용할 수 있게 json에서 읽어온 데이터를 loadedData에 저장.
        PlayerSaveData loadedData = JsonUtility.FromJson<PlayerSaveData>(json);

        // 변환 결과가 없다면 손상되었거나 형식이 잘못된 파일이다.
        if (loadedData == null)
        {
            throw new InvalidDataException($"세이브 파일을 불러올 수 없습니다. 경로: {_savePath}");
        }

        // 캐릭터 장착 정보 리스트가 없다면 현재 세이브 데이터 구조와 맞지 않는 파일이다.
        if (loadedData.characterLoadouts == null)
        {
            throw new InvalidDataException($"세이브 파일에 캐릭터 장착 정보가 없습니다. 경로: {_savePath}");
        }

        // 이전 버전의 세이브 파일에는 스테이지 기록 목록이 없을 수 있다.
        // null이면 빈 목록을 만들어 기존 세이브 파일을 계속 사용할 수 있게 한다.
        NormalizeStageClearRecords(loadedData);

        // 이전 버전 세이브에도 업적 목록을 사용할 수 있도록 보정한다.
        NormalizeAchievementRecords(loadedData);

        // PlayerSaveData자료형 loadedData를 _playerSaveData에 저장.
        _playerSaveData = loadedData;

        // 실제 저장 위치를 Unity Console에서 확인할 수 있게 표시한다.
        Debug.Log($"세이브 데이터를 불러왔습니다: {_savePath}");
    }

    

    /// <summary>
    /// 캐릭터 ID에 해당하는 장착 정보를 찾아 반환한다. 아직 장착 정보가 없다면 새로 만들어서 반환한다.
    /// </summary>
    /// <param name="characterId">장착 정보를 찾을 캐릭터 ID</param>
    public CharacterLoadoutSaveData GetOrCreateCharacterLoadout(string characterId)
    {
        // 비어 있는 캐릭터 ID는 정상적인 데이터가 아니므로 호출 위치를 확인할 수 있도록 예외를 발생시킨다.
        if (string.IsNullOrWhiteSpace(characterId))
        {
            throw new ArgumentException("캐릭터 ID가 비어 있습니다.", nameof(characterId));
        }

        // 기존 캐릭터 장착 정보 중 같은 ID가 있는지 확인한다.
        for (int i = 0; i < _playerSaveData.characterLoadouts.Count; i++)
        {
            // 캐릭터별 장착 정보 목록에서 하나씩 꺼낸다.
            CharacterLoadoutSaveData loadout = _playerSaveData.characterLoadouts[i];

            // 같은 캐릭터 ID의 장착 정보를 찾았다면 새로 만들지 않고 기존 데이터를 반환한다.
            if (loadout.characterId == characterId)
            {
                // 이전 버전의 저장 데이터도 현재 장비 저장 형식에 맞게 보정한다.
                NormalizeLoadout(loadout);

                // 보정된 캐릭터 장비 데이터를 반환한다.
                return loadout;
            }
        }

        // 해당 캐릭터의 저장 데이터가 없으므로 새로 생성한다.
        CharacterLoadoutSaveData newLoadout = new CharacterLoadoutSaveData();

        // 새 저장 데이터가 어떤 캐릭터의 데이터인지 기록한다.
        newLoadout.characterId = characterId;

        // 새 데이터도 배열과 보유 목록이 정상인지 보정한다.
        NormalizeLoadout(newLoadout);

        // 플레이어의 캐릭터별 장비 목록에 추가한다.
        _playerSaveData.characterLoadouts.Add(newLoadout);

        // 새 캐릭터의 장착 정보가 만들어졌으므로 게임을 종료해도 유지되도록 로컬 파일에 저장한다.
        SaveToFile();



        // 생성한 장착 정보를 호출한 곳에 반환한다.
        return newLoadout;
    }

    /// <summary>
    /// 해당 캐릭터가 지정한 무기를 보유하고 있는지 확인한다.
    /// </summary>
    /// <param name="characterId">보유 여부를 확인할 캐릭터 ID</param>
    /// <param name="weaponId">보유 여부를 확인할 무기 ID</param>
    /// <returns>보유 중이면 true, 보유하지 않았다면 false</returns>
    public bool OwnsWeapon(string characterId, string weaponId)
    {
        // 비어 있는 무기 ID로는 보유 여부를 확인할 수 없다.
        if (string.IsNullOrWhiteSpace(weaponId))
        {
            throw new ArgumentException(
                "보유 여부를 확인할 무기 ID가 비어 있습니다.",
                nameof(weaponId));
        }

        // 캐릭터의 저장 데이터를 가져온다.
        // 기존 저장 데이터라면 내부에서 NormalizeLoadout도 실행된다.
        CharacterLoadoutSaveData loadout =
            GetOrCreateCharacterLoadout(characterId);

        // 보유 무기 ID 목록에 해당 무기가 있는지 확인한다.
        return loadout.ownedWeaponIds.Contains(weaponId);
    }

    /// <summary>
    /// 지정한 캐릭터가 무기를 구매하도록 시도한다.
    /// 구매에 성공하면 골드를 차감하고 보유 무기 목록에 추가한다.
    /// </summary>
    /// <param name="characterId">무기를 구매할 캐릭터 ID</param>
    /// <param name="weaponId">구매할 무기 ID</param>
    /// <param name="price">구매할 무기의 가격</param>
    /// <returns>구매에 성공하면 true, 구매할 수 없다면 false</returns>
    public bool TryPurchaseWeapon(string characterId, string weaponId, int price)
    {
        // 비어 있는 무기 ID로는 구매할 수 없다.
        if (string.IsNullOrWhiteSpace(weaponId))
        {
            throw new ArgumentException(
                "구매할 무기 ID가 비어 있습니다.",
                nameof(weaponId));
        }

        // 음수 가격은 잘못 설정된 무기 데이터다.
        if (price < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(price),
                price,
                "무기 가격은 0 이상이어야 합니다.");
        }

        // 구매할 캐릭터의 저장 데이터를 가져온다.
        CharacterLoadoutSaveData loadout =
            GetOrCreateCharacterLoadout(characterId);

        // 이미 보유한 무기는 다시 구매하지 않는다.
        if (loadout.ownedWeaponIds.Contains(weaponId))
        {
            return false;
        }

        // 현재 골드가 무기 가격보다 적으면 구매하지 않는다.
        if (_playerSaveData.gold < price)
        {
            return false;
        }

        // 무기 가격만큼 현재 골드에서 차감한다.
        _playerSaveData.gold -= price;

        // 구매한 무기를 캐릭터의 보유 무기 목록에 추가한다.
        loadout.ownedWeaponIds.Add(weaponId);

        // 변경된 골드와 보유 무기 목록을 JSON 파일에 저장한다.
        SaveToFile();

        // 골드가 변경되었다고 골드 UI에 알린다.
        GoldChanged?.Invoke(_playerSaveData.gold);

        // 구매가 정상적으로 완료되었다.
        return true;
    }

    /// <summary>
    /// 지정한 캐릭터의 지정한 슬롯에 무기를 장착한다.
    /// </summary>
    /// <param name="characterId">무기를 장착할 캐릭터 ID</param>
    /// <param name="slotIndex">장착 슬롯 번호: 0, 1, 2</param>
    /// <param name="weaponId">장착할 무기의 고유 ID</param>
    public void EquipWeapon(string characterId, int slotIndex, string weaponId)
    {
        // 비어 있는 무기 ID는 장착 요청으로 인정하지 않는다.
        if (string.IsNullOrWhiteSpace(weaponId))
        {
            throw new ArgumentException("장착할 무기 ID가 비어 있습니다.", nameof(weaponId));
        }

        // 캐릭터의 장착 정보를 가져온다.
        CharacterLoadoutSaveData loadout = GetOrCreateCharacterLoadout(characterId);

        // 슬롯 번호가 0~2 범위를 벗어나면 코드 오류다.
        if (slotIndex < 0 || slotIndex >= loadout.equippedWeaponIds.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "무기 슬롯 번호가 범위를 벗어났습니다.");
        }

        // 지정한 슬롯에 무기 ID를 기록한다.
        loadout.equippedWeaponIds[slotIndex] = weaponId;

        // 장착 정보가 변경됐으므로 로컬 파일에 즉시 저장한다.
        SaveToFile();
    }

    /// <summary>
    /// 지정한 캐릭터의 두 장착 슬롯에 들어 있는 무기 ID를 서로 교환하고 저장한다.
    /// 빈 슬롯이 포함되어 있어도 정상적으로 교환할 수 있다.
    /// </summary>
    /// <param name="characterId">장착 정보를 변경할 캐릭터 ID</param>
    /// <param name="sourceSlotIndex">드래그를 시작한 장착 슬롯 번호</param>
    /// <param name="targetSlotIndex">무기를 드롭한 장착 슬롯 번호</param>
    public void SwapEquippedWeapons(
        string characterId,
        int sourceSlotIndex,
        int targetSlotIndex)
    {
        // 캐릭터의 장착 무기 저장 데이터를 가져온다.
        CharacterLoadoutSaveData loadout =
            GetOrCreateCharacterLoadout(characterId);

        // 출발 슬롯 번호가 장착 슬롯 배열의 범위를 벗어나면 설정 오류다.
        if (sourceSlotIndex < 0 ||
            sourceSlotIndex >= loadout.equippedWeaponIds.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceSlotIndex),
                sourceSlotIndex,
                "출발 장착 슬롯 번호가 범위를 벗어났습니다.");
        }

        // 도착 슬롯 번호가 장착 슬롯 배열의 범위를 벗어나면 설정 오류다.
        if (targetSlotIndex < 0 ||
            targetSlotIndex >= loadout.equippedWeaponIds.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetSlotIndex),
                targetSlotIndex,
                "도착 장착 슬롯 번호가 범위를 벗어났습니다.");
        }

        // 같은 슬롯끼리는 교환할 필요가 없으므로 저장하지 않는다.
        if (sourceSlotIndex == targetSlotIndex)
        {
            return;
        }

        // 출발 슬롯에 들어 있던 무기 ID를 임시로 보관한다.
        string sourceWeaponId =
            loadout.equippedWeaponIds[sourceSlotIndex];

        // 도착 슬롯의 무기 ID를 출발 슬롯으로 옮긴다.
        loadout.equippedWeaponIds[sourceSlotIndex] =
            loadout.equippedWeaponIds[targetSlotIndex];

        // 임시로 보관한 출발 슬롯의 무기 ID를 도착 슬롯으로 옮긴다.
        loadout.equippedWeaponIds[targetSlotIndex] =
            sourceWeaponId;

        // 교환된 장착 정보를 JSON 파일에 즉시 저장한다.
        SaveToFile();
    }

    /// <summary>
    /// 지정한 캐릭터의 무기 슬롯을 비운다.
    /// </summary>
    /// <param name="characterId">무기를 해제할 캐릭터 ID</param>
    /// <param name="slotIndex">비울 슬롯 번호: 0, 1, 2</param>
    public void UnequipWeapon(string characterId, int slotIndex)
    {
        // 캐릭터의 장착 정보를 가져온다.
        CharacterLoadoutSaveData loadout = GetOrCreateCharacterLoadout(characterId);

        // 슬롯 번호가 범위를 벗어나면 코드 오류다.
        if (slotIndex < 0 || slotIndex >= loadout.equippedWeaponIds.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "무기 슬롯 번호가 범위를 벗어났습니다.");
        }

        // null을 넣어 해당 슬롯을 장착 해제 상태로 만든다.
        loadout.equippedWeaponIds[slotIndex] = null;

        // 장착 정보가 변경됐으므로 로컬 파일에 즉시 저장한다.
        SaveToFile();
    }

    /// <summary>
    /// 저장된 캐릭터의 현재 레벨을 반환한다.
    /// 아직 저장 데이터가 없는 캐릭터는 기본 레벨인 1을 반환한다.
    /// </summary>
    /// <param name="characterId">레벨을 확인할 캐릭터의 고유 ID</param>
    public int GetCharacterLevel(string characterId)
    {
        // 빈 캐릭터 ID로는 저장 데이터를 찾을 수 없다.
        if (string.IsNullOrWhiteSpace(characterId))
        {
            throw new ArgumentException(
                "레벨을 확인할 캐릭터 ID가 비어 있습니다.",
                nameof(characterId));
        }

        // 저장된 캐릭터 데이터를 처음부터 확인한다.
        for (int index = 0;
             index < _playerSaveData.characterLoadouts.Count;
             index++)
        {
            CharacterLoadoutSaveData loadout =
                _playerSaveData.characterLoadouts[index];

            // 비어 있는 데이터는 검사하지 않는다.
            if (loadout == null)
            {
                continue;
            }

            // 찾고 있는 캐릭터가 아니면 다음 데이터로 넘어간다.
            if (loadout.characterId != characterId)
            {
                continue;
            }

            // 이전 세이브에서 잘못된 값이 들어왔더라도
            // 캐릭터의 최소 레벨은 1로 처리한다.
            return Mathf.Max(1, loadout.level);
        }

        // 아직 저장 데이터가 생성되지 않은 캐릭터는
        // 게임의 기본 시작 레벨인 1로 처리한다.
        return 1;
    }

    /// <summary>
    /// 업적 ID에 해당하는 저장 기록을 반환한다.
    /// 아직 기록이 없다면 미달성 상태의 새 기록을 생성한다.
    /// </summary>
    /// <param name="achievementId">AchievementData에 설정한 고유 ID</param>
    public AchievementSaveData GetOrCreateAchievementRecord(
        string achievementId)
    {
        // 빈 ID는 정상적인 업적을 나타낼 수 없다.
        if (string.IsNullOrWhiteSpace(achievementId))
        {
            throw new ArgumentException(
                "업적 ID가 비어 있습니다.",
                nameof(achievementId));
        }

        // 이전 세이브에서도 업적 목록을 안전하게 사용할 수 있게 한다.
        NormalizeAchievementRecords(_playerSaveData);

        // 기존 업적 기록에서 동일한 ID를 찾는다.
        for (int index = 0;
             index < _playerSaveData.achievementRecords.Count;
             index++)
        {
            AchievementSaveData achievementRecord =
                _playerSaveData.achievementRecords[index];

            // 비어 있는 기록은 검사하지 않는다.
            if (achievementRecord == null)
            {
                continue;
            }

            // 동일한 ID의 기록을 찾으면 기존 데이터를 반환한다.
            if (achievementRecord.achievementId == achievementId)
            {
                return achievementRecord;
            }
        }

        // 아직 저장 기록이 없는 업적은 미달성 상태로 생성한다.
        AchievementSaveData newRecord =
            new AchievementSaveData
            {
                achievementId = achievementId,
                isCompleted = false,
                isRewardClaimed = false
            };

        // 새 업적 기록을 플레이어 세이브 데이터에 추가한다.
        _playerSaveData.achievementRecords.Add(newRecord);

        // 새 기록이 생성됐으므로 즉시 파일에 저장한다.
        SaveToFile();

        return newRecord;
    }

    /// <summary>
    /// 지정한 업적을 달성 상태로 변경하고 즉시 저장한다.
    /// 이미 달성한 업적은 중복 처리하지 않는다.
    /// </summary>
    /// <param name="achievementId">달성 처리할 업적의 고유 ID</param>
    /// <returns>이번 호출로 새롭게 달성됐다면 true</returns>
    public bool TryCompleteAchievement(
        string achievementId)
    {
        // 기존 기록을 가져오거나 미달성 기록을 새로 만든다.
        AchievementSaveData achievementRecord =
            GetOrCreateAchievementRecord(achievementId);

        // 이미 달성한 업적은 다시 저장하거나 처리하지 않는다.
        if (achievementRecord.isCompleted)
        {
            return false;
        }

        // 업적을 달성 상태로 변경한다.
        achievementRecord.isCompleted = true;

        // 달성만 했을 뿐 보상은 아직 받지 않은 상태다.
        achievementRecord.isRewardClaimed = false;

        // 업적 달성 상태를 즉시 JSON 파일에 저장한다.
        SaveToFile();

        Debug.Log(
            $"업적 달성: {achievementId}");

        return true;
    }

    /// <summary>
    /// 달성한 업적의 보상을 지급하고 수령 완료 상태로 저장한다.
    /// 조건 미달성 업적이나 이미 보상을 받은 업적은 처리하지 않는다.
    /// </summary>
    /// <param name="achievementData">보상을 지급할 업적 Config</param>
    /// <returns>이번 호출에서 보상 지급에 성공했다면 true</returns>
    public bool TryClaimAchievementReward(
        AchievementData achievementData)
    {
        // Config가 없으면 보상 종류와 수량을 확인할 수 없다.
        if (achievementData == null)
        {
            throw new ArgumentNullException(
                nameof(achievementData));
        }

        // 해당 업적의 플레이어 저장 기록을 가져온다.
        AchievementSaveData achievementRecord =
            GetOrCreateAchievementRecord(
                achievementData.Id);

        // 아직 조건을 달성하지 않은 업적은 보상을 받을 수 없다.
        if (!achievementRecord.isCompleted)
        {
            return false;
        }

        // 이미 보상을 받은 업적은 다시 지급하지 않는다.
        if (achievementRecord.isRewardClaimed)
        {
            return false;
        }

        // Config에 설정된 보상 수량이 올바른지 확인한다.
        if (achievementData.RewardValue <= 0)
        {
            throw new InvalidOperationException(
                $"업적 '{achievementData.Id}'의 보상 수량은 " +
                "0보다 커야 합니다.");
        }

        // 보상 종류에 맞는 플레이어 데이터를 변경한다.
        switch (achievementData.RewardType)
        {
            case AchievementRewardType.Gold:

                // 골드 보상만큼 현재 보유 골드를 증가시킨다.
                _playerSaveData.gold +=
                    achievementData.RewardValue;
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(achievementData.RewardType),
                    achievementData.RewardType,
                    "지원하지 않는 업적 보상 종류입니다.");
        }

        // 같은 보상을 다시 받을 수 없도록 수령 완료 처리한다.
        achievementRecord.isRewardClaimed = true;

        // 골드와 보상 수령 상태를 한 번에 저장한다.
        // 둘을 따로 저장하면 한쪽만 저장되는 문제가 생길 수 있다.
        SaveToFile();

        // 보상 종류가 골드라면 골드 UI에 변경된 총량을 알린다.
        if (achievementData.RewardType ==
            AchievementRewardType.Gold)
        {
            GoldChanged?.Invoke(
                _playerSaveData.gold);
        }

        Debug.Log(
            $"업적 보상 수령: {achievementData.Id}, " +
            $"{achievementData.RewardType} " +
            $"{achievementData.RewardValue}");

        return true;
    }

    /// <summary>
    /// 현재 총 골드를 증가시키고 JSON과 UI에 반영한다.
    /// </summary>
    public void AddGold(int amount)
    {
        // 0 이하의 값은 정상적인 골드 획득량이 아니다.
        if (amount <= 0)
        {
            return;
        }

        // 메모리에 로드된 플레이어 총 골드를 증가시킨다.
        _playerSaveData.gold += amount;

        // 변경된 총 골드를 JSON 파일에 저장한다.
        SaveToFile();

        // 현재 총 골드가 변경됐다고 UI에 알린다.
        GoldChanged?.Invoke(_playerSaveData.gold);
    }

    /// <summary>
    /// 테스트용 ContextMenu를 현재 안전하게 실행할 수 있는지 확인한다.
    /// SaveManager의 저장 경로가 준비된 Play Mode에서만 실행한다.
    /// </summary>
    private bool CanRunTestContextMenu()
    {
        // Edit Mode에서는 Awake가 실행되지 않아 저장 경로와 데이터가
        // 준비되지 않았을 수 있으므로 실행하지 않는다.
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "SaveManager 테스트 메뉴는 Play Mode에서 실행해주세요.");

            return false;
        }

        return true;
    }

    /// <summary>
    /// 테스트를 위해 보유 골드를 0으로 초기화한다.
    /// </summary>
    [ContextMenu("테스트 1. 보유 골드 초기화")]
    private void ResetGoldForTest()
    {
        // Play Mode가 아니라면 저장 데이터를 변경하지 않는다.
        if (!CanRunTestContextMenu())
        {
            return;
        }

        // 현재 보유 골드를 초기값인 0으로 변경한다.
        _playerSaveData.gold = 0;

        // 변경된 골드를 JSON 저장 파일에 반영한다.
        SaveToFile();

        // 골드를 구독 중인 UI가 있다면 즉시 변경 사실을 알린다.
        GoldChanged?.Invoke(_playerSaveData.gold);

        Debug.Log("테스트: 보유 골드를 0으로 초기화했습니다.");
    }

    /// <summary>
    /// 테스트를 위해 Soldier의 레벨과 경험치를 초기화한다.
    /// </summary>
    [ContextMenu("테스트 2. Soldier 레벨 1 초기화")]
    private void ResetSoldierLevelForTest()
    {
        // Soldier의 레벨을 1, 경험치를 0으로 초기화한다.
        ResetCharacterLevelForTest(SoldierCharacterId);
    }

    /// <summary>
    /// 테스트를 위해 Warrior의 레벨과 경험치를 초기화한다.
    /// </summary>
    [ContextMenu("테스트 3. Warrior 레벨 1 초기화")]
    private void ResetWarriorLevelForTest()
    {
        // Warrior의 레벨을 1, 경험치를 0으로 초기화한다.
        ResetCharacterLevelForTest(WarriorCharacterId);
    }

    /// <summary>
    /// 테스트를 위해 Soldier의 현재 레벨을 1 증가시킨다.
    /// </summary>
    [ContextMenu("테스트 4. Soldier 레벨 1 증가")]
    private void AddSoldierLevelForTest()
    {
        // Soldier의 현재 경험치는 유지하고 레벨만 1 증가시킨다.
        AddCharacterLevelForTest(SoldierCharacterId);
    }

    /// <summary>
    /// 테스트를 위해 Warrior의 현재 레벨을 1 증가시킨다.
    /// </summary>
    [ContextMenu("테스트 5. Warrior 레벨 1 증가")]
    private void AddWarriorLevelForTest()
    {
        // Warrior의 현재 경험치는 유지하고 레벨만 1 증가시킨다.
        AddCharacterLevelForTest(WarriorCharacterId);
    }

    /// <summary>
    /// 테스트를 위해 보유 골드를 1,000 증가시킨다.
    /// </summary>
    [ContextMenu("테스트 6. 골드 1000 증가")]
    private void AddGoldForTest()
    {
        // Play Mode가 아니라면 저장 데이터를 변경하지 않는다.
        if (!CanRunTestContextMenu())
        {
            return;
        }

        // 기존 골드 증가 함수를 재사용한다.
        // 내부에서 저장과 GoldChanged 이벤트 발생까지 처리한다.
        AddGold(1000);

        Debug.Log(
            $"테스트: 보유 골드를 1,000 추가했습니다. 현재 골드: {_playerSaveData.gold}");
    }

    /// <summary>
    /// 지정한 캐릭터의 레벨을 1, 경험치를 0으로 초기화한다.
    /// </summary>
    private void ResetCharacterLevelForTest(string characterId)
    {
        // Play Mode가 아니라면 저장 데이터를 변경하지 않는다.
        if (!CanRunTestContextMenu())
        {
            return;
        }

        // 캐릭터의 기존 저장 데이터를 가져온다.
        // 아직 없다면 새로운 저장 데이터를 생성한다.
        CharacterLoadoutSaveData loadout =
            GetOrCreateCharacterLoadout(characterId);

        // 완전히 동일한 레벨 테스트 조건을 만들기 위해
        // 레벨뿐 아니라 현재 경험치도 초기화한다.
        loadout.level = 1;
        loadout.currentExp = 0;

        // 변경된 캐릭터 데이터를 저장 파일에 반영한다.
        SaveToFile();

        Debug.Log(
            $"테스트: {characterId}의 레벨을 1, 경험치를 0으로 초기화했습니다.");
    }

    /// <summary>
    /// 지정한 캐릭터의 현재 레벨을 1 증가시킨다.
    /// </summary>
    private void AddCharacterLevelForTest(string characterId)
    {
        // Play Mode가 아니라면 저장 데이터를 변경하지 않는다.
        if (!CanRunTestContextMenu())
        {
            return;
        }

        // 캐릭터의 기존 저장 데이터를 가져온다.
        // 아직 없다면 새로운 저장 데이터를 생성한다.
        CharacterLoadoutSaveData loadout =
            GetOrCreateCharacterLoadout(characterId);

        // 잠금 해제 테스트를 위해 현재 레벨을 1 증가시킨다.
        loadout.level++;

        // 변경된 캐릭터 데이터를 저장 파일에 반영한다.
        SaveToFile();

        Debug.Log(
            $"테스트: {characterId}의 레벨을 증가시켰습니다. 현재 레벨: {loadout.level}");
    }

    /// <summary>
    /// 인스펙터에서 선택한 스테이지의 최단 클리어 기록만 초기화한다.
    /// </summary>
    [ContextMenu("테스트 7. 선택한 스테이지 클리어 기록 초기화")]
    private void ResetSelectedStageBestClearTimeForTest()
    {
        // 세이브 데이터가 준비된 Play Mode에서만 실행한다.
        if (!CanRunTestContextMenu())
        {
            return;
        }

        // Stage1은 0, Stage2는 1로 설정되어 있으므로
        // 선택한 열거형 값을 세이브 데이터의 스테이지 인덱스로 사용한다.
        int stageIndex = (int)_resetTargetStage;

        // 선택한 스테이지의 기록만 찾아서 제거하고 저장한다.
        ResetStageBestClearTimeForTest(stageIndex);
    }

    /// <summary>
    /// 지정한 스테이지의 최단 클리어 기록을 세이브 데이터에서 제거한다.
    /// </summary>
    /// <param name="stageIndex">초기화할 스테이지의 0부터 시작하는 인덱스</param>
    private void ResetStageBestClearTimeForTest(int stageIndex)
    {
        // 음수 인덱스는 실제 스테이지를 나타낼 수 없다.
        if (stageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stageIndex),
                stageIndex,
                "스테이지 인덱스는 0 이상이어야 합니다.");
        }

        // 이전 버전의 세이브 파일에서도 목록이 null이 되지 않도록 보정한다.
        NormalizeStageClearRecords(_playerSaveData);

        bool removedRecord = false;

        // 제거하면서 목록 순서가 당겨져도 안전하도록 뒤에서부터 확인한다.
        for (int index =
                 _playerSaveData.stageClearRecords.Count - 1;
             index >= 0;
             index--)
        {
            StageClearSaveData record =
                _playerSaveData.stageClearRecords[index];

            // 비어 있는 기록이거나 다른 스테이지의 기록이면 건너뛴다.
            if (record == null ||
                record.stageIndex != stageIndex)
            {
                continue;
            }

            // 해당 스테이지의 기록을 목록에서 완전히 제거한다.
            _playerSaveData.stageClearRecords.RemoveAt(index);
            removedRecord = true;
        }

        // 삭제된 기록이 없다면 파일을 다시 저장할 필요가 없다.
        if (!removedRecord)
        {
            Debug.Log(
                $"Stage {stageIndex + 1}에 초기화할 클리어 기록이 없습니다.");

            return;
        }

        // 기록이 제거된 현재 세이브 데이터를 파일에 반영한다.
        SaveToFile();

        Debug.Log(
            $"Stage {stageIndex + 1} 클리어 기록을 초기화했습니다.");
    }

    /// <summary>
    /// 이전 버전의 저장 데이터도 현재 장비 데이터 형식에 맞게 보정한다.
    /// </summary>
    private void NormalizeLoadout(CharacterLoadoutSaveData loadout)
    {
        // 이전 저장 파일에는 보유 무기 목록이 없을 수 있다.
        if (loadout.ownedWeaponIds == null)
        {
            loadout.ownedWeaponIds = new List<string>();
        }

        // 장착 배열이 없거나 크기가 다르면 기존 값을 보존하며 3칸으로 만든다.
        if (loadout.equippedWeaponIds == null ||
            loadout.equippedWeaponIds.Length != 3)
        {
            string[] normalizedWeaponIds = new string[3];

            if (loadout.equippedWeaponIds != null)
            {
                int copyCount = Mathf.Min(
                    loadout.equippedWeaponIds.Length,
                    normalizedWeaponIds.Length);

                for (int i = 0; i < copyCount; i++)
                {
                    normalizedWeaponIds[i] =
                        loadout.equippedWeaponIds[i];
                }
            }

            loadout.equippedWeaponIds = normalizedWeaponIds;
        }

        // 이미 장착된 무기는 반드시 보유 중인 무기로 취급한다.
        for (int i = 0; i < loadout.equippedWeaponIds.Length; i++)
        {
            string equippedWeaponId =
                loadout.equippedWeaponIds[i];

            if (string.IsNullOrWhiteSpace(equippedWeaponId))
            {
                continue;
            }

            if (!loadout.ownedWeaponIds.Contains(equippedWeaponId))
            {
                loadout.ownedWeaponIds.Add(equippedWeaponId);
            }
        }
    }

    /// <summary>
    /// 스테이지 클리어 기록 목록이 항상 사용 가능한 상태인지 확인한다.
    /// </summary>
    private void NormalizeStageClearRecords(
        PlayerSaveData saveData)
    {
        if (saveData.stageClearRecords == null)
        {
            saveData.stageClearRecords =
                new List<StageClearSaveData>();
        }
    }

    /// <summary>
    /// 해당 스테이지의 최단 클리어 시간을 반환한다.
    /// 클리어 기록이 없으면 -1을 반환한다.
    /// </summary>
    public float GetBestClearTime(int stageIndex)
    {
        if (stageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stageIndex),
                stageIndex,
                "스테이지 인덱스는 0 이상이어야 합니다.");
        }

        NormalizeStageClearRecords(
            _playerSaveData);

        for (int index = 0;
             index < _playerSaveData.stageClearRecords.Count;
             index++)
        {
            StageClearSaveData record =
                _playerSaveData.stageClearRecords[index];

            // 비어 있는 기록은 건너뛴다.
            if (record == null)
            {
                continue;
            }

            if (record.stageIndex == stageIndex)
            {
                return record.bestClearTime;
            }
        }

        // 아직 해당 스테이지를 클리어한 적이 없다.
        return -1f;
    }

    /// <summary>
    /// 기존 기록보다 빠른 경우에만 최단 클리어 시간을 저장한다.
    /// 기록이 갱신됐으면 true를 반환한다.
    /// </summary>
    public bool TryUpdateBestClearTime(
        int stageIndex,
        float clearTime)
    {
        if (stageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stageIndex),
                stageIndex,
                "스테이지 인덱스는 0 이상이어야 합니다.");
        }

        if (clearTime <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(clearTime),
                clearTime,
                "클리어 시간은 0보다 커야 합니다.");
        }

        NormalizeStageClearRecords(
            _playerSaveData);

        for (int index = 0;
             index < _playerSaveData.stageClearRecords.Count;
             index++)
        {
            StageClearSaveData record =
                _playerSaveData.stageClearRecords[index];

            if (record == null ||
                record.stageIndex != stageIndex)
            {
                continue;
            }

            // 기존 정상 기록보다 느리거나 같으면 갱신하지 않는다.
            if (record.bestClearTime > 0f &&
                clearTime >= record.bestClearTime)
            {
                return false;
            }

            // 기존 기록보다 빠르므로 최단 시간을 교체한다.
            record.bestClearTime = clearTime;

            SaveToFile();

            Debug.Log(
                $"Stage {stageIndex + 1} 최단 기록 갱신: {clearTime:F2}초");

            return true;
        }

        // 첫 클리어라면 새로운 스테이지 기록을 만든다.
        StageClearSaveData newRecord =
            new StageClearSaveData
            {
                stageIndex = stageIndex,
                bestClearTime = clearTime
            };

        _playerSaveData.stageClearRecords.Add(
            newRecord);

        SaveToFile();

        Debug.Log(
            $"Stage {stageIndex + 1} 첫 클리어 기록 저장: {clearTime:F2}초");

        return true;
    }

    /// <summary>
    /// 이전 버전 세이브 파일에서도 업적 기록 목록을
    /// 안전하게 사용할 수 있도록 빈 목록을 생성한다.
    /// </summary>
    private void NormalizeAchievementRecords(
        PlayerSaveData saveData)
    {
        // 기존 세이브 파일에는 업적 목록이 없을 수 있다.
        if (saveData.achievementRecords == null)
        {
            saveData.achievementRecords =
                new List<AchievementSaveData>();
        }
    }

}
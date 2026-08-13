using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 플레이어의 세이브 데이터를 메모리에서 관리한다.
/// 캐릭터별 장착 무기 정보를 저장한다.
/// 실제 파일 저장과 불러오기는 이후 단계에서 추가한다.
/// </summary>
public class SaveManager : MonoBehaviour
{
    // 다른 씬에서도 SaveManager에 접근하기 위한 현재 인스턴스
    public static SaveManager Instance { get; private set; }

    // 로컬에 생성할 세이브 파일 이름
    const string SaveFileName = "player_save.json";

    // 실행 중 사용할 세이브 파일의 전체 경로
    string _savePath;

    [Header("현재 플레이어 세이브 데이터")]
    [SerializeField] PlayerSaveData _playerSaveData = new();

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
                return loadout;
            }
        }

        // 해당 캐릭터의 장착 정보가 아직 없으므로 새로 만든다.
        CharacterLoadoutSaveData newLoadout = new CharacterLoadoutSaveData();

        // 새 장착 정보가 어떤 캐릭터의 것인지 ID를 저장한다.
        newLoadout.characterId = characterId;

        // 플레이어의 캐릭터별 장착 정보 목록에 추가한다.
        _playerSaveData.characterLoadouts.Add(newLoadout);

        // 새 캐릭터의 장착 정보가 만들어졌으므로 게임을 종료해도 유지되도록 로컬 파일에 저장한다.
        SaveToFile();

        // 생성한 장착 정보를 호출한 곳에 반환한다.
        return newLoadout;
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

    public void AddGold(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        _playerSaveData.gold += amount;
        SaveToFile();
    }



}
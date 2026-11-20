using System;
using UnityEngine;

/// <summary>
/// EquipmentManage 씬에서 선택한 캐릭터의 장비 정보를 관리한다.
/// 처리 순서:
/// 1. GameSessionManager에서 선택한 캐릭터 ID를 가져온다.
/// 2. CharacterDatabase에서 해당 CharacterData를 찾는다.
/// 3. SaveManager에서 해당 캐릭터의 장착정보를 가져온다.
/// </summary>
public class EquipmentManager : MonoBehaviour
{
    [Header("캐릭터 데이터")]
    [Tooltip("선택한 캐릭터 ID를 CharacterData로 변환할 데이터베이스")]
    [SerializeField] CharacterDatabase _characterDatabase;

    [Header("장비 화면 UI")]
    [SerializeField]
    EquipmentManageView _view; // EquipmentManage 화면 표시를 담당하는 View

    // EquipmentCharacterSelect에서 선택한 캐릭터의 원본 데이터
    CharacterData _selectedCharacterData;

    // JSON에서 불러온 선택 캐릭터의 장착정보
    CharacterLoadoutSaveData _selectedCharacterLoadout;

    // 장비 화면의 데이터와 장비 규칙을 담당하는 Model
    EquipmentModel _model;

    // Model과 View를 연결하고 화면 흐름을 제어하는 Presenter
    EquipmentPresenter _presenter;

    // 다른 캐릭터의 장비를 관리하기 위해 캐릭터 선택 화면으로 돌아간다.
    public event Action CharacterSelectionRequested;

    // 화면을 전환하는 동안 뒤로 가기 중복 요청을 막는다.
    private bool _isLeaving;

    /// <summary>
    /// 현재 장비를 편집하고 있는 캐릭터의 원본 데이터.
    /// 이후 캐릭터 이름, 아이콘, 사용 가능한 무기를 UI에 표시할 때 사용한다.
    /// </summary>
    public CharacterData SelectedCharacterData =>
        _selectedCharacterData;

    /// <summary>
    /// 현재 캐릭터의 저장된 장착정보.
    /// 이후 Slot 1~3의 아이콘을 표시할 때 사용한다.
    /// </summary>
    public CharacterLoadoutSaveData SelectedCharacterLoadout =>
        _selectedCharacterLoadout;

    /// <summary>
    /// 장비 화면에서 사용하는 Model을 외부에서 읽을 수 있게 반환한다.
    /// </summary>
    public EquipmentModel Model => _model;

    private void Start()
    {
        InitializeSelectedCharacter();
    }

    /// <summary>
    /// 아무 UI도 선택되지 않은 최초 상태에서
    /// 방향키 입력이 들어오면 첫 번째 무기 슬롯을 선택한다.
    /// </summary>
    private void Update()
    {
        // 방향키 입력이 없다면 선택 처리를 하지 않는다.
        if (!IsAnyArrowKeyDown())
        {
            return;
        }

        // 현재 선택된 UI가 없을 때만 첫 번째 무기 슬롯을 선택한다.
        _view.TrySelectFirstWeaponSlot();
    }

    /// <summary>
    /// 키보드의 상하좌우 방향키 중 하나가 눌렸는지 확인한다.
    /// </summary>
    private bool IsAnyArrowKeyDown()
    {
        // 네 방향키 중 하나라도 이번 프레임에 눌렸다면 true를 반환한다.
        return Input.GetKeyDown(KeyCode.UpArrow)
            || Input.GetKeyDown(KeyCode.DownArrow)
            || Input.GetKeyDown(KeyCode.LeftArrow)
            || Input.GetKeyDown(KeyCode.RightArrow);
    }

    /// <summary>
    /// 캐릭터 선택 화면에서 전달된 캐릭터 ID를 이용해
    /// 캐릭터 원본 데이터와 JSON 장착정보를 준비한다.
    /// </summary>
    private void InitializeSelectedCharacter()
    {
        // Bootstrap 씬을 거치지 않았다면 세션 정보가 존재하지 않는다.
        if (GameSessionManager.Instance == null)
        {
            throw new InvalidOperationException(
                "GameSessionManager가 없습니다. Bootstrap 씬부터 실행해야 합니다.");
        }

        // Bootstrap 씬을 거치지 않았다면 세이브 데이터가 존재하지 않는다.
        if (SaveManager.Instance == null)
        {
            throw new InvalidOperationException(
                "SaveManager가 없습니다. Bootstrap 씬부터 실행해야 합니다.");
        }

        // 캐릭터 ID를 CharacterData로 변환할 데이터베이스가 필요하다.
        if (_characterDatabase == null)
        {
            throw new InvalidOperationException(
                "EquipmentManager에 CharacterDatabase가 연결되지 않았습니다.");
        }

        // EquipmentManage View가 연결되지 않았다면 UI를 표시할 수 없다.
        if (_view == null)
        {
            throw new InvalidOperationException(
                "EquipmentManager에 EquipmentManageView가 연결되지 않았습니다.");
        }

        // EquipmentCharacterSelect에서 선택한 캐릭터 ID를 가져온다.
        string selectedCharacterId =
            GameSessionManager.Instance.SelectedCharacterId;

        // 캐릭터를 선택하지 않고 EquipmentManage로 들어온 경우다.
        if (string.IsNullOrWhiteSpace(selectedCharacterId))
        {
            throw new InvalidOperationException(
                "장비를 편집할 캐릭터가 선택되지 않았습니다.");
        }

        // 선택한 ID에 맞는 CharacterData를 가져온다.
        _selectedCharacterData =
            _characterDatabase.GetCharacterData(selectedCharacterId);

        // 선택한 캐릭터의 JSON 장비 저장 데이터를 먼저 가져온다.
        _selectedCharacterLoadout =
            SaveManager.Instance.GetOrCreateCharacterLoadout(
                selectedCharacterId);

        // 캐릭터 원본 데이터와 저장 데이터를 이용해 Model을 생성한다.
        _model = new EquipmentModel(
            _selectedCharacterData,
            _selectedCharacterLoadout,
            SaveManager.Instance);

        // 생성한 Model과 Inspector에서 연결한 View를 이용해 Presenter를 생성한다.
        _presenter = new EquipmentPresenter(
            _model,
            _view);

        // 캐릭터, 레벨, 골드, 현재 장착 무기를 처음 화면에 표시한다.
        _presenter.Initialize();

        Debug.Log(
                    $"장비 관리 캐릭터를 불러왔습니다: " +
            $"{_selectedCharacterData.CharacterName}");
    }

    // Town을 유지한 상태로 창 안의 화면만 캐릭터 선택으로 바꾼다.
    public void BackToCharacterSelect()
    {
        if (_isLeaving)
        {
            return;
        }

        GameSessionManager session = GameSessionManager.Instance;

        if (session == null || CharacterSelectionRequested == null)
        {
            Debug.LogError(
                "세션 매니저와 캐릭터 선택 복귀 이벤트를 확인해주세요.",
                this);
            return;
        }

        // 구매·장착 저장 데이터는 유지하고, 새로 선택할 캐릭터 정보만 초기화한다.
        session.BeginCharacterSelection(
            CharacterSelectPurpose.LoadoutEdit);

        _isLeaving = true;
        GameAudio.Play(GameSound.UiBack);
        CharacterSelectionRequested.Invoke();
    }
}
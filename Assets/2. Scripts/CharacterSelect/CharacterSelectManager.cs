using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[Serializable]
public class CharacterSelectRuntimeUI
{
    [Header("현재 레벨 텍스트")]

    [Tooltip("0번: 선택되지 않은 버튼의 레벨 텍스트 / 1번: 선택된 버튼의 레벨 텍스트")]
    public TMP_Text[] currentLevelTexts = new TMP_Text[2];


    [Header("선택되지 않은 상태의 무기 아이콘")]

    [Tooltip("현재 장착 중인 무기 3개의 아이콘")]
    public Image[] unselectedWeaponIcons = new Image[3];


    [Header("선택된 상태의 무기 아이콘")]

    [Tooltip("현재 장착 중인 무기 3개의 아이콘")]
    public Image[] selectedWeaponIcons = new Image[3];
}

public class CharacterSelectManager : MonoBehaviour
{
    [Header("데이터베이스")]

    [Tooltip("캐릭터 데이터베이스")]
    [SerializeField]
    private CharacterDatabase _characterDatabase;

    [Tooltip("무기 데이터베이스")]
    [SerializeField]
    private WeaponDatabase _weaponDatabase;


    [Header("캐릭터 버튼")]

    [Tooltip("캐릭터 선택 버튼 배열")]
    [SerializeField]
    private Button[] _characterButtons;


    [Header("선택 연출 이미지")]

    [Tooltip("솔져 선택 연출 이미지")]
    [SerializeField]
    private Image _soldierSelectImage;

    [Tooltip("두 번째 캐릭터 선택 연출 이미지")]
    [SerializeField]
    private Image _warriorSelectImage;


    [Header("캐릭터 저장 데이터 UI")]

    [Tooltip("CharacterDatabase와 같은 순서로 등록한다. 0번 솔져, 1번 두 번째 캐릭터")]
    [SerializeField]
    private CharacterSelectRuntimeUI[] _characterRuntimeUIs;


    // 현재 선택된 캐릭터의 인덱스
    private int _selectedCharacterIndex = -1;


    private void Start()
    {
        // 처음에는 선택된 캐릭터가 없으므로 두 선택 이미지를 모두 끈다.
        UpdateSelectEffects();

        // 저장된 캐릭터 레벨과 장착 무기 아이콘을 표시한다.
        RefreshCharacterRuntimeUI();
    }


    private void LateUpdate()
    {
        // EventSystem이 없으면 UI 선택을 처리할 수 없다.
        if (EventSystem.current == null)
        {
            return;
        }

        // 현재 EventSystem이 선택하고 있는 UI 오브젝트를 가져온다.
        GameObject selectedObject =
            EventSystem.current.currentSelectedGameObject;

        // 아직 선택된 UI가 없는 상태
        if (selectedObject == null)
        {
            // 첫 방향키 입력을 받으면 Soldier 버튼을 기본 선택한다.
            if (IsAnyArrowKeyDown())
            {
                // EventSystem의 현재 선택을 Soldier 버튼으로 변경한다.
                _characterButtons[0].Select();

                // 현재 캐릭터 인덱스를 Soldier의 0번으로 저장한다.
                SelectingCharacter(0);
            }

            return;
        }

        // 현재 선택된 버튼이 어떤 캐릭터 버튼인지 확인한다.
        for (int i = 0; i < _characterButtons.Length; i++)
        {
            if (selectedObject == _characterButtons[i].gameObject)
            {
                // 현재 선택된 캐릭터 인덱스를 저장한다.
                _selectedCharacterIndex = i;

                // 선택된 캐릭터 프레임만 활성화한다.
                UpdateSelectEffects();

                break;
            }
        }
    }

    /// <summary>
    /// 캐릭터 선택 화면에서 사용하는 방향키 입력을 확인한다.
    /// </summary>
    private bool IsAnyArrowKeyDown()
    {
        return Input.GetKeyDown(KeyCode.UpArrow)
            || Input.GetKeyDown(KeyCode.DownArrow)
            || Input.GetKeyDown(KeyCode.LeftArrow)
            || Input.GetKeyDown(KeyCode.RightArrow);
    }

    /// <summary>
    /// UI에서 현재 선택한 캐릭터 인덱스를 저장한다.
    /// Soldier는 0, Warrior는 1을 전달한다.
    /// </summary>
    public void SelectingCharacter(int index)
    {
        // CharacterDatabase 범위를 벗어난 값이면 잘못된 버튼 연결이다.
        if (index < 0 || index >= _characterDatabase.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index),
                index,
                "선택한 캐릭터 인덱스가 CharacterDatabase 범위를 벗어났습니다.");
        }

        // 현재 선택한 캐릭터 인덱스를 저장한다.
        _selectedCharacterIndex = index;

        // 선택된 캐릭터에 맞춰 선택 이미지를 갱신한다.
        UpdateSelectEffects();
    }


    /// <summary>
    /// 저장된 캐릭터 레벨과 장착 무기 정보를
    /// 캐릭터 선택 화면 UI에 표시한다.
    /// </summary>
    private void RefreshCharacterRuntimeUI()
    {
        // SaveManager가 씬에 존재하지 않으면 저장 데이터를 불러올 수 없다.
        if (SaveManager.Instance == null)
        {
            throw new Exception(
                "[CharacterSelectManager] SaveManager.Instance가 존재하지 않습니다.");
        }

        // 캐릭터 데이터베이스가 연결되어 있는지 확인한다.
        if (_characterDatabase == null)
        {
            throw new Exception(
                "[CharacterSelectManager] CharacterDatabase가 연결되지 않았습니다.");
        }

        // 무기 데이터베이스가 연결되어 있는지 확인한다.
        if (_weaponDatabase == null)
        {
            throw new Exception(
                "[CharacterSelectManager] WeaponDatabase가 연결되지 않았습니다.");
        }

        // 캐릭터별 UI 배열이 연결되어 있는지 확인한다.
        if (_characterRuntimeUIs == null)
        {
            throw new Exception(
                "[CharacterSelectManager] Character Runtime UIs가 연결되지 않았습니다.");
        }

        // 캐릭터 데이터의 개수와 UI 배열의 개수가 같은지 확인한다.
        if (_characterRuntimeUIs.Length != _characterDatabase.Count)
        {
            throw new Exception(
                $"[CharacterSelectManager] 캐릭터 데이터 개수({_characterDatabase.Count})와 " +
                $"Character Runtime UIs 개수({_characterRuntimeUIs.Length})가 다릅니다.");
        }

        // 빈 무기 슬롯에 기본 무기를 넣었는지 확인하기 위한 변수다.
        bool saveDataChanged = false;

        // CharacterDatabase에 등록된 캐릭터 순서대로 UI를 갱신한다.
        for (int characterIndex = 0;
             characterIndex < _characterDatabase.Count;
             characterIndex++)
        {
            // 현재 순서의 캐릭터 데이터를 가져온다.
            CharacterData characterData =
                _characterDatabase.GetCharacterDataAt(characterIndex);

            // 현재 순서의 캐릭터 UI 묶음을 가져온다.
            CharacterSelectRuntimeUI runtimeUI =
                _characterRuntimeUIs[characterIndex];

            // 해당 캐릭터 UI 묶음이 생성되어 있는지 확인한다.
            if (runtimeUI == null)
            {
                throw new Exception(
                    $"[CharacterSelectManager] Character Runtime UIs의 " +
                    $"{characterIndex}번 Element가 비어 있습니다.");
            }

            // 캐릭터마다 레벨 텍스트는 선택 전/선택 후 총 2개여야 한다.
            if (runtimeUI.currentLevelTexts == null ||
                runtimeUI.currentLevelTexts.Length != 2)
            {
                throw new Exception(
                    $"[CharacterSelectManager] {characterData.name}의 " +
                    "Current Level Texts 배열 크기는 2여야 합니다.");
            }

            // 선택되지 않은 상태의 무기 아이콘은 총 3개여야 한다.
            if (runtimeUI.unselectedWeaponIcons == null ||
                runtimeUI.unselectedWeaponIcons.Length != 3)
            {
                throw new Exception(
                    $"[CharacterSelectManager] {characterData.name}의 " +
                    "Unselected Weapon Icons 배열 크기는 3이어야 합니다.");
            }

            // 선택된 상태의 무기 아이콘도 총 3개여야 한다.
            if (runtimeUI.selectedWeaponIcons == null ||
                runtimeUI.selectedWeaponIcons.Length != 3)
            {
                throw new Exception(
                    $"[CharacterSelectManager] {characterData.name}의 " +
                    "Selected Weapon Icons 배열 크기는 3이어야 합니다.");
            }

            // 캐릭터의 기본 장착 무기도 총 3개여야 한다.
            if (characterData.InitialEquippedWeapons == null ||
                characterData.InitialEquippedWeapons.Length != 3)
            {
                throw new Exception(
                    $"[CharacterSelectManager] {characterData.name}의 " +
                    "Initial Equipped Weapons 배열 크기는 3이어야 합니다.");
            }

            // 저장된 캐릭터의 레벨과 장착 무기 정보를 가져온다.
            // 저장 정보가 없다면 SaveManager가 기본 정보를 새로 생성한다.
            CharacterLoadoutSaveData loadout =
                SaveManager.Instance.GetOrCreateCharacterLoadout(
                    characterData.Id);

            // 저장된 장착 무기 ID 배열이 정상적인지 확인한다.
            if (loadout.equippedWeaponIds == null ||
                loadout.equippedWeaponIds.Length != 3)
            {
                // 배열이 없거나 크기가 다르면 3칸짜리 배열로 다시 만든다.
                loadout.equippedWeaponIds = new string[3];

                // 저장 데이터가 변경되었다고 표시한다.
                saveDataChanged = true;
            }


            // 선택되지 않은 버튼과 선택된 버튼의 레벨 텍스트를 모두 갱신한다.
            for (int textIndex = 0;
                 textIndex < runtimeUI.currentLevelTexts.Length;
                 textIndex++)
            {
                // Inspector에서 텍스트가 연결되지 않았는지 확인한다.
                if (runtimeUI.currentLevelTexts[textIndex] == null)
                {
                    throw new Exception(
                        $"[CharacterSelectManager] {characterData.name}의 " +
                        $"Current Level Texts {textIndex}번이 연결되지 않았습니다.");
                }

                // 저장된 해당 캐릭터의 현재 레벨을 표시한다.
                runtimeUI.currentLevelTexts[textIndex].text =
                    loadout.level.ToString();
            }


            // 캐릭터가 장착한 무기 3개를 순서대로 표시한다.
            for (int weaponSlotIndex = 0;
                 weaponSlotIndex < 3;
                 weaponSlotIndex++)
            {
                // 저장되어 있는 현재 슬롯의 무기 ID를 가져온다.
                string equippedWeaponId =
                    loadout.equippedWeaponIds[weaponSlotIndex];

                // 저장된 무기 ID가 비어 있다면 캐릭터의 기본 무기를 사용한다.
                if (string.IsNullOrWhiteSpace(equippedWeaponId))
                {
                    // 현재 슬롯에 설정된 기본 무기 데이터를 가져온다.
                    WeaponData initialWeapon =
                        characterData.InitialEquippedWeapons[weaponSlotIndex];

                    // 캐릭터의 기본 무기가 연결되지 않았다면 오류를 발생시킨다.
                    if (initialWeapon == null)
                    {
                        throw new Exception(
                            $"[CharacterSelectManager] {characterData.name}의 " +
                            $"Initial Equipped Weapons {weaponSlotIndex}번이 비어 있습니다.");
                    }

                    // 기본 무기의 ID를 현재 장착 무기 ID로 사용한다.
                    equippedWeaponId = initialWeapon.Id;

                    // 저장 데이터에도 해당 기본 무기 ID를 넣는다.
                    loadout.equippedWeaponIds[weaponSlotIndex] =
                        equippedWeaponId;

                    // 저장 데이터가 변경되었다고 표시한다.
                    saveDataChanged = true;
                }

                // 저장된 무기 ID를 이용해 실제 WeaponData를 찾는다.
                WeaponData equippedWeapon =
                    _weaponDatabase.GetWeaponData(equippedWeaponId);

                // 무기 데이터에 아이콘이 설정되어 있는지 확인한다.
                if (equippedWeapon.Icon == null)
                {
                    throw new Exception(
                        $"[CharacterSelectManager] 무기 {equippedWeapon.name}에 " +
                        "Icon이 설정되지 않았습니다.");
                }

                // 선택되지 않은 상태의 무기 아이콘 Image를 가져온다.
                Image unselectedWeaponImage =
                    runtimeUI.unselectedWeaponIcons[weaponSlotIndex];

                // 선택된 상태의 무기 아이콘 Image를 가져온다.
                Image selectedWeaponImage =
                    runtimeUI.selectedWeaponIcons[weaponSlotIndex];

                // 선택되지 않은 상태의 아이콘 Image가 연결되어 있는지 확인한다.
                if (unselectedWeaponImage == null)
                {
                    throw new Exception(
                        $"[CharacterSelectManager] {characterData.name}의 " +
                        $"Unselected Weapon Icons {weaponSlotIndex}번이 연결되지 않았습니다.");
                }

                // 선택된 상태의 아이콘 Image가 연결되어 있는지 확인한다.
                if (selectedWeaponImage == null)
                {
                    throw new Exception(
                        $"[CharacterSelectManager] {characterData.name}의 " +
                        $"Selected Weapon Icons {weaponSlotIndex}번이 연결되지 않았습니다.");
                }

                // 선택되지 않은 버튼 쪽에 장착 무기 아이콘을 표시한다.
                unselectedWeaponImage.sprite = equippedWeapon.Icon;

                // 선택되지 않은 버튼 쪽 아이콘의 이미지 비율을 유지한다.
                unselectedWeaponImage.preserveAspect = true;

                // 선택되지 않은 버튼 쪽 아이콘을 활성화한다.
                unselectedWeaponImage.enabled = true;


                // 선택된 버튼 쪽에도 동일한 장착 무기 아이콘을 표시한다.
                selectedWeaponImage.sprite = equippedWeapon.Icon;

                // 선택된 버튼 쪽 아이콘의 이미지 비율을 유지한다.
                selectedWeaponImage.preserveAspect = true;

                // 선택된 버튼 쪽 아이콘을 활성화한다.
                selectedWeaponImage.enabled = true;
            }
        }

        // 빈 무기 슬롯에 기본 무기를 넣은 경우에만 JSON 파일을 다시 저장한다.
        if (saveDataChanged)
        {
            SaveManager.Instance.SaveToFile();
        }
    }


    /// <summary>
    /// 현재 선택된 캐릭터에 맞춰 선택 연출 이미지를 표시한다.
    /// </summary>
    private void UpdateSelectEffects()
    {
        // 0번 캐릭터가 선택되었을 때 솔져 선택 이미지를 활성화한다.
        _soldierSelectImage.gameObject.SetActive(
            _selectedCharacterIndex == 0);

        // 1번 캐릭터가 선택되었을 때 두 번째 캐릭터 선택 이미지를 활성화한다.
        _warriorSelectImage.gameObject.SetActive(
            _selectedCharacterIndex == 1);
    }


    /// <summary>
    /// 선택한 캐릭터를 세션에 저장하고
    /// 캐릭터 선택 목적에 맞는 다음 씬으로 이동한다.
    /// </summary>
    public void LoadPlay()
    {
        // 아직 캐릭터를 선택하지 않았다면 이동하지 않는다.
        if (_selectedCharacterIndex < 0)
        {
            return;
        }

        // GameSessionManager가 없다면 정상적인 화면 흐름이 아니다.
        if (GameSessionManager.Instance == null)
        {
            throw new InvalidOperationException(
                "GameSessionManager가 없습니다. Bootstrap 씬부터 실행해야 합니다.");
        }

        // 선택한 인덱스에 해당하는 캐릭터 데이터를 가져온다.
        CharacterData selectedCharacter =
            _characterDatabase.GetCharacterDataAt(
                _selectedCharacterIndex);

        // 현재 세션 매니저를 가져온다.
        GameSessionManager session =
            GameSessionManager.Instance;

        // 선택한 캐릭터의 인덱스를 저장한다.
        session.SelectCharacter(
            _selectedCharacterIndex);

        // 선택한 캐릭터의 고유 ID를 저장한다.
        session.SelectCharacter(
            selectedCharacter.Id);

        // 장비를 수정하기 위해 캐릭터를 선택한 경우
        if (session.CurrentCharacterSelectPurpose ==
            CharacterSelectPurpose.LoadoutEdit)
        {
            SceneManager.LoadScene("EquipmentManage");
            return;
        }

        // 전투에 출전할 캐릭터를 선택한 경우
        if (session.CurrentCharacterSelectPurpose ==
            CharacterSelectPurpose.Battle)
        {
            // 선택된 스테이지가 없다면 잘못된 진입이다.
            if (session.SelectedStageIndex < 0)
            {
                throw new InvalidOperationException(
                    "선택된 스테이지가 없습니다.");
            }

            // 스테이지 인덱스는 0부터 시작하고,
            // 씬 이름은 Stage1부터 시작하므로 1을 더한다.
            int stageNumber =
                session.SelectedStageIndex + 1;

            // 0번 인덱스는 Stage1, 1번 인덱스는 Stage2로 이동한다.
            SceneManager.LoadScene(
                $"Stage{stageNumber}");

            return;
        }

        // 캐릭터 선택 화면에 들어온 목적이 기록되지 않은 경우
        throw new InvalidOperationException(
            "캐릭터 선택 목적이 설정되지 않았습니다.");
    }


    /// <summary>
    /// 캐릭터 선택 목적에 따라 이전 화면으로 돌아간다.
    /// 전투용 선택이면 스테이지 선택으로,
    /// 장비 관리용 선택이면 메인 메뉴로 이동한다.
    /// </summary>
    public void Back()
    {
        // 캐릭터 선택 목적은 GameSessionManager에 저장되어 있다.
        GameSessionManager session =
            GameSessionManager.Instance;

        // Bootstrap을 거치지 않았다면 이전 화면을 판단할 수 없다.
        if (session == null)
        {
            throw new InvalidOperationException(
                "GameSessionManager가 존재하지 않습니다.");
        }

        // ResetSelection을 호출하기 전에 현재 선택 목적을 보관한다.
        CharacterSelectPurpose purpose =
            session.CurrentCharacterSelectPurpose;

        // 현재 진행 중이던 스테이지 및 캐릭터 선택 정보를 초기화한다.
        session.ResetSelection();

        switch (purpose)
        {
            case CharacterSelectPurpose.Battle:
                // 전투용 캐릭터 선택은 StageSelect에서 진입했다.
                SceneManager.LoadScene("StageSelect");
                return;

            case CharacterSelectPurpose.LoadoutEdit:
                // 장비 관리용 캐릭터 선택은 메인 메뉴에서 진입했다.
                SceneManager.LoadScene("1.MainMenu");
                return;

            default:
                // 선택 목적 없이 CharacterSelect에 진입한 것은 잘못된 흐름이다.
                throw new InvalidOperationException(
                    $"처리할 수 없는 캐릭터 선택 목적입니다: {purpose}");
        }
    }
}
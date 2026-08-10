using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 선택 화면을 관리한다.
/// 선택한 캐릭터 인덱스를 CharacterDatabase에서 찾고, 해당 캐릭터의 고유 ID를 GameSessionManager에 전달한다.
/// 전투 목적으로 열었다면 선택한 Stage로 이동하고, 장비 편집 목적으로 열었다면 EquipmentManage로 이동한다.
/// </summary>
public class CharacterSelectManager : MonoBehaviour
{
    [Header("캐릭터 데이터")]
    [Tooltip("게임에 존재하는 전체 캐릭터 데이터베이스")]
    [SerializeField] CharacterDatabase _characterDatabase;

    [Header("버튼")]
    [SerializeField] Button _soldierButton;
    [SerializeField] Button _vampireButton;

    [Header("선택 표시")]
    [SerializeField] GameObject _soldierSelectImage;
    [SerializeField] GameObject _vampireSelectImage;

    // 현재 선택한 캐릭터가 CharacterDatabase에서 몇 번째인지 저장한다.
    // 아직 선택하지 않은 상태는 -1이다.
    int _currentCharacterIndex = -1;

    private void LateUpdate()
    {
        // 현재 선택된 UI가 없을 때만 방향키 입력을 확인한다.
        if (EventSystem.current.currentSelectedGameObject == null)
        {
            // 방향키를 처음 입력하면 Soldier 버튼을 기본 선택한다.
            if (IsAnyArrowKeyDown())
            {
                SelectFirstCharacter();
            }
        }

        // 현재 선택 중인 버튼에 맞게 선택 이미지를 표시한다.
        UpdateSelectEffects();
    }

    /// <summary>
    /// 첫 번째 캐릭터 버튼을 선택한다.
    /// 현재 데이터베이스 순서에서는 Soldier가 첫 번째다.
    /// </summary>
    private void SelectFirstCharacter()
    {
        _soldierButton.Select();
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
    /// 기존 버튼 이벤트 연결:
    /// Soldier는 0
    /// Vampire는 1
    /// </summary>
    /// <param name="index">CharacterDatabase의 캐릭터 순번</param>
    public void SelectingCharacter(int index)
    {
        // UI에 잘못된 인덱스가 연결되어 있다면 즉시 원인을 알려준다.
        if (index < 0 || index >= _characterDatabase.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index),index,"선택한 캐릭터 인덱스가 범위를 벗어났습니다.");
        }
        _currentCharacterIndex = index;
    }

    /// <summary>
    /// 현재 EventSystem이 선택한 버튼에 맞춰
    /// Soldier 또는 Vampire의 선택 이미지를 표시한다.
    /// </summary>
    private void UpdateSelectEffects()
    {
        GameObject selectedObject = EventSystem.current.currentSelectedGameObject;

        _soldierSelectImage.SetActive(selectedObject == _soldierButton.gameObject);

        _vampireSelectImage.SetActive(selectedObject == _vampireButton.gameObject);
    }

    /// <summary>
    /// 현재 선택한 캐릭터를 확정한다.
    /// 기존 씬 버튼이 LoadPlay에 연결되어 있으므로
    /// 메서드 이름은 그대로 유지한다.
    /// </summary>
    public void LoadPlay()
    {
        // 아직 캐릭터를 선택하지 않았다면 이동하지 않는다.
        if (_currentCharacterIndex < 0)
        {
            return;
        }

        // 선택한 인덱스를 실제 CharacterData로 변환한다.
        CharacterData selectedCharacter = _characterDatabase.GetCharacterDataAt(_currentCharacterIndex);

        GameSessionManager session = GameSessionManager.Instance;

        // 기존 인덱스 방식도 현재 사용 중이므로 함께 기록한다.
        session.SelectCharacter(_currentCharacterIndex);

        // Stage에서 캐릭터 데이터를 찾을 수 있도록 선택한 캐릭터의 고유 ID를 세션에 기록한다.
        session.SelectCharacter(selectedCharacter.Id);

        // 장비 편집 목적으로 캐릭터 선택 화면을 열었다면 Stage가 아니라 EquipmentManage로 이동한다.
        if (session.CurrentCharacterSelectPurpose == CharacterSelectPurpose.LoadoutEdit)
        {
            SceneManager.LoadScene("EquipmentManage");
            return;
        }

        // 선택된 Stage가 없다면 정상적인 전투 진입이 아니다.
        if (session.SelectedStageIndex < 0)
        {
            throw new InvalidOperationException("선택된 Stage가 없습니다.");
        }

        // Stage 인덱스는 0부터 시작하고 씬 이름은 Stage1부터 시작하므로 1을 더한다.
        int stageNumber = session.SelectedStageIndex + 1;

        // 예:
        // 인덱스 0 → Stage1 / 인덱스 1 → Stage2
        SceneManager.LoadScene($"Stage{stageNumber}");
    }
}
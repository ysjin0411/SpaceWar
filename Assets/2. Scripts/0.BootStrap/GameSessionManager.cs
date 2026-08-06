using System;
using UnityEngine;

/// <summary>
/// CharacterSelect 씬을 연 목적을 구분한다. 같은 캐릭터 선택 화면을 전투와 장비 편집에서 함께 사용한다.
/// </summary>
public enum CharacterSelectPurpose
{
    None,           // 아직 캐릭터 선택 흐름이 시작되지 않은 상태
    Battle,         // StageN에 출전할 캐릭터를 선택하는 상태
    LoadoutEdit     // SkillManage에서 장비를 수정할 캐릭터를 선택하는 상태
}


/// <summary>
/// 게임 통합 데이터 관리
/// </summary>
public class GameSessionManager : MonoBehaviour
{
    public static GameSessionManager Instance { get; private set; }     // 인스턴스 프로퍼티

    public int SelectedStageIndex { get; private set; } = -1;           // 스테이지 번호 저장
    public int SelectedCharacterIndex { get; private set; } = -1;       // 캐릭터 번호 저장

    // CharacterSelect 씬을 연 목적
    public CharacterSelectPurpose CurrentCharacterSelectPurpose
    {
        get;
        private set;
    } = CharacterSelectPurpose.None;

    // CharacterSelect 씬에서 실제로 선택한 캐릭터의 고유 ID
    public string SelectedCharacterId
    {
        get;
        private set;
    }

    private void Awake()
    {
        // 이미 세션 매니저가 존재하면 새로 생긴 오브젝트를 제거한다.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;

        // 씬이 변경돼도 GameRoot가 파괴되지 않게
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// StageSelectManager에서 선택한 스테이지 번호를 저장
    /// </summary>
    /// <param name="stageIndex"></param>
    public void SelectStage(int stageIndex)
    {
        SelectedStageIndex = stageIndex;
    }

    /// <summary>
    /// CharacterSelect 씬을 어떤 목적으로 열 것인지 기록한다. 이전에 선택했던 캐릭터 ID는 새로운 선택을 위해 비운다.
    /// </summary>
    /// <param name="purpose">Battle 또는 LoadoutEdit</param>
    public void BeginCharacterSelection(CharacterSelectPurpose purpose)
    {
        // None은 실제 캐릭터 선택 목적이 아니므로 외부에서 None으로 선택을 시작하면 코드 오류다.
        if (purpose == CharacterSelectPurpose.None)
        {
            throw new ArgumentException("캐릭터 선택 목적은 None일 수 없습니다.", nameof(purpose));
        }

        // 이번 CharacterSelect 씬의 사용 목적을 저장한다.
        CurrentCharacterSelectPurpose = purpose;

        // 새로운 캐릭터를 선택할 예정이므로 이전 화면 흐름에서 선택했던 ID를 비운다.
        SelectedCharacterId = null;
    }

    /// <summary>
    /// CharacterSelectManager에서 선택한 캐릭터 번호를 저장
    /// </summary>
    /// <param name="characterIndex"></param>
    public void SelectCharacter(int characterIndex)
    {
        SelectedCharacterIndex = characterIndex;
    }

    /// <summary>
    /// CharacterSelect 씬에서 선택한 캐릭터의 고유 ID를 저장한다.
    /// </summary>
    /// <param name="characterId">선택한 CharacterData의 ID</param>
    public void SelectCharacter(string characterId)
    {
        // 비어 있는 ID는 실제 캐릭터를 나타낼 수 없으므로 호출 위치를 확인할 수 있게 예외를 발생시킨다.
        if (string.IsNullOrWhiteSpace(characterId))
        {
            throw new ArgumentException("선택한 캐릭터 ID가 비어 있습니다.", nameof(characterId));
        }

        // 이번 화면 흐름에서 사용할 캐릭터 ID를 저장한다.
        SelectedCharacterId = characterId;
    }

    /// <summary>
    /// 현재 화면 이동 과정에서 선택한 임시 정보를 초기화한다. 캐릭터별 무기 장착 세이브 데이터에는 영향을 주지 않는다.
    /// </summary>
    public void ResetSelection()
    {
        // 선택된 스테이지가 없는 상태로 되돌린다.
        SelectedStageIndex = -1;

        // 아직 기존 인덱스 방식을 사용 중이므로 함께 초기화한다.
        SelectedCharacterIndex = -1;

        // 선택한 캐릭터 ID를 비운다.
        SelectedCharacterId = null;

        // 캐릭터 선택 목적도 없는 상태로 되돌린다.
        CurrentCharacterSelectPurpose = CharacterSelectPurpose.None;
    }
}
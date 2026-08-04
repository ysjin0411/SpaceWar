using UnityEngine;

/// <summary>
/// 게임 통합 데이터 관리
/// </summary>
public class GameSessionManager : MonoBehaviour
{
    public static GameSessionManager Instance { get; private set; }     // 인스턴스 프로퍼티

    public int SelectedStageIndex { get; private set; } = -1;           // 스테이지 번호 저장
    public int SelectedCharacterIndex { get; private set; } = -1;       // 캐릭터 번호 저장

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
    /// CharacterSelectManager에서 선택한 캐릭터 번호를 저장
    /// </summary>
    /// <param name="characterIndex"></param>
    public void SelectCharacter(int characterIndex)
    {
        SelectedCharacterIndex = characterIndex;
    }

    /// <summary>
    /// stage와 character 선택 초기화???
    /// </summary>
    public void ResetSelection()
    {
        SelectedStageIndex = -1;
        SelectedCharacterIndex = -1;
    }
}
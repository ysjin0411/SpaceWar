using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

// 스테이지 선택 화면에 필요한 정보만 저장한다.
[CreateAssetMenu(
    fileName = "StageData",
    menuName = "Game/Stage Data")]
public class StageData : ScriptableObject
{
    // 저장된 클리어 기록과 연결되는 번호다.
    // Stage1은 0, Stage2는 1을 사용한다.
    [SerializeField, Min(0)] int _stageIndex;

    // 선택 화면에 표시할 스테이지 이름이다.
    [SerializeField] string _stageName;

    // 스테이지 선택 버튼에 표시할 아이콘이다.
    [SerializeField] Sprite _stageIcon;

#if UNITY_EDITOR
    // 에디터에서 보스 정보를 추출할 원본 씬이다.
    // 게임 빌드에는 이 필드가 포함되지 않는다.
    [SerializeField] SceneAsset _sourceScene;

    // 에디터의 자동 추출 기능에서 원본 씬을 확인한다.
    public SceneAsset SourceScene => _sourceScene;
#endif

    // 씬에서 추출한 중간 보스 이름이다.
    [SerializeField, HideInInspector]
    string _midBossName = string.Empty;

    // 씬에서 추출한 중간 보스 아이콘이다.
    [SerializeField, HideInInspector]
    Sprite _midBossIcon;

    // 씬에서 추출한 최종 보스 이름이다.
    [SerializeField, HideInInspector]
    string _finalBossName = string.Empty;

    // 씬에서 추출한 최종 보스 아이콘이다.
    [SerializeField, HideInInspector]
    Sprite _finalBossIcon;

    // 잠긴 스테이지는 선택 정보와 입장을 제한한다.
    [SerializeField] bool _isLocked;

    // 선택 화면에서 사용하는 스테이지 기본 정보다.
    public int StageIndex => _stageIndex;
    public string StageName => _stageName;
    public Sprite StageIcon => _stageIcon;
    public bool IsLocked => _isLocked;

    // 선택 화면은 보스 프리팹 대신 저장된 표시 정보를 읽는다.
    public string MidBossName => _midBossName;
    public Sprite MidBossIcon => _midBossIcon;
    public string FinalBossName => _finalBossName;
    public Sprite FinalBossIcon => _finalBossIcon;

#if UNITY_EDITOR
    // 에디터에서 추출한 이름과 아이콘만 저장한다.
    // 보스 프리팹 자체의 참조는 보관하지 않는다.
    public void SetExtractedBossInfo(
        string midBossName,
        Sprite midBossIcon,
        string finalBossName,
        Sprite finalBossIcon)
    {
        _midBossName = midBossName;
        _midBossIcon = midBossIcon;
        _finalBossName = finalBossName;
        _finalBossIcon = finalBossIcon;
    }
#endif
}
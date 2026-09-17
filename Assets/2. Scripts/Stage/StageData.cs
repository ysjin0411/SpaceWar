using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

// 스테이지 선택 화면에서 사용하는 정보.
// 보스 배정은 원본 씬에서 자동으로 가져온다.
[CreateAssetMenu(
    fileName = "StageData",
    menuName = "Game/Stage Data")]
public class StageData : ScriptableObject
{
    // 기존 세이브 기록과 연결되는 인덱스.
    // Stage1은 0, Stage2는 1이다.
    [SerializeField, Min(0)] int _stageIndex;

    // 선택 화면에 표시할 스테이지 이름.
    [SerializeField] string _stageName;

    // 지도에서 사용할 스테이지 아이콘.
    [SerializeField] Sprite _stageIcon;

#if UNITY_EDITOR
    // 보스 배정을 읽어올 원본 씬.
    // SceneAsset은 에디터에서만 사용하고 빌드에는 포함하지 않는다.
    [SerializeField] SceneAsset _sourceScene;

    public SceneAsset SourceScene => _sourceScene;
#endif

    // 원본 씬에서 자동 추출한 보스 참조.
    // 수동 입력을 막기 위해 기본 Inspector에서는 숨긴다.
    [SerializeField, HideInInspector] BossEnemy _midBossPrefab;
    [SerializeField, HideInInspector] BossEnemy _finalBossPrefab;

    // 잠긴 스테이지는 지도에서 선택할 수 있지만 입장할 수 없다.
    [SerializeField] bool _isLocked;

    // 선택 화면과 에디터에서 잠금 여부를 확인한다.
    public bool IsLocked => _isLocked;

    public int StageIndex => _stageIndex;
    public string StageName => _stageName;
    public Sprite StageIcon => _stageIcon;

    // 선택 화면은 이 프리팹에서 이름과 아이콘을 읽는다.
    public BossEnemy MidBossPrefab => _midBossPrefab;
    public BossEnemy FinalBossPrefab => _finalBossPrefab;

#if UNITY_EDITOR
    // 자동 추출 도구만 사용하는 갱신 함수.
    // 변경 여부를 반환해 불필요한 에셋 저장을 피한다.
    public bool SetExtractedBosses(
        BossEnemy midBoss,
        BossEnemy finalBoss)
    {
        if (_midBossPrefab == midBoss &&
            _finalBossPrefab == finalBoss)
        {
            return false;
        }

        _midBossPrefab = midBoss;
        _finalBossPrefab = finalBoss;
        return true;
    }
#endif
}
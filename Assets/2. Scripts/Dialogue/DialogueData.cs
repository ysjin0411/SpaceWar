using System.Collections.Generic;
using UnityEngine;

// 대화 선택 조건과 대사 원본을 저장하는 설정 에셋.
// 플레이어의 퀘스트 진행 상태는 이 에셋에 저장하지 않는다.
[CreateAssetMenu(
    fileName = "DialogueData",
    menuName = "GameSettings/Dialogue")]
public class DialogueData : ScriptableObject
{
    // 대화를 구분하는 고유 ID.
    [SerializeField] private string _id;

    // 이 플래그들을 모두 가지고 있어야 대화 후보가 된다.
    [SerializeField] private string[] _requiredFlags = new string[0];

    // 이 플래그 중 하나라도 가지고 있으면 대화 후보에서 제외한다.
    [SerializeField] private string[] _hidingFlags = new string[0];

    // 조건을 만족하는 대화가 여러 개라면 높은 값을 우선한다.
    [SerializeField] private int _priority;

    // Enter로 구분해서 입력하는 대사 원본. 빈 줄은 건너뛴다.
    [SerializeField, TextArea(5, 20)]
    private string _content;

    [Header("----- 라인 -----")]

    // 원본을 변환한 결과. 내용 수정은 Content에서 하고 다시 생성한다.
    [SerializeField]
    private List<DialogueLine> _lines =
        new List<DialogueLine>();

    // 외부에서 대화 설정을 읽기 위한 프로퍼티.
    public string Id => _id;
    public string Content => _content;
    public IReadOnlyList<string> RequiredFlags => _requiredFlags;
    public IReadOnlyList<string> HidingFlags => _hidingFlags;
    public int Priority => _priority;
    public int LineCount => _lines.Count;

    // Inspector 메뉴에서 실행하면 원본을 줄 단위로 변환한다.
    [ContextMenu("라인 배열 생성")]
    public void ParseContent()
    {
#if UNITY_EDITOR
        // 에디터에서 라인 생성을 실행 취소할 수 있도록 변경 전 상태를 기록한다.
        UnityEditor.Undo.RecordObject(this, "대화 라인 배열 생성");
#endif

        // 이전 결과를 비워서 다시 생성해도 대사가 중복되지 않게 한다.
        _lines = new List<DialogueLine>();

        if (!string.IsNullOrWhiteSpace(_content))
        {
            // Windows와 다른 환경의 줄바꿈을 동일한 형태로 통일한다.
            string normalizedContent =
                _content.Replace("\r\n", "\n").Replace('\r', '\n');

            string[] lines = normalizedContent.Split('\n');

            foreach (string line in lines)
            {
                // 빈 줄이나 공백만 있는 줄은 대사로 만들지 않는다.
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                // 문장 내부의 공백은 그대로 유지한다.
                _lines.Add(new DialogueLine(line));
            }
        }

#if UNITY_EDITOR
        // 생성한 목록이 에셋 저장 대상에 포함되도록 표시한다.
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    // 요청한 순번의 대사가 있으면 반환한다.
    public bool TryGetLine(int index, out DialogueLine line)
    {
        if (index < 0 || index >= _lines.Count)
        {
            line = null;
            return false;
        }

        line = _lines[index];
        return line != null;
    }
}
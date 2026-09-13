using System;
using UnityEngine;

// 대화창에서 한 번에 출력할 대사 한 개를 저장한다.
[Serializable]
public class DialogueLine
{
    // 이 대사의 전체 문장. 한 글자씩 표시하는 처리는 Presenter가 담당한다.
    [SerializeField, TextArea(2, 6)]
    private string _text;

    // 외부에서는 대사 내용을 읽기만 한다.
    public string Text => _text;

    // 원본에서 추출한 한 줄로 대사 데이터를 만든다.
    public DialogueLine(string text)
    {
        _text = text;
    }
}
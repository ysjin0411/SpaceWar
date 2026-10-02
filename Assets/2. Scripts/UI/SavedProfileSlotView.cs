using System;
using TMPro;
using UnityEngine;

public class SavedProfileSlotView : MonoBehaviour
{
    // 목록에서의 순번.
    [SerializeField] TMP_Text _numberText;

    // 해당 사용자의 레벨.
    [SerializeField] TMP_Text _levelText;

    // 저장 파일명에서 가져온 사용자 ID.
    [SerializeField] TMP_Text _userIdText;

    // 이 사용자로 로그인하는 버튼.
    [SerializeField] UnityEngine.UI.Button _loginButton;

    // 이 행이 나타내는 사용자 ID.
    string _userId;

    // 로그인 버튼을 눌렀을 때 창에 전달할 함수.
    Action<string> _loginRequested;

    private void Awake()
    {
        // 버튼 이벤트는 행이 만들어질 때 한 번만 연결한다.
        _loginButton.onClick.AddListener(HandleLoginClicked);
    }

    // 행의 표시 정보와 로그인 요청 함수를 설정한다.
    public void Bind(
        int number,
        SavedProfileInfo profile,
        Action<string> loginRequested)
    {
        _userId = profile.UserId;
        _loginRequested = loginRequested;

        _numberText.text = number.ToString();
        _levelText.text = $"{profile.UserLevel}";

        // ID를 서식 태그로 해석하지 않고 그대로 표시한다.
        _userIdText.richText = false;
        _userIdText.text = profile.UserId;
    }

    // 이 행의 사용자 ID를 목록 창에 전달한다.
    private void HandleLoginClicked()
    {
        if (_loginRequested != null)
        {
            _loginRequested(_userId);
        }
    }

    private void OnDestroy()
    {
        // 직접 등록한 버튼 이벤트를 해제한다.
        if (_loginButton != null)
        {
            _loginButton.onClick.RemoveListener(HandleLoginClicked);
        }
    }
}
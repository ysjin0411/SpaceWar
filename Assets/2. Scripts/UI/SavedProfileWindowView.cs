using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

public class SavedProfileWindowView : MonoBehaviour
{
    // 열고 닫을 사용자 목록 패널.
    [SerializeField] GameObject _windowRoot;

    // Scroll View의 Content.
    [SerializeField] Transform _content;

    // 사용자 한 행을 표시하는 프리팹.
    [SerializeField] SavedProfileSlotView _slotPrefab;

    // 기존 로그인 처리를 담당하는 컴포넌트.
    [SerializeField] LoginController _loginController;

    // 빈 목록이나 로그인 실패를 표시할 안내 문구.
    [SerializeField] TMP_Text _messageText;

    // 이 창이 생성한 행만 관리한다.
    readonly List<SavedProfileSlotView> _slots =
        new List<SavedProfileSlotView>();

    /// <summary>
    /// 아이디 목록이 열려 있을 때 Esc로 창을 닫습니다.
    /// </summary>
    private void Update()
    {
        // X 버튼과 같은 경로로 닫아 로그인 UI의 목록 선택 상태도 해제한다.
        if (_windowRoot.activeInHierarchy && Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
        }
    }
    private void Awake()
    {
        // 처음에는 목록 창을 숨긴다.
        _windowRoot.SetActive(false);
    }

    // 우측 상단의 목록 열기 버튼에 연결한다.
    public void Open()
    {
        if (!_loginController.TryBeginProfileSelection())
        {
            return;
        }

        _windowRoot.SetActive(true);
        RefreshList();
    }

    // 창의 X 버튼에 연결한다.
    public void Close()
    {
        _windowRoot.SetActive(false);
        _loginController.EndProfileSelection();
    }

    // 저장 파일을 다시 읽어 현재 목록을 만든다.
    private void RefreshList()
    {
        ClearSlots();
        _messageText.text = string.Empty;

        try
        {
            List<SavedProfileInfo> profiles =
                SaveManager.Instance.GetSavedProfiles();

            if (profiles.Count == 0)
            {
                _messageText.text = "표시할 저장된 ID가 없습니다.";
                return;
            }

            for (int i = 0; i < profiles.Count; i++)
            {
                SavedProfileSlotView slot =
                    Instantiate(_slotPrefab, _content);

                // 화면 번호는 1부터 시작한다.
                slot.Bind(i + 1, profiles[i], HandleLoginRequested);
                slot.gameObject.SetActive(true);

                _slots.Add(slot);
            }
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException ||
            exception is System.Security.SecurityException)
        {
            // 폴더 자체를 읽지 못했을 때도 닫기 버튼은 사용할 수 있다.
            Debug.LogException(exception);
            _messageText.text = "저장된 사용자 목록을 읽지 못했습니다.";
        }
    }

    // 행의 로그인 요청을 기존 로그인 컨트롤러에 전달한다.
    private void HandleLoginRequested(string userId)
    {
        if (!_loginController.TryLoginSavedProfile(
                userId,
                out string message))
        {
            // 실패했을 때는 목록 창을 유지한다.
            _messageText.text = message;
            return;
        }

        // 로그인에 성공했을 때 창을 닫는다.
        Close();
    }

    // 다시 열었을 때 행이 중복되지 않도록 기존 행을 정리한다.
    private void ClearSlots()
    {
        foreach (SavedProfileSlotView slot in _slots)
        {
            // Destroy는 프레임 끝에 처리되므로 먼저 레이아웃에서 제외한다.
            slot.gameObject.SetActive(false);
            Destroy(slot.gameObject);
        }

        _slots.Clear();
    }
}
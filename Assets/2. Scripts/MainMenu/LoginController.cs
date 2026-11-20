using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 이름 입력, 저장 데이터 선택, 생성 확인, 로그인 화면 전환을 담당한다.
public class LoginController : MonoBehaviour
{
    // 사용자 ID를 입력하는 입력란.
    [SerializeField] TMP_InputField _idInputField;

    // 로그인 요청 버튼.
    [SerializeField] Button _loginButton;

    // 로그인 버튼의 공통 선택·눌림 효과.
    [SerializeField] UIButtonSelectionEffect _loginButtonEffect;

    // 로그인 성공 이후에만 사용할 플레이 버튼.
    [SerializeField] Button _playButton;

    // 로그인 처리 중 씬 이동을 막을 환경설정 버튼.
    [SerializeField] Button _settingButton;

    // 새 사용자 생성 여부를 묻는 확인 창.
    [SerializeField] ConfirmationDialogUI _confirmationDialog;

    // 로그인 성공 후 표시할 레벨 아이콘 전체.
    [SerializeField] GameObject _levelIcon;

    // 레벨 아이콘 아래의 사용자 레벨 텍스트.
    [SerializeField] TMP_Text _levelText;

    // 로그인 완료 안내 오브젝트.
    [SerializeField] GameObject _loginCompletedText;

    // 로그인한 사용자 ID를 표시할 텍스트.
    [SerializeField] TMP_Text _playerIdText;

    // 저장된 ID 목록을 여는 버튼.
    [SerializeField] UnityEngine.UI.Button _idListButton;

    // 로그인한 사용자를 로그아웃시키는 버튼.
    [SerializeField] UnityEngine.UI.Button _logoutButton;

    // 처리 중 중복 요청과 씬 이동을 막는 상태.
    bool _isProcessing;

    // 확인 창에 표시한 ID를 생성까지 동일하게 사용한다.
    string _requestedUserId = string.Empty;

    // 저장된 사용자 선택 창이 열려 있는지 나타낸다.
    bool _profileSelectionOpen;

    private void Start()
    {
        // 초기 연결 누락을 확인한다.
        if (_idInputField == null ||
            _loginButton == null ||
            _loginButtonEffect == null ||
            _playButton == null ||
            _settingButton == null ||
            _confirmationDialog == null ||
            _levelIcon == null ||
            _levelText == null ||
            _loginCompletedText == null ||
            _playerIdText == null ||
            _idListButton == null ||
            _logoutButton == null)
        {
            throw new MissingReferenceException(
                "LoginController의 UI 참조를 모두 연결해주세요.");
        }

        if (SaveManager.Instance == null ||
            AchievementManager.Instance == null)
        {
            throw new InvalidOperationException(
                "SaveManager와 AchievementManager가 필요합니다.");
        }

        // 입력과 화면 출력에 사용할 기본 설정.
        _idInputField.lineType = TMP_InputField.LineType.SingleLine;
        _idInputField.characterLimit = 10;
        _playerIdText.richText = false;

        // 입력란에서 Enter와 로그인 버튼 클릭을 연결한다.
        _idInputField.onSubmit.AddListener(HandleInputSubmitted);
        _loginButton.onClick.AddListener(RequestLogin);
        // 사용자가 ID 입력 내용을 변경하면 입력 효과음을 재생한다.
        _idInputField.onValueChanged.AddListener(HandleIdChanged);

        // 환경설정에서 돌아왔다면 기존 로그인 상태를 화면에 반영한다.
        RefreshLoginUI();

        if (!SaveManager.Instance.IsLoggedIn)
        {
            StartCoroutine(FocusInputAfterRelease());
        }
    }

    /// <summary>
    /// 사용자가 ID 입력 내용을 변경했을 때 입력 효과음을 재생합니다.
    /// </summary>
    private void HandleIdChanged(string value)
    {
        // 직접 입력 중이며 입력 가능한 상태일 때만 소리를 낸다.
        if (!_idInputField.isFocused || !_idInputField.interactable)
        {
            return;
        }

        // 문자열 변경에 대한 입력 효과음을 요청한다.
        UIAudioPlayer.Play(UISound.Typing);
    }

    // 입력란의 Enter 제출을 로그인 요청으로 연결한다.
    private void HandleInputSubmitted(string submittedText)
    {
        // 한글 조합을 확정하는 입력은 로그인 요청으로 사용하지 않는다.
        if (!string.IsNullOrEmpty(Input.compositionString))
        {
            return;
        }

        RequestLogin();
    }

    // Enter와 마우스 클릭이 함께 사용하는 로그인 진입점.
    private void RequestLogin()
    {
        if (_isProcessing ||
            _confirmationDialog.IsOpen ||
            SaveManager.Instance.IsLoggedIn)
        {
            return;
        }

        // 요청 당시 입력을 보관한다.
        // 버튼 효과 중이나 확인 창에서 입력값이 바뀌는 것을 방지한다.
        string input = _idInputField.text;

        SetProcessing(true);

        StartCoroutine(LoginAfterButtonEffect(input));
    }

    // 눌림 효과를 보여준 다음 파일 확인을 시작한다.
    private IEnumerator LoginAfterButtonEffect(string input)
    {
        yield return _loginButtonEffect.PlayPressEffect();

        // 눌렀던 Enter가 다음 UI에 전달되지 않도록 해제를 기다린다.
        while (Input.GetKey(KeyCode.Return) ||
               Input.GetKey(KeyCode.KeypadEnter))
        {
            yield return null;
        }

        AttemptLogin(input);
    }

    // ID를 검사하고 기존 파일을 읽거나 생성 확인 창을 연다.
    private void AttemptLogin(string input)
    {
        if (!SaveManager.Instance.TryValidateUserId(
            input,
            out string userId,
            out string message))
        {
            ReturnToInput(message);
            return;
        }

        _requestedUserId = userId;
        bool loaded;

        try
        {
            // 파일이 없을 때만 false가 반환된다.
            loaded = SaveManager.Instance.TryLoadUser(userId);
        }
        catch (Exception exception) when (IsFileOperationError(exception))
        {
            // 손상이나 접근 실패를 신규 사용자로 처리하지 않는다.
            Debug.LogException(exception);
            ReturnToInput(
                "세이브를 불러오지 못했습니다. 파일 형식과 접근 권한을 확인해주세요.");
            return;
        }

        if (loaded)
        {
            CompleteLogin();
            return;
        }

        // 이 시점에서는 파일을 만들지 않고 사용자 선택을 기다린다.
        _confirmationDialog.Show(
            $"'{_requestedUserId}' ID의 세이브데이터가 없습니다.\n" +
            "새로 만드시겠습니까?",
            HandleCreationAnswered);
    }

    // 확인 창의 예/아니오 결과를 처리한다.
    private void HandleCreationAnswered(bool accepted)
    {
        if (!accepted)
        {
            // 입력 내용은 그대로 유지하고 입력란으로 복귀한다.
            ReturnToInput(string.Empty);
            return;
        }

        try
        {
            // 확인 창에 표시했던 ID로만 새 파일을 만든다.
            SaveManager.Instance.CreateUser(_requestedUserId);
        }
        catch (Exception exception) when (IsFileOperationError(exception))
        {
            Debug.LogException(exception);
            ReturnToInput(
                "세이브를 만들지 못했습니다. 같은 이름의 파일과 저장 권한을 확인해주세요.");
            return;
        }

        CompleteLogin();
    }

    // 로그인 성공 화면을 표시하고, 후속 처리 오류는 안내창으로 알린다.
    private void CompleteLogin()
    {
        SetProcessing(false);
        RefreshLoginUI();
        // 로그인 완료 상태가 UI에 반영된 뒤 성공음을 한 번 재생한다.
        UIAudioPlayer.Play(UISound.LoginSuccess);

        try
        {
            // 로그인한 사용자의 데이터로 업적 조건을 검사한다.
            AchievementManager.Instance.EvaluateAllAchievements();
        }
        catch (Exception exception) when (IsFileOperationError(exception))
        {
            Debug.LogException(exception);

            // 로그인 성공 상태는 유지하고 업적 처리 오류만 안내한다.
            _confirmationDialog.ShowNotice(
                "로그인은 완료됐지만 업적 기록 처리에 실패했습니다.",
                SelectPlayAfterNotice);

            return;
        }

        StartCoroutine(SelectPlayAfterRelease());
    }

    // 안내 확인 후 플레이 버튼으로 선택을 옮긴다.
    private void SelectPlayAfterNotice()
    {
        StartCoroutine(SelectPlayAfterRelease());
    }

    // 현재 로그인 상태에 따라 보이는 UI와 버튼 사용 가능 여부를 맞춘다.
    private void RefreshLoginUI()
    {
        bool loggedIn = SaveManager.Instance.IsLoggedIn;

        _idInputField.gameObject.SetActive(!loggedIn);
        _loginButton.gameObject.SetActive(!loggedIn);

        _levelIcon.SetActive(loggedIn);
        _loginCompletedText.SetActive(loggedIn);
        _playerIdText.gameObject.SetActive(loggedIn);

        _playButton.interactable = loggedIn;

        if (loggedIn)
        {
            _playerIdText.text = SaveManager.Instance.CurrentUserId;
            _levelText.text =
                SaveManager.Instance.CurrentUserLevel.ToString();
        }

        // 현재 상태에 맞춰 목록 버튼과 로그아웃 버튼을 갱신한다.
        RefreshAccountButtons();
    }

    // 로그인 처리 중 입력 변경, 중복 클릭, 환경설정 이동을 막는다.
    private void SetProcessing(bool processing)
    {
        _isProcessing = processing;

        _idInputField.interactable = !processing;
        _loginButton.interactable = !processing;
        _settingButton.interactable = !processing;

        // 현재 상태에 맞춰 목록 버튼과 로그아웃 버튼을 갱신한다.
        RefreshAccountButtons();
    }

    // 안내가 있으면 확인을 받은 뒤 입력창으로 돌아간다.
    private void ReturnToInput(string message)
    {
        _requestedUserId = string.Empty;

        if (!string.IsNullOrWhiteSpace(message))
        {
            // 오류 안내가 있는 로그인 실패에만 실패음을 재생한다.
            UIAudioPlayer.Play(UISound.LoginFailure);

            // 안내를 읽는 동안 로그인과 설정 이동을 계속 막는다.
            SetProcessing(true);

            _confirmationDialog.ShowNotice(
                message,
                ResumeInput);

            return;
        }

        // 세이브 생성에서 아니오를 누른 경우에는 안내 없이 복귀한다.
        ResumeInput();
    }

    // 안내창이 닫힌 뒤 로그인 입력을 다시 허용한다.
    private void ResumeInput()
    {
        SetProcessing(false);
        StartCoroutine(FocusInputAfterRelease());
    }

    // 입력에 사용된 Enter가 해제된 다음 입력란을 다시 활성화한다.
    private IEnumerator FocusInputAfterRelease()
    {
        yield return null;

        while (Input.GetKey(KeyCode.Return) ||
               Input.GetKey(KeyCode.KeypadEnter))
        {
            yield return null;
        }

        if (_isProcessing ||
            _confirmationDialog.IsOpen ||
            SaveManager.Instance.IsLoggedIn)
        {
            yield break;
        }

        _idInputField.Select();
        _idInputField.ActivateInputField();
    }

    // 로그인에 사용한 입력이 끝난 다음 플레이 버튼을 선택한다.
    private IEnumerator SelectPlayAfterRelease()
    {
        yield return null;

        while (Input.GetKey(KeyCode.Return) ||
               Input.GetKey(KeyCode.KeypadEnter))
        {
            yield return null;
        }

        if (EventSystem.current != null)
        {
            _playButton.Select();
        }
    }

    // 파일 처리 중 예상할 수 있는 오류만 사용자 안내 대상으로 분류한다.
    private bool IsFileOperationError(Exception exception)
    {
        return exception is IOException ||
               exception is UnauthorizedAccessException ||
               exception is ArgumentException ||
               exception is System.Security.SecurityException;
    }

    private void OnDestroy()
    {
        // 이 클래스에서 등록한 이벤트만 해제한다.
        if (_idInputField != null)
        {
            _idInputField.onSubmit.RemoveListener(HandleInputSubmitted);
            // 입력 효과음을 위해 등록한 이벤트를 해제한다.
            _idInputField.onValueChanged.RemoveListener(HandleIdChanged);
        }

        if (_loginButton != null)
        {
            _loginButton.onClick.RemoveListener(RequestLogin);
        }
    }

    // 목록 창을 열 수 있는지 확인하고 뒤쪽 로그인 UI를 잠근다.
    public bool TryBeginProfileSelection()
    {
        if (_isProcessing ||
            _confirmationDialog.IsOpen ||
            SaveManager.Instance.IsLoggedIn ||
            _profileSelectionOpen)
        {
            return false;
        }

        _profileSelectionOpen = true;

        // 기존 입력칸, 로그인 버튼, 환경설정 버튼을 잠근다.
        SetProcessing(true);
        _idInputField.DeactivateInputField();

        return true;
    }

    // 목록 창이 닫히면 뒤쪽 UI의 잠금을 해제한다.
    public void EndProfileSelection()
    {
        if (!_profileSelectionOpen)
        {
            return;
        }

        _profileSelectionOpen = false;
        SetProcessing(false);

        // 로그인하지 않고 창만 닫았다면 ID 입력으로 돌아간다.
        if (!SaveManager.Instance.IsLoggedIn)
        {
            StartCoroutine(FocusInputAfterRelease());
        }
    }

    // 목록에서 선택한 기존 사용자로 로그인한다.
    public bool TryLoginSavedProfile(string userId, out string message)
    {
        message = string.Empty;

        if (!_profileSelectionOpen ||
            _confirmationDialog.IsOpen ||
            SaveManager.Instance.IsLoggedIn)
        {
            message = "현재는 사용자 목록에서 로그인할 수 없습니다.";
            return false;
        }

        try
        {
            // 실제 로그인 시점에 파일을 다시 읽고 기존 검증을 수행한다.
            if (!SaveManager.Instance.TryLoadUser(userId))
            {
                // 목록을 연 후 파일이 삭제된 경우 새 사용자를 만들지 않는다.
                message = "저장 파일이 없습니다. 창을 다시 열어 목록을 갱신해주세요.";
                // 저장된 ID를 불러오지 못한 경우 로그인 실패음을 재생한다.
                UIAudioPlayer.Play(UISound.LoginFailure);
                return false;
            }
        }
        catch (Exception exception) when (IsFileOperationError(exception))
        {
            Debug.LogException(exception);
            message = "저장 파일을 불러오지 못했습니다. 파일 상태를 확인해주세요.";
            // 저장된 ID를 불러오지 못한 경우 로그인 실패음을 재생한다.
            UIAudioPlayer.Play(UISound.LoginFailure);
            return false;
        }

        // 기존 로그인 완료 UI와 업적 확인 처리를 재사용한다.
        CompleteLogin();
        return true;
    }

    // 로그인 상태와 목록 창 상태에 따라 계정 버튼을 갱신한다.
    private void RefreshAccountButtons()
    {
        bool loggedIn = SaveManager.Instance.IsLoggedIn;

        // 로그인 전이면서 목록 창이 닫혀 있을 때만 표시한다.
        _idListButton.gameObject.SetActive(
            !loggedIn && !_profileSelectionOpen);

        // 로그아웃 버튼은 로그인 상태에서만 표시한다.
        _logoutButton.gameObject.SetActive(loggedIn);

        // 로그인·로그아웃 처리 중에는 중복 클릭을 막는다.
        _idListButton.interactable = !_isProcessing;
        _logoutButton.interactable = !_isProcessing;
    }

    // 로그아웃 버튼에서 호출한다.
    public void RequestLogout()
    {
        // 다른 처리나 안내창이 열려 있으면 요청을 받지 않는다.
        if (_isProcessing ||
            _profileSelectionOpen ||
            _confirmationDialog.IsOpen ||
            !SaveManager.Instance.IsLoggedIn)
        {
            return;
        }

        // 이전 로그인에서 기다리던 버튼 선택 코루틴을 정리한다.
        StopAllCoroutines();
        SetProcessing(true);

        try
        {
            // 현재 데이터를 저장하고 로그인 상태를 해제한다.
            SaveManager.Instance.Logout();
        }
        catch (Exception exception) when (IsFileOperationError(exception))
        {
            Debug.LogException(exception);

            // 저장 실패 시 로그인 상태를 유지하고 안내한다.
            _confirmationDialog.ShowNotice(
                "저장에 실패하여 로그아웃하지 못했습니다.",
                HandleLogoutFailureNoticeClosed);

            return;
        }

        // 이전에 입력하거나 요청했던 ID를 비운다.
        _requestedUserId = string.Empty;
        _idInputField.SetTextWithoutNotify(string.Empty);

        // 입력칸과 목록 버튼을 다시 표시하고 플레이 버튼을 잠근다.
        SetProcessing(false);
        RefreshLoginUI();

        // 로그아웃 버튼의 선택을 해제한 뒤 ID 입력으로 돌아간다.
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        StartCoroutine(FocusInputAfterRelease());
    }

    // 로그아웃 실패 안내를 닫으면 현재 계정의 버튼을 다시 사용할 수 있다.
    private void HandleLogoutFailureNoticeClosed()
    {
        SetProcessing(false);
        StartCoroutine(SelectPlayAfterRelease());
    }
}
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

    // 잘못된 입력이나 파일 처리 오류를 표시할 텍스트.
    [SerializeField] TMP_Text _loginMessageText;

    // 처리 중 중복 요청과 씬 이동을 막는 상태.
    bool _isProcessing;

    // 확인 창에 표시한 ID를 생성까지 동일하게 사용한다.
    string _requestedUserId = string.Empty;

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
            _loginMessageText == null)
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
        _idInputField.characterLimit = 20;
        _playerIdText.richText = false;
        _loginMessageText.richText = false;
        _loginMessageText.text = string.Empty;

        // 입력란에서 Enter와 로그인 버튼 클릭을 연결한다.
        _idInputField.onSubmit.AddListener(HandleInputSubmitted);
        _loginButton.onClick.AddListener(RequestLogin);

        // 환경설정에서 돌아왔다면 기존 로그인 상태를 화면에 반영한다.
        RefreshLoginUI();

        if (!SaveManager.Instance.IsLoggedIn)
        {
            StartCoroutine(FocusInputAfterRelease());
        }
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

        _loginMessageText.text = string.Empty;
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

    // 파일 읽기·생성이 성공한 뒤 로그인 완료 화면으로 전환한다.
    private void CompleteLogin()
    {
        SetProcessing(false);
        RefreshLoginUI();

        // 선택된 사용자의 데이터로 업적을 평가한다.
        // 이 과정의 저장 실패를 로그인 실패로 오인하지 않도록 따로 처리한다.
        try
        {
            AchievementManager.Instance.EvaluateAllAchievements();
        }
        catch (Exception exception) when (IsFileOperationError(exception))
        {
            Debug.LogException(exception);
            _loginMessageText.text =
                "로그인은 완료됐지만 업적 기록 처리에 실패했습니다.";
        }

        // 사용했던 Enter가 플레이까지 실행하지 않도록 해제 후 선택한다.
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
    }

    // 로그인 처리 중 입력 변경, 중복 클릭, 환경설정 이동을 막는다.
    private void SetProcessing(bool processing)
    {
        _isProcessing = processing;

        _idInputField.interactable = !processing;
        _loginButton.interactable = !processing;
        _settingButton.interactable = !processing;
    }

    // 오류나 생성 취소 후 입력 화면으로 돌아간다.
    private void ReturnToInput(string message)
    {
        _requestedUserId = string.Empty;
        _loginMessageText.text = message;

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
        }

        if (_loginButton != null)
        {
            _loginButton.onClick.RemoveListener(RequestLogin);
        }
    }
}
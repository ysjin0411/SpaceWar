using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 확인 메시지를 표시하고 예/아니오 결과를 호출자에게 전달한다.
// 실제 세이브 생성은 로그인 담당 코드에서 처리한다.
public class ConfirmationDialogUI : MonoBehaviour
{
    // 필요할 때 활성화할 알림창 전체 오브젝트.
    [SerializeField] GameObject _panel;

    // 알림창 뒤쪽의 로그인·플레이·환경설정 UI 그룹.
    [SerializeField] CanvasGroup _backgroundGroup;

    // 확인할 내용을 표시하는 본문 텍스트.
    [SerializeField] TMP_Text _messageText;

    // 확인과 취소에 사용하는 버튼.
    [SerializeField] Button _yesButton;
    [SerializeField] Button _noButton;

    // 안내 모드에서만 표시하는 하단 중앙 확인 버튼.
    [SerializeField] private Button _confirmButton;

    // 현재 창이 확인 버튼 하나만 사용하는 안내 모드인지 나타낸다.
    private bool _isNotice;

    // 창을 열기 전 배경의 조작 가능 상태.
    bool _previousInteractable;

    // 창을 열기 전 배경의 마우스 입력 수신 상태.
    bool _previousBlocksRaycasts;

    // 창을 열기 전에 선택되어 있던 UI 오브젝트.
    GameObject _previousSelection;

    // 응답을 기다리는 동안에만 버튼 처리를 허용한다.
    bool _canRespond;

    // 예/아니오 결과를 전달할 함수. 처리할 일이 없을 때는 빈 함수로 둔다.
    Action<bool> _onAnswered = delegate { };

    // 외부에서 알림창이 열려 있는지 확인한다.
    public bool IsOpen { get; private set; }

    private void Awake()
    {
        // Inspector 연결 누락을 실행 초기에 확인한다.
        if (_panel == null ||
            _backgroundGroup == null ||
            _messageText == null ||
            _yesButton == null ||
            _noButton == null ||
            _confirmButton == null)
        {
            throw new MissingReferenceException(
                "ConfirmationDialogUI의 UI 참조를 모두 연결해주세요.");
        }

        // 사용자 ID에 포함된 문자가 TMP 서식으로 해석되지 않게 한다.
        _messageText.richText = false;

        // 처음에는 확인 창을 숨긴다.
        _panel.SetActive(false);

        // 버튼 클릭은 이 컴포넌트에서 한 번만 연결한다.
        _yesButton.onClick.AddListener(HandleYes);
        _noButton.onClick.AddListener(HandleNo);
        // 안내 창의 확인 버튼도 코드에서 클릭을 연결한다.
        _confirmButton.onClick.AddListener(HandleConfirm);
    }

    // 두 모드가 배경 잠금과 창 표시 처리를 공유한다.
    private void Open(
        string message,
        Action<bool> onAnswered,
        bool isNotice)
    {
        if (IsOpen)
        {
            throw new InvalidOperationException(
                "이미 확인 창이 열려 있습니다.");
        }

        if (onAnswered == null)
        {
            throw new ArgumentNullException(nameof(onAnswered));
        }

        // 현재 배경 상태와 선택 대상을 보관한다.
        _previousInteractable = _backgroundGroup.interactable;
        _previousBlocksRaycasts = _backgroundGroup.blocksRaycasts;

        _previousSelection = EventSystem.current != null
            ? EventSystem.current.currentSelectedGameObject
            : null;

        // 뒤쪽 UI의 조작과 선택을 해제한다.
        _backgroundGroup.interactable = false;
        _backgroundGroup.blocksRaycasts = false;

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        // 이번 확인 요청의 내용과 결과 처리 함수를 보관한다.
        _messageText.text = message;
        _onAnswered = onAnswered;
        IsOpen = true;
        _canRespond = false;

        // 창을 열 때마다 모드에 맞는 버튼만 표시한다.
        _isNotice = isNotice;

        _yesButton.gameObject.SetActive(!isNotice);
        _noButton.gameObject.SetActive(!isNotice);
        _confirmButton.gameObject.SetActive(isNotice);

        // 창을 연 입력이 곧바로 응답 버튼까지 누르지 못하도록 잠근다.
        _yesButton.interactable = false;
        _noButton.interactable = false;
        _confirmButton.interactable = false;

        // 다른 화면보다 앞에 알림창을 표시한다.
        _panel.transform.SetAsLastSibling();
        _panel.SetActive(true);

        StartCoroutine(EnableResponseAfterRelease());
    }

    // 창을 연 Enter 입력이 끝난 뒤 현재 모드의 버튼만 활성화한다.
    private IEnumerator EnableResponseAfterRelease()
    {
        yield return null;

        while (Input.GetKey(KeyCode.Return) ||
               Input.GetKey(KeyCode.KeypadEnter))
        {
            yield return null;
        }

        _canRespond = true;

        _yesButton.interactable = !_isNotice;
        _noButton.interactable = !_isNotice;
        _confirmButton.interactable = _isNotice;

        if (EventSystem.current != null)
        {
            // 안내는 확인을, 선택이 필요한 창은 아니오를 기본 선택한다.
            if (_isNotice)
            {
                _confirmButton.Select();
            }
            else
            {
                _noButton.Select();
            }
        }
    }

    // 예 버튼의 응답을 전달한다.
    private void HandleYes()
    {
        Answer(true);
    }

    // 아니오 버튼의 응답을 전달한다.
    private void HandleNo()
    {
        Answer(false);
    }

    // 창을 먼저 닫은 뒤 호출자가 지정한 작업을 실행한다.
    private void Answer(bool accepted)
    {
        if (!IsOpen || !_canRespond)
        {
            return;
        }

        Action<bool> callback = _onAnswered;

        CloseWindow();
        callback.Invoke(accepted);
    }

    // 배경 조작과 이전 UI 선택 상태를 복구한다.
    private void CloseWindow()
    {
        StopAllCoroutines();

        IsOpen = false;
        _canRespond = false;
        _onAnswered = delegate { };

        _panel.SetActive(false);

        _backgroundGroup.interactable = _previousInteractable;
        _backgroundGroup.blocksRaycasts = _previousBlocksRaycasts;

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);

            if (_previousSelection != null &&
                _previousSelection.activeInHierarchy)
            {
                EventSystem.current.SetSelectedGameObject(
                    _previousSelection);
            }
        }
    }

    private void OnDisable()
    {
        // 관리 오브젝트가 꺼질 때 배경이 잠긴 채 남지 않게 한다.
        if (IsOpen)
        {
            CloseWindow();
        }
    }

    private void OnDestroy()
    {
        // 확인 버튼에 등록한 이벤트를 해제한다.
        if (_confirmButton != null)
        {
            _confirmButton.onClick.RemoveListener(HandleConfirm);
        }

        // 등록했던 버튼 이벤트를 해제한다.
        if (_yesButton != null)
        {
            _yesButton.onClick.RemoveListener(HandleYes);
        }

        if (_noButton != null)
        {
            _noButton.onClick.RemoveListener(HandleNo);
        }
    }

    // 요청한 화면이 닫혔을 때 결과 콜백을 실행하지 않고 확인창을 정리한다.
    public void Cancel()
    {
        if (IsOpen)
        {
            CloseWindow();
        }
    }

    // 기존 세이브 생성·퀘스트 포기에서 사용하는 예/아니오 확인창.
    public void Show(string message, Action<bool> onAnswered)
    {
        Open(message, onAnswered, false);
    }

    // 오류나 설명을 확인 버튼 하나로 보여주는 안내창.
    public void ShowNotice(string message, Action onConfirmed)
    {
        if (onConfirmed == null)
        {
            throw new ArgumentNullException(nameof(onConfirmed));
        }

        // 안내창의 확인 결과를 매개변수 없는 완료 콜백으로 전달한다.
        Open(message, accepted => onConfirmed.Invoke(), true);
    }

    // 안내창을 닫고 안내 확인 후 처리를 실행한다.
    private void HandleConfirm()
    {
        if (!_isNotice)
        {
            return;
        }

        // 기존 Answer가 먼저 창과 배경 상태를 복구한 뒤 콜백을 실행한다.
        Answer(true);
    }
}
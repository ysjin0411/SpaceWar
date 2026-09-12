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
            _noButton == null)
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
    }

    // 메시지와 응답 처리 함수를 받아 확인 창을 연다.
    public void Show(string message, Action<bool> onAnswered)
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

        // 로그인에 사용한 Enter가 확인 버튼까지 누르지 않도록 잠근다.
        _yesButton.interactable = false;
        _noButton.interactable = false;

        // 다른 화면보다 앞에 알림창을 표시한다.
        _panel.transform.SetAsLastSibling();
        _panel.SetActive(true);

        StartCoroutine(EnableResponseAfterRelease());
    }

    // 창을 연 프레임이 지나고 Enter가 해제된 뒤 응답을 허용한다.
    private IEnumerator EnableResponseAfterRelease()
    {
        yield return null;

        while (Input.GetKey(KeyCode.Return) ||
               Input.GetKey(KeyCode.KeypadEnter))
        {
            yield return null;
        }

        _canRespond = true;
        _yesButton.interactable = true;
        _noButton.interactable = true;

        // 기본 선택은 아니오로 두어 의도치 않은 생성을 방지한다.
        if (EventSystem.current != null)
        {
            _noButton.Select();
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
}
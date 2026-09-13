using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 대화 UI를 표시하고 사용자 클릭을 Presenter에 전달한다.
public class NpcDialogueUI : MonoBehaviour
{
    // 표시하거나 숨길 대화창 전체.
    [SerializeField] private GameObject _dialogueRoot;

    // 이름과 대사를 표시할 텍스트.
    [SerializeField] private TMP_Text _npcNameText;
    [SerializeField] private TMP_Text _dialogueText;

    // 현재 NPC의 애니메이션 프레임을 표시할 이미지.
    [SerializeField] private Image _npcImage;

    // 대화창 본문을 클릭했는지 감지할 버튼.
    [SerializeField] private Button _advanceButton;

    // 대화 종료 버튼.
    [SerializeField] private Button _closeButton;

    // NPC의 고유 기능을 선택하는 기존 버튼.
    [SerializeField] private Button _actionButton;

    // 버튼 안에 표시할 기능 이름.
    [SerializeField] private TMP_Text _actionButtonText;

    // UI 이미지에 반영할 NPC 렌더러와 이미지의 기본 크기.
    private SpriteRenderer _portraitSource;
    private Vector3 _imageScale;

    // View가 사용 가능한 상태인지 Presenter가 확인한다.
    public bool IsReady { get; private set; }

    // 루트뿐 아니라 View 자체가 꺼졌는지도 확인한다.
    public bool IsVisible =>
        isActiveAndEnabled &&
        _dialogueRoot != null &&
        _dialogueRoot.activeInHierarchy;

    // View는 대사 진행을 결정하지 않고 입력 사실만 전달한다.
    public event Action AdvanceRequested;
    public event Action CloseRequested;

    // 고유 기능 버튼 클릭을 Presenter에 전달한다.
    public event Action ActionRequested;

    // 연결을 확인하고 UI 이벤트를 준비한다.
    private void Awake()
    {
        // 고유 기능 버튼과 텍스트 연결도 함께 확인한다.
        if (_dialogueRoot == null ||
            _npcNameText == null ||
            _dialogueText == null ||
            _npcImage == null ||
            _advanceButton == null ||
            _closeButton == null ||
            _actionButton == null ||
            _actionButtonText == null)
        {
            Debug.LogError(
                "NpcDialogueUI의 Inspector 연결을 확인해주세요.", this);

            enabled = false;
            return;
        }

        _imageScale = _npcImage.rectTransform.localScale;
        _npcImage.preserveAspect = true;
        _npcImage.raycastTarget = false;

        // 버튼 클릭을 View의 입력 이벤트로 변환한다.
        _advanceButton.onClick.AddListener(RequestAdvance);
        _closeButton.onClick.AddListener(RequestClose);
        _actionButton.onClick.AddListener(RequestAction);

        _dialogueRoot.SetActive(false);
        IsReady = true;
    }

    // 이름과 초상 원본을 연결하고 창을 표시한다.
    public void Show(string npcName, SpriteRenderer portraitSource)
    {
        _npcNameText.text = npcName;
        _dialogueText.text = string.Empty;
        _portraitSource = portraitSource;

        _dialogueRoot.SetActive(true);
        RefreshPortrait();

        // 방향키와 Submit 입력으로도 닫기 버튼을 사용할 수 있게 한다.
        if (EventSystem.current != null)
        {
            _closeButton.Select();
        }
    }

    // 전체 문장을 먼저 배치한 뒤 글자만 숨긴다.
    // 이렇게 하면 타이핑 도중 줄바꿈 위치가 계속 바뀌지 않는다.
    public int PrepareLine(string text)
    {
        _dialogueText.maxVisibleCharacters = int.MaxValue;
        _dialogueText.text = text;
        _dialogueText.ForceMeshUpdate();

        // 원본 문자열 길이 대신 TMP가 처리한 글자 수를 사용한다.
        int characterCount = _dialogueText.textInfo.characterCount;

        _dialogueText.maxVisibleCharacters = 0;
        return characterCount;
    }

    // Presenter가 결정한 수만큼 글자를 보여준다.
    public void SetVisibleCharacterCount(int count)
    {
        _dialogueText.maxVisibleCharacters = count;
    }

    // Animator가 갱신한 NPC 프레임을 UI에도 반영한다.
    private void LateUpdate()
    {
        if (IsVisible)
        {
            RefreshPortrait();
        }
    }

    // 마을 NPC의 현재 애니메이션 프레임만 복사한다.
    // 마을 배치용 Flip은 대화창에 반영하지 않는다.
    private void RefreshPortrait()
    {
        if (_portraitSource == null)        {
            return;
        }

        // SpriteRenderer의 flipX는 스프라이트 원본을 바꾸지 않는다.
        // 원본 프레임을 사용하면 대화창은 원본 방향으로 표시된다.
        _npcImage.sprite = _portraitSource.sprite;

        // 대화창 초상화의 크기는 유지하고 반전은 제거한다.
        Vector3 scale = _imageScale;
        scale.x = Mathf.Abs(scale.x);
        scale.y = Mathf.Abs(scale.y);

        _npcImage.rectTransform.localScale = scale;
    }

    // Presenter가 결정한 문구와 표시 여부를 화면에 반영한다.
    public void SetAction(string label, bool visible)
    {
        if (_actionButton == null || _actionButtonText == null)
        {
            return;
        }

        // 문구가 바뀐 경우에만 텍스트를 갱신한다.
        if (_actionButtonText.text != label)
        {
            _actionButtonText.text = label;
        }

        // 숨길 버튼이 키보드 선택 중이라면 닫기 버튼으로 옮긴다.
        if (!visible && _actionButton.gameObject.activeSelf)
        {
            EventSystem eventSystem = EventSystem.current;

            if (eventSystem != null)
            {
                GameObject selected =
                    eventSystem.currentSelectedGameObject;

                if (selected != null &&
                    (selected == _actionButton.gameObject ||
                     selected.transform.IsChildOf(_actionButton.transform)))
                {
                    _closeButton.Select();
                }
            }
        }

        // 상태가 달라졌을 때만 활성 상태를 변경한다.
        if (_actionButton.gameObject.activeSelf != visible)
        {
            _actionButton.gameObject.SetActive(visible);
        }
    }

    // 대화창의 선택 상태와 화면 표시를 정리한다.
    public void Hide()
    {
        EventSystem eventSystem = EventSystem.current;

        if (eventSystem != null && _dialogueRoot != null)
        {
            GameObject selected = eventSystem.currentSelectedGameObject;

            if (selected != null &&
                selected.transform.IsChildOf(_dialogueRoot.transform))
            {
                eventSystem.SetSelectedGameObject(null);
            }
        }

        if (_dialogueRoot != null)
        {
            _dialogueRoot.SetActive(false);
        }

        _portraitSource = null;
    }

    // 본문 클릭을 Presenter에 전달한다.
    private void RequestAdvance()
    {
        if (AdvanceRequested != null)
        {
            AdvanceRequested.Invoke();
        }
    }

    // 닫기 클릭을 Presenter에 전달한다.
    private void RequestClose()
    {
        if (CloseRequested != null)
        {
            CloseRequested.Invoke();
        }
    }

    // 화면에 표시된 고유 기능 버튼의 클릭만 전달한다.
    private void RequestAction()
    {
        if (!IsVisible ||
            !_actionButton.gameObject.activeInHierarchy ||
            !_actionButton.IsInteractable())
        {
            return;
        }

        if (ActionRequested != null)
        {
            ActionRequested.Invoke();
        }
    }

    // View가 꺼졌을 때도 대화 종료를 요청한다.
    private void OnDisable()
    {
        RequestClose();
    }

    // 직접 등록한 버튼 이벤트만 해제한다.
    private void OnDestroy()
    {
        if (_advanceButton != null)
        {
            _advanceButton.onClick.RemoveListener(RequestAdvance);
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(RequestClose);
        }

        // 이 View가 등록한 고유 기능 버튼 이벤트를 해제한다.
        if (_actionButton != null)
        {
            _actionButton.onClick.RemoveListener(RequestAction);
        }
    }
}
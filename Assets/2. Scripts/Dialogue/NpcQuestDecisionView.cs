using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 수락·거절 버튼과 처리 실패 안내를 표시한다.
public class NpcQuestDecisionView : MonoBehaviour
{
    // 수락·거절 영역 전체.
    [SerializeField] private GameObject _root;

    // 사용자 선택을 받을 버튼.
    [SerializeField] private Button _acceptButton;
    [SerializeField] private Button _declineButton;

    // 저장 실패 등의 안내를 표시한다.
    [SerializeField] private TMP_Text _errorText;

    public bool IsReady { get; private set; }

    // 수락 여부는 View가 결정하지 않고 Presenter에 전달한다.
    public event Action AcceptRequested;
    public event Action DeclineRequested;

    // 필수 연결과 이벤트를 준비한다.
    private void Awake()
    {
        if (_root == null ||
            _acceptButton == null ||
            _declineButton == null ||
            _errorText == null)
        {
            Debug.LogError(
                "NpcQuestDecisionView의 Inspector 연결을 확인해주세요.", this);

            enabled = false;
            return;
        }

        _acceptButton.onClick.AddListener(RequestAccept);
        _declineButton.onClick.AddListener(RequestDecline);

        _errorText.text = string.Empty;
        _root.SetActive(false);
        IsReady = true;
    }

    // Presenter가 정한 시점에만 선택 영역을 표시한다.
    public void SetVisible(bool visible)
    {
        if (!IsReady || _root.activeSelf == visible)
        {
            return;
        }

        // 숨기는 버튼에 선택 상태가 남지 않게 한다.
        if (!visible && EventSystem.current != null)
        {
            GameObject selected =
                EventSystem.current.currentSelectedGameObject;

            if (selected != null &&
                selected.transform.IsChildOf(_root.transform))
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        _errorText.text = string.Empty;
        _root.SetActive(visible);

        // 새로 표시할 때 선택 위치를 수락 버튼으로 맞춘다.
        if (visible && EventSystem.current != null)
        {
            _acceptButton.Select();
        }
    }

    // 저장 실패 시 선택 영역 안에서 이유를 보여준다.
    public void ShowError(string message)
    {
        _errorText.text = message;
    }

    // 수락 클릭을 전달한다.
    private void RequestAccept()
    {
        if (AcceptRequested != null)
        {
            AcceptRequested.Invoke();
        }
    }

    // 거절 클릭을 전달한다.
    private void RequestDecline()
    {
        if (DeclineRequested != null)
        {
            DeclineRequested.Invoke();
        }
    }

    // 직접 등록한 이벤트를 정리한다.
    private void OnDestroy()
    {
        if (_acceptButton != null)
        {
            _acceptButton.onClick.RemoveListener(RequestAccept);
        }

        if (_declineButton != null)
        {
            _declineButton.onClick.RemoveListener(RequestDecline);
        }
    }
}
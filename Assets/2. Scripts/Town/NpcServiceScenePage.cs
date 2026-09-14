using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 추가로 불러온 선택 씬의 UI를 Town 서비스 창 안에 배치한다.
public class NpcServiceScenePage : MonoBehaviour
{
    // 이 씬에서 사용하던 화면 전체 Canvas.
    [SerializeField] private Canvas _screenCanvas;

    // 이 씬에서 사용하던 카메라와 입력 시스템.
    // 창 안에서는 Town의 카메라와 EventSystem을 사용한다.
    [SerializeField] private Camera _sceneCamera;
    [SerializeField] private EventSystem _sceneEventSystem;

    // 원래 화면을 제작한 기준 해상도.
    [SerializeField]
    private Vector2 _referenceSize =
        new Vector2(1920f, 1080f);

    // UI가 들어갈 Town 창의 영역.
    private RectTransform _host;

    // Town 아래로 이동시킨 UI는 씬을 내릴 때 별도로 정리한다.
    private bool _isHosted;

    // 선택 씬이 화면을 그리기 전에 Town 창에 연결한다.
    public void Attach(RectTransform host, EventSystem townEventSystem)
    {
        if (_screenCanvas == null ||
            _sceneCamera == null ||
            _sceneEventSystem == null ||
            host == null ||
            townEventSystem == null ||
            _referenceSize.x <= 0f ||
            _referenceSize.y <= 0f)
        {
            throw new InvalidOperationException(
                "NpcServiceScenePage의 연결과 기준 해상도를 확인해주세요.");
        }

        _host = host;
        _isHosted = true;

        // 추가 씬의 카메라·AudioListener·입력 시스템을 함께 끈다.
        _sceneCamera.gameObject.SetActive(false);
        _sceneEventSystem.gameObject.SetActive(false);
        EventSystem.current = townEventSystem;

        // 화면 크기는 Town 창에 맞춰 이 컴포넌트가 계산한다.
        CanvasScaler scaler = _screenCanvas.GetComponent<CanvasScaler>();

        if (scaler != null)
        {
            scaler.enabled = false;
        }

        // 중첩 Canvas가 Town 서비스 창의 표시 순서를 따르게 한다.
        _screenCanvas.overrideSorting = false;

        RectTransform screen = (RectTransform)_screenCanvas.transform;
        screen.SetParent(_host, false);
        screen.anchorMin = new Vector2(0.5f, 0.5f);
        screen.anchorMax = new Vector2(0.5f, 0.5f);
        screen.pivot = new Vector2(0.5f, 0.5f);
        screen.anchoredPosition3D = Vector3.zero;
        screen.localRotation = Quaternion.identity;
        screen.sizeDelta = _referenceSize;

        FitToWindow();
    }

    // 창 크기가 바뀌어도 화면 비율을 유지한다.
    private void LateUpdate()
    {
        if (_isHosted)
        {
            FitToWindow();
        }
    }

    // 원래 화면을 늘이거나 찌그러뜨리지 않고 창 안에 맞춘다.
    private void FitToWindow()
    {
        if (_host == null || _screenCanvas == null)
        {
            return;
        }

        float scale = Mathf.Min(
            _host.rect.width / _referenceSize.x,
            _host.rect.height / _referenceSize.y);

        _screenCanvas.transform.localScale =
            Vector3.one * Mathf.Max(0f, scale);
    }

    // Town 아래로 이동한 UI는 원래 씬과 소속이 달라지므로 직접 정리한다.
    public void Release()
    {
        if (!_isHosted)
        {
            return;
        }

        _isHosted = false;

        if (_screenCanvas != null)
        {
            _screenCanvas.gameObject.SetActive(false);
            Destroy(_screenCanvas.gameObject);
        }
    }

    // 원래 선택 씬이 내려가면 남은 화면도 제거한다.
    private void OnDestroy()
    {
        Release();
    }
}
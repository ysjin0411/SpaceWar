using System;
using UnityEngine;

/// <summary>
/// 동일한 배경 SpriteRenderer 두 장을 이어 붙여
/// 끊임없이 흐르는 배경 스크롤을 만든다.
/// </summary>
public class BackgroundScroller : MonoBehaviour
{
    [Header("배경 연결")]
    [Tooltip("이어 붙여 사용할 배경 SpriteRenderer 두 개")]
    [SerializeField]
    SpriteRenderer[] _backgroundRenderers =
        new SpriteRenderer[2];

    [Header("스크롤 설정")]
    [Tooltip("배경이 왼쪽으로 이동하는 속도")]
    [Min(0f)]
    [SerializeField] float _scrollSpeed = 1f;

    [Header("런타임 데이터")]
    [Tooltip("현재 배경이 움직이고 있는지 확인")]
    [SerializeField] bool _isScrolling = true;

    // 배경 한 장의 실제 월드 너비
    [SerializeField] float _backgroundWidth;

    // 현재 씬의 메인 카메라
    Camera _mainCamera;

    private void Awake()
    {
        // 정확히 두 장의 배경이 연결됐는지 확인한다.
        if (_backgroundRenderers == null ||
            _backgroundRenderers.Length != 2)
        {
            throw new InvalidOperationException(
                $"{name}의 BackgroundScroller에는 배경 두 개가 필요합니다.");
        }

        // 배열에 비어 있는 배경이 있는지 확인한다.
        for (int i = 0; i < _backgroundRenderers.Length; i++)
        {
            if (_backgroundRenderers[i] == null)
            {
                throw new InvalidOperationException(
                    $"{name}의 {i}번 Background Renderer가 비어 있습니다.");
            }
        }

        // 화면 경계를 계산할 메인 카메라를 가져온다.
        _mainCamera = Camera.main;

        if (_mainCamera == null)
        {
            throw new InvalidOperationException(
                "MainCamera 태그가 설정된 카메라가 없습니다.");
        }

        ArrangeBackgrounds();
    }

    private void Update()
    {
        // 스크롤이 정지된 상태라면 배경을 움직이지 않는다.
        if (!_isScrolling)
        {
            return;
        }

        // 두 배경을 같은 속도로 왼쪽으로 이동시킨다.
        foreach (SpriteRenderer background in _backgroundRenderers)
        {
            background.transform.Translate(
                Vector3.left * _scrollSpeed * Time.deltaTime,
                Space.World);
        }

        // 화면 왼쪽으로 벗어난 배경을 오른쪽 끝으로 이동시킨다.
        RecycleBackgrounds();
    }

    /// <summary>
    /// 첫 번째 배경 오른쪽에 두 번째 배경을 정확히 배치한다.
    /// </summary>
    private void ArrangeBackgrounds()
    {
        // SpriteRenderer의 Scale까지 적용된 실제 월드 너비를 가져온다.
        _backgroundWidth =
            _backgroundRenderers[0].bounds.size.x;

        if (_backgroundWidth <= 0f)
        {
            throw new InvalidOperationException(
                $"{name}의 배경 너비가 0보다 작거나 같습니다.");
        }

        // 첫 번째 배경을 카메라 중앙에 배치한다.
        Vector3 firstPosition =
            _backgroundRenderers[0].transform.position;

        firstPosition.x = _mainCamera.transform.position.x;

        _backgroundRenderers[0].transform.position =
            firstPosition;

        // 두 번째 배경을 첫 번째 배경의 오른쪽에 배치한다.
        Vector3 secondPosition =
            _backgroundRenderers[1].transform.position;

        secondPosition.x =
            firstPosition.x + _backgroundWidth;

        secondPosition.y = firstPosition.y;
        secondPosition.z = firstPosition.z;

        _backgroundRenderers[1].transform.position =
            secondPosition;
    }

    /// <summary>
    /// 왼쪽으로 완전히 벗어난 배경을 현재 오른쪽 배경 뒤로 옮긴다.
    /// </summary>
    private void RecycleBackgrounds()
    {
        // 배경 중심이 이 위치까지 오면 화면 왼쪽으로 벗어난 상태다.
        float recyclePositionX =
            _mainCamera.transform.position.x -
            _backgroundWidth;

        foreach (SpriteRenderer background in _backgroundRenderers)
        {
            // 아직 재배치 위치에 도달하지 않았다면 그대로 둔다.
            if (background.transform.position.x >
                recyclePositionX)
            {
                continue;
            }

            // 현재 두 배경 중 가장 오른쪽에 있는 X 위치를 찾는다.
            float rightmostPositionX = Mathf.Max(
                _backgroundRenderers[0].transform.position.x,
                _backgroundRenderers[1].transform.position.x);

            // 왼쪽으로 사라진 배경을 오른쪽 배경 뒤에 붙인다.
            Vector3 recycledPosition =
                background.transform.position;

            recycledPosition.x =
                rightmostPositionX + _backgroundWidth;

            background.transform.position =
                recycledPosition;
        }
    }

    /// <summary>
    /// 중간보스 사망 연출 등에서 배경 스크롤을 멈춘다.
    /// </summary>
    public void StopScrolling()
    {
        _isScrolling = false;
    }

    /// <summary>
    /// 맵 전환이 끝난 뒤 배경 스크롤을 다시 시작한다.
    /// </summary>
    public void ResumeScrolling()
    {
        _isScrolling = true;
    }

    /// <summary>
    /// 1-2 구간으로 넘어갈 때 두 배경의 스프라이트를 교체한다.
    /// </summary>
    public void ChangeBackground(Sprite newBackgroundSprite)
    {
        if (newBackgroundSprite == null)
        {
            throw new ArgumentNullException(
                nameof(newBackgroundSprite));
        }

        // 두 배경을 새로운 맵 이미지로 함께 교체한다.
        foreach (SpriteRenderer background in _backgroundRenderers)
        {
            background.sprite = newBackgroundSprite;
        }

        // 새 스프라이트의 크기로 너비와 위치를 다시 계산한다.
        ArrangeBackgrounds();
    }
}
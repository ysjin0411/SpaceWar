using DG.Tweening;       // DOTween 이동 연출을 사용합니다.
using UnityEngine;       // Unity 기본 타입을 사용합니다.

public class Gold : MonoBehaviour
{
    [Header("골드 데이터")]
    // 이 골드 프리팹 한 개를 획득했을 때 증가하는 수량입니다.
    [SerializeField] int _goldAmount = 10;

    [Header("이동 연출")]
    // 골드 아이콘까지 이동하는 데 걸리는 시간입니다.
    [SerializeField] float _moveDuration = 0.6f;

    // UI 골드 아이콘의 위치를 런타임에 전달받습니다.
    RectTransform _goldTarget;

    // UI 화면 좌표를 월드 좌표로 변환할 카메라입니다.
    Camera _worldCamera;

    // 골드 도착 후 UI와 실제 골드를 갱신할 GoldUI입니다.
    GoldUI _goldUI;

    /// <summary>
    /// 생성된 골드에 UI 목적지를 전달합니다.
    /// </summary>
    public void Initialize(
        RectTransform goldTarget,
        Camera worldCamera,
        GoldUI goldUI)
    {
        // Stage의 골드 아이콘을 저장합니다.
        _goldTarget = goldTarget;

        // Main Camera를 저장합니다.
        _worldCamera = worldCamera;

        // 골드 도착 처리를 담당할 GoldUI를 저장합니다.
        _goldUI = goldUI;

        // 필요한 참조를 모두 받았으므로 이동을 시작합니다.
        MoveToGoldUI();
    }

    /// <summary>
    /// UI 아이콘의 화면 위치를 월드 위치로 변환한 후 이동합니다.
    /// </summary>
    private void MoveToGoldUI()
    {
        // 필요한 참조가 없다면 이동할 수 없습니다.
        if (_goldTarget == null || _worldCamera == null || _goldUI == null)
        {
            Debug.LogError("Gold 초기화에 필요한 참조가 없습니다.");
            Destroy(gameObject);
            return;
        }

        // Screen Space Overlay UI 아이콘의 화면 좌표를 가져옵니다.
        Vector3 targetScreenPosition = _goldTarget.position;

        // 카메라에서 현재 골드가 있는 평면까지의 거리를 설정합니다.
        targetScreenPosition.z =
            Mathf.Abs(
                transform.position.z -
                _worldCamera.transform.position.z);

        // UI 아이콘의 화면 좌표를 월드 좌표로 변환합니다.
        Vector3 targetWorldPosition =
            _worldCamera.ScreenToWorldPoint(targetScreenPosition);

        // 변환 과정에서 생길 수 있는 Z값 차이를 현재 골드와 동일하게 맞춥니다.
        targetWorldPosition.z = transform.position.z;

        // 현재 월드 위치에서 아이콘에 대응되는 월드 위치까지 이동합니다.
        transform.DOMove(
                targetWorldPosition,
                _moveDuration)
            // 아이콘에 가까워질수록 빨라지는 움직임입니다.
            .SetEase(Ease.InQuad)
            // 골드가 파괴되면 Tween도 함께 제거합니다.
            .SetLink(gameObject)
            // 목적지에 도착하면 획득 처리를 실행합니다.
            .OnComplete(GetGoldAndDestroy);
    }

    /// <summary>
    /// 골드가 아이콘 위치에 도착했을 때 호출됩니다.
    /// </summary>
    private void GetGoldAndDestroy()
    {
        // 이 골드 프리팹 자체에 설정된 수량만큼 지급합니다.
        //_goldUI.CollectGold(_goldAmount);

        // 획득이 끝난 월드 골드를 제거합니다.
        Destroy(gameObject);
    }
}
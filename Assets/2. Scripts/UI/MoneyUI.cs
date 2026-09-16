using System;
using UnityEngine;
using UnityEngine.Serialization;

// 재화 생성과 목적지 연결, 한 번에 생성한 재화의 회수를 관리한다.
public abstract class MoneyUI : MonoBehaviour
{
    // 이 종류의 재화가 최종적으로 도착할 UI 위치다.
    [FormerlySerializedAs("_goldTarget")]
    [SerializeField] private RectTransform _moneyTarget;

    // 생성된 재화 프리팹을 담는 UI 부모다.
    [FormerlySerializedAs("_goldContainer")]
    [SerializeField] private RectTransform _moneyContainer;

    // 생성 위치 주변으로 퍼지는 반경이다.
    [SerializeField, Min(0f)] private float _spreadRadius = 80f;

    // 자식 UI가 어떤 종류의 재화를 처리하는지 지정한다.
    public abstract MoneyType Type { get; }

    // 자식 UI가 획득 팝업 등을 배치할 때 사용하는 목적지다.
    protected RectTransform Target => _moneyTarget;

    // 실제 재화 지급과 해당 UI 갱신은 자식이 구현한다.
    public abstract void CollectMoney(int amount);

    // 생성 전에 목적지와 Canvas 설정을 확인한다.
    public void ValidateConfiguration()
    {
        if (_moneyTarget == null || _moneyContainer == null)
        {
            throw new InvalidOperationException(
                $"{name}: Money Target과 Money Container를 연결해주세요.");
        }

        Canvas containerCanvas =
            _moneyContainer.GetComponentInParent<Canvas>();

        Canvas targetCanvas =
            _moneyTarget.GetComponentInParent<Canvas>();

        if (containerCanvas == null || targetCanvas == null ||
            containerCanvas.rootCanvas != targetCanvas.rootCanvas)
        {
            throw new InvalidOperationException(
                $"{name}: 재화 컨테이너와 목적지는 같은 Root Canvas에 있어야 합니다.");
        }

        Canvas rootCanvas = containerCanvas.rootCanvas;

        if (rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay &&
            rootCanvas.worldCamera == null)
        {
            throw new InvalidOperationException(
                $"{name}: Canvas의 World Camera를 연결해주세요.");
        }
    }

    // 재화를 지정한 개수만큼 생성하고 모두 회수되면 완료를 알린다.
    public void SpawnMoney(
        Money prefab,
        Vector3 worldPosition,
        int count,
        Action onAllCollected)
    {
        ValidateConfiguration();

        if (prefab == null || prefab.Type != Type)
        {
            throw new ArgumentException(
                "재화 프리팹과 수집 UI의 종류가 일치하지 않습니다.");
        }

        if (count <= 0)
        {
            if (onAllCollected != null)
            {
                onAllCollected.Invoke();
            }

            return;
        }

        Camera worldCamera = Camera.main;

        if (worldCamera == null)
        {
            throw new InvalidOperationException(
                "MainCamera 태그가 있는 카메라가 필요합니다.");
        }

        Canvas rootCanvas =
            _moneyContainer.GetComponentInParent<Canvas>().rootCanvas;

        Camera uiCamera = null;

        if (rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = rootCanvas.worldCamera;
        }

        // 몬스터의 월드 위치를 UI 컨테이너의 월드 위치로 변환한다.
        Vector2 screenPosition =
            RectTransformUtility.WorldToScreenPoint(
                worldCamera, worldPosition);

        if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(
                _moneyContainer, screenPosition, uiCamera,
                out Vector3 startPosition))
        {
            throw new InvalidOperationException(
                "재화 생성 위치를 UI 좌표로 변환하지 못했습니다.");
        }

        int remainingCount = count;

        // 이 호출에서 생성한 재화만 집계한다.
        Action handleCollected = () =>
        {
            if (remainingCount <= 0)
            {
                return;
            }

            remainingCount--;

            if (remainingCount == 0 && onAllCollected != null)
            {
                onAllCollected.Invoke();
            }
        };

        float startAngle = UnityEngine.Random.Range(0f, 360f);

        for (int i = 0; i < count; i++)
        {
            float angle = (startAngle + 360f * i / count) *
                          Mathf.Deg2Rad;

            Vector3 localOffset = new Vector3(
                Mathf.Cos(angle),
                Mathf.Sin(angle),
                0f) * _spreadRadius;

            // 컨테이너의 크기와 회전을 반영한 퍼짐 위치다.
            Vector3 spreadPosition = startPosition +
                _moneyContainer.TransformVector(localOffset);

            Money spawned = Instantiate(prefab, _moneyContainer);
            spawned.transform.position = startPosition;

            spawned.Initialize(
                spreadPosition,
                _moneyTarget,
                this,
                handleCollected);
        }
    }
}
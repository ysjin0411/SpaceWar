using System;
using UnityEngine;
using TMPro;

public class GoldUI : MonoBehaviour
{
    [Header("골드 UI 연결")]
    [Tooltip("골드가 최종적으로 이동할 CurrentGoldImage")]
    [SerializeField] RectTransform _goldTarget;

    [Tooltip("생성되는 골드를 자식으로 넣을 Canvas")]
    [SerializeField] RectTransform _goldContainer;

    [Header("골드 퍼짐 설정")]
    [Tooltip("적이 죽은 위치에서 골드가 퍼지는 반지름")]
    [Min(0f)]
    [SerializeField] float _spreadRadius = 80f;

    [Header("골드 수량 표시")]
    [Tooltip("JSON에 저장된 현재 총 골드를 표시할 텍스트")]
    [SerializeField] TMP_Text _currentGoldText;

    /// <summary>
    /// Stage가 시작되면 JSON에서 불러온 현재 총 골드를 표시하고,
    /// 이후 골드 변경 이벤트를 구독한다.
    /// </summary>
    private void Start()
    {
        if (_currentGoldText == null)
        {
            throw new InvalidOperationException(
                $"{name}의 GoldUI에 Current Gold Text가 연결되지 않았습니다.");
        }

        if (SaveManager.Instance == null)
        {
            throw new InvalidOperationException(
                "SaveManager가 없습니다. Bootstrap 씬부터 실행해야 합니다.");
        }

        // 이후 골드가 변경될 때마다 텍스트를 갱신하도록 등록한다.
        SaveManager.Instance.GoldChanged += UpdateGoldText;

        // Stage 진입 직후 JSON에 저장된 현재 총 골드를 표시한다.
        UpdateGoldText(SaveManager.Instance.CurrentGold);
    }

    /// <summary>
    /// SaveManager가 전달한 총 골드를 화면에 표시한다.
    /// </summary>
    private void UpdateGoldText(int currentGold)
    {
        // 1000 이상의 숫자는 1,000 형식으로 표시한다.
        _currentGoldText.text = currentGold.ToString("N0");
    }

    /// <summary>
    /// 씬에서 GoldUI가 제거될 때 이벤트 구독을 해제한다.
    /// </summary>
    private void OnDestroy()
    {
        // SaveManager가 먼저 제거된 경우에는 해제할 대상이 없다.
        if (SaveManager.Instance == null)
        {
            return;
        }

        // 파괴된 GoldUI 함수가 이후에도 호출되지 않게 구독을 해제한다.
        SaveManager.Instance.GoldChanged -= UpdateGoldText;
    }

    /// <summary>
    /// 몬스터가 죽은 위치에 골드를 개수만큼 생성하고
    /// 360도를 균등하게 나눠 원형으로 퍼뜨린다.
    /// </summary>
    public void SpawnGold(Gold goldPrefab,Vector3 worldPosition,int dropCount)
    {
        if (goldPrefab == null)
        {
            throw new ArgumentNullException(nameof(goldPrefab));
        }

        if (dropCount <= 0)
        {
            return;
        }

        if (_goldTarget == null || _goldContainer == null)
        {
            throw new InvalidOperationException(
                $"{name}의 GoldUI 참조가 연결되지 않았습니다.");
        }

        if (Camera.main == null)
        {
            throw new InvalidOperationException(
                "MainCamera 태그가 설정된 카메라가 없습니다.");
        }

        // 적이 죽은 월드 위치를 UI 화면 좌표로 변환한다.
        Vector2 screenPosition =
            RectTransformUtility.WorldToScreenPoint(
                Camera.main,
                worldPosition);

        // 매번 똑같은 모양으로 퍼지지 않도록 시작 각도를 무작위로 정한다.
        float startAngle = UnityEngine.Random.Range(0f, 360f);

        // 드롭 개수만큼 골드를 생성한다.
        for (int index = 0; index < dropCount; index++)
        {
            // 360도를 드롭 개수로 균등하게 나눈다.
            float angle =
                startAngle +
                360f * index / dropCount;

            // 각도를 방향 벡터로 변환한다.
            float angleRadians = angle * Mathf.Deg2Rad;

            Vector2 spreadDirection = new Vector2(
                Mathf.Cos(angleRadians),
                Mathf.Sin(angleRadians));

            // 시작 위치에서 원의 바깥쪽에 있는 목적지를 계산한다.
            Vector3 spreadPosition =
                (Vector3)screenPosition +
                (Vector3)(spreadDirection * _spreadRadius);

            // Canvas 안에 골드 UI를 생성한다.
            Gold spawnedGold = Instantiate(
                goldPrefab,
                _goldContainer);

            // 모든 골드는 적이 죽은 위치에서 시작한다.
            spawnedGold.transform.position = screenPosition;

            // 각 골드마다 서로 다른 원형 목적지를 전달한다.
            spawnedGold.Initialize(
                spreadPosition,
                _goldTarget,
                this);
        }
    }

    /// <summary>
    /// 골드가 UI 목적지에 도착하면 실제 세이브 골드를 증가시킨다.
    /// </summary>
    public void CollectGold(int amount)
    {
        if (SaveManager.Instance == null)
        {
            throw new InvalidOperationException(
                "SaveManager가 없습니다. Bootstrap 씬부터 실행해야 합니다.");
        }

        SaveManager.Instance.AddGold(amount);
    }
}
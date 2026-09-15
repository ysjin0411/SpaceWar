using System;
using UnityEngine;
using TMPro;
using System.Collections.Generic;

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

    // 골드 한 개가 도착할 때마다 생성할 획득 텍스트 프리팹이다.
    [SerializeField] private GoldGainPopup _goldGainPopupPrefab;

    // 골드 아이콘의 위쪽 끝에서 텍스트 중심까지의 추가 간격이다.
    [SerializeField, Min(0f)] private float _popupOffsetY = 20f;

    // 동시에 여러 개가 도착해도 하나씩 구분해서 보여줄 최소 간격이다.
    // 실제 골드 지급에는 영향을 주지 않는다.
    [SerializeField, Min(0.01f)] private float _popupInterval = 0.06f;

    // 각 골드의 획득량을 개별적으로 보관한다. 금액을 합산하지 않는다.
    private readonly Queue<int> _pendingGoldPopups = new Queue<int>();

    // 다음 획득 텍스트를 표시할 때까지 남은 시간이다.
    private float _popupTimer;

    // 이번 스테이지에서 골드를 획득할 때 획득량을 외부에 알린다.
    public event Action<int> GoldCollected;

    /// <summary>
    /// Stage가 시작되면 JSON에서 불러온 현재 총 골드를 표시하고,
    /// 이후 골드 변경 이벤트를 구독한다.
    /// </summary>
    private void Start()
    {
        // 연출에 필요한 프리팹과 기준 아이콘 연결을 시작 시 검사한다.
        if (_goldGainPopupPrefab == null || _goldTarget == null)
        {
            throw new InvalidOperationException(
                $"{name}: Gold Gain Popup Prefab과 Gold Target을 연결하세요.");
        }

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

    private void Update()
    {
        // 일시정지 중에는 표시 간격과 텍스트 재생을 함께 멈춘다.
        if (Time.timeScale <= 0f)
        {
            return;
        }

        // 다음 텍스트를 표시하기까지의 시간을 줄인다.
        _popupTimer = Mathf.Max(
            0f, _popupTimer - Time.deltaTime);

        // 표시할 금액이 없거나 아직 간격이 지나지 않았다면 기다린다.
        if (_pendingGoldPopups.Count == 0 || _popupTimer > 0f)
        {
            return;
        }

        // 가장 먼저 도착한 골드의 금액 하나만 꺼낸다.
        int amount = _pendingGoldPopups.Dequeue();

        // 매번 새로운 텍스트를 생성하므로 기존 숫자와 합쳐지지 않는다.
        GoldGainPopup popup = Instantiate(
            _goldGainPopupPrefab,
            _goldTarget,
            false);

        // 아이콘의 위쪽 끝보다 조금 높은 곳에서 시작한다.
        Vector2 startPosition = new Vector2(
            0f,
            _goldTarget.rect.height * 0.5f + _popupOffsetY);

        popup.Play(amount, startPosition);

        // 다음 텍스트는 설정한 간격 뒤에 생성한다.
        _popupTimer = _popupInterval;
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
    public void SpawnGold(Gold goldPrefab,Vector3 worldPosition,int dropCount, Action onAllCollected = null)
    {
        if (goldPrefab == null)
        {
            throw new ArgumentNullException(nameof(goldPrefab));
        }

        // 생성할 골드가 없다면 기다릴 획득 연출도 없다.
        if (dropCount <= 0)
        {
            onAllCollected?.Invoke();
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

        // 이번 SpawnGold 호출에서 생성하는 골드만 추적한다.
        // 다른 몬스터가 떨어뜨린 골드의 획득에는 영향을 받지 않는다.
        int remainingCount = dropCount;

        // 골드 한 개가 실제로 지급될 때마다 남은 개수를 줄인다.
        Action handleOneCollected = () =>
        {
            if (remainingCount <= 0)
            {
                return;
            }

            remainingCount--;

            // 마지막 골드까지 지급된 시점에 한 번만 완료를 알린다.
            if (remainingCount == 0)
            {
                onAllCollected?.Invoke();
            }
        };

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

            // 각 골드가 지급을 마치면 이번 묶음의 남은 개수를 줄인다.
            spawnedGold.Initialize(
                spreadPosition,
                _goldTarget,
                this,
                handleOneCollected);
        }
    }

    public void CollectGold(int amount)
    {
        // 유효한 획득량만 처리한다.
        if (amount <= 0)
        {
            return;
        }

        if (SaveManager.Instance == null)
        {
            throw new InvalidOperationException(
                "SaveManager가 없습니다. Bootstrap 씬부터 실행해야 합니다.");
        }

        // 골드가 도착한 즉시 실제 보유량과 저장 데이터에 반영한다.
        SaveManager.Instance.AddGold(amount);

        // 이번 골드 한 개의 금액을 표시 대기열에 넣는다.
        // 다른 골드의 금액과 합산하지 않는다.
        _pendingGoldPopups.Enqueue(amount);

        // 기존 스테이지 결과 집계에도 획득량을 전달한다.
        GoldCollected?.Invoke(amount);
    }
}
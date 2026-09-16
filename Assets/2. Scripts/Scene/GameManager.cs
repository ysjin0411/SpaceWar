using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 스테이지의 증강 게이지와 증강 시작·종료 흐름을 관리한다.
/// 증강 후보 선택과 UI 표시는 Upgrader MVP 시스템에 위임한다.
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("스테이지 UI")]
    // 증강 게이지 화면 표시와 연출을 담당하는 UI
    [SerializeField] private GaugeUIManager _gaugeUIManager;

    [Header("증강 게이지")]

    // 증강 선택이 열리는 최대 게이지
    [Min(0.01f)]
    [SerializeField] private float _maxGaugeBar = 100f;

    // 현재까지 충전된 증강 게이지
    [SerializeField] private float _timeCharge;

    // 증강이 적용되지 않았을 때의 초당 자연 충전량
    [Min(0f)]
    [SerializeField] private float _naturalGaugeChargePerSecond = 1f;

    [Header("스테이지 획득량")]

    // 이번 스테이지에서 획득한 경험치 총합
    [SerializeField] int _stageEarnedExperience;

    // 이번 스테이지에서 실제로 획득한 골드 총합
    [SerializeField] int _stageEarnedGold;

    // 이번 스테이지에서 실제로 지급한 User 경험치의 총합.
    // 누적 합계는 넉넉한 정수 범위로 관리한다.
    [SerializeField] private long _stageEarnedUserExperience;

    // 재화 종류별 수집 UI 목록이다.
    // 같은 MoneyType을 처리하는 UI는 하나만 등록한다.
    [SerializeField] private MoneyUI[] _moneyUIs = new MoneyUI[0];

    // 월드의 보스 위치를 화면 좌표로 변환할 게임 카메라.
    [SerializeField] private Camera _itemWorldCamera;

    // 화면 전체를 덮는 Overlay Canvas 아래의 아이템 연출 영역.
    [SerializeField] private RectTransform _itemEffectRoot;

    // 기존 STAGE 안내 위치에 퀘스트 진행도를 표시할 담당자.
    [SerializeField] private StageFlowManager _itemNoticeFlow;

    // 여러 아이템을 얻었을 때 연출이 겹치지 않도록 순서대로 보관한다.
    private readonly Queue<IEnumerator> _itemEffectQueue =
        new Queue<IEnumerator>();

    // 연출 대기열이 중복으로 실행되는 것을 막는다.
    private bool _isPlayingItemEffects;

    // Money UIs에서 찾은 골드 UI를 보관한다.
    // 골드 획득 이벤트를 구독하고, 종료 시 같은 대상에서 해제한다.
    private GoldUI _goldUI;

    // 결과창에서 이번 판의 User 경험치 획득량을 읽는다.
    public long StageEarnedUserExperience => _stageEarnedUserExperience;

    // 결과창에서 읽을 수 있도록 획득량을 읽기 전용으로 공개한다.
    public int StageEarnedExperience => _stageEarnedExperience;
    public int StageEarnedGold => _stageEarnedGold;

    // 같은 GameObject에 존재하는 캐릭터 성장 시스템
    private CharacterProgression _characterProgression;

    // 같은 GameObject에 존재하는 증강 Composition Root
    private Upgrader _upgrader;

    // 현재 증강 선택 화면이 열려 있는지 확인한다.
    private bool _isUpgradeOpen;

    // 이번 스테이지에서 경험치를 획득할 때 획득량을 외부에 알린다.
    public event Action<int> ExperienceGained;

    private void Awake()
    {
        _characterProgression =
            GetComponent<CharacterProgression>();

        _upgrader =
            GetComponent<Upgrader>();

        if (_characterProgression == null)
        {
            throw new MissingComponentException(
                $"{name}에 CharacterProgression 컴포넌트가 없습니다.");
        }

        if (_upgrader == null)
        {
            throw new MissingComponentException(
                $"{name}에 Upgrader 컴포넌트가 없습니다.");
        }

        if (_gaugeUIManager == null)
        {
            throw new InvalidOperationException(
                $"{name}의 GaugeUIManager가 연결되지 않았습니다.");
        }

        if (_maxGaugeBar <= 0f)
        {
            throw new InvalidOperationException(
                $"{name}의 MaxGaugeBar는 0보다 커야 합니다.");
        }

        if (_naturalGaugeChargePerSecond < 0f)
        {
            throw new InvalidOperationException(
                $"{name}의 자연 게이지 충전속도는 0 이상이어야 합니다.");
        }

        // Inspector에서 등록한 Money UIs 목록에서 골드용 UI를 찾는다.
        MoneyUI moneyUI = GetMoneyUI(MoneyType.Gold);

        // 골드 획득 이벤트를 제공하는 GoldUI인지 확인한다.
        if (!(moneyUI is GoldUI goldUI))
        {
            throw new InvalidOperationException(
                $"{name}: Gold 종류의 Money UI에는 GoldUI를 등록해주세요.");
        }

        // 이벤트 구독과 해제에 사용할 대상을 기억한다.
        _goldUI = goldUI;

        // 실제 골드 지급이 끝났을 때 스테이지 획득량을 집계한다.
        _goldUI.GoldCollected += HandleGoldCollected;
    }

    private void Start()
    {
        // 새로운 스테이지가 시작됐으므로 이번 판의 획득량을 초기화한다.
        _stageEarnedExperience = 0;
        _stageEarnedGold = 0;
        // 새 스테이지 또는 재시작 시 이번 판의 집계만 초기화한다.
        // 저장된 User 경험치는 유지한다.
        _stageEarnedUserExperience = 0;

        // 새로운 스테이지에서는 빈 게이지부터 시작한다.
        _timeCharge = 0f;
        _isUpgradeOpen = false;

        _gaugeUIManager.StartCharging(
            _timeCharge,
            _maxGaugeBar);
    }

    /// <summary>
    /// 현재 스테이지의 캐릭터에게 경험치를 지급한다.
    /// 지급된 경험치 양을 결과 집계 시스템에 알린다.
    /// </summary>
    public void AddExperience(int amount)
    {
        // 0 이하의 경험치는 지급하거나 집계하지 않는다.
        if (amount <= 0)
        {
            return;
        }

        // 결과창에 표시하기 위해 이번 스테이지 획득 경험치를 누적한다.
        _stageEarnedExperience += amount;

        // 기존 캐릭터 경험치 지급
        _characterProgression.AddExp(amount);

        // 이번 스테이지에서 획득한 경험치 양을 외부에 알린다.
        ExperienceGained?.Invoke(amount);
    }

    /// <summary>
    /// 적 처치 등의 보상으로 게이지를 즉시 증가시킨다.
    /// 자연 충전속도 증강은 이 보너스에는 영향을 주지 않는다.
    /// </summary>
    public void BonusGauge(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        // 증강 선택 중에는 추가 게이지를 받지 않는다.
        if (_isUpgradeOpen)
        {
            return;
        }

        _timeCharge = Mathf.Min(
            _maxGaugeBar,
            _timeCharge + amount);

        _gaugeUIManager.AnimateBonus(
            _timeCharge,
            _maxGaugeBar);

        if (_timeCharge >= _maxGaugeBar)
        {
            OpenUpgrade();
        }
    }

    private void Update()
    {
        // 증강 선택 중에는 자연 게이지를 충전하지 않는다.
        if (_isUpgradeOpen)
        {
            return;
        }

        _timeCharge = Mathf.Min(
            _maxGaugeBar,
            _timeCharge +
            Time.deltaTime * _naturalGaugeChargePerSecond);

        if (_timeCharge >= _maxGaugeBar)
        {
            OpenUpgrade();
        }
    }

    /// <summary>
    /// 증강 선택을 종료하고 인게임과 게이지 충전을 재개한다.
    /// 증강 UI 종료는 UpgradeSelectionView가 담당한다.
    /// </summary>
    public void ExitUpgrade()
    {
        _isUpgradeOpen = false;
        _timeCharge = 0f;

        // 증강 선택 중 멈췄던 인게임 시간을 복구한다.
        Time.timeScale = 1f;

        // 빈 게이지부터 다음 증강 충전을 시작한다.
        _gaugeUIManager.StartCharging(
            _timeCharge,
            _maxGaugeBar);
    }

    // 재화 종류에 맞는 UI로 생성 요청을 전달한다.
    public void SpawnMoney(
        Money prefab,
        Vector3 worldPosition,
        int count,
        Action onAllCollected = null)
    {
        if (prefab == null)
        {
            throw new ArgumentNullException(nameof(prefab));
        }

        GetMoneyUI(prefab.Type).SpawnMoney(
            prefab,
            worldPosition,
            count,
            onAllCollected);
    }

    /// <summary>
    /// 게이지가 최대치에 도달하면 증강 선택을 시작한다.
    /// </summary>
    private void OpenUpgrade()
    {
        // 다른 시스템이 게임을 정지한 동안에는 증강창을 열지 않는다.
        // 게이지는 유지되므로 게임 재개 후 Update에서 다시 시도한다.
        if (_isUpgradeOpen || Time.timeScale <= 0f)
        {
            return;
        }

        _isUpgradeOpen = true;

        // 게이지를 정확히 최대치 상태로 표시한다.
        _gaugeUIManager.CompleteGauge();

        // 증강 선택 중에는 인게임 시간을 정지한다.
        Time.timeScale = 0f;

        // 후보 추첨과 화면 표시는 Upgrader에 위임한다.
        _upgrader.UpgradeSelectFirst();
    }

    /// <summary>
    /// 시간에 따라 충전되는 게이지의 초당 충전량을 증가시킨다.
    /// </summary>
    public void IncreaseNaturalGaugeChargeRate(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        _naturalGaugeChargePerSecond += amount;
    }

    /// <summary>
    /// 골드가 실제로 지급됐을 때 이번 스테이지 획득 골드를 누적한다.
    /// </summary>
    private void HandleGoldCollected(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        _stageEarnedGold += amount;
    }

    private void OnDestroy()
    {
        // 파괴된 GameManager의 함수가 호출되지 않도록 이벤트 연결을 해제한다.
        if (_goldUI != null)
        {
            _goldUI.GoldCollected -= HandleGoldCollected;
        }
    }

    // 몬스터 처치로 얻은 User 경험치를 지급하고 결과창용으로 집계한다.
    public void AddUserExperience(int amount)
    {
        // 보상이 0인 몬스터는 지급과 저장을 생략한다.
        if (amount <= 0)
        {
            return;
        }

        if (SaveManager.Instance == null)
        {
            Debug.LogError(
                "User 경험치 지급에 필요한 SaveManager가 없습니다.",
                this);
            return;
        }

        try
        {
            // User 레벨업과 파일 저장까지 완료한다.
            SaveManager.Instance.AddUserExperience(amount);
        }
        catch (Exception exception)
        {
            // 지급 오류는 기록하되 몬스터의 나머지 사망 처리는 계속 진행한다.
            // 실패한 보상은 결과창 획득량에 포함하지 않는다.
            Debug.LogException(exception, this);
            return;
        }

        // 실제 저장에 성공한 보상만 누적한다.
        _stageEarnedUserExperience += amount;
    }

    // 프리팹의 재화 종류에 맞는 UI를 찾는다.
    public MoneyUI GetMoneyUI(MoneyType type)
    {
        MoneyUI result = null;

        foreach (MoneyUI moneyUI in _moneyUIs)
        {
            if (moneyUI == null)
            {
                throw new InvalidOperationException(
                    "Money UIs에 비어 있는 항목이 있습니다.");
            }

            if (moneyUI.Type != type)
            {
                continue;
            }

            if (result != null)
            {
                throw new InvalidOperationException(
                    $"{type}의 MoneyUI가 중복 등록됐습니다.");
            }

            result = moneyUI;
        }

        if (result == null)
        {
            throw new InvalidOperationException(
                $"{type}의 MoneyUI가 등록되지 않았습니다.");
        }

        return result;
    }

    // 드롭 초기화 단계에서 아이템 연출 연결을 검사한다.
    public void ValidateItemEffectConfiguration()
    {
        if (_itemWorldCamera == null ||
            _itemEffectRoot == null ||
            _itemNoticeFlow == null)
        {
            throw new InvalidOperationException(
                $"{name}: 아이템 연출 카메라, 영역, StageFlowManager를 연결해주세요.");
        }

        Canvas canvas = _itemEffectRoot.GetComponentInParent<Canvas>();

        if (canvas == null ||
            canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            throw new InvalidOperationException(
                "Item Effect Root는 Overlay Canvas 아래에 배치해주세요.");
        }
    }

    // 이미 지급된 아이템의 연출과 진행도 표시를 예약한다.
    public void EnqueueItemEffect(
        Item prefab,
        Vector3 worldPosition,
        List<string> progressMessages,
        Action onCompleted)
    {
        // 처치 순간의 화면 위치를 기억한다.
        // 대기 중 보스가 제거되더라도 Transform을 다시 참조하지 않는다.
        Vector3 screenPosition =
            _itemWorldCamera.WorldToScreenPoint(worldPosition);

        _itemEffectQueue.Enqueue(
            PlayItemEffect(
                prefab,
                new Vector2(screenPosition.x, screenPosition.y),
                progressMessages,
                onCompleted));

        if (_isPlayingItemEffects)
        {
            return;
        }

        _isPlayingItemEffects = true;
        StartCoroutine(ProcessItemEffects());
    }

    // 대기 중인 아이템을 하나씩 표시한다.
    private IEnumerator ProcessItemEffects()
    {
        while (_itemEffectQueue.Count > 0)
        {
            yield return _itemEffectQueue.Dequeue();
        }

        _isPlayingItemEffects = false;
    }

    // 아이템 연출 후 해당 획득의 진행도를 표시하고 완료를 알린다.
    private IEnumerator PlayItemEffect(
        Item prefab,
        Vector2 startScreenPosition,
        List<string> progressMessages,
        Action onCompleted)
    {
        // 생성한 인스턴스에서 Item의 연출을 실행한다.
        Item instance = Instantiate(prefab, _itemEffectRoot, false);

        yield return instance.PlayCollectEffect(
            _itemEffectRoot, startScreenPosition);

        Destroy(instance.gameObject);

        // 아이콘이 사라진 다음 진행도 문구를 표시한다.
        yield return _itemNoticeFlow.ShowQuestProgress(progressMessages);

        if (onCompleted != null)
        {
            onCompleted.Invoke();
        }
    }
}
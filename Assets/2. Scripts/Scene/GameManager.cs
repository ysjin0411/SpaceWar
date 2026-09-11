using System;
using UnityEngine;

/// <summary>
/// 스테이지의 증강 게이지와 증강 시작·종료 흐름을 관리한다.
/// 증강 후보 선택과 UI 표시는 Upgrader MVP 시스템에 위임한다.
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("스테이지 UI")]

    // 몬스터가 떨어뜨린 골드를 화면에 표시하는 UI
    [SerializeField] private GoldUI _goldUI;

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

        if (_goldUI == null)
        {
            throw new InvalidOperationException(
                $"{name}의 GoldUI가 연결되지 않았습니다.");
        }

        // 골드가 실제로 UI에 도착해 지급됐을 때 획득량을 누적한다.
        _goldUI.GoldCollected += HandleGoldCollected;
    }

    private void Start()
    {
        // 새로운 스테이지가 시작됐으므로 이번 판의 획득량을 초기화한다.
        _stageEarnedExperience = 0;
        _stageEarnedGold = 0;

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

    // 골드 생성 요청과 묶음 전체의 획득 완료 함수를 GoldUI에 전달한다.
    public void SpawnGold(
        Gold goldPrefab,
        Vector3 worldPosition,
        int dropCount,
        Action onAllCollected = null)
    {
        if (_goldUI == null)
        {
            throw new InvalidOperationException(
                $"{name}에 GoldUI가 연결되지 않았습니다.");
        }

        _goldUI.SpawnGold(
            goldPrefab,
            worldPosition,
            dropCount,
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
}
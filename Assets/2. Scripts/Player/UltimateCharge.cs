using System;
using UnityEngine;

// 현재 전투에서 획득한 필살기 충전 수를 관리한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController))]
public sealed class UltimateCharge : MonoBehaviour
{
    // 필살기 준비 상태가 되기 위해 필요한 건전지 개수다.
    [SerializeField, Min(1)] private int _requiredCount = 3;

    // Inspector에서 실행 중 충전 개수를 확인할 수 있다.
    [SerializeField] private int _currentCount;

    // 현재 캐릭터의 실제 필살기 구현이다.
    private UltimateAbility _ability;

    // 화면 기준 필살기 위치를 계산할 씬 카메라다.
    private Camera _worldCamera;

    // 캐릭터 프리팹 내부의 플레이어 컴포넌트다.
    private PlayerController _player;

    // 씬에 있는 참조는 StageManager가 생성 후 전달한다.
    private StageFlowManager _stageFlow;
    private UltimateUI _ui;

    // 씬 참조가 모두 연결된 이후에만 획득을 처리한다.
    private bool _isInitialized;

    public int CurrentCount => _currentCount;
    public int RequiredCount => _requiredCount;
    public bool IsReady => _currentCount >= _requiredCount;

    private void Awake()
    {
        _player = GetComponent<PlayerController>();

        // 솔져와 워리어가 서로 다른 구현을 가져도 같은 방식으로 호출한다.
        _ability = GetComponent<UltimateAbility>();
    }

    private void Update()
    {
        if (!_isInitialized ||
            !IsReady ||
            !_player.CanControl ||
            Time.timeScale <= 0f ||
            !IsBattlePhase())
        {
            return;
        }

        if (_ability == null || _ability.IsRunning)
        {
            return;
        }

        if (!Input.GetKeyDown(KeyCode.R))
        {
            return;
        }

        // 실제 공격이 시작된 경우에만 충전을 소비한다.
        if (_ability.TryActivate(_worldCamera))
        {
            ResetCharge();
        }
    }

    // 현재 진행 중인 공격과 남은 연출을 정리한다.
    private void CancelAbility()
    {
        if (_ability != null)
        {
            _ability.Cancel();
        }
    }

    // 생성된 플레이어에 현재 스테이지의 시스템과 UI를 연결한다.
    public void Initialize(
        StageFlowManager stageFlow,
        UltimateUI ui,
        Camera worldCamera)
    {
        if (_isInitialized)
        {
            throw new InvalidOperationException(
                $"{name}: UltimateCharge가 이미 초기화됐습니다.");
        }

        if (stageFlow == null ||
            ui == null ||
            worldCamera == null ||
            _requiredCount < 1)
        {
            throw new InvalidOperationException(
                $"{name}: 필살기 충전 설정과 씬 참조를 확인해주세요.");
        }

        // 실행 전에 프리팹 설정 오류를 확인한다.
        // 아직 필살기를 연결하지 않은 캐릭터는 충전 기능만 유지한다.
        if (_ability != null)
        {
            _ability.ValidateConfiguration();
        }

        _worldCamera = worldCamera;

        ui.ValidateConfiguration(_requiredCount);

        _stageFlow = stageFlow;
        _ui = ui;
        _currentCount = 0;
        _isInitialized = true;

        // 사망 또는 스테이지 종료 시 충전 상태를 정리한다.
        _player.Died += HandlePlayerDied;
        _stageFlow.PhaseChanged += HandlePhaseChanged;

        RefreshUI();
    }

    // true는 아이템을 소비해도 된다는 뜻이다.
    // 최대 충전 상태에서도 true를 반환하되 충전 수는 바꾸지 않는다.
    public bool TryCollect()
    {
        if (!_isInitialized ||
            !isActiveAndEnabled ||
            !_player.CanControl ||
            Time.timeScale <= 0f ||
            !IsBattlePhase())
        {
            return false;
        }

        if (IsReady)
        {
            return true;
        }

        _currentCount++;
        RefreshUI();
        GameAudio.Play(IsReady ? GameSound.UltimateReady : GameSound.PickupUltimate);

        return true;
    }

    // 입장, 구간 이동, 보스 경고, 결과 화면에서는 획득하지 않는다.
    private bool IsBattlePhase()
    {
        StagePhase phase = _stageFlow.CurrentPhase;

        return phase == StagePhase.Section1 ||
               phase == StagePhase.MidBossBattle ||
               phase == StagePhase.Section2 ||
               phase == StagePhase.FinalBossBattle;
    }

    // 충전 데이터가 변경된 경우에만 UI를 갱신한다.
    private void RefreshUI()
    {
        _ui.Show(_currentCount, IsReady);
    }

    // 플레이어가 사망하면 준비 프레임과 충전을 제거한다.
    private void HandlePlayerDied()
    {
        CancelAbility();
        ResetCharge();
    }

    // 같은 스테이지의 구간 이동에서는 충전을 유지한다.
    // 클리어와 실패에서만 초기화한다.
    private void HandlePhaseChanged(StagePhase phase)
    {
        if (phase == StagePhase.SectionTransition)
        {
            // 다음 구간으로 이전 공격이 넘어가지 않도록 정리한다.
            // 사용하지 않고 모아둔 충전은 그대로 유지한다.
            CancelAbility();
            return;
        }

        if (phase == StagePhase.Cleared ||
            phase == StagePhase.Failed)
        {
            CancelAbility();
            ResetCharge();
        }
    }

    private void ResetCharge()
    {
        _currentCount = 0;

        if (_ui != null)
        {
            _ui.Show(0, false);
        }
    }

    private void OnDestroy()
    {
        // 씬 종료 시 파괴된 플레이어를 이벤트가 참조하지 않게 한다.
        if (_player != null)
        {
            _player.Died -= HandlePlayerDied;
        }

        if (_stageFlow != null)
        {
            _stageFlow.PhaseChanged -= HandlePhaseChanged;
        }
    }
}
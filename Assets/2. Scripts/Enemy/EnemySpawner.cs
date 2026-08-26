using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 현재 스테이지에서 사용할 적 생성 패턴을 선택하고 실행한다.
/// 구체적인 적 종류와 배치 방식은 EnemySpawnPattern 자식 클래스가 담당한다.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("적 생성 패턴")]

    [SerializeField]
    List<EnemySpawnPattern> _patterns =
        new List<EnemySpawnPattern>(); // 현재 스테이지에서 사용할 패턴 에셋 목록

    [Header("스테이지 참조")]

    [SerializeField]
    StageFlowManager _stageFlowManager; // 일반 전투와 보스 전투 구간을 구분하는 관리자

    [Header("패턴 실행 설정")]

    [Min(0.1f)]
    [SerializeField]
    float _patternCoolTime = 3f; // 서로 다른 패턴을 실행하는 공통 시간 간격

    // 다음 패턴을 실행하기까지 누적된 시간
    float _timer;

    // StageManager로부터 필요한 런타임 정보를 전달받았는지 확인
    bool _isInitialized;

    // 현재 일반 적 패턴을 생성할 수 있는 전투 구간인지 확인
    bool _isSpawning;

    // 현재 플레이어와 GameManager를 패턴에 전달하는 실행 컨텍스트
    EnemySpawnContext _spawnContext;

    // 패턴별 쿨다운 관리와 가중치 선택을 담당하는 선택기
    EnemySpawnPatternSelector _patternSelector;

    /// <summary>
    /// StageManager로부터 현재 플레이어와 GameManager를 전달받고
    /// 패턴 실행에 필요한 런타임 객체를 생성한다.
    /// </summary>
    public void Initialize(
        PlayerController playerController,
        GameManager gameManager)
    {
        if (playerController == null)
        {
            throw new ArgumentNullException(
                nameof(playerController));
        }

        if (gameManager == null)
        {
            throw new ArgumentNullException(
                nameof(gameManager));
        }

        if (_stageFlowManager == null)
        {
            throw new InvalidOperationException(
                "EnemySpawner에 StageFlowManager가 연결되지 않았습니다.");
        }

        // 현재 스테이지 정보를 패턴에 전달할 실행 컨텍스트를 만든다.
        _spawnContext = new EnemySpawnContext(
            playerController,
            gameManager);

        // Inspector에 등록된 패턴 에셋들로 범용 선택기를 만든다.
        // 목록이 비었거나 중복된 패턴이 있으면 선택기 생성 시 예외가 발생한다.
        _patternSelector = new EnemySpawnPatternSelector(
            _patterns);

        // 새로운 스테이지 실행이므로 공통 패턴 타이머를 초기화한다.
        _timer = 0f;

        _isInitialized = true;

        // 초기화 시점이 일반 전투 구간이라면 바로 패턴 생성을 허용한다.
        _isSpawning =
            _stageFlowManager.CurrentPhase == StagePhase.Section1 ||
            _stageFlowManager.CurrentPhase == StagePhase.Section2;
    }

    private void Update()
    {
        // 초기화 전이거나 보스 관련 구간이면 일반 적을 생성하지 않는다.
        if (!_isInitialized || !_isSpawning)
        {
            return;
        }

        // 일반 전투 중에만 패턴별 남은 쿨다운을 감소시킨다.
        _patternSelector.UpdateCooldowns(
            Time.deltaTime);

        // 다음 패턴을 실행하기 위한 공통 시간을 누적한다.
        _timer += Time.deltaTime;

        if (_timer < _patternCoolTime)
        {
            return;
        }

        // 모든 패턴이 개별 쿨다운 중이면
        // 공통 타이머를 유지하고 다음 프레임에 다시 선택을 시도한다.
        if (!_patternSelector.TrySelect(
            out EnemySpawnPatternRuntimeState selectedState))
        {
            return;
        }

        // 선택된 패턴이 자신에게 설정된 적과 대형을 생성한다.
        selectedState.Pattern.Execute(
            _spawnContext);

        // 패턴 실행이 정상적으로 끝난 뒤 개별 쿨다운을 시작한다.
        selectedState.StartCooldown();

        // 다음 패턴 실행 간격을 처음부터 측정한다.
        _timer = 0f;
    }

    private void OnEnable()
    {
        // 스테이지 전투 구간 변경을 감지한다.
        if (_stageFlowManager != null)
        {
            _stageFlowManager.PhaseChanged +=
                HandlePhaseChanged;
        }
    }

    private void OnDisable()
    {
        // 비활성화되거나 씬이 종료될 때 이벤트 구독을 해제한다.
        if (_stageFlowManager != null)
        {
            _stageFlowManager.PhaseChanged -=
                HandlePhaseChanged;
        }
    }

    /// <summary>
    /// 스테이지 구간에 따라 일반 적 생성을 시작하거나 중단한다.
    /// </summary>
    private void HandlePhaseChanged(
        StagePhase phase)
    {
        switch (phase)
        {
            case StagePhase.Section1:
            case StagePhase.Section2:
                StartSpawning();
                break;

            default:
                StopSpawning();
                break;
        }
    }

    /// <summary>
    /// 새로운 일반 전투 구간의 적 생성을 시작한다.
    /// </summary>
    public void StartSpawning()
    {
        // 새로운 전투 구간에서는 공통 생성 시간을 초기화한다.
        _timer = 0f;

        // 중간보스 이후 Section2가 시작될 때
        // 이전 전투 구간에서 남은 패턴 쿨다운을 초기화한다.
        if (_patternSelector != null)
        {
            _patternSelector.ResetCooldowns();
        }

        _isSpawning = true;
    }

    /// <summary>
    /// 보스 경고와 보스 전투 중 일반 적 생성을 중단한다.
    /// </summary>
    public void StopSpawning()
    {
        _timer = 0f;
        _isSpawning = false;
    }
}
using System;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;

// 외계인 보스의 참조와 팔의 기본 상태를 관리한다.
public sealed class Boss_Alien : BossEnemy
{
    // 로봇 파괴부터 본체 등장 완료까지 전환 상태를 유지한다.
    private bool _isPhaseTransitioning;

    // 전환 연출 중에는 보스에게 들어오는 피해를 차단한다.
    protected override bool CanReceiveDamage =>
        base.CanReceiveDamage && !_isPhaseTransitioning;

    // 팔과 손의 시각적 연결을 담당한다.
    [SerializeField] private BossAlienArmRig _armRig;

    // 조준 방향과 최대 팔 길이를 계산하는 어깨 위치다.
    [SerializeField] private Transform _armSocket;

    // 준비 자세에서 손목이 이동할 위치다.
    // HandRoot의 자식으로 만들지 않는다.
    [SerializeField] private Transform _handPreparePoint;

    // 본체 애니메이션에서 발생하는 공격 타이밍을 전달한다.
    [SerializeField] private BossAlienAnimationEventRelay _animationEventRelay;

    // 미사일 발사 위치를 순서대로 연결한다.
    // 배열 인덱스 0~3이 각 소켓 번호에 대응한다.
    [SerializeField] private Transform[] _missileSockets;

    // 팔을 뻗는 동안 플레이어와 접촉했는지 검사한다.
    [SerializeField] private BossAlienGrabSensor _grabSensor;

    // 잡힌 플레이어의 이동 권한과 손 추적을 관리한다.
    [SerializeField] private BossAlienGrabController _grabController;

    // 잡은 플레이어를 옮겨 흔들 때 사용할 손목 기준 위치다.
    [SerializeField] private Transform _handHoldPoint;

    // 던지기 직전에 손목을 당길 위치다.
    [SerializeField] private Transform _handThrowWindupPoint;

    // 균열 실드의 피해 감소율을 읽을 설정이다.
    [SerializeField] private BossAlienPhaseOneConfig _phaseOneConfig;

    // 현재 남아 있는 균열 수와 변경 이벤트를 제공한다.
    [SerializeField] private BossAlienCrackController _crackController;

    // 켜고 끌 실드 외형이다. 자식 ShieldVisual을 연결한다.
    [SerializeField] private GameObject _shieldVisual;

    // 본체 등장 연출을 재생할 BodyVisual의 Animator다.
    [SerializeField] private Animator _phaseTransitionAnimator;

    // 전환 스프라이트에 팔이 포함되어 있으므로 숨길 기존 GrabArm이다.
    [SerializeField] private GameObject _robotArmRoot;

    // 로봇 파괴 연출이 끝난 뒤 관리자가 생성할 본체 프리팹이다.
    [SerializeField] private Boss_AlienBody _phaseTwoPrefab;

    // 본체 루트가 생성될 월드 위치와 회전을 제공한다.
    [SerializeField] private Transform _phaseTwoSpawnPoint;

    // 비활성화나 플레이어 사망 시 중단할 전환 코루틴이다.
    private Coroutine _phaseTransitionRoutine;

    // Animator의 Base Layer에 만든 전환 상태를 가리킨다.
    private static readonly int PhaseTransitionState =
        Animator.StringToHash("Base Layer.Alien_PhaseTransition");

    [Header("체력 특수 패턴")]

    // 미사일 포위 포격처럼 체력 조건으로 발동할 패턴을 등록한다.
    // 균열 공명은 이 목록에 넣지 않는다.
    // 높은 체력 퍼센트부터 등록한다.
    [SerializeField]
    private List<HealthBossPatternEntry> _healthSpecialPatternEntries =
    new List<HealthBossPatternEntry>();

    [Header("균열 특수 패턴")]

    // 체력 목록과 별개로, 균열 3개에서만 요청할 실행 패턴이다.
    [SerializeField]
    private BossAlienResonancePattern _resonancePattern;

    // 균열 3스택이 완성된 순간 시스템 다이어로그에 표시할 경고다.
    [SerializeField, TextArea(2, 3)]
    private string _resonanceWarningMessage =
        "경고! 균열 공명 감지. 충격파 발생에 대비하십시오.";

    // 균열 공명 경고가 아직 표시 중인지 기억한다.
    private bool _isResonanceWarningPending;

    // 공명 패턴이 경고 종료를 기다릴 때 확인한다.
    public bool IsResonanceWarningPending => _isResonanceWarningPending;

    // 각 항목을 이미 요청했는지 기록한다.
    // 동일한 패턴을 여러 기준에 넣어도 항목마다 한 번씩 요청한다.
    private bool[] _hasRequestedHealthPatterns = Array.Empty<bool>();

    // 설정 검사가 끝난 뒤에만 체력 조건을 검사한다.
    private bool _isHealthPatternReady;

    // 기본 손목 위치를 보스 루트의 로컬 좌표로 보관한다.
    private Vector3 _restHandLocalPosition;

    // 필수 참조와 기본 위치 저장이 끝났는지 확인한다.
    private bool _isArmReady;

    // 패턴이 사용할 팔과 위치 정보를 제공한다.
    public BossAlienArmRig ArmRig => _armRig;
    public Transform ArmSocket => _armSocket;

    // 실행 중인 패턴이 공격 이벤트를 구독하고 해제할 때 사용한다.
    public BossAlienAnimationEventRelay AnimationEventRelay =>
        _animationEventRelay;
    public Vector3 PrepareHandPosition => _handPreparePoint.position;

    // 보스가 이동해도 기본 손 위치가 함께 이동하도록 계산한다.
    public Vector3 RestHandPosition =>
        transform.TransformPoint(_restHandLocalPosition);

    // 로봇이 전투 중이고 전환 연출에 들어가지 않았을 때만 공격한다.
    public bool CanRunArmPattern =>
        !_isPhaseTransitioning &&
        _isArmReady &&
        isActiveAndEnabled &&
        HasBattleStarted &&
        !IsDead &&
        TargetPlayer != null &&
        TargetPlayer.isActiveAndEnabled &&
        !TargetPlayer.IsDead;

    // 플레이어 사망 후 일반 패턴이 다시 요청되는 것을 막는다.
    protected override bool CanUseNormalPatterns => CanRunArmPattern;

    // Grab 패턴에 필요한 컴포넌트와 현재 월드 위치를 제공한다.
    public BossAlienGrabSensor GrabSensor => _grabSensor;
    public BossAlienGrabController GrabController => _grabController;
    public Vector3 HoldHandPosition => _handHoldPoint.position;
    public Vector3 ThrowWindupHandPosition => _handThrowWindupPoint.position;

    // 로봇과 본체를 합쳐 총 2페이즈인 보스다.
    public override int TotalPhaseCount => 2;

    // 이 프리팹은 로봇인 1페이즈만 담당한다.
    public override int CurrentPhaseNumber => 1;

    // 로봇의 체력은 실드바 위치에 표시한다.
    // 실제 피해를 막는 손톱 실드와는 별개의 UI 설정이다.
    public override bool UsesShieldHealthBar => true;

    protected override void Awake()
    {
        // 공통 체력, 이동 객체, 필수 컴포넌트를 초기화한다.
        base.Awake();

        // 균열 실드와 공명 실행에 필요한 연결을 검사한다.
        if (_phaseOneConfig == null ||
            _crackController == null ||
            _shieldVisual == null ||
            _resonancePattern == null)
        {
            throw new InvalidOperationException(
                $"{name}: Phase One Config, Crack Controller, " +
                "Shield Visual, Resonance Pattern을 연결하세요.");
        }

        // 보스 루트를 끄는 잘못된 연결을 방지한다.
        if (_shieldVisual == gameObject ||
            !_shieldVisual.transform.IsChildOf(transform))
        {
            throw new InvalidOperationException(
                $"{name}: Shield Visual에는 보스의 자식 외형을 연결하세요.");
        }

        // 전투 시작 시에는 균열이 없으므로 실드를 끈다.
        _shieldVisual.SetActive(false);

        // 공격 타이밍을 받을 컴포넌트가 연결되어 있는지 확인한다.
        if (_animationEventRelay == null)
        {
            throw new InvalidOperationException(
                $"{name}: Animation Event Relay를 연결하세요.");
        }

        // 팔 패턴에 필요한 프리팹 참조를 검사한다.
        if (_armRig == null ||
            _armRig.HandRoot == null ||
            _armSocket == null ||
            _handPreparePoint == null)
        {
            throw new InvalidOperationException(
                $"{name}: Arm Rig, Arm Socket, Hand Prepare Point를 연결하세요.");
        }

        // 준비 위치가 손을 따라 움직이는 잘못된 계층을 막는다.
        if (_handPreparePoint.IsChildOf(_armRig.HandRoot))
        {
            throw new InvalidOperationException(
                $"{name}: Hand Prepare Point는 HandRoot 밖에 배치하세요.");
        }

        // 생성 당시의 기본 손목 위치를 저장한다.
        _restHandLocalPosition =
            transform.InverseTransformPoint(_armRig.HandRoot.position);

        // 잡기와 던지기에 필요한 연결이 빠졌는지 확인한다.
        if (_grabSensor == null ||
            _grabController == null ||
            _handHoldPoint == null ||
            _handThrowWindupPoint == null)
        {
            throw new InvalidOperationException(
                $"{name}: Grab Sensor, Grab Controller와 손 위치 두 곳을 연결하세요.");
        }

        // 목적지가 손을 따라 움직이면 도착할 수 없으므로 잘못된 계층을 막는다.
        if (_handHoldPoint.IsChildOf(_armRig.HandRoot) ||
            _handThrowWindupPoint.IsChildOf(_armRig.HandRoot))
        {
            throw new InvalidOperationException(
                $"{name}: Hold Point와 Throw Windup Point는 HandRoot 밖에 두세요.");
        }

        _isArmReady = true;
        ResetArm();
    }

    protected override void OnBossInitialized()
    {
        base.OnBossInitialized();

        // 초기화된 플레이어의 사망 시 패턴을 즉시 중단한다.
        TargetPlayer.Died -= HandlePlayerDied;
        TargetPlayer.Died += HandlePlayerDied;

        // 전투 시작 시 체력 특수패턴 설정과 발동 기록을 준비한다.
        InitializeHealthPatterns();
    }

    private void OnEnable()
    {
        // 전투 도중 비활성화됐다가 켜진 경우 구독을 복구한다.
        if (TargetPlayer != null)
        {
            TargetPlayer.Died -= HandlePlayerDied;
            TargetPlayer.Died += HandlePlayerDied;
        }

        // 균열 개수가 바뀔 때만 실드 외형을 갱신한다.
        _crackController.CountChanged += HandleCrackCountChanged;

        // 균열 3스택 도달 이벤트를 받는다.
        _crackController.MaxStacksReached += HandleMaxCracksReached;

        HandleCrackCountChanged(_crackController.Count);
    }

    protected override void OnDisable()
    {
        // 전투 중단 시 진행 중인 본체 등장 처리를 취소한다.
        StopPhaseTransitionRoutine();

        // 비활성화된 보스가 다이어로그 완료 이벤트를 받지 않도록 정리한다.
        ClearResonanceWarningWait();

        // 비활성화된 보스가 균열 이벤트를 계속 받지 않도록 정리한다.
        if (_crackController != null)
        {
            _crackController.CountChanged -= HandleCrackCountChanged;
            // 비활성화 후에는 새 공명 패턴을 요청하지 않는다.
            _crackController.MaxStacksReached -= HandleMaxCracksReached;
        }

        if (_shieldVisual != null)
        {
            _shieldVisual.SetActive(false);
        }

        // 제거되거나 비활성화될 때 이벤트 구독을 해제한다.
        if (TargetPlayer != null)
        {
            TargetPlayer.Died -= HandlePlayerDied;
        }

        // 실행 중 반복자를 정리하여 패턴의 finally까지 실행한다.
        StopPatternsForPhaseChange();

        // 실행 전 대기 중이던 공명의 균열 보존 상태도 정리한다.
        if (_crackController != null)
        {
            _crackController.ClearAll();
        }

        // Runner가 중단 과정에서 이동을 재개할 수 있으므로
        // 중단 처리 다음에 이동을 잠근다.
        SetControllable(false);
        ResetArm();

        base.OnDisable();
    }

    private void HandlePlayerDied()
    {
        // 전투 중단 시 진행 중인 본체 등장 처리를 취소한다.
        StopPhaseTransitionRoutine();

        // 플레이어 사망은 timeScale이 0이어도 즉시 정리한다.
        StopPatternsForPhaseChange();

        // 실행 전 대기 중이던 공명의 균열 보존 상태도 정리한다.
        if (_crackController != null)
        {
            _crackController.ClearAll();
        }

        SetControllable(false);
        ResetArm();
    }

    public void ResetArm()
    {
        // Awake 도중 설정 오류가 난 경우에도 안전하게 종료한다.
        if (!_isArmReady || _armRig == null ||
            _armRig.HandRoot == null)
        {
            return;
        }

        // 정상 종료와 중단 모두 열린 손과 기본 위치로 복구한다.
        _armRig.SetHandClosed(false);
        _armRig.SetHandPosition(RestHandPosition);
    }

    // 지정한 소켓의 현재 월드 위치를 반환한다.
    // 보스가 이동해도 이동한 소켓 위치에서 발사할 수 있다.
    public Vector3 GetMissileSpawnPosition(int socketIndex)
    {
        if (_missileSockets == null ||
            socketIndex < 0 ||
            socketIndex >= _missileSockets.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(socketIndex), "미사일 소켓 범위를 벗어났습니다.");
        }

        if (_missileSockets[socketIndex] == null)
        {
            throw new InvalidOperationException(
                $"{name}: Missile Sockets의 {socketIndex}번을 연결하세요.");
        }

        return _missileSockets[socketIndex].position;
    }

    // 균열 개수에 따라 실드를 갱신하고, 균열이 없어지면 경고 대기도 해제한다.
    private void HandleCrackCountChanged(int count)
    {
        _shieldVisual.SetActive(
            isActiveAndEnabled && !IsDead && count > 0);

        // 패턴 종료나 사망으로 균열이 제거된 경우도 함께 처리한다.
        if (count == 0)
        {
            ClearResonanceWarningWait();
        }
    }

    // 균열 수와 관계없이 설정된 감소율을 한 번만 적용한다.
    protected override int CalculateIncomingDamage(int damage)
    {
        if (!_crackController.isActiveAndEnabled ||
            _crackController.Count == 0)
        {
            return damage;
        }

        float reduction = Mathf.Clamp(
            _phaseOneConfig.ShieldDamageReductionPercent, 0f, 100f);

        // 100% 감소는 피해를 완전히 차단한다.
        if (reduction >= 100f)
        {
            return 0;
        }

        // 정수 체력에 맞춰 반올림한다.
        // 100% 미만에서는 작은 공격도 최소 1의 피해를 준다.
        return Mathf.Max(
            1,
            Mathf.RoundToInt(damage * (1f - reduction / 100f)));
    }

    // 사망 순간 균열과 실드를 함께 정리한다.
    protected override void OnBossDefeated()
    {
        _crackController.ClearAll();
        _shieldVisual.SetActive(false);

        base.OnBossDefeated();
    }

    // 체력 조건을 검증하고 이번 전투의 발동 기록을 초기화한다.
    private void InitializeHealthPatterns()
    {
        _isHealthPatternReady = false;
        float previousPercent = 100f;

        for (int i = 0; i < _healthSpecialPatternEntries.Count; i++)
        {
            HealthBossPatternEntry entry = _healthSpecialPatternEntries[i];

            if (entry == null || entry.Pattern == null)
            {
                throw new InvalidOperationException(
                    $"{name}: 체력 특수패턴 {i + 1}번의 Pattern을 연결하세요.");
            }

            // 유효하지 않은 숫자와 0%, 100% 기준을 제외한다.
            if (!(entry.HealthPercent > 0f && entry.HealthPercent < 100f))
            {
                throw new InvalidOperationException(
                    $"{name}: 체력 기준은 0보다 크고 100보다 작아야 합니다.");
            }

            // 한 번에 여러 기준을 넘으면 목록 순서대로 요청한다.
            if (entry.HealthPercent > previousPercent)
            {
                throw new InvalidOperationException(
                    $"{name}: 체력 특수패턴은 높은 퍼센트부터 등록하세요.");
            }

            previousPercent = entry.HealthPercent;
        }

        _hasRequestedHealthPatterns =
            new bool[_healthSpecialPatternEntries.Count];

        _isHealthPatternReady = true;
        CheckHealthPatterns();
    }

    // 부모가 피해 감소와 체력 차감을 처리한 뒤 호출한다.
    protected override void OnBossDamaged(int damage)
    {
        base.OnBossDamaged(damage);

        // 실제로 감소한 로봇 체력을 기준으로 발동 여부를 검사한다.
        CheckHealthPatterns();
    }

    // 조건을 만족한 항목을 전투 중 한 번씩 요청한다.
    private void CheckHealthPatterns()
    {
        if (!_isHealthPatternReady ||
            !CanRunArmPattern ||
            CurrentHp <= 0 ||
            MaxHp <= 0)
        {
            return;
        }

        float healthPercent = (float)CurrentHp / MaxHp * 100f;

        for (int i = 0; i < _healthSpecialPatternEntries.Count; i++)
        {
            if (_hasRequestedHealthPatterns[i])
            {
                continue;
            }

            HealthBossPatternEntry entry = _healthSpecialPatternEntries[i];

            if (healthPercent > entry.HealthPercent)
            {
                continue;
            }

            // 실행 과정에서 다시 피해가 들어와도 중복 요청하지 않는다.
            _hasRequestedHealthPatterns[i] = true;

            // 진행 중인 패턴을 끊지 않고 기존 특수패턴 대기열에 등록한다.
            RequestSpecialPattern(entry.Pattern);
        }
    }

    // 균열 3개가 완성되면 즉시 경고를 표시하고 공명을 예약한다.
    private void HandleMaxCracksReached()
    {
        if (!CanRunArmPattern ||
            !_crackController.TryReserveResonance())
        {
            return;
        }

        // 이전 구독을 정리하고, 표시 요청 전에 완료 신호를 받을 준비를 한다.
        ClearResonanceWarningWait();
        _isResonanceWarningPending = true;
        SystemDialogueCompleted += ClearResonanceWarningWait;

        // 기존 시스템 다이어로그 UI에 경고를 표시한다.
        RequestSystemDialogue(_resonanceWarningMessage);

        // 현재 그랩이 끝난 뒤 공명을 우선 실행한다.
        RequestSpecialPattern(_resonancePattern, prioritize: true);
    }

    // 경고가 끝났거나 공명이 취소되면 대기 상태와 이벤트 구독을 정리한다.
    private void ClearResonanceWarningWait()
    {
        SystemDialogueCompleted -= ClearResonanceWarningWait;
        _isResonanceWarningPending = false;
    }

    // 로봇 체력이 소진되면 최종 사망 대신 본체 등장 연출을 시작한다.
    protected override bool TryBeginNextPhase()
    {
        // 전환 중에 다시 호출되어도 연출이나 사망 처리를 반복하지 않는다.
        if (_isPhaseTransitioning)
        {
            return true;
        }

        // 전환에 필요한 연출, 본체 프리팹, 생성 위치를 검사한다.
        if (_phaseTransitionAnimator == null ||
            !_phaseTransitionAnimator.isActiveAndEnabled ||
            !_phaseTransitionAnimator.HasState(0, PhaseTransitionState) ||
            _robotArmRoot == null ||
            _phaseTwoPrefab == null ||
            _phaseTwoSpawnPoint == null)
        {
            throw new InvalidOperationException(
                $"{name}: 전환 Animator, Alien_PhaseTransition, " +
                "Robot Arm Root, Phase Two Prefab, " +
                "Phase Two Spawn Point를 확인하세요.");
        }

        // 생성 위치가 로봇의 자식인지 확인한다.
        if (!_phaseTwoSpawnPoint.IsChildOf(transform))
        {
            throw new InvalidOperationException(
                $"{name}: Phase Two Spawn Point는 로봇의 자식이어야 합니다.");
        }

        // 정리 중 추가 피해가 들어와도 전환을 방해하지 못하게 한다.
        _isPhaseTransitioning = true;

        // 실행 중인 공격과 예약된 공격을 취소한다.
        StopPatternsForPhaseChange();

        // 이전 경고 완료를 기다리던 구독을 정리한다.
        ClearResonanceWarningWait();

        // 로봇의 균열과 실드 외형을 제거한다.
        _crackController.ClearAll();
        _shieldVisual.SetActive(false);

        // 패턴 종료 과정에서 이동이 재개될 수 있으므로 마지막에 잠근다.
        SetControllable(false);

        // 잡힌 플레이어와 팔 상태를 정리하고 기존 팔 외형을 숨긴다.
        ResetArm();
        _robotArmRoot.SetActive(false);

        // 전환 중 로봇 몸체가 플레이어에게 접촉 피해를 주지 않게 한다.
        // 시각적 애니메이션은 물리 시뮬레이션과 별개로 계속 재생된다.
        if (TryGetComponent(out Rigidbody2D body))
        {
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
        }

        // 정리가 끝난 로봇에서 파괴 및 본체 등장 연출을 재생한다.
        _phaseTransitionRoutine = StartCoroutine(PlayPhaseTransition());

        // 부모의 최종 사망·보상 처리를 실행하지 않는다.
        return true;
    }

    // 전환 연출이 끝나면 관리자에게 본체 프리팹 교체를 요청한다.
    private IEnumerator PlayPhaseTransition()
    {
        // 로봇의 전환 연출을 처음부터 재생한다.
        _phaseTransitionAnimator.Play(PhaseTransitionState, 0, 0f);

        // Animator가 새 상태를 평가할 시간을 준다.
        yield return null;

        while (true)
        {
            // 전투가 중단됐다면 본체를 생성하지 않는다.
            if (!isActiveAndEnabled ||
                IsDead ||
                TargetPlayer == null ||
                !TargetPlayer.isActiveAndEnabled ||
                TargetPlayer.IsDead)
            {
                _phaseTransitionRoutine = null;
                yield break;
            }

            // Animator 비활성화를 정상적인 연출 완료로 취급하지 않는다.
            if (_phaseTransitionAnimator == null ||
                !_phaseTransitionAnimator.isActiveAndEnabled)
            {
                Debug.LogError(
                    $"{name}: 전환 중 Animator가 비활성화됐습니다.",
                    this);

                _phaseTransitionRoutine = null;
                yield break;
            }

            AnimatorStateInfo state =
                _phaseTransitionAnimator.GetCurrentAnimatorStateInfo(0);

            // 다른 상태가 전환 연출을 덮어썼다면 교체를 중단한다.
            if (state.fullPathHash != PhaseTransitionState)
            {
                Debug.LogError(
                    $"{name}: 페이즈 전환 애니메이션이 중단됐습니다.",
                    this);

                _phaseTransitionRoutine = null;
                yield break;
            }

            // 반복하지 않는 전환 클립이 끝났을 때만 교체한다.
            if (state.normalizedTime >= 1f)
            {
                break;
            }

            yield return null;
        }

        // 요청 중 관리자가 로봇을 비활성화하므로 좌표를 먼저 보관한다.
        Vector3 spawnPosition = _phaseTwoSpawnPoint.position;
        Quaternion spawnRotation = _phaseTwoSpawnPoint.rotation;

        // OnDisable에서 완료 직전의 코루틴을 다시 중단하지 않도록 비운다.
        _phaseTransitionRoutine = null;

        // 로봇의 체력을 회복하거나 무적을 해제하지 않는다.
        // 관리자가 새 본체를 준비한 뒤 로봇을 제거한다.
        RequestPhaseReplacement(
            _phaseTwoPrefab,
            spawnPosition,
            spawnRotation);
    }

    // 전투 중단 후 전환 코루틴이 뒤늦게 완료되는 것을 막는다.
    private void StopPhaseTransitionRoutine()
    {
        if (_phaseTransitionRoutine == null)
        {
            return;
        }

        StopCoroutine(_phaseTransitionRoutine);
        _phaseTransitionRoutine = null;

        // 전투 중단을 정상적인 등장 완료로 취급하지 않는다.
        // 여기서는 무적을 해제하지 않는다.
    }
}
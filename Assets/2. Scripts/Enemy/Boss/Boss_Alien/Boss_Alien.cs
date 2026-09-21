using System;
using UnityEngine;
using System.Collections.Generic;

// 외계인 보스의 참조와 팔의 기본 상태를 관리한다.
public sealed class Boss_Alien : BossEnemy
{
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

    // 살아 있는 플레이어와 전투 중일 때만 팔 패턴을 허용한다.
    public bool CanRunArmPattern =>
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

    // 현재 구현된 로봇 1페이즈 체력은 파란 바에 표시한다.
    // 균열로 발생하는 피해 감소 실드의 활성 여부와는 별개다.
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
}
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 요청받은 보스 패턴을 대기열에 저장하고 한 번에 하나씩 실행한다.
/// </summary>
public sealed class BossPatternRunner
{
    readonly MonoBehaviour _coroutineHost; // 패턴 코루틴을 실행해 줄 MonoBehaviour
    readonly BossPatternContext _context; // 모든 패턴에 전달할 공통 Context

    readonly Queue<BossPattern> _normalWaitingPatterns =
    new Queue<BossPattern>(); // 실행을 기다리는 일반 패턴 대기열

    readonly Queue<BossPattern> _specialWaitingPatterns =
        new Queue<BossPattern>(); // 체력 조건으로 요청된 특수 패턴 대기열

    BossPattern _currentPattern; // 현재 실행 중인 패턴
    Coroutine _runningCoroutine; // 현재 실행 중인 패턴 코루틴

    // 패턴 취소 시 finally 정리 코드를 실행할 실제 반복자다.
    IEnumerator _patternExecution;
    bool _isRunning; // 현재 패턴이 실행 중인지 확인하는 값

    // 균열 공명처럼 다른 대기 특수패턴보다 먼저 실행할 패턴이다.
    // 이미 실행 중인 패턴을 중단하지는 않는다.
    private readonly Queue<BossPattern> _prioritySpecialWaitingPatterns =
        new Queue<BossPattern>();


    public bool IsRunning => _isRunning;

    public BossPattern CurrentPattern =>
        _currentPattern;

    /// <summary>
    /// 코루틴 실행 주체와 패턴 Context를 전달받는다.
    /// </summary>
    public BossPatternRunner(
        MonoBehaviour coroutineHost,
        BossPatternContext context)
    {
        _coroutineHost = coroutineHost
            ?? throw new ArgumentNullException(
                nameof(coroutineHost));

        _context = context
            ?? throw new ArgumentNullException(
                nameof(context));
    }

    /// <summary>
    /// 시간 조건을 만족한 일반 패턴의 실행을 요청한다.
    /// </summary>
    public void RequestNormal(
        BossPattern pattern)
    {
        Request(
            pattern,
            _normalWaitingPatterns);
    }

    // 우선순위가 없으면 기존 체력 특수패턴 대기열을 사용한다.
    public void RequestSpecial(BossPattern pattern, bool prioritize = false)
    {
        Request(
            pattern,
            prioritize
                ? _prioritySpecialWaitingPatterns
                : _specialWaitingPatterns);
    }

    /// <summary>
    /// 전달받은 대기열에 패턴을 등록한다.
    /// </summary>
    private void Request(
        BossPattern pattern,
        Queue<BossPattern> targetQueue)
    {
        if (pattern == null)
        {
            throw new ArgumentNullException(
                nameof(pattern));
        }

        // 사망한 보스는 새로운 패턴을 요청하지 않는다.
        if (_context.Boss.IsDead)
        {
            return;
        }

        targetQueue.Enqueue(pattern);

        // 실행 중인 패턴이 없다면 바로 다음 패턴을 시작한다.
        TryStartNextPattern();
    }

    /// <summary>
    /// 실행 중인 패턴과 대기 중인 모든 패턴을 취소한다.
    /// </summary>
    public void StopAll()
    {
        // 일반 패턴과 특수 패턴 대기열을 모두 비운다.
        _normalWaitingPatterns.Clear();
        _specialWaitingPatterns.Clear();
        // 중단과 사망 시 우선 특수패턴 예약도 제거한다.
        _prioritySpecialWaitingPatterns.Clear();

        if (_runningCoroutine != null)
        {
            _coroutineHost.StopCoroutine(
                _runningCoroutine);
        }

        // 중단된 패턴의 finally를 실행하여 UI와 게임 정지를 정리한다.
        DisposePatternExecution();

        bool shouldResumeMovement =
            _currentPattern != null &&
            _currentPattern.PauseRandomMovement;

        _currentPattern = null;
        _runningCoroutine = null;
        _isRunning = false;

        // 사망 등으로 취소된 경우에도 특수패턴 상태를 정리한다.
        _context.Boss.SetSpecialPatternRunning(false);

        // 사망 때문에 중단한 경우에는 랜덤 이동을 다시 시작하지 않는다.
        if (shouldResumeMovement &&
            !_context.Boss.IsDead)
        {
            _context.Boss.SetControllable(true);
        }
    }

    /// <summary>
    /// 실행 중인 패턴이 없다면 특수 패턴을 우선하여 실행한다.
    /// </summary>
    private void TryStartNextPattern()
    {
        // 현재 패턴은 중간에 중단하지 않는다.
        if (_isRunning)
        {
            return;
        }

        if (_context.Boss.IsDead)
        {
            _normalWaitingPatterns.Clear();
            _specialWaitingPatterns.Clear();
            // 중단과 사망 시 우선 특수패턴 예약도 제거한다.
            _prioritySpecialWaitingPatterns.Clear();
            return;
        }

        // 이번에 선택한 패턴이 특수패턴 대기열에서 나왔는지 기록한다.
        bool isSpecialPattern;

        // 실행 중인 패턴이 끝났을 때 우선 특수패턴부터 선택한다.
        if (_prioritySpecialWaitingPatterns.Count > 0)
        {
            _currentPattern = _prioritySpecialWaitingPatterns.Dequeue();
            isSpecialPattern = true;
        }
        else if (_specialWaitingPatterns.Count > 0)
        {
            _currentPattern = _specialWaitingPatterns.Dequeue();
            isSpecialPattern = true;
        }
        else if (_normalWaitingPatterns.Count > 0)
        {
            _currentPattern = _normalWaitingPatterns.Dequeue();
            isSpecialPattern = false;
        }
        else
        {
            return;
        }

        // 이벤트 처리 중 다른 요청이 들어와도 중복 실행하지 않는다.
        _isRunning = true;

        // 실제 패턴 실행 전에 알린다.
        // 특수패턴의 시스템 경고가 시작되기 전에 일반 적 생성이 중단된다.
        _context.Boss.SetSpecialPatternRunning(isSpecialPattern);

        _runningCoroutine = _coroutineHost.StartCoroutine(
            ExecuteCurrentPattern());
    }

    /// <summary>
    /// 현재 패턴을 실행하고 완료되면 다음 패턴을 시작한다.
    /// </summary>
    private IEnumerator ExecuteCurrentPattern()
    {
        // 현재 패턴이 랜덤 이동 정지를 요구하면 이동을 정지한다.
        if (_currentPattern.PauseRandomMovement)
        {
            _context.Boss.SetControllable(false);
        }

        // 실행 중인 반복자를 보관하여 취소할 때도 정리할 수 있게 한다.
        _patternExecution = _currentPattern.Execute(_context);

        // 실제 패턴의 실행이 끝날 때까지 기다린다.
        yield return _patternExecution;

        // 정상 완료한 반복자를 정리한다.
        DisposePatternExecution();

        bool shouldResumeMovement =
            _currentPattern.PauseRandomMovement;

        _currentPattern = null;
        _runningCoroutine = null;
        _isRunning = false;

        // 현재 특수패턴이 끝났으므로 외부 시스템에 종료를 알린다.
        // 다음 대기 패턴도 특수패턴이면 시작 시 다시 true로 변경된다.
        _context.Boss.SetSpecialPatternRunning(false);

        // 보스가 살아 있다면 패턴에서 정지했던 랜덤 이동을 재개한다.
        if (shouldResumeMovement &&
            !_context.Boss.IsDead)
        {
            _context.Boss.SetControllable(true);
        }

        // 대기 중인 다음 패턴을 실행한다.
        TryStartNextPattern();
    }

    // 패턴 종료 또는 취소 시 실행 상태와 복귀 충돌을 정리한다.
    private void DisposePatternExecution()
    {
        // 중복 정리를 방지하도록 참조를 먼저 비운다.
        IEnumerator execution = _patternExecution;
        _patternExecution = null;

        try
        {
            // 패턴 내부의 finally 등 종료 처리를 실행한다.
            if (execution is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        finally
        {
            // 정상 종료, 사망으로 인한 취소 모두 충돌 설정을 복원한다.
            if (_context.Boss != null)
            {
                _context.Boss.RestoreReturnCollision();
            }
        }
    }
}
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
    bool _isRunning; // 현재 패턴이 실행 중인지 확인하는 값

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

    /// <summary>
    /// 체력 조건을 만족한 특수 패턴의 실행을 요청한다.
    /// 현재 패턴이 끝난 후 일반 대기 패턴보다 먼저 실행된다.
    /// </summary>
    public void RequestSpecial(
        BossPattern pattern)
    {
        Request(
            pattern,
            _specialWaitingPatterns);
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

        if (_runningCoroutine != null)
        {
            _coroutineHost.StopCoroutine(
                _runningCoroutine);
        }

        bool shouldResumeMovement =
            _currentPattern != null &&
            _currentPattern.PauseRandomMovement;

        _currentPattern = null;
        _runningCoroutine = null;
        _isRunning = false;

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
            return;
        }

        // 체력 조건 특수 패턴이 대기 중이면 가장 먼저 선택한다.
        if (_specialWaitingPatterns.Count > 0)
        {
            _currentPattern =
                _specialWaitingPatterns.Dequeue();
        }
        // 특수 패턴이 없을 때만 일반 패턴을 선택한다.
        else if (_normalWaitingPatterns.Count > 0)
        {
            _currentPattern =
                _normalWaitingPatterns.Dequeue();
        }
        else
        {
            return;
        }

        _isRunning = true;

        _runningCoroutine =
            _coroutineHost.StartCoroutine(
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

        // 실제 패턴의 실행이 끝날 때까지 기다린다.
        yield return _currentPattern.Execute(
            _context);

        bool shouldResumeMovement =
            _currentPattern.PauseRandomMovement;

        _currentPattern = null;
        _runningCoroutine = null;
        _isRunning = false;

        // 보스가 살아 있다면 패턴에서 정지했던 랜덤 이동을 재개한다.
        if (shouldResumeMovement &&
            !_context.Boss.IsDead)
        {
            _context.Boss.SetControllable(true);
        }

        // 대기 중인 다음 패턴을 실행한다.
        TryStartNextPattern();
    }
}
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

    readonly Queue<BossPattern> _waitingPatterns =
        new Queue<BossPattern>(); // 실행을 기다리고 있는 패턴 대기열

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
    /// 패턴 실행을 요청한다.
    /// 다른 패턴이 실행 중이라면 대기열에 저장한다.
    /// </summary>
    public void Request(BossPattern pattern)
    {
        if (pattern == null)
        {
            throw new ArgumentNullException(
                nameof(pattern));
        }

        // 죽은 보스에게 새로운 패턴을 요청하지 않는다.
        if (_context.Boss.IsDead)
        {
            return;
        }

        _waitingPatterns.Enqueue(pattern);

        // 현재 실행 중인 패턴이 없다면 바로 다음 패턴을 실행한다.
        TryStartNextPattern();
    }

    /// <summary>
    /// 실행 중인 패턴과 대기 중인 모든 패턴을 취소한다.
    /// </summary>
    public void StopAll()
    {
        _waitingPatterns.Clear();

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
    /// 실행 중인 패턴이 없으면 대기열의 다음 패턴을 시작한다.
    /// </summary>
    private void TryStartNextPattern()
    {
        if (_isRunning)
        {
            return;
        }

        if (_waitingPatterns.Count == 0)
        {
            return;
        }

        if (_context.Boss.IsDead)
        {
            _waitingPatterns.Clear();
            return;
        }

        _currentPattern =
            _waitingPatterns.Dequeue();

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
using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ExpUI : MonoBehaviour
{
    /// <summary>
    /// 경험치 UI에 순서대로 반영할 성장 결과.
    /// </summary>
    private struct ProgressSnapshot
    {
        public int Level;
        public int CurrentExp;
        public int RequiredExp;

        public ProgressSnapshot(
            int level,
            int currentExp,
            int requiredExp)
        {
            Level = level;
            CurrentExp = currentExp;
            RequiredExp = requiredExp;
        }
    }

    [Header("컴포넌트")]
    [Tooltip("현재 캐릭터의 레벨과 경험치를 관리하는 시스템")]
    [SerializeField] CharacterProgression _characterProgression;

    [Header("레벨")]
    [Tooltip("현재 캐릭터 레벨을 표시할 텍스트")]
    [SerializeField] TMP_Text _currentLevelText;

    [Header("경험치 이미지")]
    [Tooltip("경험치 비율을 fillAmount로 표시할 이미지")]
    [SerializeField] Image _currentLevelGaugeImage;

    [Header("경험치 연출 설정")]
    [Tooltip("fillAmount가 1초 동안 증가하는 양")]
    [Min(0.01f)]
    [SerializeField] float _fillSpeed = 1f;

    [Tooltip("레벨업 직전 게이지가 가득 찬 상태를 유지할 시간")]
    [Min(0f)]
    [SerializeField] float _fullGaugeHoldDuration = 0.15f;

    [Header("경험치 UI 런타임 데이터")]
    [SerializeField] int _displayedLevel;
    [SerializeField] bool _isInitialized;

    // 빠르게 여러 적을 처치했을 때 경험치 연출을 순서대로 보관한다.
    readonly Queue<ProgressSnapshot> _progressQueue = new();

    // 현재 재생 중인 경험치 게이지 연출이다.
    Sequence _fillSequence;

    private void Awake()
    {
        if (_characterProgression == null)
        {
            throw new InvalidOperationException(
                $"{name}의 ExpUI에 CharacterProgression이 연결되지 않았습니다.");
        }

        if (_currentLevelText == null)
        {
            throw new InvalidOperationException(
                $"{name}의 ExpUI에 Current Level Text가 연결되지 않았습니다.");
        }

        if (_currentLevelGaugeImage == null)
        {
            throw new InvalidOperationException(
                $"{name}의 ExpUI에 Current Level Gauge Image가 연결되지 않았습니다.");
        }
    }

    private void OnEnable()
    {
        // 경험치가 변경될 때마다 새로운 성장 결과를 전달받는다.
        _characterProgression.OnProgressChanged += UpdateExp;

        // CharacterProgression이 이미 초기화됐다면 현재 저장값을 표시한다.
        UpdateExp(
            _characterProgression.CurrentLevel,
            _characterProgression.CurrentExp,
            _characterProgression.RequiredExp);
    }

    private void OnDisable()
    {
        // 비활성화된 UI가 계속 이벤트를 받지 않도록 연결을 해제한다.
        _characterProgression.OnProgressChanged -= UpdateExp;

        // 현재 재생 중인 DOTween 연출을 제거한다.
        _fillSequence?.Kill();
        _fillSequence = null;

        // 아직 재생되지 않은 경험치 연출도 제거한다.
        _progressQueue.Clear();
    }

    /// <summary>
    /// 변경된 경험치 결과를 연출 대기열에 추가한다.
    /// </summary>
    private void UpdateExp(
        int currentLevel,
        int currentExp,
        int requiredExp)
    {
        // CharacterProgression이 아직 초기화되기 전이면 기다린다.
        if (requiredExp <= 0)
        {
            return;
        }

        float expRatio = Mathf.Clamp01(
            (float)currentExp / requiredExp);

        // 최초 데이터는 연출하지 않고 저장된 상태를 즉시 표시한다.
        if (!_isInitialized)
        {
            _displayedLevel = currentLevel;
            _currentLevelText.text = $"{currentLevel}";
            _currentLevelGaugeImage.fillAmount = expRatio;
            _isInitialized = true;
            return;
        }

        // 여러 적이 동시에 죽더라도 경험치 연출이 누락되지 않도록 저장한다.
        _progressQueue.Enqueue(
            new ProgressSnapshot(
                currentLevel,
                currentExp,
                requiredExp));

        // 현재 연출이 없다면 대기 중인 경험치 연출을 시작한다.
        if (_fillSequence == null)
        {
            PlayNextProgress();
        }
    }

    /// <summary>
    /// 대기열의 다음 경험치 변경을 DOTween으로 재생한다.
    /// </summary>
    private void PlayNextProgress()
    {
        // 재생할 경험치 변경이 더 이상 없다.
        if (_progressQueue.Count == 0)
        {
            _fillSequence = null;
            return;
        }

        ProgressSnapshot progress =
            _progressQueue.Dequeue();

        float targetFillAmount = Mathf.Clamp01(
            (float)progress.CurrentExp /
            progress.RequiredExp);

        _fillSequence = DOTween.Sequence();

        // 레벨이 증가했다면 기존 게이지를 먼저 끝까지 채운다.
        if (progress.Level > _displayedLevel)
        {
            for (int level = _displayedLevel + 1;
                 level <= progress.Level;
                 level++)
            {
                int nextLevel = level;

                // 첫 번째 레벨업은 현재 게이지 위치부터 1까지 채운다.
                // 추가 레벨업은 0부터 1까지 다시 채운다.
                float startFillAmount =
                    level == _displayedLevel + 1
                        ? _currentLevelGaugeImage.fillAmount
                        : 0f;

                float fillDuration =
                    (1f - startFillAmount) / _fillSpeed;

                _fillSequence.Append(
                    _currentLevelGaugeImage
                        .DOFillAmount(1f, fillDuration)
                        .SetEase(Ease.OutQuad));

                // 가득 찬 상태가 화면에 보이도록 잠깐 유지한다.
                _fillSequence.AppendInterval(
                    _fullGaugeHoldDuration);

                // 레벨을 올리고 경험치 게이지를 0으로 초기화한다.
                _fillSequence.AppendCallback(() =>
                {
                    _displayedLevel = nextLevel;
                    _currentLevelText.text = $"{nextLevel}";
                    _currentLevelGaugeImage.fillAmount = 0f;
                });
            }

            // 레벨업 후 남은 경험치가 있다면 그 비율까지 다시 채운다.
            float remainingFillDuration =
                targetFillAmount / _fillSpeed;

            _fillSequence.Append(
                _currentLevelGaugeImage
                    .DOFillAmount(
                        targetFillAmount,
                        remainingFillDuration)
                    .SetEase(Ease.OutQuad));
        }
        else
        {
            // 레벨업이 없다면 현재 위치에서 목표 경험치까지 채운다.
            float fillDistance = Mathf.Abs(
                targetFillAmount -
                _currentLevelGaugeImage.fillAmount);

            float fillDuration =
                fillDistance / _fillSpeed;

            _fillSequence.Append(
                _currentLevelGaugeImage
                    .DOFillAmount(
                        targetFillAmount,
                        fillDuration)
                    .SetEase(Ease.OutQuad));
        }

        // 일시정지 중에도 경험치 UI 연출은 계속 재생한다.
        _fillSequence.SetUpdate(true);

        // 현재 연출이 끝나면 대기열의 다음 경험치를 재생한다.
        _fillSequence.OnComplete(() =>
        {
            _fillSequence = null;
            PlayNextProgress();
        });
    }
}
using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 보스 패턴이 확인할 UI 진행 결과다.
public enum ConfusionResult
{
    None,
    Running,
    Success,
    Failed,
    Cancelled
}

/// <summary>
/// 정신 혼란의 키 입력, 제한 시간, Life와 입력 연출을 처리한다.
/// 게임 시간 정지와 플레이어 피해는 보스 패턴이 담당한다.
/// </summary>
public sealed class MentalConfusionUI : MonoBehaviour
{
    // 알파벳들이 배치되는 마스크 내부 공간이다.
    [SerializeField] RectTransform _keyContainer;

    // 정답 알파벳이 마스크 밖에서 사라지는 공간이다.
    [SerializeField] RectTransform _keyEffectRoot;

    // 현재 입력 위치를 나타내는 원이다.
    [SerializeField] RectTransform _currentKeyRing;

    // 원 안쪽에 파란색 또는 빨간색으로 점멸할 Image다.
    [SerializeField] Image _feedbackFlash;

    // 남은 시간 비율을 표시하는 Filled Image다.
    [SerializeField] Image _timerFill;

    // Life 아이콘들을 배치할 공간이다.
    [SerializeField] RectTransform _lifeContainer;

    // 알파벳 한 개를 표시하는 TMP 프리팹이다.
    [SerializeField] TMP_Text _keyPrefab;

    // Life 한 개를 표시하는 Image 프리팹이다.
    [SerializeField] Image _lifePrefab;

    // 남아 있는 Life의 이미지다.
    [SerializeField] Sprite _lifeFullSprite;

    // 소진된 Life의 이미지다.
    [SerializeField] Sprite _lifeEmptySprite;

    // 대기열 알파벳 사이의 거리다.
    [Min(1f)]
    [SerializeField] float _keySpacing = 170f;

    // Life 아이콘 사이의 거리다.
    [Min(1f)]
    [SerializeField] float _lifeSpacing = 44f;

    // 다음 알파벳들이 왼쪽으로 이동하는 시간이다.
    [Min(0.01f)]
    [SerializeField] float _slideDuration = 0.15f;

    // 정답 알파벳이 위로 사라지는 시간이다.
    [Min(0.01f)]
    [SerializeField] float _vanishDuration = 0.18f;

    // 정답 알파벳이 위로 이동할 거리다.
    [Min(0f)]
    [SerializeField] float _vanishDistance = 45f;

    // 원의 점멸과 확대·흔들림에 사용할 시간이다.
    [Min(0.01f)]
    [SerializeField] float _feedbackDuration = 0.15f;

    // 정답 연출에 사용할 파란색이다.
    [SerializeField]
    Color _successColor =
        new Color(0.15f, 0.65f, 1f, 1f);

    // 오답 연출에 사용할 빨간색이다.
    [SerializeField]
    Color _failureColor =
        new Color(1f, 0.15f, 0.15f, 1f);

    // 입력해야 할 전체 문자열과 현재 입력 순서다.
    string _sequence = string.Empty;
    int _currentIndex;

    // 실행 중 생성한 알파벳과 Life 아이콘이다.
    readonly List<TMP_Text> _keys = new List<TMP_Text>();
    readonly List<Image> _lives = new List<Image>();

    // 제한 시간과 현재 남은 시간이다.
    float _timeLimit;
    float _remainingTime;

    // 남은 Life다.
    int _remainingLives;

    // 대기열 첫 알파벳이 도착할 위치다.
    Vector3 _firstKeyPosition;

    // 연출을 반복해도 원의 위치와 크기가 누적되지 않게 보관한다.
    Vector2 _ringPosition;
    Vector3 _ringScale;

    // UI 생성에 사용된 키 입력을 같은 프레임에 재사용하지 않는다.
    int _startFrame;

    // 마지막 정답·오답 연출이 끝난 뒤 결과를 확정한다.
    ConfusionResult _pendingResult;
    float _resultDelay;

    // 프리팹 한 인스턴스는 한 번의 패턴에 사용한다.
    bool _hasBegun;

    // 보스 패턴에서 조회할 현재 결과다.
    public ConfusionResult Result { get; private set; }

    // 마지막 입력 연출까지 끝난 상태인지 반환한다.
    public bool IsFinished =>
        Result == ConfusionResult.Success ||
        Result == ConfusionResult.Failed ||
        Result == ConfusionResult.Cancelled;

    private void Awake()
    {
        // 필수 UI 연결이 없으면 실행 전에 원인을 알린다.
        if (_keyContainer == null ||
            _keyEffectRoot == null ||
            _currentKeyRing == null ||
            _feedbackFlash == null ||
            _feedbackFlash.sprite == null ||
            _timerFill == null ||
            _lifeContainer == null ||
            _keyPrefab == null ||
            _lifePrefab == null ||
            _lifeFullSprite == null ||
            _lifeEmptySprite == null)
        {
            throw new InvalidOperationException(
                $"{name}: 정신 혼란 UI의 Inspector 참조를 모두 연결하세요.");
        }

        // 연출이 끝나면 복귀할 원의 기본 상태다.
        _ringPosition = _currentKeyRing.anchoredPosition;
        _ringScale = _currentKeyRing.localScale;

        _feedbackFlash.color = Color.clear;
        Result = ConfusionResult.None;
    }

    /// <summary>
    /// 보스 패턴이 UI 생성 직후 호출한다.
    /// 문자열은 대소문자를 구분하지 않고 A~Z만 허용한다.
    /// </summary>
    public void Begin(string sequence, float timeLimit, int lifeCount)
    {
        if (_hasBegun)
        {
            throw new InvalidOperationException(
                $"{name}: 이미 시작한 UI입니다. 새 프리팹을 생성하세요.");
        }

        if (string.IsNullOrWhiteSpace(sequence) ||
            timeLimit <= 0f || lifeCount <= 0)
        {
            throw new ArgumentException(
                "입력 문자열, 제한 시간, Life를 확인하세요.");
        }

        string normalized = sequence.ToUpperInvariant();

        // 표시할 문자와 실제 키 판정 범위를 일치시킨다.
        foreach (char letter in normalized)
        {
            if (letter < 'A' || letter > 'Z')
            {
                throw new ArgumentException(
                    "정신 혼란 입력 문자열에는 A~Z만 사용할 수 있습니다.");
            }
        }

        _hasBegun = true;
        _sequence = normalized;
        _timeLimit = timeLimit;
        _remainingTime = timeLimit;
        _remainingLives = lifeCount;
        _currentIndex = 0;
        _pendingResult = ConfusionResult.None;

        // Canvas 배치를 갱신한 뒤 원의 위치를 대기열 좌표로 변환한다.
        Canvas.ForceUpdateCanvases();

        _firstKeyPosition = _keyContainer.InverseTransformPoint(
            _currentKeyRing.position);

        _firstKeyPosition.z = 0f;

        CreateKeys();
        CreateLives();

        _timerFill.fillAmount = 1f;
        _startFrame = Time.frameCount;
        Result = ConfusionResult.Running;
    }

    /// <summary>
    /// 전체 문자열에 맞춰 알파벳을 생성한다.
    /// 화면 밖 알파벳은 기존 RectMask2D가 가린다.
    /// </summary>
    private void CreateKeys()
    {
        for (int i = 0; i < _sequence.Length; i++)
        {
            TMP_Text key = Instantiate(_keyPrefab, _keyContainer);

            key.text = _sequence[i].ToString();
            key.color = Color.white;
            key.raycastTarget = false;

            RectTransform rect = key.rectTransform;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            rect.localPosition =
                _firstKeyPosition + Vector3.right * (_keySpacing * i);

            _keys.Add(key);
        }
    }

    /// <summary>
    /// Life 아이콘들을 컨테이너 중앙에 나란히 배치한다.
    /// </summary>
    private void CreateLives()
    {
        // 설정한 개수가 많으면 간격을 줄여 컨테이너 안에 배치한다.
        float availableWidth = Mathf.Max(
            0f,
            _lifeContainer.rect.width -
            _lifePrefab.rectTransform.sizeDelta.x);

        float spacing = _remainingLives > 1
            ? Mathf.Min(
                _lifeSpacing,
                availableWidth / (_remainingLives - 1))
            : 0f;

        float startX = -(_remainingLives - 1) * spacing * 0.5f;

        for (int i = 0; i < _remainingLives; i++)
        {
            Image life = Instantiate(_lifePrefab, _lifeContainer);

            life.sprite = _lifeFullSprite;
            life.color = Color.white;
            life.raycastTarget = false;

            RectTransform rect = life.rectTransform;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.anchoredPosition =
                new Vector2(startX + spacing * i, 0f);

            _lives.Add(life);
        }
    }

    private void Update()
    {
        if (Result != ConfusionResult.Running ||
            Time.frameCount == _startFrame)
        {
            return;
        }

        // 승패가 결정됐다면 추가 입력 없이 마지막 연출만 기다린다.
        if (_pendingResult != ConfusionResult.None)
        {
            _resultDelay -= Time.unscaledDeltaTime;

            if (_resultDelay <= 0f)
            {
                Result = _pendingResult;
            }

            return;
        }

        // 게임이 정지되어도 패턴의 제한 시간은 흐른다.
        _remainingTime = Mathf.Max(
            0f, _remainingTime - Time.unscaledDeltaTime);

        _timerFill.fillAmount = _remainingTime / _timeLimit;

        // 시간이 끝난 프레임에는 추가 정답을 인정하지 않는다.
        if (_remainingTime <= 0f)
        {
            PlayFeedback(false);
            Finish(false);
            return;
        }

        // 문자 입력 대신 물리 키를 확인하므로 한글 입력 상태에도 대응한다.
        int pressedCount = 0;
        char pressedLetter = '\0';

        for (int i = 0; i < 26; i++)
        {
            KeyCode key = (KeyCode)((int)KeyCode.A + i);

            if (Input.GetKeyDown(key))
            {
                pressedCount++;
                pressedLetter = (char)('A' + i);
            }
        }

        if (pressedCount == 0)
        {
            return;
        }

        // 여러 알파벳을 동시에 눌러 정답을 무작위로 맞히는 것을 막는다.
        // 같은 프레임의 동시 입력은 오답 한 번으로 처리한다.
        bool correct =
            pressedCount == 1 &&
            pressedLetter == _sequence[_currentIndex];

        if (correct)
        {
            AcceptKey();
        }
        else
        {
            RejectKey();
        }
    }

    /// <summary>
    /// 정답 판정을 즉시 다음 키로 넘기고 시각 연출을 실행한다.
    /// 연출 중에도 다음 프레임의 키 입력을 받을 수 있다.
    /// </summary>
    private void AcceptKey()
    {
        TMP_Text accepted = _keys[_currentIndex];
        _currentIndex++;

        PlayFeedback(true);
        VanishKey(accepted);
        SlideRemainingKeys();

        if (_currentIndex >= _sequence.Length)
        {
            Finish(true);
        }
    }

    /// <summary>
    /// 오답은 현재 알파벳을 유지하고 Life만 하나 차감한다.
    /// </summary>
    private void RejectKey()
    {
        _remainingLives--;
        PlayFeedback(false);

        Image lostLife = _lives[_remainingLives];

        // 차감될 Life를 빨갛게 점멸시킨 뒤 소진 이미지로 바꾼다.
        Sequence sequence = DOTween.Sequence();
        sequence.SetUpdate(true);
        sequence.SetId(this);

        sequence.Append(
            lostLife.DOColor(_failureColor, _feedbackDuration * 0.5f));

        sequence.AppendCallback(() =>
        {
            lostLife.sprite = _lifeEmptySprite;
        });

        sequence.Append(
            lostLife.DOColor(Color.white, _feedbackDuration * 0.5f));

        if (_remainingLives <= 0)
        {
            Finish(false);
        }
    }

    /// <summary>
    /// 파란 점멸과 확대 또는 빨간 점멸과 좌우 흔들림을 실행한다.
    /// </summary>
    private void PlayFeedback(bool correct)
    {
        // 연속 입력으로 기존 연출을 끊어도 기본 위치와 크기로 복원한다.
        _feedbackFlash.DOKill();
        _currentKeyRing.DOKill();

        _currentKeyRing.anchoredPosition = _ringPosition;
        _currentKeyRing.localScale = _ringScale;

        Color flashColor = correct ? _successColor : _failureColor;
        flashColor.a = 0.8f;
        _feedbackFlash.color = flashColor;

        _feedbackFlash
            .DOFade(0f, _feedbackDuration)
            .SetUpdate(true)
            .SetId(this);

        if (correct)
        {
            _currentKeyRing
                .DOPunchScale(
                    _ringScale * 0.06f,
                    _feedbackDuration,
                    1,
                    0.5f)
                .SetUpdate(true)
                .SetId(this);
        }
        else
        {
            _currentKeyRing
                .DOShakeAnchorPos(
                    _feedbackDuration,
                    new Vector2(8f, 0f),
                    12,
                    0f,
                    false,
                    true)
                .SetUpdate(true)
                .SetId(this);
        }
    }

    /// <summary>
    /// 정답 알파벳을 마스크 밖으로 옮긴 뒤 위로 올리며 숨긴다.
    /// </summary>
    private void VanishKey(TMP_Text key)
    {
        RectTransform rect = key.rectTransform;

        // 대기열 이동 중인 글자도 즉시 정답 소멸 연출로 전환한다.
        rect.DOKill();

        // 현재 화면 위치를 유지한 채 마스크 밖으로 옮긴다.
        rect.SetParent(_keyEffectRoot, true);
        key.color = _successColor;

        Sequence sequence = DOTween.Sequence();
        sequence.SetUpdate(true);
        sequence.SetId(this);

        sequence.Append(
            rect.DOLocalMoveY(
                    rect.localPosition.y + _vanishDistance,
                    _vanishDuration)
                .SetEase(Ease.OutQuad));

        sequence.Join(
            rect.DOScale(
                rect.localScale * 0.75f,
                _vanishDuration));

        sequence.Join(key.DOFade(0f, _vanishDuration));

        sequence.OnComplete(() =>
        {
            Destroy(key.gameObject);
        });
    }

    /// <summary>
    /// 남은 글자들을 새 순서에 맞는 위치로 이동시킨다.
    /// </summary>
    private void SlideRemainingKeys()
    {
        for (int i = _currentIndex; i < _keys.Count; i++)
        {
            RectTransform rect = _keys[i].rectTransform;

            // 빠른 연속 입력 시 현재 위치에서 새 목적지로 이동한다.
            rect.DOKill();

            Vector3 target =
                _firstKeyPosition +
                Vector3.right * (_keySpacing * (i - _currentIndex));

            rect.DOLocalMove(target, _slideDuration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .SetId(this);
        }
    }

    /// <summary>
    /// 결과를 예약하고 마지막 입력 연출이 끝날 때까지 UI를 유지한다.
    /// </summary>
    private void Finish(bool success)
    {
        _pendingResult = success
            ? ConfusionResult.Success
            : ConfusionResult.Failed;

        _resultDelay = Mathf.Max(
            _vanishDuration,
            Mathf.Max(_slideDuration, _feedbackDuration));
    }

    private void OnDisable()
    {
        // 이 UI가 만든 Tween만 종료한다.
        DOTween.Kill(this);

        // 패턴 도중 UI가 제거되면 성공·실패와 구분해서 알린다.
        if (Result == ConfusionResult.Running)
        {
            Result = ConfusionResult.Cancelled;
        }
    }
}
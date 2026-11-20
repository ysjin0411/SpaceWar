using System;
using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(SpriteRenderer))]
public class SpriteOneShotEffect : MonoBehaviour
{
    // 처음부터 끝까지 재생할 스프라이트를 순서대로 연결한다.
    [SerializeField] private Sprite[] _frames;

    // 1초 동안 재생할 스프라이트 개수다.
    [SerializeField, Min(1f)]
    private float _framesPerSecond = 20f;

    // 프리팹의 원래 크기를 기준으로 한 시작 배율이다.
    // 1이면 원래 크기, 0.5면 절반 크기에서 시작한다.
    [SerializeField, Min(0f)]
    private float _startScaleMultiplier = 1f;

    // 프리팹의 원래 크기를 기준으로 한 최종 배율이다.
    // 2이면 원래 크기의 두 배까지 커진다.
    [SerializeField, Min(0f)]
    private float _endScaleMultiplier = 2f;

    // 시작 크기에서 최종 크기까지 변하는 데 걸리는 시간이다.
    [SerializeField, Min(0.01f)]
    private float _scaleDuration = 0.15f;

    // 크기가 변하는 속도 곡선이다.
    // OutQuad는 처음에 빠르게 커지고 끝에서 부드럽게 느려진다.
    [SerializeField]
    private Ease _scaleEase = Ease.OutQuad;

    // 현재 프레임을 표시할 렌더러다.
    private SpriteRenderer _spriteRenderer;

    // 처음 설정된 로컬 스케일을 배율 계산의 기준으로 보관한다.
    private Vector3 _originalScale;

    // 현재 실행 중인 스케일 트윈만 따로 관리한다.
    private Tween _scaleTween;

    // 프레임 재생 시작 후 흐른 시간이다.
    private float _elapsed;

    // 보스 종료 정리 연출만 게임 정지와 무관하게 재생한다.
    private bool _useUnscaledTime;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();

        // 재활성화할 때 확대된 크기가 다시 곱해지지 않도록
        // 처음 크기를 한 번만 저장한다.
        _originalScale = transform.localScale;

        // 프레임에 접근하기 전에 필수 설정을 확인한다.
        if (_frames == null || _frames.Length == 0 ||
            _framesPerSecond <= 0f)
        {
            throw new InvalidOperationException(
                $"{name}: Frames와 Frames Per Second를 설정하세요.");
        }

        // 재생 중 빈 이미지가 나오지 않도록 모든 프레임을 확인한다.
        for (int i = 0; i < _frames.Length; i++)
        {
            if (_frames[i] == null)
            {
                throw new InvalidOperationException(
                    $"{name}: Frames의 {i}번 스프라이트가 비어 있습니다.");
            }
        }
    }

    private void OnEnable()
    {
        // 생성되거나 다시 활성화되면 첫 프레임부터 재생한다.
        _elapsed = 0f;
        _spriteRenderer.sprite = _frames[0];

        // 프레임 재생과 동시에 크기 변화도 시작한다.
        SetSpriteScale();
    }

    // 생성된 이펙트 인스턴스의 전체 재생 시간을 지정한다.
    // 무기 연사 속도에 맞춰 프레임 교체와 자동 제거 시점이 함께 빨라진다.
    public void SetPlaybackDuration(float duration)
    {
        // 유효하지 않은 재생 시간은 설정 오류로 처리한다.
        if (float.IsNaN(duration) ||
            float.IsInfinity(duration) ||
            duration <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                duration,
                "이펙트 재생 시간은 0보다 큰 유한한 값이어야 합니다.");
        }

        // 지정된 시간 안에 모든 프레임의 재생 구간이 끝나도록 계산한다.
        // 생성된 인스턴스의 값만 바뀌므로 원본 프리팹에는 영향이 없다.
        _framesPerSecond = _frames.Length / duration;

        // 새 재생 시간으로 첫 프레임부터 시작한다.
        _elapsed = 0f;
        _spriteRenderer.sprite = _frames[0];

        // 크기 변화가 있는 이펙트도 전체 재생 시간 안에 끝나도록 갱신한다.
        SetSpriteScale();
    }

    private void SetSpriteScale()
    {
        // 이전 스케일 연출이 남아 있다면 종료한다.
        _scaleTween?.Kill();
        _scaleTween = null;

        // 항상 원래 크기에 시작 배율을 곱해 초기화한다.
        transform.localScale =
            _originalScale * _startScaleMultiplier;

        // 시작과 끝이 같으면 고정 크기로 표시하고 트윈은 만들지 않는다.
        if (Mathf.Approximately(
            _startScaleMultiplier,
            _endScaleMultiplier))
        {
            return;
        }

        // 전체 프레임이 재생되는 시간을 계산한다.
        float playbackDuration =
            _frames.Length / _framesPerSecond;

        // 이펙트가 사라진 뒤까지 확대가 이어지지 않도록
        // 스케일 연출 시간을 전체 재생 시간 이내로 제한한다.
        float duration = Mathf.Min(
            Mathf.Max(0.01f, _scaleDuration),
            playbackDuration);

        // 이미지 원본을 수정하지 않고 표시 오브젝트의 크기를 변경한다.
        _scaleTween = transform
            .DOScale(
                _originalScale * _endScaleMultiplier,
                duration)
            .SetEase(_scaleEase)
            .SetUpdate(UpdateType.Normal, _useUnscaledTime)
            .OnKill(() => _scaleTween = null);
    }

    /// <summary>
    /// 결과창이 게임 시간을 멈춰도 프레임과 크기 연출을 끝까지 재생한다.
    /// </summary>
    public void UseUnscaledTime()
    {
        // 프레임 애니메이션의 시간 기준을 전환한다.
        _useUnscaledTime = true;

        // OnEnable에서 이미 시작된 크기 트윈도 같은 시간 기준으로 맞춘다.
        _scaleTween?.SetUpdate(UpdateType.Normal, true);
    }
    private void Update()
    {
        // 게임 시간에 맞춰 진행하므로 일시정지하면 재생도 멈춘다.
        _elapsed += _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        // 현재 시간에 해당하는 프레임 번호를 구한다.
        int frameIndex = Mathf.FloorToInt(
            _elapsed * _framesPerSecond);

        // 마지막 프레임의 표시 시간까지 끝나면 제거한다.
        if (frameIndex >= _frames.Length)
        {
            Destroy(gameObject);
            return;
        }

        // 스케일은 유지하면서 표시할 스프라이트만 교체한다.
        _spriteRenderer.sprite = _frames[frameIndex];
    }

    private void OnDisable()
    {
        // 비활성화되거나 제거될 때 남은 스케일 연출을 종료한다.
        _scaleTween?.Kill();
        _scaleTween = null;

        // 다시 사용할 경우를 위해 원래 크기로 복원한다.
        transform.localScale = _originalScale;
    }
}
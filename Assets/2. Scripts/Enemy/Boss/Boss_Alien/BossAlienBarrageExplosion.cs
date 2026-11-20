using System;
using DG.Tweening;
using UnityEngine;

// 포격의 폭발 외형을 재생하고, 잔광을 페이드아웃한 뒤 제거한다.
// 피해 판정은 이 컴포넌트에서 처리하지 않는다.
[DisallowMultipleComponent]
public sealed class BossAlienBarrageExplosion : MonoBehaviour
{
    // 자식 Visual의 SpriteRenderer와 Animator를 연결한다.
    [SerializeField] private SpriteRenderer _visual;
    [SerializeField] private Animator _animator;

    // Visual Scale이 1일 때 최대 폭발 이미지의 기준 반경이다.
    // 생성한 폭발 이미지의 PPU600 기준 근삿값이다.
    [SerializeField, Min(0.01f)]
    private float _referenceRadius = 0.97f;

    // 전투 종료 여부를 확인할 보스다.
    private Boss_Alien _owner;

    // Config에서 전달받을 페이드아웃 시간이다.
    private float _fadeDuration;

    // 실행 중인 트윈을 보관하여 취소 시 정리한다.
    private Tween _fadeTween;

    // 초기화 여부와 페이드아웃 시작 여부다.
    private bool _initialized;
    private bool _isFading;

    // 정상 종료와 취소 모두 true가 된다.
    // 패턴에서는 사망·취소 여부도 별도로 확인한다.
    public bool IsFinished { get; private set; }

    private void Awake()
    {
        // Animator와 SpriteRenderer는 같은 자식 Visual에 있어야 한다.
        if (_visual == null ||
            _animator == null ||
            _visual.gameObject != _animator.gameObject ||
            _visual.transform == transform ||
            !_visual.transform.IsChildOf(transform) ||
            _animator.runtimeAnimatorController == null ||
            !IsPositive(_referenceRadius))
        {
            throw new InvalidOperationException(
                $"{name}: 자식 Visual의 연결과 기준 반경을 확인하세요.");
        }

        // 화면 밖에서도 애니메이션이 완료되어야 대기가 끝난다.
        _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // 초기화 전에는 재생하지 않는다.
        _animator.enabled = false;
        _visual.enabled = false;
    }

    // 생성 직후 호출한다.
    // radius는 피해 반경, visualScale은 외형에만 적용하는 추가 배율이다.
    // fadeDuration은 이후 포격 설정에서 전달한다.
    public void Initialize(
        Boss_Alien owner,
        float radius,
        float visualScale,
        float fadeDuration)
    {
        if (_initialized || IsFinished ||
            owner == null ||
            !IsPositive(radius) ||
            !IsPositive(visualScale) ||
            !IsPositive(fadeDuration))
        {
            throw new InvalidOperationException(
                $"{name}: 폭발 초기화 상태와 전달값을 확인하세요.");
        }

        _owner = owner;
        _fadeDuration = fadeDuration;

        // 최대 폭발 외형을 반경에 맞춘 뒤 추가 배율을 적용한다.
        // 실제 피해 반경은 이 계산으로 변경되지 않는다.
        float scale = radius / _referenceRadius * visualScale;
        _visual.transform.localScale = new Vector3(scale, scale, 1f);
        _visual.color = Color.white;

        // 기본 폭발 상태를 처음부터 재생하고 첫 이미지를 즉시 반영한다.
        _animator.enabled = true;
        _animator.Rebind();
        _animator.Update(0f);

        _visual.enabled = true;
        _initialized = true;
        GameAudio.Play(GameSound.AlienBarrageBlast);
    }

    private void Update()
    {
        if (!_initialized || IsFinished)
        {
            return;
        }

        // 시간이 멈춰 있어도 전투 종료 시 정리한다.
        if (_owner == null || !_owner.CanRunArmPattern)
        {
            Cancel();
            return;
        }

        // 페이드아웃은 한 번만 시작한다.
        if (_isFading)
        {
            return;
        }

        // 현재 Controller는 반복하지 않는 폭발 상태 하나로 구성되어 있다.
        // normalizedTime 1 이상이면 클립 전체 재생이 끝난 상태다.
        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);

        if (state.normalizedTime < 1f)
        {
            return;
        }

        _isFading = true;

        // 마지막 잔광 이미지를 유지한 채 색상의 알파만 낮춘다.
        _animator.enabled = false;
        _fadeTween = _visual
            .DOFade(0f, _fadeDuration)
            .SetEase(Ease.OutSine)
            .OnComplete(Complete);
    }

    // 페이드아웃까지 완료된 뒤 종료 상태를 알리고 루트를 제거한다.
    private void Complete()
    {
        _fadeTween = null;
        IsFinished = true;
        _initialized = false;
        _visual.enabled = false;
        Destroy(gameObject);
    }

    // 패턴 취소 시 잔광을 기다리지 않고 즉시 정리한다.
    public void Cancel()
    {
        StopVisual();
        Destroy(gameObject);
    }

    private void OnDisable()
    {
        // 외부에서 비활성화하거나 제거한 경우에도 트윈을 남기지 않는다.
        StopVisual();
    }

    // 완료 콜백을 실행하지 않고 트윈과 외형을 정리한다.
    private void StopVisual()
    {
        IsFinished = true;
        _initialized = false;

        if (_fadeTween != null)
        {
            _fadeTween.Kill(false);
            _fadeTween = null;
        }

        if (_animator != null)
        {
            _animator.enabled = false;
        }

        if (_visual != null)
        {
            _visual.enabled = false;
        }
    }

    // 반경·배율·시간에 사용할 유효한 양수를 검사한다.
    private static bool IsPositive(float value)
    {
        return !float.IsNaN(value) &&
               !float.IsInfinity(value) &&
               value > 0f;
    }
}
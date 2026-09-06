using System;
using UnityEngine;

/// <summary>
/// 적의 피격 이벤트를 받아 흰색 밝기 효과를 적용한다.
/// 공유 Material을 수정하지 않고 해당 Renderer에만 적용한다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public sealed class EnemyHitFlash : MonoBehaviour
{
    // 맞은 순간 원래 색에 흰색을 섞는 비율이다.
    [Range(0f, 1f)]
    [SerializeField] float _flashStrength = 0.55f;

    // 피격 후 원래 색으로 돌아오는 시간이다.
    [Min(0.01f)]
    [SerializeField] float _flashDuration = 0.08f;

    // Shader Graph에 등록한 프로퍼티 이름이다.
    static readonly int HitFlashId =
        Shader.PropertyToID("_HitFlash");

    // 피격 연출을 표시할 스프라이트다.
    SpriteRenderer _spriteRenderer;

    // 부모 적의 피격 이벤트와 사망 상태를 확인한다.
    IDamageable _damageable;

    // 공유 Material을 복제하지 않고 개별 값을 전달한다.
    MaterialPropertyBlock _propertyBlock;

    // 현재 피격 연출이 끝날 때까지 남은 시간이다.
    float _remainingTime;

    // 원래 색으로 복귀한 후 불필요한 갱신을 하지 않는다.
    bool _isFlashing;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _damageable = GetComponentInParent<IDamageable>();
        _propertyBlock = new MaterialPropertyBlock();

        if (_damageable == null)
        {
            throw new InvalidOperationException(
                $"{name}: 부모에서 IDamageable을 찾을 수 없습니다.");
        }

        // 잘못된 Material 연결을 실행 시 바로 확인한다.
        Material material = _spriteRenderer.sharedMaterial;

        if (material == null || !material.HasProperty(HitFlashId))
        {
            throw new InvalidOperationException(
                $"{name}: EnemyHitFlash Material을 연결하세요.");
        }
    }

    private void OnEnable()
    {
        // 재활성화할 때 이전 피격 밝기가 남지 않게 한다.
        ResetFlash();

        _damageable.Damaged += HandleDamaged;
    }

    /// <summary>
    /// 맞을 때마다 밝기를 다시 올리고 복귀 시간을 새로 시작한다.
    /// 연속 피격도 무시하지 않는다.
    /// </summary>
    private void HandleDamaged(int damage)
    {
        if (_damageable.IsDead || damage <= 0)
        {
            return;
        }

        _remainingTime = _flashDuration;
        _isFlashing = true;

        ApplyFlash(_flashStrength);
    }

    private void LateUpdate()
    {
        if (!_isFlashing)
        {
            return;
        }

        // 사망 연출에 피격 밝기가 남지 않도록 즉시 해제한다.
        if (_damageable.IsDead)
        {
            ResetFlash();
            return;
        }

        // 게임 정지 중에는 피격 연출도 함께 멈춘다.
        _remainingTime = Mathf.Max(
            0f, _remainingTime - Time.deltaTime);

        float ratio = _remainingTime / _flashDuration;

        // 밝기가 빠르게 내려가면서 원래 색으로 부드럽게 복귀한다.
        ApplyFlash(_flashStrength * ratio * ratio);

        if (_remainingTime <= 0f)
        {
            _isFlashing = false;
        }
    }

    /// <summary>
    /// 이 Renderer에만 흰색 혼합 비율을 전달한다.
    /// 다른 프로퍼티 값은 유지한다.
    /// </summary>
    private void ApplyFlash(float amount)
    {
        _spriteRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetFloat(HitFlashId, amount);
        _spriteRenderer.SetPropertyBlock(_propertyBlock);
    }

    /// <summary>
    /// 피격 연출을 종료하고 원래 색으로 복귀한다.
    /// </summary>
    private void ResetFlash()
    {
        _remainingTime = 0f;
        _isFlashing = false;
        ApplyFlash(0f);
    }

    private void OnDisable()
    {
        if (_damageable != null)
        {
            _damageable.Damaged -= HandleDamaged;
        }

        ResetFlash();
    }
}
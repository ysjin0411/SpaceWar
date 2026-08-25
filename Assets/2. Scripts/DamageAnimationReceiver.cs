using System;
using UnityEngine;

/// <summary>
/// 부모의 IDamageable 피격 이벤트를 받아
/// Renderer Animator의 피격 애니메이션을 재생한다.
/// </summary>
[RequireComponent(typeof(Animator))]
public class DamageAnimationReceiver : MonoBehaviour
{
    // Animator에 등록할 피격 Trigger의 해시값이다.
    static readonly int DamagedTriggerHash =
        Animator.StringToHash("Damaged");

    // 피격 Trigger를 전달할 Renderer Animator다.
    Animator _animator;

    // 피격 이벤트를 발생시키는 부모 전투 객체다.
    IDamageable _damageable;

    private void Awake()
    {
        _animator =
            GetComponent<Animator>();

        _damageable =
            GetComponentInParent<IDamageable>();

        if (_damageable == null)
        {
            throw new InvalidOperationException(
                $"{name}의 부모에서 IDamageable을 찾을 수 없습니다.");
        }
    }

    private void OnEnable()
    {
        // 부모가 대미지를 받을 때 Animator Trigger를 실행하도록 구독한다.
        _damageable.Damaged +=
            HandleDamaged;
    }

    private void OnDisable()
    {
        // 비활성화된 Renderer가 계속 이벤트를 받지 않도록 해제한다.
        if (_damageable != null)
        {
            _damageable.Damaged -=
                HandleDamaged;
        }
    }

    /// <summary>
    /// 부모가 대미지를 받으면 피격 애니메이션 Trigger를 실행한다.
    /// </summary>
    private void HandleDamaged(int damage)
    {
        _animator.SetTrigger(
            DamagedTriggerHash);
    }
}
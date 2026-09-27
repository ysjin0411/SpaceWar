using System;
using System.Collections.Generic;
using UnityEngine;

// 한 번의 콤보 타격에 대한 범위 검사와 중복 피해 방지를 담당한다.
// MonoBehaviour가 아니므로 오브젝트에 붙이지 않는다.
public sealed class BossAlienComboHitbox
{
    // 이 타격의 대상 플레이어다.
    private readonly PlayerController _target;

    // 공격 시작 시 확정한 월드 좌표, 크기, 회전이다.
    private readonly Vector2 _center;
    private readonly Vector2 _size;
    private readonly float _angle;
    private readonly float _damage;

    // 공격이 유지되는 동안 같은 목록을 재사용한다.
    private readonly List<Collider2D> _hits =
        new List<Collider2D>();

    // 플레이어와 실드 레이어만 검사한다.
    private ContactFilter2D _filter;

    // 실드가 방어했거나 플레이어에게 피해를 요청하면 타격을 소비한다.
    // 다음 공격은 새 객체를 만들어 별도로 판정한다.
    private bool _consumed;

    // 방향이 고정된 경고의 값을 받아 해당 타격의 판정을 만든다.
    /// <summary>
    /// 방향이 고정된 경고의 값을 받아 해당 타격의 판정을 만든다.
    /// </summary>
    public BossAlienComboHitbox(
        PlayerController target,
        Vector2 center,
        Vector2 size,
        float angle,
        float damage)
    {
        // 방향이 고정된 경고의 값을 받아 해당 타격의 판정을 만든다.
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        if (!IsFinite(center.x) || !IsFinite(center.y) ||
            !IsFinite(size.x) || !IsFinite(size.y) ||
            size.x <= 0f || size.y <= 0f ||
            !IsFinite(angle) || damage < 0)
        {
            throw new ArgumentException(
                "콤보의 좌표, 크기, 각도, 피해량을 확인하세요.");
        }

        _target = target;
        _center = center;
        _size = size;
        _angle = angle;
        _damage = damage;

        _filter = new ContactFilter2D();
        _filter.SetLayerMask(
            LayerMask.GetMask("Player", "PlayerShield"));
        _filter.useTriggers = true;
    }

    // 공격 활성 구간에서 반복 호출한다.
    // 한 번 타격을 소비하면 이후 호출은 아무 피해도 주지 않는다.
    /// <summary>
    /// 한 번 타격을 소비하면 이후 호출은 아무 피해도 주지 않는다.
    /// </summary>
    public void Tick()
    {
        if (_consumed ||
            _damage <= 0 ||
            _target == null ||
            !_target.isActiveAndEnabled ||
            _target.IsDead)
        {
            return;
        }

        // 같은 프레임에 변경된 위치를 물리 검사에 반영한다.
        Physics2D.SyncTransforms();

        _hits.Clear();
        Physics2D.OverlapBox(
            _center,
            _size,
            _angle,
            _filter,
            _hits);

        // 기존 폭발 판정과 동일하게 실드 방어를 먼저 검사한다.
        for (int i = 0; i < _hits.Count; i++)
        {
            Collider2D hit = _hits[i];

            if (!hit.TryGetComponent(out Shield shield))
                continue;

            Vector2 hitPoint = hit.ClosestPoint(_center);

            if (shield.TryBlockDamage(_damage, hitPoint))
            {
                // 방어된 공격이 같은 타격 구간에서 본체를 다시 때리지 않는다.
                _consumed = true;
                return;
            }
        }

        int shieldLayer = LayerMask.NameToLayer("PlayerShield");

        for (int i = 0; i < _hits.Count; i++)
        {
            Collider2D hit = _hits[i];

            // 실드 콜라이더를 플레이어 몸체로 해석하지 않는다.
            if (hit.gameObject.layer == shieldLayer)
                continue;

            PlayerController player =
                hit.GetComponentInParent<PlayerController>();

            if (player != _target)
                continue;

            // 호출 전에 소비 상태를 기록하여 중복 피해 요청을 막는다.
            _consumed = true;
            player.TakeHit(_damage);
            return;
        }
    }

    // 물리 검사에 사용할 수 없는 숫자를 걸러낸다.
    /// <summary>
    /// 물리 검사에 사용할 수 없는 숫자를 걸러낸다.
    /// </summary>
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
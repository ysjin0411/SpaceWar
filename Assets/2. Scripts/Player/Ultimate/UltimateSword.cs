using System;
using System.Collections.Generic;
using UnityEngine;

// 오른쪽으로 이동하다 첫 적에게 피해와 타격 연출을 남기고 사라지는 검이다.
public sealed class UltimateSword : MonoBehaviour
{
    // 초당 오른쪽으로 이동하는 월드 거리다.
    [SerializeField, Min(0.01f)] private float _moveSpeed = 30f;

    // 검 한 발이 대상 하나에 주는 피해량이다.
    [SerializeField, Min(0.01f)] private float _damage = 50f;

    // 생성 후 자동으로 제거되기까지의 게임 시간이다.
    [SerializeField, Min(0.01f)] private float _lifeTime = 2f;

    // 검 중심에 배치할 피해 판정의 너비와 높이다.
    // 월드 단위이며 이미지 크기와 별도로 조절한다.
    [SerializeField] private Vector2 _hitSize = new Vector2(2f, 0.35f);

    // 피해를 받을 적의 Collider 레이어다.
    [SerializeField] private LayerMask _enemyLayers;

    // 명중 후 검과 독립적으로 재생하는 타격 이펙트다.
    [SerializeField] private SpriteOneShotEffect _hitEffectPrefab;

    // 시작 위치에서 겹친 대상 목록을 재사용한다.
    private readonly List<Collider2D> _hits = new List<Collider2D>();

    // 이동 경로의 충돌 결과를 재사용한다.
    private readonly List<RaycastHit2D> _castHits = new List<RaycastHit2D>();

    // 검마다 독립적으로 명중 여부를 관리한다.
    private bool _hasHit;

    private ContactFilter2D _filter;
    private float _remainingTime;

    /// <summary>발사자가 사용할 수 있도록 수치와 필수 이펙트를 검사한다.</summary>
    public void ValidateConfiguration()
    {
        if (_moveSpeed <= 0f ||
            _damage <= 0f ||
            _lifeTime <= 0f ||
            _hitSize.x <= 0f ||
            _hitSize.y <= 0f ||
            _enemyLayers.value == 0 || _hitEffectPrefab == null)
        {
            throw new InvalidOperationException(
                $"{name}: 필살기 검의 수치, Enemy Layers, Hit Effect Prefab을 확인해주세요.");
        }
    }

    /// <summary>Trigger를 포함한 검색 조건과 수명을 초기화한다.</summary>
    private void Awake()
    {
        ValidateConfiguration();

        // 적이 Trigger Collider를 사용하는 경우도 검사한다.
        _filter = new ContactFilter2D();
        _filter.SetLayerMask(_enemyLayers);
        _filter.useTriggers = true;

        _remainingTime = _lifeTime;
    }

    /// <summary>이동 경로의 첫 적에게 명중하거나 수명 동안 이동한다.</summary>
    private void Update()
    {
        if (_hasHit || Time.deltaTime <= 0f)
        {
            return;
        }

        // 수명이 끝나는 프레임에는 남은 시간만큼만 이동한다.
        float stepTime = Mathf.Min(Time.deltaTime, _remainingTime);
        float distance = _moveSpeed * stepTime;

        Vector3 start = transform.position;
        Vector3 end = start + Vector3.right * distance;

        // Transform으로 이동한 적도 현재 위치로 검색한다.
        Physics2D.SyncTransforms();

        // 생성 시 이미 적과 겹쳤다면 이동 전 명중으로 처리한다.
        _hits.Clear();
        Physics2D.OverlapBox(start, _hitSize, 0f, _filter, _hits);
        IDamageable nearestTarget = null;
        Vector2 nearestPoint = default;
        float nearestDistance = float.PositiveInfinity;
        foreach (Collider2D hit in _hits)
        {
            // 이미 겹친 대상들 중 검 중심에 가까운 대상을 선택한다.
            if (hit == null) continue;
            IDamageable target = hit.GetComponentInParent<IDamageable>();
            if (target == null || target.IsDead) continue;
            Vector2 point = hit.ClosestPoint(start);
            float sqrDistance = (point - (Vector2)start).sqrMagnitude;
            if (sqrDistance < nearestDistance)
            {
                nearestTarget = target;
                nearestPoint = point;
                nearestDistance = sqrDistance;
            }
        }
        if (nearestTarget != null)
        {
            HitAndDestroy(nearestTarget, nearestPoint);
            return;
        }

        // 이동하는 사각형을 검사해 빠른 검이 적을 건너뛰지 않게 한다.
        _castHits.Clear();
        Physics2D.BoxCast(start, _hitSize, 0f, Vector2.right, _filter, _castHits, distance);
        nearestDistance = float.PositiveInfinity;
        foreach (RaycastHit2D hit in _castHits)
        {
            // 검색 결과 순서에 의존하지 않고 첫 접촉 거리를 비교한다.
            if (hit.collider == null || hit.distance >= nearestDistance) continue;
            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
            if (target == null || target.IsDead) continue;
            nearestTarget = target;
            nearestPoint = hit.point;
            nearestDistance = hit.distance;
        }
        if (nearestTarget != null)
        {
            // 관통한 끝 위치 대신 최초 접촉 위치까지만 이동한다.
            transform.position = start + Vector3.right * nearestDistance;
            HitAndDestroy(nearestTarget, nearestPoint);
            return;
        }

        transform.position = end;
        _remainingTime -= stepTime;

        if (_remainingTime <= 0f)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>검 한 발의 명중을 처리하고 독립된 이펙트를 남긴다.</summary>
    private void HitAndDestroy(IDamageable target, Vector2 hitPoint)
    {
        // Destroy가 지연되어도 같은 검의 피해가 반복되지 않게 한다.
        if (_hasHit) return;
        _hasHit = true;

        // 검의 형제로 생성해 검이 사라진 후에도 재생한다.
        // 궁극기 취소 시에는 기존 실행 루트와 함께 정리된다.
        Instantiate(_hitEffectPrefab,
            new Vector3(hitPoint.x, hitPoint.y, transform.position.z),
            Quaternion.identity, transform.parent);
        GameAudio.Play(GameSound.UltimateSwordImpact);
        target.TakeHit(_damage);

        // 즉시 숨긴 뒤 프레임 끝에 제거한다.
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    /// <summary>선택한 검의 실제 판정 영역을 표시한다.</summary>
    private void OnDrawGizmosSelected()
    {
        // Scene에서 선택하면 실제 피해 판정 크기를 확인할 수 있다.
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(
            transform.position,
            new Vector3(_hitSize.x, _hitSize.y, 0f));
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

// 오른쪽으로 이동하며 적을 관통하는 필살기 검이다.
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

    // 매 프레임 배열을 새로 만들지 않고 검색 결과 목록을 재사용한다.
    private readonly List<Collider2D> _hits = new List<Collider2D>();

    // 이미 피해를 받은 대상은 다시 공격하지 않는다.
    private readonly HashSet<IDamageable> _damagedTargets =
        new HashSet<IDamageable>();

    private ContactFilter2D _filter;
    private float _remainingTime;

    // 이후 발사하는 쪽에서도 프리팹 설정을 검사할 수 있다.
    public void ValidateConfiguration()
    {
        if (_moveSpeed <= 0f ||
            _damage <= 0f ||
            _lifeTime <= 0f ||
            _hitSize.x <= 0f ||
            _hitSize.y <= 0f ||
            _enemyLayers.value == 0)
        {
            throw new InvalidOperationException(
                $"{name}: 필살기 검의 수치와 Enemy Layers를 확인해주세요.");
        }
    }

    private void Awake()
    {
        ValidateConfiguration();

        // 적이 Trigger Collider를 사용하는 경우도 검사한다.
        _filter = new ContactFilter2D();
        _filter.SetLayerMask(_enemyLayers);
        _filter.useTriggers = true;

        _remainingTime = _lifeTime;
    }

    private void Update()
    {
        if (Time.deltaTime <= 0f)
        {
            return;
        }

        // 수명이 끝나는 프레임에는 남은 시간만큼만 이동한다.
        float stepTime = Mathf.Min(Time.deltaTime, _remainingTime);
        float distance = _moveSpeed * stepTime;

        Vector3 start = transform.position;
        Vector3 end = start + Vector3.right * distance;

        // 수평 이동 전후의 판정을 합친 사각형으로 경로 전체를 검사한다.
        Vector2 sweepCenter = (start + end) * 0.5f;
        Vector2 sweepSize =
            new Vector2(_hitSize.x + distance, _hitSize.y);

        _hits.Clear();

        Physics2D.OverlapBox(
            sweepCenter,
            sweepSize,
            0f,
            _filter,
            _hits);

        foreach (Collider2D hit in _hits)
        {
            // 피해 처리 도중 스테이지가 끝나 공격이 취소될 수 있다.
            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            if (hit == null)
            {
                continue;
            }

            IDamageable target =
                hit.GetComponentInParent<IDamageable>();

            if (target == null || target.IsDead)
            {
                continue;
            }

            if (_damagedTargets.Add(target))
            {
                target.TakeHit(_damage);
            }
        }

        transform.position = end;
        _remainingTime -= stepTime;

        if (_remainingTime <= 0f)
        {
            Destroy(gameObject);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Scene에서 선택하면 실제 피해 판정 크기를 확인할 수 있다.
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(
            transform.position,
            new Vector3(_hitSize.x, _hitSize.y, 0f));
    }
}
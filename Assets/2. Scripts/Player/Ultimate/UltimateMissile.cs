using System;
using System.Collections.Generic;
using UnityEngine;

// 지정된 월드 좌표까지 직선 이동한 뒤 폭발하는 필살기 미사일이다.
public sealed class UltimateMissile : MonoBehaviour
{
    // 미사일의 초당 이동 거리다.
    [SerializeField, Min(0.01f)]
    private float _moveSpeed = 30f;

    // 폭발 한 번으로 대상 하나에 전달할 피해량이다.
    [SerializeField, Min(0.01f)]
    private float _damage = 50f;

    // 도착 좌표를 중심으로 피해를 줄 반경이다.
    [SerializeField, Min(0.01f)]
    private float _explosionRadius = 1.2f;

    // 일반 적과 보스의 피격 Collider가 사용하는 레이어다.
    [SerializeField] private LayerMask _enemyLayers;

    // 일반 몬스터의 사망 폭발을 복제하여 확대한 프리팹이다.
    [SerializeField] private SpriteOneShotEffect _explosionPrefab;

    // 이동 경로에 남길 독립적인 연기 프리팹이다.
    [SerializeField] private SpriteOneShotEffect _smokePrefab;

    // 연기를 배치할 월드 거리 간격이다.
    [SerializeField, Min(0.01f)]
    private float _smokeSpacing = 0.4f;

    // 각각의 연기가 생성 후 남아 있는 게임 시간이다.
    [SerializeField, Min(0.01f)]
    private float _smokeDuration = 2f;

    // 목표 위치는 발사할 때 한 번 결정한다.
    private Vector3 _targetPosition;

    // 연기는 미사일의 자식이 아닌 필살기 실행 루트에 생성한다.
    private Transform _effectRoot;

    // 직전에 연기를 배치한 위치다.
    private Vector3 _lastSmokePosition;

    private bool _isInitialized;
    private bool _hasExploded;

    // 하나의 적에 Collider가 여러 개 있어도 한 번만 피해를 준다.
    private readonly HashSet<IDamageable> _damagedTargets =
        new HashSet<IDamageable>();

    public void ValidateConfiguration()
    {
        if (_moveSpeed <= 0f ||
            _damage <= 0f ||
            _explosionRadius <= 0f ||
            _smokeSpacing <= 0f ||
            _smokeDuration <= 0f ||
            _enemyLayers.value == 0 ||
            _explosionPrefab == null ||
            _smokePrefab == null)
        {
            throw new InvalidOperationException(
                $"{name}: 필살기 미사일의 수치와 프리팹을 확인해주세요.");
        }
    }

    // 생성 직후 발사자가 목표 위치와 연출 루트를 전달한다.
    public void Initialize(Vector3 targetPosition, Transform effectRoot)
    {
        if (effectRoot == null)
        {
            throw new ArgumentNullException(nameof(effectRoot));
        }

        _targetPosition = targetPosition;
        _effectRoot = effectRoot;
        _lastSmokePosition = transform.position;

        Vector3 direction = _targetPosition - transform.position;

        // 원본 이미지가 오른쪽을 바라보는 것을 기준으로 회전한다.
        if (direction.sqrMagnitude > 0.000001f)
        {
            float angle =
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        _isInitialized = true;
    }

    private void Update()
    {
        if (!_isInitialized ||
            _hasExploded ||
            Time.deltaTime <= 0f)
        {
            return;
        }

        Vector3 nextPosition = Vector3.MoveTowards(
            transform.position,
            _targetPosition,
            _moveSpeed * Time.deltaTime);

        // 빠르게 이동해도 프레임 사이의 경로에 연기를 채운다.
        LeaveSmoke(nextPosition);

        transform.position = nextPosition;

        if ((nextPosition - _targetPosition).sqrMagnitude <= 0.000001f)
        {
            Explode();
        }
    }

    // 마지막 연기 위치부터 현재 위치까지 일정 간격으로 배치한다.
    private void LeaveSmoke(Vector3 endPosition)
    {
        Vector3 offset = endPosition - _lastSmokePosition;
        float distance = offset.magnitude;

        if (distance < _smokeSpacing)
        {
            return;
        }

        Vector3 direction = offset / distance;

        while (distance >= _smokeSpacing)
        {
            _lastSmokePosition += direction * _smokeSpacing;

            SpriteOneShotEffect smoke = Instantiate(
                _smokePrefab,
                _lastSmokePosition,
                Quaternion.identity,
                _effectRoot);

            // 기존 연기 프레임을 지정한 전체 시간에 맞춰 재생한다.
            smoke.SetPlaybackDuration(_smokeDuration);

            distance -= _smokeSpacing;
        }
    }

    private void Explode()
    {
        _hasExploded = true;
        GameAudio.Play(GameSound.UltimateBombardImpact); // 실제 폭발 시작.

        // 피해로 스테이지가 끝나더라도 함께 정리할 수 있도록
        // 폭발 연출도 필살기 실행 루트에 소속시킨다.
        Instantiate(
            _explosionPrefab,
            _targetPosition,
            Quaternion.identity,
            _effectRoot);

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            _targetPosition,
            _explosionRadius,
            _enemyLayers);

        foreach (Collider2D hit in hits)
        {
            // 앞선 피해 처리로 클리어되어 필살기가 취소됐을 수 있다.
            if (!gameObject.activeInHierarchy)
            {
                break;
            }

            if (hit == null)
            {
                continue;
            }

            IDamageable target = hit.GetComponentInParent<IDamageable>();

            if (target == null || target.IsDead)
            {
                continue;
            }

            if (!_damagedTargets.Add(target))
            {
                continue;
            }

            target.TakeHit(_damage);
        }

        // 연기와 폭발은 별도 형제 오브젝트라 계속 재생된다.
        Destroy(gameObject);
    }
}
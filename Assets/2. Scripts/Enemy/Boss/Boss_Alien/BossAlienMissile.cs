using System;
using UnityEngine;

// 외계인 보스 미사일의 곡선 이동과 방향 회전을 담당한다.
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public sealed class BossAlienMissile : MonoBehaviour
{
    // 발사 순간 Config에서 전달받은 한 발의 폭발 피해량이다.
    private int _damage;

    // 발사한 보스와 공격 대상의 전투 종료 여부를 확인한다.
    private BossEnemy _owner;
    private PlayerController _targetPlayer;

    // 목표에 도착했을 때 생성할 전용 폭발 이펙트다.
    private BossAlienMissileExplosion _explosionPrefab;

    // 발사 시 전달받아 보관할 폭발 반경이다.
    private float _explosionRadius;

    // 발사 시 확정되는 시작점과 목표점이다.
    private Vector3 _startPoint;
    private Vector3 _endPoint;


    // 출발 방향과 목표 접근 방향을 결정하는 두 제어점이다.
    private Vector3 _startControlPoint;
    private Vector3 _endControlPoint;

    // 전체 비행 시간과 현재까지 흐른 시간이다.
    private float _flightDuration;
    private float _elapsedTime;

    // 발사 정보가 전달된 이후에만 이동한다.
    private bool _isFlying;

    private void Awake()
    {
        // 위치는 곡선 계산으로 직접 갱신한다.
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;

        // 충돌 시 물리적으로 밀리지 않도록 설정한다.
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    // 경로, 폭발 정보와 함께 발사자와 공격 대상을 전달받는다.
    public void Launch(
        Vector3 startPoint,
        Vector3 endPoint,
        Vector2 startControlOffset,
        Vector2 endControlOffset,
        float flightDuration,
        BossAlienMissileExplosion explosionPrefab,
        float explosionRadius,
        int damage,
        BossEnemy owner,
        PlayerController targetPlayer)
    {
        // 미사일이 전투 종료 여부를 확인할 참조를 저장한다.
        if (owner == null)
        {
            throw new ArgumentNullException(nameof(owner));
        }

        if (targetPlayer == null)
        {
            throw new ArgumentNullException(nameof(targetPlayer));
        }

        _owner = owner;
        _targetPlayer = targetPlayer;

        // 음수 피해량을 막고 발사 당시 피해량을 저장한다.
        if (damage < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(damage), "피해량은 0 이상이어야 합니다.");
        }

        _damage = damage;

        // 생성할 이펙트와 반경을 발사 전에 확인한다.
        if (explosionPrefab == null)
        {
            throw new ArgumentNullException(nameof(explosionPrefab));
        }

        if (float.IsNaN(explosionRadius) ||
            float.IsInfinity(explosionRadius) ||
            explosionRadius <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(explosionRadius), "폭발 반경은 0보다 커야 합니다.");
        }

        // 비행 중 Config가 바뀌어도 이 미사일은 발사 당시 값을 사용한다.
        _explosionPrefab = explosionPrefab;
        _explosionRadius = explosionRadius;

        if (float.IsNaN(flightDuration) ||
            float.IsInfinity(flightDuration) ||
            flightDuration <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(flightDuration), "비행 시간은 0보다 커야 합니다.");
        }

        // 목표 위치를 발사 순간에 저장하여 비행 중에는 바뀌지 않게 한다.
        _startPoint = startPoint;
        _endPoint = endPoint;

        // 첫 제어점은 시작점 기준, 두 번째는 목표점 기준으로 계산한다.
        _startControlPoint = startPoint + (Vector3)startControlOffset;
        _endControlPoint = endPoint + (Vector3)endControlOffset;

        _flightDuration = flightDuration;
        _elapsedTime = 0f;

        transform.position = _startPoint;

        // 첫 제어점을 향하는 방향으로 출발한다.
        FaceDirection(_startControlPoint - _startPoint);
        _isFlying = true;
    }

    private void Update()
    {
        // 발사 전이거나 이미 폭발한 미사일은 처리하지 않는다.
        if (!_isFlying)
        {
            return;
        }

        // 로봇은 체력이 0이어도 페이즈 전환 중에는 IsDead가 false다.
        // 발사자의 체력이 소진됐으면 폭발 없이 미사일을 제거한다.
        if (_owner == null ||
            !_owner.isActiveAndEnabled ||
            _owner.IsDead ||
            _owner.CurrentHp <= 0 ||
            _targetPlayer == null ||
            !_targetPlayer.isActiveAndEnabled ||
            _targetPlayer.IsDead)
        {
            _isFlying = false;
            Destroy(gameObject);
            return;
        }

        // 일반적인 일시정지에서는 미사일을 유지하고 이동만 멈춘다.
        if (Time.deltaTime <= 0f)
        {
            return;
        }

        _elapsedTime += Time.deltaTime;

        // 비행 진행도를 0~1 범위로 계산한다.
        float t = Mathf.Clamp01(_elapsedTime / _flightDuration);
        float remaining = 1f - t;

        // 3차 베지어 곡선 위의 위치를 계산한다.
        // 시작점 → 첫 제어점 방향으로 출발하고,
        // 두 번째 제어점 → 목표점 방향으로 접근한다.
        Vector3 nextPosition =
            remaining * remaining * remaining * _startPoint +
            3f * remaining * remaining * t * _startControlPoint +
            3f * remaining * t * t * _endControlPoint +
            t * t * t * _endPoint;

        // 곡선의 접선 방향을 계산해 미사일 회전에 사용한다.
        Vector3 direction =
            3f * remaining * remaining *
                (_startControlPoint - _startPoint) +
            6f * remaining * t *
                (_endControlPoint - _startControlPoint) +
            3f * t * t *
                (_endPoint - _endControlPoint);

        FaceDirection(direction);
        transform.position = nextPosition;

        // 목표에 도착하면 한 번만 폭발하고 미사일을 제거한다.
        if (t >= 1f)
        {
            _isFlying = false;

            // 미사일과 독립된 오브젝트로 생성하여 끝까지 재생되게 한다.
            BossAlienMissileExplosion explosion = Instantiate(
                _explosionPrefab,
                _endPoint,
                Quaternion.identity);

            // Config에서 전달받은 반경에 맞춰 시각 효과를 조절한다.
            explosion.Initialize(_explosionRadius);

            // 도착 순간에만 범위를 검사한다. 잔광이 남아도 추가 피해는 없다.
            BossAlienExplosionDamage.Apply(
                _endPoint, _explosionRadius, _damage);

            Destroy(gameObject);
        }
    }

    // 왼쪽을 향한 원본 미사일 이미지를 이동 방향에 맞춘다.
    private void FaceDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        float angle =
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // 원본의 앞쪽이 -X이므로 180도를 보정한다.
        transform.rotation = Quaternion.Euler(0f, 0f, angle - 180f);
    }
}
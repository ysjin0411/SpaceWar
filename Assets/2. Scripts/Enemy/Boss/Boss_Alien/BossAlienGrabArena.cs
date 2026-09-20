using System;
using System.Collections.Generic;
using UnityEngine;

// 던져진 플레이어와 전투 영역 경계의 충돌을 검사한다.
public sealed class BossAlienGrabArena : MonoBehaviour
{
    // 전투 영역의 안쪽 경계를 계산할 실제 벽 콜라이더들이다.
    // 네 벽은 회전 없이 배치한다.
    [SerializeField] private BoxCollider2D _leftWall;
    [SerializeField] private BoxCollider2D _rightWall;
    [SerializeField] private BoxCollider2D _ceiling;
    [SerializeField] private BoxCollider2D _floor;

    // 전투 영역의 경계 레이어만 검사한다.
    private ContactFilter2D _filter;

    // 매번 배열을 생성하지 않도록 충돌 결과 목록을 재사용한다.
    private readonly List<RaycastHit2D> _hits =
        new List<RaycastHit2D>();

    // 첫 벽 충돌의 보스, 충돌 위치, 표면 방향을 전달한다.
    // 이후 균열 생성과 카메라 흔들림을 연결할 때 사용한다.
    public event Action<Boss_Alien, Vector2, Vector2> ImpactOccurred;

    private void Awake()
    {
        // 시작할 때 경계 연결과 전투 영역이 올바른지 검사한다.
        GetInteriorRect();

        int layer = LayerMask.NameToLayer("BossArenaBoundary");

        if (layer < 0)
        {
            throw new InvalidOperationException(
                $"{name}: BossArenaBoundary 레이어가 필요합니다.");
        }

        _filter = new ContactFilter2D();
        _filter.SetLayerMask(1 << layer);

        // 경계를 Trigger로 구성했으므로 검사에 포함한다.
        _filter.useTriggers = true;
    }

    // 플레이어를 실제로 옮기기 전에 호출한다.
    // movement는 이번 이동량이고, hit에는 가장 가까운 충돌이 반환된다.
    public bool TryFindImpact(
        Collider2D playerBody,
        Vector2 movement,
        out RaycastHit2D hit)
    {
        hit = default(RaycastHit2D);

        if (!isActiveAndEnabled ||
            playerBody == null ||
            !playerBody.isActiveAndEnabled)
        {
            return false;
        }

        float distance = movement.magnitude;

        if (distance <= 0f)
        {
            return false;
        }

        // 같은 프레임에 갱신한 위치를 물리 검사에 반영한다.
        Physics2D.SyncTransforms();

        _hits.Clear();

        // 플레이어 중심의 선 대신 실제 몸 콜라이더 전체를 이동시켜 검사한다.
        playerBody.Cast(
            movement / distance,
            _filter,
            _hits,
            distance);

        float nearestDistance = float.PositiveInfinity;
        bool found = false;

        for (int i = 0; i < _hits.Count; i++)
        {
            RaycastHit2D candidate = _hits[i];

            // 이 전투 영역 아래에 있는 경계만 사용한다.
            if (candidate.collider == null ||
                !candidate.collider.transform.IsChildOf(transform) ||
                candidate.distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = candidate.distance;
            hit = candidate;
            found = true;
        }

        return found;
    }

    // 던지기 실행 코드가 첫 충돌에만 호출한다.
    public void NotifyFirstImpact(Boss_Alien boss, RaycastHit2D hit)
    {
        if (!isActiveAndEnabled ||
            boss == null ||
            boss.IsDead ||
            hit.collider == null)
        {
            return;
        }

        if (ImpactOccurred != null)
        {
            ImpactOccurred.Invoke(boss, hit.point, hit.normal);
        }
    }

    // 벽 두께를 제외한 실제 전투 영역을 월드 좌표로 반환한다.
    // 예고, 충격파, 마무리 공격이 모두 이 영역을 기준으로 계산한다.
    public Rect GetInteriorRect()
    {
        ValidateBoundary(_leftWall);
        ValidateBoundary(_rightWall);
        ValidateBoundary(_ceiling);
        ValidateBoundary(_floor);

        // 같은 프레임에 변경된 Transform을 bounds에 반영한다.
        Physics2D.SyncTransforms();

        float left = _leftWall.bounds.max.x;
        float right = _rightWall.bounds.min.x;
        float bottom = _floor.bounds.max.y;
        float top = _ceiling.bounds.min.y;

        if (left >= right || bottom >= top)
        {
            throw new InvalidOperationException(
                $"{name}: 벽 연결 순서와 전투 영역 크기를 확인하세요.");
        }

        return Rect.MinMaxRect(left, bottom, right, top);
    }

    // 균열 위치에서 지정 방향으로 진행해 반대편 경계에 닿는 거리다.
    // 방향을 정규화하므로 반환값은 월드 단위 거리다.
    public float GetTravelDistance(Vector2 origin, Vector2 direction)
    {
        if (!Finite(origin.x) || !Finite(origin.y) ||
            !Finite(direction.x) || !Finite(direction.y) ||
            direction.sqrMagnitude < 0.0001f)
        {
            throw new ArgumentException(
                "충격파 시작 위치와 방향이 유효하지 않습니다.");
        }

        Rect area = GetInteriorRect();

        // 충돌 위치 계산에서 발생할 수 있는 작은 오차만 허용한다.
        const float tolerance = 0.02f;

        if (origin.x < area.xMin - tolerance ||
            origin.x > area.xMax + tolerance ||
            origin.y < area.yMin - tolerance ||
            origin.y > area.yMax + tolerance)
        {
            throw new ArgumentOutOfRangeException(
                nameof(origin), "균열 위치가 전투 영역 밖에 있습니다.");
        }

        direction.Normalize();

        // 각 축의 진행 방향에 있는 경계까지의 거리를 구한다.
        float xDistance = float.PositiveInfinity;
        float yDistance = float.PositiveInfinity;

        if (direction.x > 0.0001f)
        {
            xDistance = (area.xMax - origin.x) / direction.x;
        }
        else if (direction.x < -0.0001f)
        {
            xDistance = (area.xMin - origin.x) / direction.x;
        }

        if (direction.y > 0.0001f)
        {
            yDistance = (area.yMax - origin.y) / direction.y;
        }
        else if (direction.y < -0.0001f)
        {
            yDistance = (area.yMin - origin.y) / direction.y;
        }

        // 두 축 중 먼저 만나는 경계가 충격파의 도착 지점이다.
        float distance = Mathf.Min(xDistance, yDistance);

        if (!Finite(distance) || distance <= 0f)
        {
            throw new InvalidOperationException(
                $"{name}: 충격파 방향이 전투 영역 안쪽을 향하는지 확인하세요.");
        }

        return distance;
    }

    // 이 Arena 아래에 있는 활성화된 사각형 경계만 허용한다.
    private void ValidateBoundary(BoxCollider2D boundary)
    {
        if (boundary == null ||
            !boundary.isActiveAndEnabled ||
            !boundary.transform.IsChildOf(transform) ||
            !boundary.isTrigger ||
            boundary.gameObject.layer !=
                LayerMask.NameToLayer("BossArenaBoundary"))
        {
            throw new InvalidOperationException(
                $"{name}: 네 경계의 연결·활성 상태·Trigger·레이어를 확인하세요.");
        }

        // 회전된 벽의 bounds는 실제 표면과 다르므로 사용하지 않는다.
        if (Quaternion.Angle(
                boundary.transform.rotation, Quaternion.identity) > 0.01f)
        {
            throw new InvalidOperationException(
                $"{name}: 전투 경계의 월드 회전은 0이어야 합니다.");
        }
    }

    // 좌표 계산에 사용할 수 있는 유효한 숫자인지 확인한다.
    private static bool Finite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
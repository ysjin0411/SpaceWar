using System.Collections.Generic;
using UnityEngine;

// 손의 현재 영역과 이동 경로에서 잡을 수 있는 플레이어를 찾는다.
[RequireComponent(typeof(BoxCollider2D))]
public sealed class BossAlienGrabSensor : MonoBehaviour
{
    // 손바닥의 잡기 영역이다.
    private BoxCollider2D _collider;

    // 플레이어 몸만 검사하며 실드는 포함하지 않는다.
    private ContactFilter2D _filter;

    // 반복 검사에서 결과 목록을 재사용한다.
    private readonly List<Collider2D> _overlaps =
        new List<Collider2D>();
    private readonly List<RaycastHit2D> _castHits =
        new List<RaycastHit2D>();

    private void Awake()
    {
        _collider = GetComponent<BoxCollider2D>();
        _collider.isTrigger = true;

        int playerLayer = LayerMask.NameToLayer("Player");

        if (playerLayer < 0)
        {
            throw new System.InvalidOperationException(
                $"{name}: Player 레이어가 필요합니다.");
        }

        _filter = new ContactFilter2D();
        _filter.SetLayerMask(1 << playerLayer);
        _filter.useTriggers = true;

        // 팔을 뻗는 구간에서만 패턴이 판정을 켠다.
        _collider.enabled = false;
    }

    // 준비·복귀 중에는 끄고, 잡기 시도 구간에서만 켠다.
    public void SetDetectionEnabled(bool enabled)
    {
        _collider.enabled = enabled;
    }

    // 손을 이동하기 전에 호출한다.
    // movement는 이번에 이동할 월드 거리이며,
    // hitDistance는 그 경로에서 접촉하기까지의 거리다.
    public bool TryDetectPlayer(
        Vector2 movement,
        out PlayerController player,
        out float hitDistance)
    {
        player = null;
        hitDistance = 0f;

        if (!isActiveAndEnabled || !_collider.enabled)
        {
            return false;
        }

        // 손과 플레이어의 최신 Transform을 물리 검사에 반영한다.
        Physics2D.SyncTransforms();

        // 이미 손 안에 들어온 플레이어도 놓치지 않는다.
        _overlaps.Clear();
        _collider.Overlap(_filter, _overlaps);

        for (int i = 0; i < _overlaps.Count; i++)
        {
            PlayerController candidate =
                _overlaps[i].GetComponentInParent<PlayerController>();

            if (CanGrab(candidate))
            {
                player = candidate;
                return true;
            }
        }

        float distance = movement.magnitude;

        if (distance <= 0f)
        {
            return false;
        }

        // 손 중심의 선이 아니라 콜라이더 전체 크기로 경로를 검사한다.
        _castHits.Clear();
        _collider.Cast(
            movement / distance, _filter, _castHits, distance);

        // 여러 결과 중 가장 먼저 접촉하는 플레이어를 선택한다.
        float nearestDistance = float.PositiveInfinity;

        for (int i = 0; i < _castHits.Count; i++)
        {
            RaycastHit2D hit = _castHits[i];
            PlayerController candidate =
                hit.collider.GetComponentInParent<PlayerController>();

            if (!CanGrab(candidate) || hit.distance >= nearestDistance)
            {
                continue;
            }

            player = candidate;
            nearestDistance = hit.distance;
        }

        if (player == null)
        {
            return false;
        }

        hitDistance = nearestDistance;
        return true;
    }

    // 사망·비활성화·다른 강제 이동 상태에서는 잡지 않는다.
    private static bool CanGrab(PlayerController player)
    {
        return player != null &&
            player.isActiveAndEnabled &&
            player.CanControl;
    }

    private void OnDisable()
    {
        // 패턴 중간에 비활성화돼도 잡기 영역이 남지 않게 한다.
        if (_collider != null)
        {
            _collider.enabled = false;
        }
    }
}
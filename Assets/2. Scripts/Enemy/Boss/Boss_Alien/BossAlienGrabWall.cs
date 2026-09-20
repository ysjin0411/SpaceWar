using System;
using UnityEngine;

// 왼쪽 벽의 던지기 도착 위치와 충돌 알림을 제공한다.
public sealed class BossAlienGrabWall : MonoBehaviour
{
    // 벽에 도착할 플레이어 중심의 최소·최대 높이다.
    // 현재 플레이어 이동 영역과 맞춘다.
    [SerializeField] private float _minY = -4f;
    [SerializeField] private float _maxY = 4f;

    // 던지기가 벽에 도착하면 보스와 충돌 위치를 전달한다.
    // 이후 균열 생성과 카메라 흔들림이 이 알림을 사용한다.
    public event Action<Boss_Alien, Vector3> ImpactOccurred;

    private void Awake()
    {
        // 잘못된 범위로 도착 위치가 계산되는 것을 막는다.
        if (_minY > _maxY)
        {
            throw new InvalidOperationException(
                $"{name}: Min Y는 Max Y보다 클 수 없습니다.");
        }
    }

    // 던지기 시작 높이를 유지하되 플레이어 이동 영역 안으로 제한한다.
    public Vector3 GetLandingPosition(Vector3 playerPosition)
    {
        return new Vector3(
            transform.position.x,
            Mathf.Clamp(playerPosition.y, _minY, _maxY),
            playerPosition.z);
    }

    // 패턴이 벽 도착을 확인한 순간 한 번 호출한다.
    public void NotifyImpact(Boss_Alien boss, Vector3 impactPosition)
    {
        if (!isActiveAndEnabled || boss == null || boss.IsDead)
        {
            return;
        }

        if (ImpactOccurred != null)
        {
            ImpactOccurred.Invoke(boss, impactPosition);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Scene 창에서 선택하면 도착 기준선을 표시한다.
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(
            new Vector3(transform.position.x, _minY, transform.position.z),
            new Vector3(transform.position.x, _maxY, transform.position.z));
    }
}
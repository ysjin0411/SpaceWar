using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 플레이어를 바라본 뒤 살짝 후퇴하고 전방으로 돌진하는 보스 패턴이다.
/// </summary>
[CreateAssetMenu(
    fileName = "BossChargePattern",
    menuName = "BossPattern/Charge")]
public sealed class BossChargePattern : BossPattern
{
    [Header("조준 설정")]

    [Min(0f)]
    [SerializeField] float _aimDuration = 0.5f; // 플레이어를 바라본 뒤 대기하는 시간

    [SerializeField] float _visualAngleOffset; // 원본 이미지의 정면 방향을 보정하는 Z축 각도

    [Header("후퇴 설정")]

    [Min(0f)]
    [SerializeField] float _backstepDistance = 0.5f; // 돌진 전에 반대 방향으로 물러나는 거리

    [Min(0.01f)]
    [SerializeField] float _backstepDuration = 0.2f; // 후퇴 이동에 걸리는 시간

    [Header("돌진 설정")]

    [Min(0f)]
    [SerializeField] float _chargeDistance = 8f; // 조준한 방향으로 돌진하는 거리

    [Min(0f)]
    [SerializeField] float _passThroughDistance = 2f; // 조준한 플레이어 위치를 지나 추가로 이동할 거리

    [Min(0.01f)]
    [SerializeField] float _chargeDuration = 0.35f; // 돌진 이동에 걸리는 시간

    [Header("패턴 종료 설정")]

    [Min(0f)]
    [SerializeField] float _recoveryDuration = 0.5f; // 돌진 후 다음 행동까지 대기하는 시간

    [Header("스폰 위치 복귀 설정")]

    [Min(0.01f)]
    [SerializeField] float _returnDuration = 0.3f; // 박치기 종료 위치에서 원래 스폰 위치까지 복귀하는 시간

    /// <summary>
    /// 플레이어 방향을 고정한 후 후퇴와 돌진을 순서대로 실행한다.
    /// </summary>
    public override IEnumerator Execute(
        BossPatternContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(
                nameof(context));
        }

        // 패턴 종료 후 원래 외형 회전으로 되돌리기 위해 저장한다.
        Quaternion originalLocalRotation =
            context.VisualTransform.localRotation;

        // 플레이어의 Vector3 위치에서 XY 좌표만 가져온다.
        Vector2 playerPosition =
            (Vector2)context.TargetPlayer.transform.position;

        // 보스 위치에서 플레이어를 향하는 방향을 계산한다.
        Vector2 chargeDirection =
            (playerPosition -
             context.Rigidbody.position).normalized;

        // 보스와 플레이어 위치가 완전히 같으면 오른쪽을 기본 방향으로 사용한다.
        if (chargeDirection.sqrMagnitude <= 0f)
        {
            chargeDirection = Vector2.right;
        }

        // 방향 벡터를 Z축 각도로 변환한다.
        float targetAngle =
            Mathf.Atan2(
                chargeDirection.y,
                chargeDirection.x) *
            Mathf.Rad2Deg +
            _visualAngleOffset;

        // Rigidbody 루트는 고정하고 외형 자식만 플레이어 방향으로 회전한다.
        context.VisualTransform.rotation =
            Quaternion.Euler(
                0f,
                0f,
                targetAngle);

        // 플레이어에게 돌진 방향을 보여주는 조준 시간을 가진다.
        if (_aimDuration > 0f)
        {
            yield return new WaitForSeconds(
                _aimDuration);
        }

        // 조준한 방향의 반대로 살짝 물러난다.
        yield return MoveByDistance(
            context.Rigidbody,
            -chargeDirection,
            _backstepDistance,
            _backstepDuration);

        // 후퇴가 끝난 현재 위치에서 조준 당시 플레이어 위치까지의 거리를 계산한다.
        float distanceToPlayer =
            Vector2.Distance(
                context.Rigidbody.position,
                playerPosition);

        // 최소 돌진 거리와 플레이어를 완전히 통과하는 거리 중 큰 값을 사용한다.
        float actualChargeDistance =
            Mathf.Max(
                _chargeDistance,
                distanceToPlayer + _passThroughDistance);

        // 조준 당시 플레이어 위치를 지나도록 빠르게 돌진한다.
        yield return MoveByDistance(
            context.Rigidbody,
            chargeDirection,
            actualChargeDistance,
            _chargeDuration);

        // 돌진 직후 바로 다음 행동으로 넘어가지 않도록 후딜레이를 적용한다.
        if (_recoveryDuration > 0f)
        {
            yield return new WaitForSeconds(
                _recoveryDuration);
        }

        // 현재 위치에서 보스의 원래 스폰 위치로 향하는 차이를 계산한다.
        Vector2 returnOffset =
            context.Boss.MovementCenter -
            context.Rigidbody.position;

        // 원래 스폰 위치로 돌아갈 방향을 계산한다.
        Vector2 returnDirection =
            returnOffset.normalized;

        // 현재 위치에서 원래 스폰 위치까지의 거리를 계산한다.
        float returnDistance =
            returnOffset.magnitude;

        // 박치기 종료 위치에서 원래 스폰 위치로 빠르게 복귀한다.
        yield return MoveByDistance(
            context.Rigidbody,
            returnDirection,
            returnDistance,
            _returnDuration);

        // 스폰 위치에 정확히 도착하도록 마지막 위치를 보정한다.
        context.Rigidbody.MovePosition(
            context.Boss.MovementCenter);

        // 패턴 시작 전 외형 회전 상태로 되돌린다.
        context.VisualTransform.localRotation =
            originalLocalRotation;

        // 패턴 시작 전 외형 회전 상태로 되돌린다.
        context.VisualTransform.localRotation =
            originalLocalRotation;
    }

    /// <summary>
    /// Rigidbody2D를 지정된 방향과 거리만큼 일정 시간 동안 이동시킨다.
    /// </summary>
    private IEnumerator MoveByDistance(
        Rigidbody2D rigidbody,
        Vector2 direction,
        float distance,
        float duration)
    {
        // 이동 거리가 없다면 별도의 이동을 처리하지 않는다.
        if (distance <= 0f)
        {
            yield break;
        }

        Vector2 startPosition =
            rigidbody.position;

        Vector2 targetPosition =
            startPosition +
            direction * distance;

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.fixedDeltaTime;

            float moveRatio =
                Mathf.Clamp01(
                    elapsedTime / duration);

            Vector2 nextPosition =
                Vector2.Lerp(
                    startPosition,
                    targetPosition,
                    moveRatio);

            // 물리 프레임에 맞춰 보스의 위치를 갱신한다.
            rigidbody.MovePosition(
                nextPosition);

            yield return new WaitForFixedUpdate();
        }

        // 프레임 오차가 발생해도 마지막 위치에 정확히 도착시킨다.
        rigidbody.MovePosition(
            targetPosition);
    }
}
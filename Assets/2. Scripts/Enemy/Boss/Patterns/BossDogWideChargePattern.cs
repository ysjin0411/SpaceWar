using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Boss Dog가 거대화한 뒤 화면을 가로질러 돌진하는 체력 특수 패턴이다.
/// 시스템 경고 메시지가 끝난 후 실제 패턴 행동을 시작한다.
/// </summary>
[CreateAssetMenu(
    fileName = "BossDogWideChargePattern",
    menuName = "BossPattern/Dog/Wide Charge")]
public sealed class BossDogWideChargePattern : BossPattern
{
    [Header("시스템 경고")]

    // 패턴 시작 시 시스템 다이얼로그에 출력할 메시지다.
    [TextArea]
    [SerializeField]
    string _systemMessage;

    [Header("스폰 위치 이동")]

    // 시스템 경고가 끝난 뒤 스폰 위치로 이동하는 데 걸리는 시간이다.
    [Min(0.01f)]
    [SerializeField]
    float _moveToSpawnDuration = 0.4f;

    [Header("보스 거대화")]

    // 원래 크기에 곱할 거대화 배율이다.
    [Min(1f)]
    [SerializeField]
    float _enlargedScaleMultiplier = 2f;

    // 보스가 거대해지는 데 걸리는 시간이다.
    [Min(0.01f)]
    [SerializeField]
    float _enlargeDuration = 0.3f;

    // 패턴 종료 후 원래 크기로 돌아오는 데 걸리는 시간이다.
    [Min(0.01f)]
    [SerializeField]
    float _restoreScaleDuration = 0.25f;

    [Header("돌진 시작 위치")]

    // BossSpawner 위치를 기준으로 한 오른쪽 위 돌진 시작 Offset이다.
    [SerializeField] Vector2 _upperChargeStartOffset = new Vector2(7f, 3f);

    // BossSpawner 위치를 기준으로 한 오른쪽 중앙 돌진 시작 Offset이다.
    [SerializeField] Vector2 _middleChargeStartOffset = new Vector2(7f, 0f);

    // BossSpawner 위치를 기준으로 한 오른쪽 아래 돌진 시작 Offset이다.
    [SerializeField] Vector2 _lowerChargeStartOffset = new Vector2(7f, -3f);

    // 선택된 오른쪽 돌진 시작 위치까지 이동하는 시간이다.
    [Min(0.01f)][SerializeField] float _moveToChargeStartDuration = 0.4f;

    [Header("돌진 범위 경고")]

    // 돌진 범위를 표시할 1×1 사각형 SpriteRenderer 프리팹이다.
    [SerializeField]
    SpriteRenderer _chargeWarningPrefab;

    // 오른쪽 시작 위치에서 왼쪽으로 돌진할 거리다.
    [Min(0.1f)]
    [SerializeField]
    float _chargeDistance = 16f;

    // 화면에 표시할 돌진 위험 범위의 높이다.
    [Min(0.1f)]
    [SerializeField]
    float _chargeWarningHeight = 3f;

    // 돌진 위험 범위에 적용할 색상과 최대 투명도다.
    [SerializeField]
    Color _chargeWarningColor =
        new Color(1f, 0f, 0f, 0.45f);

    // 한 번 나타나거나 사라지는 데 걸리는 시간이다.
    [Min(0.01f)]
    [SerializeField]
    float _chargeWarningBlinkDuration = 0.15f;

    // 돌진 위험 범위를 점멸할 횟수다.
    [Min(1)]
    [SerializeField]
    int _chargeWarningBlinkCount = 4;

    [Header("광범위 돌진")]

    // 선택된 오른쪽 위치에서 화면 왼쪽까지 돌진하는 시간이다.
    [Min(0.01f)]
    [SerializeField]
    float _chargeDuration = 0.3f;

    [Header("패턴 종료 복귀")]

    // 돌진이 끝난 위치에서 BossSpawner 위치로 빠르게 복귀하는 시간이다.
    [Min(0.01f)]
    [SerializeField]
    float _returnToSpawnDuration = 0.35f;


    /// <summary>
    /// 시스템 경고 메시지를 출력하고,
    /// 점멸 완료 이벤트를 받을 때까지 패턴 진행을 멈춘다.
    /// </summary>
    public override IEnumerator Execute(
        BossPatternContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(
                nameof(context));
        }

        // 이 패턴이 다른 종류의 보스에 잘못 장착되는 것을 방지한다.
        if (!(context.Boss is Boss_Dog))
        {
            throw new InvalidOperationException(
                $"{name} 패턴은 Boss_Dog만 사용할 수 있습니다.");
        }

        // Boss Dog의 실제 충돌 판정에 사용하는 BoxCollider2D를 가져온다.
        BoxCollider2D bossCollider =
            context.Boss.GetComponent<BoxCollider2D>();

        if (bossCollider == null)
        {
            throw new MissingComponentException(
                $"{context.Boss.name}에 BoxCollider2D가 없습니다.");
        }

        // 패턴 종료 후 복원하기 위해 Collider의 원래 값을 저장한다.
        Vector2 originalColliderSize =
            bossCollider.size;

        Vector2 originalColliderOffset =
            bossCollider.offset;

        // 패턴 종료 시 정확한 크기로 돌아가기 위해
        // 현재 보스 이미지의 원래 크기를 저장한다.
        Vector3 originalVisualScale =
            context.VisualTransform.localScale;

        bool isDialogueCompleted = false;

        // UI가 점멸 완료를 알려주면 대기 상태를 끝낸다.
        Action handleDialogueCompleted =
            () => isDialogueCompleted = true;

        // 메시지를 요청하기 전에 먼저 완료 이벤트를 구독해야
        // 빠르게 완료되더라도 이벤트를 놓치지 않는다.
        context.Boss.SystemDialogueCompleted +=
            handleDialogueCompleted;

        try
        {
            // 생성된 Boss_Dog가 시스템 다이얼로그에 메시지를 전달한다.
            context.Boss.RequestSystemDialogue(
                _systemMessage);

            // 시스템 메시지 점멸이 끝날 때까지 기다린다.
            // 다른 체력 이벤트나 패턴 요청이 들어와도 현재 패턴은 유지된다.
            yield return new WaitUntil(() =>
                isDialogueCompleted ||
                context.Boss.IsDead);
        }
        finally
        {
            // 패턴이 정상 종료되거나 중단되더라도 이벤트 구독을 해제한다.
            context.Boss.SystemDialogueCompleted -=
                handleDialogueCompleted;
        }

        // 시스템 메시지 도중 보스가 죽었다면 다음 행동을 실행하지 않는다.
        if (context.Boss.IsDead)
        {
            yield break;
        }

        // 랜덤 이동 중이던 위치에서 최초 보스 스폰 위치로 이동한다.
        yield return MoveToPosition(
            context.Rigidbody,
            context.Boss.MovementCenter,
            _moveToSpawnDuration);

        // 이동 도중 보스가 죽었다면 다음 행동을 실행하지 않는다.
        if (context.Boss.IsDead)
        {
            yield break;
        }





        // 다음 단계의 위험 범위 점멸과 실제 돌진 코드는
        // 반드시 이 위치와 원래 크기 복원 코드 사이에 추가한다.

        // X와 Y만 확대하고 2D에서 사용하지 않는 Z 크기는 유지한다.
        Vector3 enlargedVisualScale =
            new Vector3(
                originalVisualScale.x * _enlargedScaleMultiplier,
                originalVisualScale.y * _enlargedScaleMultiplier,
                originalVisualScale.z);

        // 외형과 동일한 비율로 확대할 Collider 크기를 계산한다.
        Vector2 enlargedColliderSize =
            originalColliderSize *
            _enlargedScaleMultiplier;

        Vector2 enlargedColliderOffset =
            originalColliderOffset *
            _enlargedScaleMultiplier;

        // 보스 외형과 실제 충돌 범위를 함께 부드럽게 확대한다.
        yield return ScaleBossSize(
            context.VisualTransform,
            enlargedVisualScale,
            bossCollider,
            enlargedColliderSize,
            enlargedColliderOffset,
            _enlargeDuration);

        if (context.Boss.IsDead)
        {
            yield break;
        }

        // 오른쪽 위, 중앙, 아래 중 하나를 같은 확률로 선택한다.
        Vector2 chargeStartPosition =
            SelectChargeStartPosition(
                context.Boss.MovementCenter);

        // 거대화 상태로 선택된 돌진 시작 위치까지 이동한다.
        yield return MoveToPosition(
            context.Rigidbody,
            chargeStartPosition,
            _moveToChargeStartDuration);

        if (context.Boss.IsDead)
        {
            yield break;
        }

        // 현재 돌진 시작 위치에서 왼쪽으로 실제 돌진할 목적지를 계산한다.
        Vector2 chargeEndPosition =
            chargeStartPosition +
            Vector2.left * _chargeDistance;

        // 실제 돌진 경로와 동일한 위치에 위험 범위를 생성한다.
        SpriteRenderer chargeWarning =
            CreateChargeWarning(
                chargeStartPosition,
                chargeEndPosition);

        try
        {
            // 빨간 돌진 위험 범위를 설정된 횟수만큼 점멸시킨다.
            yield return BlinkChargeWarning(
                chargeWarning);
        }
        finally
        {
            // 패턴이 중단되어도 위험 범위가 남지 않게 제거한다.
            if (chargeWarning != null)
            {
                Destroy(
                    chargeWarning.gameObject);
            }
        }

        if (context.Boss.IsDead)
        {
            yield break;
        }

        // 위험 범위에 표시했던 것과 동일한 목적지까지 빠르게 돌진한다.
        yield return MoveToPosition(
            context.Rigidbody,
            chargeEndPosition,
            _chargeDuration);

        if (context.Boss.IsDead)
        {
            yield break;
        }


        // 돌진이 끝나면 거대화 상태를 유지한 채
        // 최초 BossSpawner 위치로 빠르게 복귀한다.
        yield return MoveToPosition(
            context.Rigidbody,
            context.Boss.MovementCenter,
            _returnToSpawnDuration);

        if (context.Boss.IsDead)
        {
            yield break;
        }


        // BossSpawner 위치에 도착한 후
        // 외형과 실제 충돌 범위를 원래 크기로 함께 되돌린다.
        yield return ScaleBossSize(
            context.VisualTransform,
            originalVisualScale,
            bossCollider,
            originalColliderSize,
            originalColliderOffset,
            _restoreScaleDuration);
    }

    /// <summary>
    /// Rigidbody2D를 현재 위치에서 지정된 목표 위치까지 일정 시간 동안 이동시킨다.
    /// 특수 패턴 시작과 종료 시 스폰 위치로 돌아갈 때 함께 사용한다.
    /// </summary>
    private IEnumerator MoveToPosition(
        Rigidbody2D rigidbody,
        Vector2 targetPosition,
        float duration)
    {
        Vector2 startPosition =
            rigidbody.position;

        // 이미 목표 위치에 도착해 있다면 불필요하게 기다리지 않는다.
        if ((targetPosition - startPosition).sqrMagnitude
            <= 0.0001f)
        {
            rigidbody.MovePosition(
                targetPosition);

            yield break;
        }

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

            // 물리 프레임마다 보스 위치를 갱신한다.
            rigidbody.MovePosition(
                nextPosition);

            yield return new WaitForFixedUpdate();
        }

        // 마지막 위치가 오차 없이 목표 위치와 일치하도록 보정한다.
        rigidbody.MovePosition(
            targetPosition);
    }

    /// <summary>
    /// 보스 외형과 BoxCollider2D를 같은 시간 동안 함께 확대하거나 축소한다.
    /// 외형과 실제 충돌 범위가 서로 다른 크기로 보이는 것을 방지한다.
    /// </summary>
    private IEnumerator ScaleBossSize(
        Transform visualTransform,
        Vector3 targetVisualScale,
        BoxCollider2D bossCollider,
        Vector2 targetColliderSize,
        Vector2 targetColliderOffset,
        float duration)
    {
        Vector3 startVisualScale =
            visualTransform.localScale;

        Vector2 startColliderSize =
            bossCollider.size;

        Vector2 startColliderOffset =
            bossCollider.offset;

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.fixedDeltaTime;

            float scaleRatio =
                Mathf.Clamp01(
                    elapsedTime / duration);

            // 보스 이미지를 목표 크기까지 변경한다.
            visualTransform.localScale =
                Vector3.Lerp(
                    startVisualScale,
                    targetVisualScale,
                    scaleRatio);

            // 실제 충돌 판정도 동일한 비율로 변경한다.
            bossCollider.size =
                Vector2.Lerp(
                    startColliderSize,
                    targetColliderSize,
                    scaleRatio);

            bossCollider.offset =
                Vector2.Lerp(
                    startColliderOffset,
                    targetColliderOffset,
                    scaleRatio);

            // Collider 변경을 물리 프레임과 맞춘다.
            yield return new WaitForFixedUpdate();
        }

        // 마지막 프레임의 오차가 남지 않도록 정확한 값으로 보정한다.
        visualTransform.localScale =
            targetVisualScale;

        bossCollider.size =
            targetColliderSize;

        bossCollider.offset =
            targetColliderOffset;
    }

    /// <summary>
    /// BossSpawner 위치를 기준으로 오른쪽 위, 중앙, 아래 중
    /// 하나의 돌진 시작 위치를 같은 확률로 선택한다.
    /// </summary>
    private Vector2 SelectChargeStartPosition(
        Vector2 spawnPosition)
    {
        // Random.Range의 int 최댓값은 포함되지 않으므로
        // 0, 1, 2 중 하나가 각각 같은 확률로 선택된다.
        int selectedPositionIndex =
            UnityEngine.Random.Range(0, 3);

        Vector2 selectedOffset;

        switch (selectedPositionIndex)
        {
            case 0:

                // 오른쪽 위 위치를 선택한다.
                selectedOffset =
                    _upperChargeStartOffset;
                break;

            case 1:

                // 오른쪽 중앙 위치를 선택한다.
                selectedOffset =
                    _middleChargeStartOffset;
                break;

            default:

                // 오른쪽 아래 위치를 선택한다.
                selectedOffset =
                    _lowerChargeStartOffset;
                break;
        }

        // 절대 좌표가 아니라 실제 보스 스폰 위치에
        // 선택된 Offset을 더해서 최종 위치를 계산한다.
        return spawnPosition +
               selectedOffset;
    }

    /// <summary>
    /// 실제 돌진 시작점과 끝점을 사용하여
    /// 직사각형 모양의 빨간 위험 범위를 생성한다.
    /// </summary>
    private SpriteRenderer CreateChargeWarning(
        Vector2 startPosition,
        Vector2 endPosition)
    {
        if (_chargeWarningPrefab == null)
        {
            throw new InvalidOperationException(
                $"{name}에 돌진 범위 경고 프리팹이 설정되지 않았습니다.");
        }

        if (_chargeWarningPrefab.sprite == null)
        {
            throw new InvalidOperationException(
                $"{name}의 돌진 범위 경고 프리팹에 Sprite가 없습니다.");
        }

        SpriteRenderer warningRenderer =
            Instantiate(_chargeWarningPrefab);

        // 돌진 시작점과 끝점의 정중앙에 위험 범위를 배치한다.
        warningRenderer.transform.position =
            (startPosition + endPosition) * 0.5f;

        Vector2 sourceSpriteSize =
            warningRenderer.sprite.bounds.size;

        if (sourceSpriteSize.x <= 0f ||
            sourceSpriteSize.y <= 0f)
        {
            throw new InvalidOperationException(
                $"{name}의 돌진 범위 경고 Sprite 크기가 올바르지 않습니다.");
        }

        float warningWidth =
            Vector2.Distance(
                startPosition,
                endPosition);

        // 1×1 사각형 Sprite를 실제 돌진 거리와 설정된 높이에 맞게 늘린다.
        warningRenderer.transform.localScale =
            new Vector3(
                warningWidth / sourceSpriteSize.x,
                _chargeWarningHeight / sourceSpriteSize.y,
                1f);

        // 처음에는 완전히 투명하게 만든다.
        Color hiddenColor =
            _chargeWarningColor;

        hiddenColor.a = 0f;
        warningRenderer.color = hiddenColor;

        return warningRenderer;
    }

    /// <summary>
    /// 돌진 위험 범위를 붉게 나타냈다가 숨기는 동작을 반복한다.
    /// </summary>
    private IEnumerator BlinkChargeWarning(
        SpriteRenderer warningRenderer)
    {
        for (int i = 0;
             i < _chargeWarningBlinkCount;
             i++)
        {
            // 위험 범위를 서서히 표시한다.
            yield return FadeWarningAlpha(
                warningRenderer,
                _chargeWarningColor.a,
                _chargeWarningBlinkDuration);

            // 위험 범위를 다시 서서히 숨긴다.
            yield return FadeWarningAlpha(
                warningRenderer,
                0f,
                _chargeWarningBlinkDuration);
        }
    }

    /// <summary>
    /// 위험 범위 SpriteRenderer의 알파값을 부드럽게 변경한다.
    /// </summary>
    private IEnumerator FadeWarningAlpha(
        SpriteRenderer warningRenderer,
        float targetAlpha,
        float duration)
    {
        float startAlpha =
            warningRenderer.color.a;

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;

            float fadeRatio =
                Mathf.Clamp01(
                    elapsedTime / duration);

            Color currentColor =
                _chargeWarningColor;

            currentColor.a =
                Mathf.Lerp(
                    startAlpha,
                    targetAlpha,
                    fadeRatio);

            warningRenderer.color =
                currentColor;

            yield return null;
        }

        Color finalColor =
            _chargeWarningColor;

        finalColor.a = targetAlpha;
        warningRenderer.color = finalColor;
    }
}
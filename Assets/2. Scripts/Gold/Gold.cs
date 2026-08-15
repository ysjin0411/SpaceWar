using System;
using DG.Tweening;
using UnityEngine;

public class Gold : MonoBehaviour
{
    [Header("골드 데이터")]
    [Tooltip("이 골드 한 개가 지급하는 수량")]
    [Min(1)]
    [SerializeField] int _goldAmount = 10;

    [Header("퍼지는 연출")]
    [Tooltip("적 사망 위치에서 바깥으로 퍼지는 시간")]
    [Min(0.05f)]
    [SerializeField] float _spreadDuration = 0.25f;

    [Tooltip("퍼진 위치에서 잠시 머무는 시간")]
    [Min(0f)]
    [SerializeField] float _spreadWaitDuration = 0.1f;

    [Header("획득 연출")]
    [Tooltip("퍼진 위치에서 골드 UI까지 이동하는 속도")]
    [Min(0.1f)]
    [SerializeField] float _collectSpeed = 1000f;

    RectTransform _goldTarget;
    GoldUI _goldUI;

    /// <summary>
    /// 생성된 골드를 원 바깥쪽으로 퍼뜨린 뒤
    /// 골드 UI 목적지로 이동시킨다.
    /// </summary>
    public void Initialize(
        Vector3 spreadPosition,
        RectTransform goldTarget,
        GoldUI goldUI)
    {
        if (goldTarget == null)
        {
            throw new ArgumentNullException(nameof(goldTarget));
        }

        if (goldUI == null)
        {
            throw new ArgumentNullException(nameof(goldUI));
        }

        _goldTarget = goldTarget;
        _goldUI = goldUI;

        PlayCollectAnimation(spreadPosition);
    }

    /// <summary>
    /// 모든 골드가 동시에 원형으로 퍼진 뒤,
    /// 같은 속도로 골드 UI를 향해 이동한다.
    /// 목표까지의 거리가 다르므로 도착 시간도 각각 달라진다.
    /// </summary>
    private void PlayCollectAnimation(Vector3 spreadPosition)
    {
        // 생성 순간에는 작은 크기로 시작한다.
        transform.localScale = Vector3.zero;

        // 원형으로 퍼진 위치에서 골드 UI까지의 거리를 계산한다.
        float collectDistance = Vector3.Distance(
            spreadPosition,
            _goldTarget.position);

        // 이동시간 = 이동거리 ÷ 이동속도
        // 거리가 가까운 골드는 이동시간이 짧고,
        // 거리가 먼 골드는 이동시간이 길어진다.
        float collectDuration =
            collectDistance / _collectSpeed;

        Sequence sequence = DOTween.Sequence();

        // 골드가 제거되면 실행 중인 Tween도 함께 제거한다.
        sequence.SetLink(gameObject);

        // 모든 골드가 동시에 각자의 원형 위치로 퍼진다.
        sequence.Append(
            transform.DOMove(
                    spreadPosition,
                    _spreadDuration)
                .SetEase(Ease.OutQuad));

        // 퍼지는 동안 원래 크기로 커진다.
        sequence.Join(
            transform.DOScale(
                    Vector3.one,
                    _spreadDuration)
                .SetEase(Ease.OutBack));

        // 원형으로 퍼진 상태를 잠시 보여준다.
        sequence.AppendInterval(_spreadWaitDuration);

        // 모든 골드가 동시에 출발하지만 같은 속도로 이동한다.
        // 각 골드의 거리가 다르므로 도착 시간은 서로 달라진다.
        sequence.Append(
            transform.DOMove(
                    _goldTarget.position,
                    collectDuration)
                .SetEase(Ease.Linear));

        // 각자의 이동시간에 맞춰 크기도 줄어든다.
        sequence.Join(
            transform.DOScale(
                Vector3.one * 0.6f,
                collectDuration));

        // 먼저 도착한 골드부터 순서대로 지급한다.
        sequence.OnComplete(CollectAndDestroy);
    }

    private void CollectAndDestroy()
    {
        _goldUI.CollectGold(_goldAmount);

        Destroy(gameObject);
    }
}
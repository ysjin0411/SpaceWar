using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

// 모든 재화 프리팹이 공유하는 금액과 이동·회수 연출이다.
public abstract class Money : MonoBehaviour
{
    // 재화 프리팹 한 개를 회수했을 때 지급할 금액이다.
    // 기존 Gold의 금액 설정을 이어받는다.
    [FormerlySerializedAs("_goldAmount")]
    [SerializeField, Min(1)] private int _amount = 10;

    // 생성 위치에서 주변으로 퍼지는 시간이다.
    [SerializeField, Min(0.05f)]
    private float _spreadDuration = 0.25f;

    // 퍼진 위치에서 잠시 머무르는 시간이다.
    [SerializeField, Min(0f)]
    private float _spreadWaitDuration = 0.1f;

    // 도착 UI로 이동하는 속도다.
    [SerializeField, Min(0.1f)]
    private float _collectSpeed = 1000f;

    // 지급과 UI 표시를 담당하는 해당 재화의 수집 UI다.
    private MoneyUI _moneyUI;

    // 이 재화 한 개의 회수가 끝났을 때 실행할 함수다.
    private Action _onCollected;

    // 같은 재화를 중복 지급하지 않도록 관리한다.
    private bool _isCollected;

    // 자식 클래스가 자신이 어떤 재화인지 지정한다.
    public abstract MoneyType Type { get; }

    // 드롭 설정 검증에서 한 개의 지급 금액을 확인한다.
    public int Amount => _amount;

    // 생성된 재화에 목적지와 회수 완료 함수를 전달한다.
    public void Initialize(
        Vector3 spreadPosition,
        RectTransform target,
        MoneyUI moneyUI,
        Action onCollected)
    {
        if (target == null || moneyUI == null)
        {
            throw new ArgumentException("재화의 도착 UI가 없습니다.");
        }

        if (moneyUI.Type != Type)
        {
            throw new InvalidOperationException(
                $"{name}: 재화 종류와 수집 UI의 종류가 다릅니다.");
        }

        if (_amount <= 0)
        {
            throw new InvalidOperationException(
                $"{name}: 재화 금액은 1 이상이어야 합니다.");
        }

        _moneyUI = moneyUI;
        _onCollected = onCollected;
        _isCollected = false;

        PlayCollectAnimation(spreadPosition, target);
    }

    // 모든 재화가 동일한 퍼짐·대기·회수 연출을 사용한다.
    private void PlayCollectAnimation(
        Vector3 spreadPosition,
        RectTransform target)
    {
        transform.localScale = Vector3.zero;

        float distance = Vector3.Distance(
            spreadPosition, target.position);

        float collectDuration = distance / _collectSpeed;

        Sequence sequence = DOTween.Sequence();
        sequence.SetLink(gameObject);

        // 생성 위치에서 바깥쪽으로 퍼진다.
        sequence.Append(
            transform.DOMove(spreadPosition, _spreadDuration)
                .SetEase(Ease.OutQuad));

        sequence.Join(
            transform.DOScale(Vector3.one, _spreadDuration)
                .SetEase(Ease.OutBack));

        sequence.AppendInterval(_spreadWaitDuration);

        // 재화별로 전달받은 UI 위치로 이동한다.
        sequence.Append(
            transform.DOMove(target.position, collectDuration)
                .SetEase(Ease.Linear));

        sequence.Join(
            transform.DOScale(Vector3.one * 0.6f, collectDuration)
                .SetEase(Ease.Linear));

        sequence.OnComplete(CollectAndDestroy);
    }

    // 도착한 재화를 지급한 뒤 회수 완료를 알린다.
    private void CollectAndDestroy()
    {
        if (_isCollected)
        {
            return;
        }

        _isCollected = true;

        // 실제 지급 방식은 해당 재화 UI의 구현을 사용한다.
        _moneyUI.CollectMoney(_amount);

        Action completed = _onCollected;
        _onCollected = null;

        Destroy(gameObject);

        if (completed != null)
        {
            completed.Invoke();
        }
    }
}
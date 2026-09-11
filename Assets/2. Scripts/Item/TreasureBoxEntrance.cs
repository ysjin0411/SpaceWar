using System;
using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(TreasureBox))]
public class TreasureBoxEntrance : MonoBehaviour
{
    // 착지 지점으로부터 위쪽으로 떨어진 시작 높이다.
    // 월드 좌표 단위이며 화면 크기에 맞춰 조절한다.
    [SerializeField, Min(0.1f)]
    float _dropHeight = 8f;

    // 시작 위치에서 착지 지점까지 떨어지는 시간이다.
    [SerializeField, Min(0.01f)]
    float _dropDuration = 0.6f;

    // 첫 번째 반동으로 튀어 오르는 높이다.
    [SerializeField, Min(0.01f)]
    float _firstBounceHeight = 0.6f;

    // 첫 번째 반동이 올라갔다 내려오는 전체 시간이다.
    [SerializeField, Min(0.01f)]
    float _firstBounceDuration = 0.3f;

    // 두 번째 반동의 높이다. 첫 번째보다 작게 설정한다.
    [SerializeField, Min(0.01f)]
    float _secondBounceHeight = 0.25f;

    // 두 번째 반동이 올라갔다 내려오는 전체 시간이다.
    [SerializeField, Min(0.01f)]
    float _secondBounceDuration = 0.2f;

    // 등장 연출이 끝나면 피격 시작을 요청할 상자다.
    TreasureBox _treasureBox;

    // 진행 중인 낙하 및 반동 시퀀스다.
    Sequence _entranceSequence;

    // 같은 상자에서 등장 연출을 중복 실행하지 않게 한다.
    bool _hasStarted;

    private void Awake()
    {
        // 같은 오브젝트에 붙은 상자 로직을 가져온다.
        _treasureBox = GetComponent<TreasureBox>();
    }

    // 생성 담당 코드가 씬의 TreasureBoxPoint 위치를 전달한다.
    public void Play(Vector3 landingPosition)
    {
        if (_hasStarted)
        {
            throw new InvalidOperationException(
                $"{name}: 등장 연출은 한 번만 실행할 수 있습니다.");
        }

        // 외형 생성과 최종 등급 추첨을 먼저 완료한다.
        // 상자는 Waiting 상태이므로 아직 공격받지 않는다.
        _treasureBox.InitializeRandom();
        _hasStarted = true;

        // 착지 지점 바로 위에서 시작한다.
        transform.position =
            landingPosition + Vector3.up * _dropHeight;

        _entranceSequence = DOTween.Sequence();

        // 상자가 파괴되면 연결된 연출도 종료한다.
        _entranceSequence.SetLink(gameObject);

        // 아래로 갈수록 빨라지는 낙하를 표현한다.
        _entranceSequence.Append(
            transform.DOMove(landingPosition, _dropDuration)
                .SetEase(Ease.InQuad));

        // 첫 번째 반동을 추가한다.
        AppendBounce(
            landingPosition,
            _firstBounceHeight,
            _firstBounceDuration);

        // 첫 번째보다 작은 두 번째 반동을 추가한다.
        AppendBounce(
            landingPosition,
            _secondBounceHeight,
            _secondBounceDuration);

        _entranceSequence.OnComplete(() =>
        {
            _entranceSequence = null;

            // 두 반동이 끝난 뒤 최종 위치를 정확히 맞춘다.
            transform.position = landingPosition;

            // 착지 완료 후에만 Collider를 켜고 피해 누적을 시작한다.
            _treasureBox.BeginReceivingDamage();
        });
    }

    // 상승과 하강을 묶어 한 번의 반동으로 추가한다.
    private void AppendBounce(
        Vector3 landingPosition,
        float height,
        float duration)
    {
        Vector3 peakPosition =
            landingPosition + Vector3.up * height;

        float halfDuration = duration * 0.5f;

        // 상승할수록 속도가 줄어들게 한다.
        _entranceSequence.Append(
            transform.DOMove(peakPosition, halfDuration)
                .SetEase(Ease.OutQuad));

        // 내려오면서 속도가 빨라지게 한다.
        _entranceSequence.Append(
            transform.DOMove(landingPosition, halfDuration)
                .SetEase(Ease.InQuad));
    }

    private void OnDestroy()
    {
        // 파괴된 Transform을 연출이 계속 참조하지 않도록 정리한다.
        _entranceSequence?.Kill();
        _entranceSequence = null;
    }
}
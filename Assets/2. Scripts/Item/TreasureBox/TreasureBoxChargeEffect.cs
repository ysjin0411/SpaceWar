using System;
using UnityEngine;

[RequireComponent(typeof(TreasureBox))]
public class TreasureBoxChargeEffect : MonoBehaviour
{
    // 등급별 외형이 생성되는 공통 VisualRoot다.
    // 상자 본체가 아닌 이 자식에만 크기와 회전 연출을 적용한다.
    [SerializeField]
    Transform _visualRoot;

    // Charge 중 원래 크기보다 최대 몇 배 더 커질지 결정한다.
    // 0.05면 원래 크기의 100~105% 사이에서 반복한다.
    [SerializeField, Range(0f, 0.2f)]
    float _scaleAmount = 0.05f;

    // 좌우로 기울어지는 최대 각도다.
    [SerializeField, Range(0f, 10f)]
    float _shakeAngle = 3f;

    // 커졌다 줄어들고 좌우로 흔들리는 한 주기의 시간이다.
    [SerializeField, Min(0.05f)]
    float _cycleSeconds = 0.3f;

    // Charge 진입과 종료를 알려주는 상자 본체다.
    TreasureBox _treasureBox;

    // 연출 종료 시 복구할 원래 크기다.
    Vector3 _originalScale;

    // 연출 종료 시 복구할 원래 회전이다.
    Quaternion _originalRotation;

    // 현재 Charge 연출을 재생 중인지 나타낸다.
    bool _isCharging;

    // 현재 반복 주기 안에서 흐른 시간이다.
    float _cycleTime;

    private void Awake()
    {
        // 본체를 직접 변형하지 않도록 자식 VisualRoot 연결을 확인한다.
        if (_visualRoot == null ||
            _visualRoot == transform ||
            !_visualRoot.IsChildOf(transform) ||
            _cycleSeconds <= 0f)
        {
            throw new InvalidOperationException(
                $"{name}: 자식 VisualRoot와 반복 시간을 확인하세요.");
        }

        _treasureBox = GetComponent<TreasureBox>();
        _originalScale = _visualRoot.localScale;
        _originalRotation = _visualRoot.localRotation;
    }

    private void OnEnable()
    {
        // 상태가 바뀔 때만 연출의 시작과 종료를 결정한다.
        _treasureBox.StateChanged += HandleStateChanged;

        // 이미 Charge인 상태에서 활성화되어도 현재 상태에 맞춰 재생한다.
        HandleStateChanged(_treasureBox.State);
    }

    // Charge에 들어가면 재생하고, 다른 상태가 되면 원래 외형으로 복구한다.
    private void HandleStateChanged(TreasureBoxState state)
    {
        _isCharging = state == TreasureBoxState.Charge;
        _cycleTime = 0f;

        RestoreVisual();
    }

    private void Update()
    {
        // Charge 외의 상태에서는 외형을 변경하지 않는다.
        if (!_isCharging)
        {
            return;
        }

        // 게임이 일시정지되면 Charge 연출도 함께 멈춘다.
        _cycleTime = Mathf.Repeat(
            _cycleTime + Time.deltaTime,
            _cycleSeconds);

        float phase = _cycleTime / _cycleSeconds * Mathf.PI * 2f;

        // 0에서 시작해 1까지 올라갔다 0으로 돌아오는 값을 만든다.
        float pulse = (1f - Mathf.Cos(phase)) * 0.5f;

        // 프리팹의 원래 크기를 기준으로 조금 커졌다 줄어든다.
        _visualRoot.localScale =
            _originalScale * (1f + pulse * _scaleAmount);

        // 원래 회전을 기준으로 좌우로 조금씩 기울인다.
        float angle = Mathf.Sin(phase) * _shakeAngle;

        _visualRoot.localRotation =
            _originalRotation * Quaternion.Euler(0f, 0f, angle);
    }

    // 승급, 개봉, 비활성화 시 연출로 변경한 값을 원래대로 돌린다.
    private void RestoreVisual()
    {
        _visualRoot.localScale = _originalScale;
        _visualRoot.localRotation = _originalRotation;
    }

    private void OnDisable()
    {
        // 비활성화된 컴포넌트가 상태 알림을 받지 않도록 해제한다.
        _treasureBox.StateChanged -= HandleStateChanged;
        _isCharging = false;

        RestoreVisual();
    }
}
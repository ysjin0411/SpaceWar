using System;
using UnityEngine;

// 건전지 아이콘과 필살기 준비 프레임의 표시를 담당한다.
public sealed class UltimateUI : MonoBehaviour
{
    // 왼쪽부터 순서대로 배치한 건전지 아이콘을 연결한다.
    [SerializeField] private GameObject[] _batteryIcons;

    // 고정 테두리와 스파크 애니메이션을 포함한 프레임 루트다.
    [SerializeField] private GameObject _readyFrame;

    // 기존 AssistantText를 관리하는 시스템 대화 UI다.
    [SerializeField] private SystemDialogueUI _systemDialogue;

    // 충전 완료 안내를 실제로 표시할 게임 시간이다.
    [SerializeField, Min(0.01f)]
    private float _readyMessageDuration = 3f;

    // 충전 완료로 바뀌는 순간에만 안내를 요청하기 위한 상태다.
    private bool _wasReady;

    private void Awake()
    {
        // 캐릭터 초기화 전에 편집용으로 켜둔 UI를 숨긴다.
        Show(0, false);
    }

    // 최대 충전 개수를 표현할 만큼 아이콘이 준비됐는지 검사한다.
    public void ValidateConfiguration(int requiredCount)
    {
        // 안내를 표시할 대상과 표시 시간이 올바른지 확인한다.
        if (_systemDialogue == null ||
            float.IsNaN(_readyMessageDuration) ||
            float.IsInfinity(_readyMessageDuration) ||
            _readyMessageDuration <= 0f)
        {
            throw new InvalidOperationException(
                $"{name}: System Dialogue와 안내 시간을 확인해주세요.");
        }

        if (_batteryIcons == null ||
            _batteryIcons.Length < requiredCount ||
            _readyFrame == null)
        {
            throw new InvalidOperationException(
                $"{name}: 건전지 아이콘 개수와 Ready Frame을 확인해주세요.");
        }

        for (int i = 0; i < _batteryIcons.Length; i++)
        {
            if (_batteryIcons[i] == null)
            {
                throw new InvalidOperationException(
                    $"{name}: Battery Icons의 {i}번 항목이 비어 있습니다.");
            }

            // 같은 오브젝트를 여러 칸에 넣으면 개수를 표현할 수 없다.
            for (int j = 0; j < i; j++)
            {
                if (_batteryIcons[i] == _batteryIcons[j])
                {
                    throw new InvalidOperationException(
                        $"{name}: 같은 건전지 아이콘이 중복 등록됐습니다.");
                }
            }
        }
    }

    // 충전 아이콘, 준비 프레임, 최초 준비 안내를 갱신한다.
    public void Show(int chargeCount, bool isReady)
    {
        if (_batteryIcons != null)
        {
            for (int i = 0; i < _batteryIcons.Length; i++)
            {
                if (_batteryIcons[i] != null)
                {
                    _batteryIcons[i].SetActive(i < chargeCount);
                }
            }
        }

        if (_readyFrame != null)
        {
            // 안내 시간이 끝나도 준비 상태라면 프레임은 유지한다.
            _readyFrame.SetActive(isReady);
        }

        if (_systemDialogue != null)
        {
            if (isReady && !_wasReady)
            {
                // 미충전에서 완충으로 바뀌는 순간에만 요청한다.
                _systemDialogue.ShowTimedAssistant(
                    this,
                    "R키를 눌러 필살기를 사용하세요!",
                    _readyMessageDuration);
            }
            else if (!isReady)
            {
                // 사망, 클리어, 이후 구현할 필살기 사용 시 안내도 취소한다.
                _systemDialogue.CancelTimedAssistant(this);
            }
        }

        _wasReady = isReady;
    }

    private void OnDisable()
    {
        // UI가 종료되면 남아 있는 자신의 안내 요청을 제거한다.
        if (_systemDialogue != null)
        {
            _systemDialogue.CancelTimedAssistant(this);
        }

        _wasReady = false;
    }
}
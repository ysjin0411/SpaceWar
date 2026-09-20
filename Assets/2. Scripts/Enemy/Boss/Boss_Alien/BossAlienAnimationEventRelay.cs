using System;
using UnityEngine;

// 본체 애니메이션의 공격 타이밍을 실행 중인 패턴에 전달한다.
public sealed class BossAlienAnimationEventRelay : MonoBehaviour
{
    // 해치가 완전히 열려 소켓별 발사 일정을 시작할 순간을 알린다.
    public event Action MissileSequenceStartRequested;

    // 준비 자세가 끝나고 팔을 뻗을 순간을 알린다.
    public event Action GrabExtendRequested;

    // 잡힌 플레이어를 던질 순간을 알린다.
    public event Action GrabThrowRequested;

    // 해치 개방 애니메이션의 이벤트에서 한 번 호출한다.
    // 실제 발사 횟수와 간격은 실행 중인 패턴이 Config로 관리한다.
    public void NotifyMissileSequenceStart()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (MissileSequenceStartRequested != null)
        {
            MissileSequenceStartRequested.Invoke();
        }
    }

    // 그랩 준비 애니메이션의 발사 프레임에서 호출한다.
    public void NotifyGrabExtend()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (GrabExtendRequested != null)
        {
            GrabExtendRequested.Invoke();
        }
    }

    // 그랩 던지기 애니메이션의 손을 놓는 프레임에서 호출한다.
    public void NotifyGrabThrow()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (GrabThrowRequested != null)
        {
            GrabThrowRequested.Invoke();
        }
    }
}
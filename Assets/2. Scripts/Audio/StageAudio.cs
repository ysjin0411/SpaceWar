using System.Collections;
using UnityEngine;

public sealed class StageAudio : MonoBehaviour
{
    // 해당 전투 씬의 진행 관리자와 보스 관리자.
    [SerializeField] private StageFlowManager _flow;
    [SerializeField] private BossEncounterManager _bosses;
    // Stage1과 Stage2에 각각 다른 음악을 연결한다.
    [SerializeField] private GameSound _battleMusic;
    [SerializeField] private GameSound _midBossMusic;
    [SerializeField] private GameSound _finalBossMusic;
    // 다음 페이즈가 없는 씬에서는 None.
    [SerializeField] private GameSound _phaseTwoMusic;
    // 동일 단계의 중복 이벤트와 초기 재적용을 막는 상태.
    private StagePhase _lastPhase = StagePhase.None;

    /// <summary>진행과 보스 교체 이벤트를 구독합니다.</summary>
    private void OnEnable()
    {
        // 누락된 연결을 명확히 알린다.
        if (_flow == null || _bosses == null)
            throw new MissingReferenceException("StageAudio의 Flow와 Bosses를 연결하세요.");
        _lastPhase = StagePhase.None;
        _flow.PhaseChanged += HandlePhaseChanged;
        _bosses.BossReplaced += HandleBossReplaced;
    }

    /// <summary>이미 시작한 단계가 있다면 초기 상태를 반영합니다.</summary>
    private IEnumerator Start()
    {
        // 저장된 Mixer 음량이 먼저 적용되도록 기다린다.
        yield return null;
        HandlePhaseChanged(_flow.CurrentPhase);
    }

    /// <summary>전투 진행 단계에 맞춰 음악과 알림을 재생합니다.</summary>
    private void HandlePhaseChanged(StagePhase phase)
    {
        // 한 단계가 반복 통지되어도 결과음을 여러 번 내지 않는다.
        if (phase == _lastPhase) return;
        _lastPhase = phase;
        switch (phase)
        {
            case StagePhase.StageIntro:
            case StagePhase.Section1:
            case StagePhase.SectionTransition:
            case StagePhase.Section2:
                // 일반 전투 구간의 음악을 유지하거나 복귀시킨다.
                GameAudio.SetMusic(_battleMusic);
                break;
            case StagePhase.MidBossWarning:
            case StagePhase.FinalBossWarning:
                // 보스 경고는 이벤트당 한 번만 재생한다.
                GameAudio.Play(GameSound.BossWarning);
                break;
            case StagePhase.MidBossBattle:
                GameAudio.SetMusic(_midBossMusic);
                break;
            case StagePhase.FinalBossBattle:
                GameAudio.SetMusic(_finalBossMusic);
                break;
            case StagePhase.Cleared:
            case StagePhase.Failed:
                // 결과 화면에서는 전투 지속음을 종료하고 결과음을 재생한다.
                GameAudio.StopGameplaySounds();
                GameAudio.SetMusic(GameSound.None);
                GameAudio.Play(phase == StagePhase.Cleared
                    ? GameSound.StageClear : GameSound.StageFailed);
                break;
        }
    }

    /// <summary>실제 보스 교체가 완료됐을 때 2페이즈 음악으로 바꿉니다.</summary>
    private void HandleBossReplaced(BossEnemy boss)
    {
        // 현재 프로젝트의 Alien 2페이즈 교체만 처리한다.
        if (boss is Boss_AlienBody && _phaseTwoMusic != GameSound.None)
            GameAudio.SetMusic(_phaseTwoMusic);
    }

    /// <summary>씬 종료나 비활성화 시 이벤트를 해제합니다.</summary>
    private void OnDisable()
    {
        // 이미 파괴된 관리자에 접근하지 않는다.
        if (_flow != null) _flow.PhaseChanged -= HandlePhaseChanged;
        if (_bosses != null) _bosses.BossReplaced -= HandleBossReplaced;
    }
}

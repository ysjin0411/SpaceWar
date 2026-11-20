using System;
using UnityEngine;

/// <summary>
/// 업적 슬롯이 화면에 표시할 현재 상태다.
/// </summary>
public enum AchievementState
{
    InProgress,       // 아직 조건을 달성하지 못한 상태
    RewardAvailable, // 조건을 달성했지만 보상을 받지 않은 상태
    Completed        // 보상까지 모두 받은 최종 완료 상태
}

/// <summary>
/// 저장된 플레이 기록과 AchievementData의 조건을 비교해
/// 업적 달성 여부를 판정한다.
/// </summary>
[DefaultExecutionOrder(100)]
public class AchievementManager : MonoBehaviour
{
    // 다른 씬에서도 현재 업적 시스템에 접근하기 위한 인스턴스다.
    public static AchievementManager Instance
    {
        get;
        private set;
    }

    [Header("업적 데이터")]

    // 게임에 존재하는 모든 업적 Config가 등록된 데이터베이스다.
    [SerializeField] AchievementDatabase _achievementDatabase;

    // 이후 업적창에서 전체 Config를 읽을 수 있도록 공개한다.
    public AchievementDatabase Database =>
        _achievementDatabase;

    // 업적 상태가 변경됐을 때 UI에 알리는 이벤트다.
    public event Action AchievementStateChanged;

    private void Awake()
    {
        // 중복 매니저가 생성되면 새 컴포넌트만 제거한다.
        // GameRoot 자체를 제거하면 다른 전역 시스템도 사라지므로
        // Destroy(gameObject)가 아니라 Destroy(this)를 사용한다.
        if (Instance != null &&
            Instance != this)
        {
            Destroy(this);
            return;
        }

        // 현재 컴포넌트를 전역 접근 인스턴스로 등록한다.
        Instance = this;

        // Database가 없으면 업적 조건을 확인할 수 없다.
        if (_achievementDatabase == null)
        {
            throw new InvalidOperationException(
                $"{name}의 AchievementDatabase가 연결되지 않았습니다.");
        }

        // SaveManager가 먼저 초기화돼 있어야 저장 기록을 확인할 수 있다.
        if (SaveManager.Instance == null)
        {
            throw new InvalidOperationException(
                "SaveManager가 없습니다. Bootstrap 씬 설정을 확인해주세요.");
        }
    }

    /// <summary>
    /// Database에 등록된 모든 업적 조건을 검사한다.
    /// 새롭게 달성된 업적은 즉시 저장한다.
    /// </summary>
    public void EvaluateAllAchievements()
    {
        // 사용자 데이터가 선택되기 전에는 업적 기록을 만들거나 평가하지 않는다.
        if (!SaveManager.Instance.IsLoggedIn)
        {
            return;
        }

        // 이번 검사에서 새롭게 달성된 업적이 있는지 기록한다.
        bool hasNewlyCompletedAchievement = false;

        // Database 등록 순서대로 모든 업적을 검사한다.
        for (int index = 0;
             index < _achievementDatabase.Count;
             index++)
        {
            AchievementData achievementData =
                _achievementDatabase.GetAchievementDataAt(index);

            // 이미 달성한 업적은 조건을 다시 검사하지 않는다.
            AchievementSaveData achievementRecord =
                SaveManager.Instance.GetOrCreateAchievementRecord(
                    achievementData.Id);

            if (achievementRecord.isCompleted)
            {
                continue;
            }

            // 아직 조건을 만족하지 않았다면 다음 업적으로 넘어간다.
            if (!IsConditionMet(achievementData))
            {
                continue;
            }

            // 조건을 만족했으므로 달성 상태로 저장한다.
            bool wasCompleted =
                SaveManager.Instance.TryCompleteAchievement(
                    achievementData.Id);

            if (wasCompleted)
            {
                hasNewlyCompletedAchievement = true;
            }
        }

        // 새롭게 달성된 업적이 있을 때만 UI에 변경 사실을 알린다.
        if (hasNewlyCompletedAchievement)
        {
            GameAudio.Play(GameSound.AchievementUnlock);
            AchievementStateChanged?.Invoke();
        }
    }

    /// <summary>
    /// 업적의 달성 및 보상 수령 기록을 확인해
    /// UI에서 사용할 현재 상태를 반환한다.
    /// </summary>
    /// <param name="achievementData">상태를 확인할 업적 Config</param>
    public AchievementState GetAchievementState(
        AchievementData achievementData)
    {
        // Config가 없으면 어떤 업적인지 확인할 수 없다.
        if (achievementData == null)
        {
            throw new ArgumentNullException(
                nameof(achievementData));
        }

        // 해당 업적의 저장 기록을 가져온다.
        AchievementSaveData achievementRecord =
            SaveManager.Instance.GetOrCreateAchievementRecord(
                achievementData.Id);

        // 보상을 이미 받았다면 최종 완료 상태다.
        if (achievementRecord.isRewardClaimed)
        {
            return AchievementState.Completed;
        }

        // 조건은 달성했지만 보상을 받지 않았다면
        // 보상 수령이 가능한 상태다.
        if (achievementRecord.isCompleted)
        {
            return AchievementState.RewardAvailable;
        }

        // 아직 조건을 달성하지 못한 진행 중 상태다.
        return AchievementState.InProgress;
    }

    /// <summary>
    /// 업적 슬롯의 보상받기 버튼으로부터 보상 수령을 요청받는다.
    /// 지급 성공 시 업적 UI 전체에 상태 변경을 알린다.
    /// </summary>
    /// <param name="achievementData">보상을 받을 업적 Config</param>
    /// <returns>보상 수령에 성공했다면 true</returns>
    public bool TryClaimReward(
        AchievementData achievementData)
    {
        // Config가 없으면 보상을 지급할 수 없다.
        if (achievementData == null)
        {
            throw new ArgumentNullException(
                nameof(achievementData));
        }

        // SaveManager가 보상 지급과 중복 수령 방지를 처리한다.
        bool wasClaimed =
            SaveManager.Instance.TryClaimAchievementReward(
                achievementData);

        // 수령할 수 없는 상태였다면 UI를 갱신하지 않는다.
        if (!wasClaimed)
        {
            return false;
        }
        GameAudio.Play(GameSound.QuestReward); // 실제 업적 보상 수령.

        // 슬롯 상태와 메인메뉴 알림 이펙트가
        // 즉시 다시 계산될 수 있도록 알린다.
        AchievementStateChanged?.Invoke();

        return true;
    }

    /// <summary>
    /// 달성했지만 아직 보상을 받지 않은 업적이
    /// 하나라도 존재하는지 확인한다.
    /// </summary>
    /// <returns>수령 가능한 업적이 있다면 true</returns>
    public bool HasClaimableAchievement()
    {
        // Database에 등록된 전체 업적을 확인한다.
        for (int index = 0;
             index < _achievementDatabase.Count;
             index++)
        {
            AchievementData achievementData =
                _achievementDatabase.GetAchievementDataAt(index);

            AchievementSaveData achievementRecord =
                SaveManager.Instance.GetOrCreateAchievementRecord(
                    achievementData.Id);

            // 달성했고 아직 보상을 받지 않은 업적을 찾았다.
            if (achievementRecord.isCompleted &&
                !achievementRecord.isRewardClaimed)
            {
                return true;
            }
        }

        // 모든 업적을 확인했지만 수령 가능한 업적이 없다.
        return false;
    }

    /// <summary>
    /// 한 업적의 조건 종류에 맞춰 실제 저장 데이터를 검사한다.
    /// </summary>
    private bool IsConditionMet(
        AchievementData achievementData)
    {
        switch (achievementData.ConditionType)
        {
            case AchievementConditionType.StageClearWithinTime:
                return IsStageClearTimeConditionMet(
                    achievementData);

            case AchievementConditionType.CharacterLevelReached:
                return IsCharacterLevelConditionMet(
                    achievementData);

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(achievementData.ConditionType),
                    achievementData.ConditionType,
                    "지원하지 않는 업적 조건입니다.");
        }
    }

    /// <summary>
    /// 지정한 스테이지의 최단 기록이
    /// Config의 제한 시간 이내인지 검사한다.
    /// </summary>
    private bool IsStageClearTimeConditionMet(
        AchievementData achievementData)
    {
        // Config에 지정된 스테이지의 최단 기록을 가져온다.
        float bestClearTime =
            SaveManager.Instance.GetBestClearTime(
                achievementData.TargetStageIndex);

        // -1은 아직 해당 스테이지를 클리어하지 않았다는 의미다.
        if (bestClearTime <= 0f)
        {
            return false;
        }

        // 최단 기록이 제한 시간 이하라면 조건을 만족한다.
        return bestClearTime <=
               achievementData.TargetClearTimeSeconds;
    }

    /// <summary>
    /// 지정한 캐릭터의 저장 레벨이
    /// Config의 목표 레벨 이상인지 검사한다.
    /// </summary>
    private bool IsCharacterLevelConditionMet(
        AchievementData achievementData)
    {
        // Config에 지정된 캐릭터의 현재 저장 레벨을 가져온다.
        int currentLevel =
            SaveManager.Instance.GetCharacterLevel(
                achievementData.TargetCharacterId);

        // 현재 레벨이 목표 레벨 이상이면 조건을 만족한다.
        return currentLevel >=
               achievementData.TargetCharacterLevel;
    }

    private void OnDestroy()
    {
        // 현재 등록된 인스턴스 자신이 제거될 때만 참조를 비운다.
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
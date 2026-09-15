using System;
using System.Collections.Generic;
using UnityEngine;

// UI와 독립적으로 퀘스트 상태 조회와 수락을 처리한다.
public static class QuestService
{
    // 저장까지 완료된 퀘스트 변경을 알린다.
    // 이후 퀘스트창 Presenter가 구독해서 목록을 갱신할 수 있다.
    public static event Action<string> QuestChanged;

    // 현재 로그인 사용자의 퀘스트 상태를 반환한다.
    public static QuestState GetState(QuestData quest)
    {
        SaveManager saveManager = SaveManager.Instance;

        if (quest == null ||
            string.IsNullOrWhiteSpace(quest.Id) ||
            saveManager == null ||
            !saveManager.IsLoggedIn)
        {
            return QuestState.Locked;
        }

        List<QuestSaveData> records =
            saveManager.PlayerSaveData.questRecords;

        // 오래된 JSON에는 questRecords가 없을 수 있다.
        if (records != null)
        {
            for (int i = 0; i < records.Count; i++)
            {
                QuestSaveData record = records[i];

                if (record == null || record.questId != quest.Id)
                {
                    continue;
                }

                // 수락한 퀘스트는 현재 레벨과 관계없이 저장 상태를 따른다.
                switch (record.state)
                {
                    case QuestState.InProgress:
                    case QuestState.ReadyToTurnIn:
                    case QuestState.RewardClaimed:
                        return record.state;

                    default:
                        // 잘못된 기록을 시작 가능으로 취급해 중복 수락하지 않는다.
                        return QuestState.Locked;
                }
            }
        }

        // 수락 기록이 없을 때만 시작 레벨을 검사한다.
        return saveManager.CurrentUserLevel >= quest.RequiredUserLevel
            ? QuestState.Available
            : QuestState.Locked;
    }

    // 시작 가능한 퀘스트만 수락하고 즉시 파일에 저장한다.
    public static bool TryAccept(QuestData quest, out string error)
    {
        error = string.Empty;

        if (GetState(quest) != QuestState.Available)
        {
            error = "현재 수락할 수 없는 퀘스트입니다.";
            return false;
        }

        SaveManager saveManager = SaveManager.Instance;
        PlayerSaveData saveData = saveManager.PlayerSaveData;

        // 이전 형식의 세이브에도 새 기록을 추가할 수 있도록 준비한다.
        bool createdList = saveData.questRecords == null;

        if (createdList)
        {
            saveData.questRecords = new List<QuestSaveData>();
        }

        QuestSaveData record = new QuestSaveData
        {
            questId = quest.Id,
            state = QuestState.InProgress
        };

        saveData.questRecords.Add(record);

        try
        {
            // 현재 로그인한 사용자의 JSON에 저장한다.
            saveManager.SaveToFile();
        }
        catch (Exception exception)
        {
            // 저장에 실패했는데 메모리만 수락 상태로 남지 않게 되돌린다.
            saveData.questRecords.Remove(record);

            if (createdList)
            {
                saveData.questRecords = null;
            }

            Debug.LogException(exception);
            error = "퀘스트 수락을 저장하지 못했습니다. 다시 시도해주세요.";
            return false;
        }

        // 저장에 성공한 뒤에만 다른 기능에 변경을 알린다.
        if (QuestChanged != null)
        {
            QuestChanged.Invoke(quest.Id);
        }

        return true;
    }

    // 진행 중인 퀘스트를 포기하고 변경 내용을 저장한다.
    public static bool TryAbandon(QuestData quest, out string error)
    {
        error = string.Empty;

        // 목표 달성·보상 수령 완료 상태는 포기 대상에서 제외한다.
        if (GetState(quest) != QuestState.InProgress)
        {
            error = "진행 중인 퀘스트만 포기할 수 있습니다.";
            return false;
        }

        SaveManager saveManager = SaveManager.Instance;
        List<QuestSaveData> records =
            saveManager.PlayerSaveData.questRecords;

        // 현재 상태를 판정한 진행 기록을 찾는다.
        int recordIndex = records.FindIndex(
            record => record != null && record.questId == quest.Id);

        if (recordIndex < 0)
        {
            error = "퀘스트 진행 기록을 찾을 수 없습니다.";
            return false;
        }

        // 저장 실패 시 진행 상태를 복구할 수 있도록 원본을 보관한다.
        QuestSaveData originalRecord = records[recordIndex];
        records.RemoveAt(recordIndex);

        try
        {
            saveManager.SaveToFile();
        }
        catch (Exception exception)
        {
            // 파일 저장에 실패하면 메모리의 진행 기록도 원래대로 복구한다.
            records.Insert(recordIndex, originalRecord);

            Debug.LogException(exception);
            error = "퀘스트 포기를 저장하지 못했습니다. 다시 시도해주세요.";
            return false;
        }

        // 저장까지 성공한 뒤 퀘스트 화면에 변경을 알린다.
        if (QuestChanged != null)
        {
            QuestChanged.Invoke(quest.Id);
        }

        return true;
    }

    // 현재 진행 중인 퀘스트 중 이번에 목표를 달성한 기록을 찾는다.
    // 여기서는 상태를 바꾸거나 파일을 저장하지 않는다.
    public static List<QuestSaveData> FindSatisfiedQuests(
        QuestDatabase database,
        PlayerSaveData saveData,
        bool achievementWindowOpened)
    {
        if (database == null)
        {
            throw new InvalidOperationException(
                "SaveManager에 QuestDatabase를 연결해주세요.");
        }

        List<QuestSaveData> result = new List<QuestSaveData>();

        if (saveData.questRecords == null)
        {
            return result;
        }

        foreach (QuestSaveData record in saveData.questRecords)
        {
            // 이미 목표를 달성했거나 보상을 받은 퀘스트는 다시 처리하지 않는다.
            if (record == null || record.state != QuestState.InProgress)
            {
                continue;
            }

            foreach (QuestData quest in database.Quests)
            {
                if (quest == null || quest.Id != record.questId)
                {
                    continue;
                }

                if (IsObjectiveSatisfied(
                        quest.Objective, saveData, achievementWindowOpened))
                {
                    result.Add(record);
                }

                // 같은 기록은 한 번만 처리한다.
                break;
            }
        }

        return result;
    }

    // 목표 설정과 실제 플레이어 데이터를 비교한다.
    private static bool IsObjectiveSatisfied(
        QuestObjectiveData objective,
        PlayerSaveData saveData,
        bool achievementWindowOpened)
    {
        // 설정이 올바르다는 것과 목표를 달성했다는 것은 별개다.
        if (objective == null || !objective.IsConfigured)
        {
            return false;
        }

        switch (objective.Type)
        {
            case QuestObjectiveType.CharacterLevel:
            case QuestObjectiveType.WeaponOwned:
                {
                    if (saveData.characterLoadouts == null)
                    {
                        return false;
                    }

                    foreach (CharacterLoadoutSaveData character
                             in saveData.characterLoadouts)
                    {
                        if (character == null ||
                            character.characterId != objective.Character.Id)
                        {
                            continue;
                        }

                        // 지정한 캐릭터의 실제 저장 레벨을 검사한다.
                        if (objective.Type == QuestObjectiveType.CharacterLevel)
                        {
                            return character.level >= objective.RequiredLevel;
                        }

                        // 구매 여부를 다시 요구하지 않고 현재 소유 기록을 검사한다.
                        return character.ownedWeaponIds != null &&
                               character.ownedWeaponIds.Contains(objective.Weapon.Id);
                    }

                    return false;
                }

            case QuestObjectiveType.StageClear:
                {
                    if (saveData.stageClearRecords == null)
                    {
                        return false;
                    }

                    foreach (StageClearSaveData stage in saveData.stageClearRecords)
                    {
                        // 기존 저장 구조에서 음수 시간은 미클리어를 의미한다.
                        if (stage != null &&
                            stage.stageIndex == objective.StageIndex &&
                            stage.bestClearTime >= 0f)
                        {
                            return true;
                        }
                    }

                    return false;
                }

            case QuestObjectiveType.OpenAchievementWindow:
                // NPC를 통해 업적창을 연 처리에서만 true를 전달한다.
                return achievementWindowOpened;

            default:
                return false;
        }
    }

    // 저장이 완료된 목표 달성을 화면에 알린다.
    public static void NotifyProgressChanges(
        List<QuestSaveData> changedRecords)
    {
        Action<string> handlers = QuestChanged;

        if (handlers == null)
        {
            return;
        }

        foreach (QuestSaveData record in changedRecords)
        {
            foreach (Action<string> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler.Invoke(record.questId);
                }
                catch (Exception exception)
                {
                    // 화면 갱신 오류를 파일 저장 실패로 취급하지 않는다.
                    Debug.LogException(exception);
                }
            }
        }
    }

    // UI에서 요청한 보상 수령을 저장 관리자에 전달한다.
    public static bool TryClaimReward(QuestData quest, out string error)
    {
        SaveManager saveManager = SaveManager.Instance;

        if (saveManager == null || !saveManager.IsLoggedIn)
        {
            error = "로그인한 사용자 정보가 없습니다.";
            return false;
        }

        return saveManager.TryClaimQuestReward(quest, out error);
    }
}
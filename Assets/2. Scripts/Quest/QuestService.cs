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
}
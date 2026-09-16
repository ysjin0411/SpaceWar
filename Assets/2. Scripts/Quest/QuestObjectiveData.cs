using System;
using System.Collections.Generic;
using UnityEngine;

// 퀘스트의 목표 종류와 목표 달성에 필요한 설정이다.
[Serializable]
public class QuestObjectiveData
{
    // 이 퀘스트에서 검사할 목표 종류다.
    [SerializeField]
    private QuestObjectiveType _type = QuestObjectiveType.None;

    // 레벨 또는 무기 보유 여부를 검사할 캐릭터다.
    [SerializeField] private CharacterData _character;

    // 캐릭터 레벨 목표의 필요 레벨이다.
    [SerializeField, Min(1)] private int _requiredLevel = 1;

    // 클리어해야 하는 스테이지 번호다. Stage 1은 0이다.
    [SerializeField, Min(0)] private int _stageIndex;

    // 무기 보유 목표에서 검사할 무기다.
    [SerializeField] private WeaponData _weapon;

    // 아이템 수집 목표에서 요구하는 모든 아이템과 수량이다.
    // 목록의 모든 조건을 만족해야 목표를 달성한다.
    [SerializeField]
    private List<QuestItemRequirement> _requiredItems =
        new List<QuestItemRequirement>();

    // 외부에서는 목표 설정을 읽기만 한다.
    public QuestObjectiveType Type => _type;
    public CharacterData Character => _character;
    public int RequiredLevel => _requiredLevel;
    public int StageIndex => _stageIndex;
    public WeaponData Weapon => _weapon;
    public IReadOnlyList<QuestItemRequirement> RequiredItems =>
        _requiredItems;

    // 선택한 목표에 필요한 설정이 모두 있는지 검사한다.
    public bool IsConfigured
    {
        get
        {
            switch (_type)
            {
                case QuestObjectiveType.CharacterLevel:
                    return _character != null &&
                           !string.IsNullOrWhiteSpace(_character.Id) &&
                           _requiredLevel >= 1;

                case QuestObjectiveType.StageClear:
                    return _stageIndex >= 0;

                case QuestObjectiveType.WeaponOwned:
                    return _character != null &&
                           !string.IsNullOrWhiteSpace(_character.Id) &&
                           _weapon != null &&
                           !string.IsNullOrWhiteSpace(_weapon.Id);

                case QuestObjectiveType.OpenAchievementWindow:
                    return true;

                case QuestObjectiveType.ItemOwned:
                    return AreRequiredItemsConfigured();

                default:
                    return false;
            }
        }
    }

    // 수집 목록의 누락과 같은 아이템의 중복 등록을 검사한다.
    private bool AreRequiredItemsConfigured()
    {
        if (_requiredItems == null || _requiredItems.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < _requiredItems.Count; i++)
        {
            QuestItemRequirement requirement = _requiredItems[i];

            if (requirement == null || !requirement.IsConfigured)
            {
                return false;
            }

            // 같은 아이템은 한 항목으로 등록하고 필요 수량을 지정한다.
            for (int j = 0; j < i; j++)
            {
                if (_requiredItems[j].Item.Id == requirement.Item.Id)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
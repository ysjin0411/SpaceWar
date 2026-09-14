using System;
using UnityEngine;

// QuestData의 Inspector 안에서 설정할 퀘스트 목표 데이터.
[Serializable]
public class QuestObjectiveData
{
    // 이 퀘스트의 목표 종류.
    [SerializeField]
    private QuestObjectiveType _type = QuestObjectiveType.None;

    // 레벨 달성 또는 무기 소유를 검사할 캐릭터.
    [SerializeField]
    private CharacterData _character;

    // CharacterLevel 조건에서 요구하는 캐릭터 레벨.
    [SerializeField, Min(1)]
    private int _requiredLevel = 1;

    // StageClear 조건에서 검사할 스테이지 번호.
    // 기존 저장 데이터와 동일하게 Stage 1은 0, Stage 2는 1이다.
    [SerializeField, Min(0)]
    private int _stageIndex;

    // WeaponOwned 조건에서 소유 여부를 검사할 무기.
    [SerializeField]
    private WeaponData _weapon;

    // 외부에서는 목표 설정을 읽기만 한다.
    public QuestObjectiveType Type => _type;
    public CharacterData Character => _character;
    public int RequiredLevel => _requiredLevel;
    public int StageIndex => _stageIndex;
    public WeaponData Weapon => _weapon;

    // 선택한 목표 종류에 필요한 설정이 들어 있는지 확인한다.
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
                    // 업적창 열기는 추가 대상 설정이 필요 없다.
                    return true;

                default:
                    // 목표 미설정 상태를 달성 가능한 조건으로 취급하지 않는다.
                    return false;
            }
        }
    }
}
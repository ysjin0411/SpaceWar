using System;
using System.Collections.Generic;
using UnityEngine;

// 상자가 거쳐 가는 등급 순서다.
public enum TreasureBoxGrade
{
    Wood = 1,
    Silver = 2,
    Gold = 3,
    Emerald = 4,
    Diamond = 5
}

// 인스펙터에서 보상 프리팹과 생성 개수를 한 항목으로 설정한다.
[Serializable]
public class TreasureBoxRewardEntry
{
    // 상자 개봉 시 생성할 보상 프리팹이다.
    [SerializeField]
    GameObject _rewardPrefab;

    // 해당 보상 프리팹을 생성할 개수다.
    // 예: 골드 프리팹을 지정하고 5를 입력하면 골드 오브젝트 5개를 생성한다.
    [SerializeField, Min(1)]
    int _count = 1;

    // 보상 지급 코드에서 프리팹과 개수를 읽을 때 사용한다.
    public GameObject RewardPrefab => _rewardPrefab;
    public int Count => _count;
}

// 등급별 고정 설정을 보관한다.
// 현재 등급과 누적 피해량은 실제 상자에서 별도로 관리한다.
[CreateAssetMenu(
    fileName = "TreasureBoxGradeData",
    menuName = "GameData/TreasureBoxGradeData")]
public class TreasureBoxGradeData : ScriptableObject
{
    // 이 데이터가 나타내는 상자 등급이다.
    [SerializeField]
    TreasureBoxGrade _grade = TreasureBoxGrade.Wood;

    // 해당 등급에서 보여줄 외형 프리팹이다.
    // 효과 크기와 배치는 프리팹에 설정한 값을 사용한다.
    [SerializeField]
    GameObject _visualPrefab;

    // Charge 상태에 진입하는 데 필요한 누적 피해량이다.
    // 기준을 채운 뒤 별도의 추가 타격이 있어야 승급하거나 열린다.
    [SerializeField, Min(1)]
    float _requiredDamage = 100;

    // 처음 등장할 때 최종 보상 등급을 추첨하는 가중치다.
    // 승급할 때 다시 추첨하지 않는다.
    [SerializeField, Min(0f)]
    float _selectionWeight = 1f;

    // 이 등급에서 최종 개봉했을 때 지급할 보상 목록이다.
    // 등록된 모든 항목을 각 항목의 Count만큼 생성한다.
    [SerializeField]
    List<TreasureBoxRewardEntry> _rewards =
        new List<TreasureBoxRewardEntry>();

    // 외부에서 등급별 설정을 읽을 때 사용한다.
    public TreasureBoxGrade Grade => _grade;
    public GameObject VisualPrefab => _visualPrefab;
    public float RequiredDamage => _requiredDamage;
    public float SelectionWeight => _selectionWeight;

    // 외부 코드에서 목록을 조회할 수 있도록 공개한다.
    // IReadOnlyList를 사용해 목록의 추가·삭제 기능은 노출하지 않는다.
    public IReadOnlyList<TreasureBoxRewardEntry> Rewards => _rewards;
}
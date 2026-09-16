using System;
using System.Collections.Generic;
using UnityEngine;

// 이 컴포넌트가 붙은 오브젝트의 드롭 설정과 실행을 담당한다.
[DisallowMultipleComponent]
public class Drop : MonoBehaviour
{
    // Money, Quest, 기타 그룹에 드롭할 프리팹과 개수를 등록한다.
    [SerializeField]
    private List<DropGroup> _dropGroups = new List<DropGroup>();

    // 아이템이 나타날 월드 위치.
    // 보스 머리 위에 만든 자식 Transform을 연결한다.
    [SerializeField] private Transform _itemDropPoint;

    // 재화 생성 요청을 전달할 현재 스테이지의 관리자다.
    private GameManager _gameManager;

    // 초기화와 중복 드롭 여부를 관리한다.
    private bool _isInitialized;
    private bool _hasDropped;

    // 생성한 쪽에서 현재 스테이지의 관리자를 전달한다.
    public void Initialize(GameManager gameManager)
    {
        if (gameManager == null)
        {
            throw new ArgumentNullException(nameof(gameManager));
        }

        _gameManager = gameManager;
        ValidateConfiguration();
        _isInitialized = true;
    }

    // 프리팹 설정 오류를 실제 지급 전에 확인한다.
    private void ValidateConfiguration()
    {
        if (_dropGroups == null)
        {
            throw new InvalidOperationException(
                $"{name}: Drop Groups 목록을 확인해주세요.");
        }

        foreach (DropGroup group in _dropGroups)
        {
            if (group == null || group.Entries == null ||
                string.IsNullOrWhiteSpace(group.GroupName))
            {
                throw new InvalidOperationException(
                    $"{name}: 드롭 그룹 이름과 목록을 확인해주세요.");
            }

            bool moneyGroup = group.GroupName == "Money";
            bool questGroup = group.GroupName == "Quest";

            foreach (DropPrefabEntry entry in group.Entries)
            {
                if (entry == null || entry.Prefab == null ||
                    entry.Count < 1)
                {
                    throw new InvalidOperationException(
                        $"{name}: {group.GroupName}의 Prefab과 Count를 확인해주세요.");
                }

                Money money = entry.Prefab.GetComponent<Money>();
                Item item = entry.Prefab.GetComponent<Item>();

                // 인벤토리 아이템은 아이콘과 연출 시작 위치가 필요하다.
                if (item != null)
                {
                    if (_itemDropPoint == null || item.Icon == null)
                    {
                        throw new InvalidOperationException(
                            $"{name}: Item Drop Point와 아이템 Icon을 연결해주세요.");
                    }

                    _gameManager.ValidateItemEffectConfiguration();
                }

                // 하나의 프리팹이 재화와 인벤토리 아이템을 겸하지 않는다.
                if (money != null && item != null)
                {
                    throw new InvalidOperationException(
                        $"{entry.Prefab.name}: Money와 Item을 함께 붙일 수 없습니다.");
                }

                // Money 그룹에는 Money를 상속받은 프리팹만 등록한다.
                // 재화 프리팹도 반드시 Money 그룹에 등록한다.
                if (moneyGroup != (money != null))
                {
                    throw new InvalidOperationException(
                        $"{entry.Prefab.name}: 재화는 Money 그룹에 등록해주세요.");
                }

                bool questItem =
                    item != null && item.Type == ItemType.Quest;

                // Quest 그룹과 퀘스트 아이템의 분류가 일치해야 한다.
                if (questGroup != questItem)
                {
                    throw new InvalidOperationException(
                        $"{entry.Prefab.name}: 퀘스트 아이템은 Quest 그룹에 등록해주세요.");
                }

                if (item != null &&
                    string.IsNullOrWhiteSpace(item.Id))
                {
                    throw new InvalidOperationException(
                        $"{entry.Prefab.name}: Item ID가 비어 있습니다.");
                }

                if (money != null)
                {
                    if (money.Amount < 1)
                    {
                        throw new InvalidOperationException(
                            $"{entry.Prefab.name}: 재화 금액을 확인해주세요.");
                    }

                    // 해당 재화의 도착 UI가 준비됐는지도 확인한다.
                    _gameManager.GetMoneyUI(money.Type)
                        .ValidateConfiguration();
                }
            }
        }
    }

    // 완료를 기다릴 필요가 없는 일반 적이 사용하는 호출이다.
    public void Execute()
    {
        Execute(null);
    }

    // 보상을 지급하고, 재화·아이템·진행도 표시가 끝나면 완료를 알린다.
    public void Execute(Action onCompleted)
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException(
                $"{name}: Drop.Initialize를 먼저 호출해주세요.");
        }

        if (_hasDropped)
        {
            return;
        }

        _hasDropped = true;

        Vector3 moneyPosition = transform.position;

        // 모든 보상을 등록하기 전에 완료가 호출되지 않도록
        // 등록 작업 자체를 대기 작업 하나로 계산한다.
        int remaining = 1;

        Action finishOne = () =>
        {
            remaining--;

            if (remaining == 0 && onCompleted != null)
            {
                onCompleted.Invoke();
            }
        };

        foreach (DropGroup group in _dropGroups)
        {
            foreach (DropPrefabEntry entry in group.Entries)
            {
                if (group.GroupName == "Money")
                {
                    Money moneyPrefab =
                        entry.Prefab.GetComponent<Money>();

                    // 재화 묶음 전체의 회수가 끝날 때 하나를 완료한다.
                    remaining++;

                    _gameManager.SpawnMoney(
                        moneyPrefab,
                        moneyPosition,
                        entry.Count,
                        finishOne);

                    continue;
                }

                Item itemPrefab = entry.Prefab.GetComponent<Item>();

                for (int i = 0; i < entry.Count; i++)
                {
                    // Item이 아닌 기타 드롭 오브젝트는 기존처럼 생성한다.
                    if (itemPrefab == null)
                    {
                        Instantiate(
                            entry.Prefab,
                            transform.position,
                            entry.Prefab.transform.rotation);

                        continue;
                    }

                    SaveManager saveManager = SaveManager.Instance;

                    // 한 개를 지급할 때마다 최신 수량과 퀘스트 상태를 확인한다.
                    if (saveManager == null ||
                        !saveManager.CanDropItem(itemPrefab))
                    {
                        break;
                    }

                    List<string> messages;
                    string error;

                    // 지급과 저장에 성공한 아이템만 획득 연출을 재생한다.
                    if (!saveManager.TryAddDropItem(
                            itemPrefab, out messages, out error))
                    {
                        Debug.LogWarning(error, this);
                        break;
                    }

                    remaining++;

                    // 머리 위치는 지금 읽어 값으로 전달한다.
                    // 연출 중에는 보스 오브젝트의 생존 여부에 의존하지 않는다.
                    _gameManager.EnqueueItemEffect(
                        itemPrefab,
                        _itemDropPoint.position,
                        messages,
                        finishOne);
                }
            }
        }

        // 모든 보상의 등록이 끝났다.
        // 기다릴 연출이 없으면 여기서 바로 완료된다.
        finishOne();
    }
}
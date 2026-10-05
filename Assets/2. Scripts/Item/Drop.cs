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

    // 전투 중 필드에 생성할 스테이지 전용 아이템 목록이다.
    // 기존 Drop Groups와 별도로 각 항목의 백분율을 추첨한다.
    [SerializeField]
    private List<StageDropEntry> _stageDrops =
        new List<StageDropEntry>();

    // 아이템이 나타날 월드 위치.
    // 보스 머리 위에 만든 자식 Transform을 연결한다.
    [SerializeField] private Transform _itemDropPoint;

    // 재화 생성 요청을 전달할 현재 스테이지의 관리자다.
    private GameManager _gameManager;

    // 초기화와 중복 드롭 여부를 관리한다.
    private bool _isInitialized;
    private bool _hasDropped;

    // 이번 사용에 필요한 게임 매니저와 드롭 상태를 초기화한다.
    public void Initialize(GameManager gameManager)
    {
        if (gameManager == null)
        {
            throw new ArgumentNullException(nameof(gameManager));
        }

        // 설정 검증이 완료되기 전에는 드롭을 실행할 수 없게 한다.
        _isInitialized = false;

        // 이번 사용에서 드롭 처리를 담당할 게임 매니저를 연결한다.
        _gameManager = gameManager;

        // 일반 드롭과 스테이지 드롭 설정을 검증한다.
        ValidateConfiguration();
        ValidateStageDropConfiguration();

        // 이전 사용에서 드롭했던 기록을 지워 다시 드롭할 수 있게 한다.
        _hasDropped = false;

        // 모든 준비가 끝난 뒤 드롭 실행을 허용한다.
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

        // 필드 아이템은 생성까지만 처리한다.
        // 획득이나 수명 종료를 보스 보상 완료 조건으로 기다리지 않는다.
        ExecuteStageDrops();

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

    // 스테이지 전용 드랍 설정을 생성 전에 검사한다.
    private void ValidateStageDropConfiguration()
    {
        if (_stageDrops == null)
        {
            throw new InvalidOperationException(
                $"{name}: Stage Drops 목록을 확인해주세요.");
        }

        foreach (StageDropEntry entry in _stageDrops)
        {
            if (entry == null || entry.Prefab == null)
            {
                throw new InvalidOperationException(
                    $"{name}: Stage Drops에 비어 있는 항목이 있습니다.");
            }

            float chance = entry.DropChancePercent;

            // Range는 Inspector 입력을 제한하므로,
            // 런타임에서도 유효한 수치인지 별도로 확인한다.
            if (float.IsNaN(chance) ||
                float.IsInfinity(chance) ||
                chance < 0f ||
                chance > 100f)
            {
                throw new InvalidOperationException(
                    $"{name}: {entry.Prefab.name}의 드랍 확률은 " +
                    "0~100 사이여야 합니다.");
            }

            // 기존 인벤토리 아이템과 재화에는 별도의 지급 절차가 있다.
            // Stage Drops에 등록하여 그 절차를 건너뛰지 않도록 한다.
            Item inventoryItem = entry.Prefab.GetComponent<Item>();
            Money money = entry.Prefab.GetComponent<Money>();

            if (inventoryItem != null || money != null)
            {
                throw new InvalidOperationException(
                    $"{name}: {entry.Prefab.name}에는 Item 또는 Money가 있습니다. " +
                    "인벤토리 아이템과 재화는 기존 Drop Groups에 등록해주세요.");
            }
        }
    }

    // 각 항목을 독립적으로 추첨하고 성공한 프리팹을 생성한다.
    private void ExecuteStageDrops()
    {
        foreach (StageDropEntry entry in _stageDrops)
        {
            if (!entry.Roll())
            {
                continue;
            }

            // 몬스터가 사라져도 아이템은 남아 있어야 하므로
            // 몬스터의 자식으로 생성하지 않는다.
            GameObject instance = Instantiate(
                entry.Prefab,
                transform.position,
                entry.Prefab.transform.rotation);

            // 추가 로딩된 씬이 있더라도 드랍한 몬스터의 씬에 소속시킨다.
            // 해당 스테이지 씬을 해제하면 아이템도 함께 제거된다.
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(
                instance,
                gameObject.scene);
        }
    }
}
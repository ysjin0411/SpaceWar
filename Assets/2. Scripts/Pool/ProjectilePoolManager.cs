using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

// 다른 일반 컴포넌트보다 먼저 풀을 준비한다.
[DefaultExecutionOrder(-1000)]
public sealed class ProjectilePoolManager : MonoBehaviour
{
    [Serializable]
    private sealed class PoolSetting
    {
        // 풀링할 프리팹. 루트에 PoolObject가 필요하다.
        public GameObject Prefab;

        // 씬 시작 시 실제로 미리 생성할 개수.
        [Min(1)]
        public int PrewarmCount = 30;

        // 사용 후 풀에 보관할 최대 개수.
        [Min(1)]
        public int MaxSize = 100;
    }

    // 현재 전투 씬에서 사용하는 풀 매니저.
    public static ProjectilePoolManager Instance { get; private set; }

    // 프리팹별 생성 개수와 보관 개수를 설정한다.
    [SerializeField]
    private PoolSetting[] _settings;

    // 프리팹을 키로 사용해 해당 프리팹의 풀을 찾는다.
    private readonly Dictionary<GameObject, ObjectPool<GameObject>>
        _pools = new();

    // 최초 생성 중 OnEnable 등이 실행되지 않도록 하는 비활성 부모.
    private Transform _creationRoot;

    private void Awake()
    {
        // 같은 씬에서 두 매니저가 동작하는 설정 오류를 확인한다.
        if (Instance != null && Instance != this)
        {
            throw new InvalidOperationException(
                "ProjectilePoolManager는 하나만 배치하세요.");
        }

        Instance = this;

        // 미리 생성한 탄환은 비활성 상태로 준비한다.
        GameObject creationRoot = new GameObject("InactiveCreationRoot");
        creationRoot.transform.SetParent(transform, false);
        creationRoot.SetActive(false);
        _creationRoot = creationRoot.transform;

        foreach (PoolSetting setting in _settings)
        {
            CreatePool(setting);
        }
    }

    // 프리팹 하나에 대응하는 풀을 생성하고 미리 채운다.
    private void CreatePool(PoolSetting setting)
    {
        if (setting.Prefab == null ||
            !setting.Prefab.TryGetComponent(out PoolObject prefabPoolObject))
        {
            throw new InvalidOperationException(
                "등록할 프리팹 루트에 PoolObject가 필요합니다.");
        }

        if (setting.PrewarmCount < 1 ||
            setting.MaxSize < setting.PrewarmCount)
        {
            throw new InvalidOperationException(
                "PrewarmCount는 1 이상, MaxSize는 PrewarmCount 이상이어야 합니다.");
        }

        if (_pools.ContainsKey(setting.Prefab))
        {
            throw new InvalidOperationException(
                $"{setting.Prefab.name} 프리팹이 중복 등록되었습니다.");
        }

        // 생성 콜백에서 완성된 풀을 참조하기 위해 먼저 변수를 선언한다.
        ObjectPool<GameObject> pool = null;

        pool = new ObjectPool<GameObject>(
            createFunc: () =>
            {
                // 비활성 부모 아래에서 생성해 초기 활성화를 막는다.
                GameObject instance = Instantiate(
                    setting.Prefab, _creationRoot);

                instance.SetActive(false);

                // 실제 대기 장소는 매니저 아래로 옮긴다.
                instance.transform.SetParent(transform, false);

                // 생성된 오브젝트에 소속 풀을 알려준다.
                instance.GetComponent<PoolObject>().Bind(pool);

                return instance;
            },
            actionOnGet: instance =>
            {
                // 위치를 설정한 뒤 활성화하려고 여기서는 대여만 기록한다.
                instance.GetComponent<PoolObject>().MarkBorrowed();
            },
            actionOnRelease: instance =>
            {
                // OnDisable에서 투사체별 정리 코드가 실행된다.
                instance.SetActive(false);

                // 반납된 오브젝트를 매니저 아래에 보관한다.
                instance.transform.SetParent(transform, false);
            },
            actionOnDestroy: instance =>
            {
                // 보관 한도를 넘거나 풀을 정리할 때 실제로 제거한다.
                Destroy(instance);
            },
            collectionCheck: true,
            defaultCapacity: setting.PrewarmCount,
            maxSize: setting.MaxSize);

        _pools.Add(setting.Prefab, pool);

        // 모두 꺼낸 다음 반납해야 서로 다른 N개가 생성된다.
        GameObject[] prewarmed = new GameObject[setting.PrewarmCount];

        for (int i = 0; i < prewarmed.Length; i++)
        {
            prewarmed[i] = pool.Get();
        }

        for (int i = 0; i < prewarmed.Length; i++)
        {
            prewarmed[i].GetComponent<PoolObject>().ReturnToPool();
        }
    }

    // 지정한 프리팹의 오브젝트를 발사 위치에 꺼낸다.
    public GameObject Spawn(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation)
    {
        if (!_pools.TryGetValue(prefab, out ObjectPool<GameObject> pool))
        {
            throw new InvalidOperationException(
                $"{prefab.name} 프리팹이 풀에 등록되지 않았습니다.");
        }

        GameObject instance = pool.Get();

        // 매니저의 Transform 영향을 받지 않도록 분리한다.
        instance.transform.SetParent(null, false);
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.transform.localScale = prefab.transform.localScale;

        // 위치 설정 후 활성화하여 OnEnable에서도 새 위치를 사용한다.
        instance.SetActive(true);

        return instance;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        // 풀에서 대기 중인 오브젝트를 정리한다.
        foreach (ObjectPool<GameObject> pool in _pools.Values)
        {
            pool.Dispose();
        }

        _pools.Clear();
    }
}
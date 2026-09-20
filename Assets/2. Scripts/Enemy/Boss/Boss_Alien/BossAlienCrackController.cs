using System;
using System.Collections.Generic;
using UnityEngine;

// 보스 한 마리가 만든 균열의 생성과 수명을 관리한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(Boss_Alien))]
public sealed class BossAlienCrackController : MonoBehaviour
{
    // 균열 하나의 위치·방향·만료 시각을 보관한다.
    // 특수패턴도 이 정보를 읽어 균열 위치에서 공격할 수 있다.
    public sealed class Crack
    {
        public Vector2 Position { get; }
        public Vector2 Normal { get; }
        public float ExpiresAt { get; }
        public GameObject Visual { get; }

        public Crack(
            Vector2 position,
            Vector2 normal,
            float expiresAt,
            GameObject visual)
        {
            Position = position;
            Normal = normal;
            ExpiresAt = expiresAt;
            Visual = visual;
        }
    }

    // 균열의 지속시간을 읽을 로봇 페이즈 설정이다.
    [SerializeField] private BossAlienPhaseOneConfig _config;

    // 충돌 위치에 생성할 균열 프리팹이다.
    [SerializeField] private GameObject _crackPrefab;

    // 이 컴포넌트가 담당하는 보스와 현재 씬의 전투 경계다.
    private Boss_Alien _boss;
    private BossAlienGrabArena _arena;

    // 각 균열을 따로 저장하므로 새 균열이 기존 수명을 갱신하지 않는다.
    private readonly List<Crack> _cracks = new List<Crack>();

    // 특수패턴 실행 중에는 만료 시각이 지나도 균열을 유지한다.
    private bool _preserveCracks;

    // 실드와 특수패턴에서 조회할 현재 균열 정보다.
    public int Count => _cracks.Count;
    public IReadOnlyList<Crack> Cracks => _cracks.AsReadOnly();

    // 개수가 바뀌면 실드 상태를 갱신할 때 사용한다.
    public event Action<int> CountChanged;

    // 3개에 도달한 순간 특수패턴을 요청할 때 사용한다.
    public event Action MaxStacksReached;

    private void Awake()
    {
        _boss = GetComponent<Boss_Alien>();

        if (_config == null || _crackPrefab == null)
        {
            throw new InvalidOperationException(
                $"{name}: Config와 Crack Prefab을 연결하세요.");
        }

        float lifetime = _config.CrackLifetime;

        if (float.IsNaN(lifetime) ||
            float.IsInfinity(lifetime) ||
            lifetime <= 0f)
        {
            throw new InvalidOperationException(
                $"{name}: Crack Lifetime은 유효한 양수여야 합니다.");
        }
    }

    private void OnEnable()
    {
        // 보스 프리팹에 씬 참조를 저장할 수 없으므로 실행 시 찾는다.
        BossAlienGrabArena[] arenas =
            FindObjectsByType<BossAlienGrabArena>(
                FindObjectsSortMode.None);

        if (arenas.Length != 1 || !arenas[0].isActiveAndEnabled)
        {
            throw new InvalidOperationException(
                $"{name}: 씬에 활성화된 Grab Arena가 하나 필요합니다.");
        }

        _arena = arenas[0];
        _arena.ImpactOccurred += HandleImpact;

        // 실제 특수패턴의 시작과 종료에 맞춰 균열을 유지·제거한다.
        _boss.SpecialPatternStateChanged += HandleSpecialPatternState;
    }

    private void Update()
    {
        // 사망 후에는 균열을 남기지 않는다.
        if (_boss.IsDead)
        {
            ClearAll();
            return;
        }

        RemoveExpired();
    }

    // 이 보스가 던진 플레이어의 첫 경계 충돌만 처리한다.
    private void HandleImpact(
        Boss_Alien boss,
        Vector2 position,
        Vector2 normal)
    {
        if (boss != _boss ||
            _boss.IsDead ||
            _preserveCracks ||
            normal.sqrMagnitude < 0.0001f)
        {
            return;
        }

        // 이번 프레임에 이미 만료된 균열을 먼저 정리한다.
        RemoveExpired();

        if (Count >= BossAlienPhaseOneConfig.MaxCrackCount)
        {
            return;
        }

        normal.Normalize();

        // 원본 그림은 오른쪽으로 퍼진다.
        // 오른쪽 방향을 충돌 면의 안쪽 방향에 맞춘다.
        float angle = Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg;
        Quaternion rotation = Quaternion.Euler(0f, 0f, angle);

        // 보스의 자식으로 만들지 않아 보스가 움직여도 벽에 남는다.
        GameObject visual = Instantiate(
            _crackPrefab,
            new Vector3(position.x, position.y, 0f),
            rotation);

        _cracks.Add(new Crack(
            position,
            normal,
            Time.time + _config.CrackLifetime,
            visual));

        CountChanged?.Invoke(Count);

        if (Count == BossAlienPhaseOneConfig.MaxCrackCount)
        {
            MaxStacksReached?.Invoke();
        }
    }

    // 만료된 균열만 제거한다. 나머지 균열의 만료 시각은 유지한다.
    private void RemoveExpired()
    {
        if (_preserveCracks)
        {
            return;
        }

        bool changed = false;

        for (int i = _cracks.Count - 1; i >= 0; i--)
        {
            if (Time.time < _cracks[i].ExpiresAt)
            {
                continue;
            }

            Destroy(_cracks[i].Visual);
            _cracks.RemoveAt(i);
            changed = true;
        }

        if (changed)
        {
            CountChanged?.Invoke(Count);
        }
    }

    // 대기 중이 아니라 특수패턴이 실제 시작된 시점부터 유지한다.
    private void HandleSpecialPatternState(bool isRunning)
    {
        if (isRunning)
        {
            _preserveCracks = true;
            return;
        }

        // 특수패턴이 끝나면 남아 있던 균열을 모두 제거한다.
        ClearAll();
    }

    // 특수패턴 종료·페이즈 전환·보스 제거 시 사용할 공통 정리다.
    public void ClearAll()
    {
        _preserveCracks = false;

        if (Count == 0)
        {
            return;
        }

        foreach (Crack crack in _cracks)
        {
            if (crack.Visual != null)
            {
                Destroy(crack.Visual);
            }
        }

        _cracks.Clear();
        CountChanged?.Invoke(0);
    }

    private void OnDisable()
    {
        // 씬의 경계에 보스의 이벤트 구독을 남기지 않는다.
        if (_arena != null)
        {
            _arena.ImpactOccurred -= HandleImpact;
            _arena = null;
        }

        if (_boss != null)
        {
            _boss.SpecialPatternStateChanged -= HandleSpecialPatternState;
        }

        ClearAll();
    }

    // 정확히 3개가 모였을 때 공명에 사용할 균열을 확보한다.
    // 예약 이후에는 수명 만료와 중복 예약을 막는다.
    public bool TryReserveResonance()
    {
        if (!isActiveAndEnabled ||
            _boss == null ||
            !_boss.CanRunArmPattern ||
            _preserveCracks ||
            Count != BossAlienPhaseOneConfig.MaxCrackCount)
        {
            return false;
        }

        // 기존 ClearAll에서 이 상태도 함께 해제된다.
        _preserveCracks = true;
        return true;
    }
}
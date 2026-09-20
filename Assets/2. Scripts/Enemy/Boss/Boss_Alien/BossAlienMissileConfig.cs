using System;
using UnityEngine;

// 미사일 패턴의 공통 설정과 소켓별 발사 설정을 저장한다.
[CreateAssetMenu(
    menuName = "Boss/Configs/Alien Missile",
    fileName = "BossAlienMissileConfig")]
public sealed class BossAlienMissileConfig : ScriptableObject
{
    // 생성할 미사일 프리팹의 루트 컴포넌트다.
    [SerializeField] private BossAlienMissile _missilePrefab;

    // 목표 위치에 도착했을 때 생성할 폭발 이펙트다.
    [SerializeField]
    private BossAlienMissileExplosion _explosionPrefab;

    // 미사일 한 발이 폭발할 때 적용할 피해량이다.
    [SerializeField, Min(0)]
    private int _damage = 10;

    // 폭발 중심에서 피해를 적용할 월드 반경이다.
    [SerializeField, Min(0.01f)]
    private float _explosionRadius = 1f;

    // 발사 위치에서 목표 위치까지 이동하는 시간이다.
    [SerializeField, Min(0.01f)]
    private float _flightDuration = 1.5f;

    // 배열 순서가 MissileSocket_0~3에 대응한다.
    // 발사 횟수와 경과 시간 같은 실행 상태는 저장하지 않는다.
    [SerializeField]
    private SocketSettings[] _sockets =
    {
        new SocketSettings(),
        new SocketSettings(),
        new SocketSettings(),
        new SocketSettings()
    };

    // 실행 코드가 읽을 공통 설정이다.
    public BossAlienMissile MissilePrefab => _missilePrefab;
    public BossAlienMissileExplosion ExplosionPrefab => _explosionPrefab;
    public int Damage => _damage;
    public float ExplosionRadius => _explosionRadius;
    public float FlightDuration => _flightDuration;
    public int SocketCount => _sockets.Length;

    // 지정된 소켓의 발사 설정을 반환한다.
    public SocketSettings GetSocketSettings(int socketIndex)
    {
        return _sockets[socketIndex];
    }

    // 소켓 하나의 발사 일정과 곡선 모양을 정의한다.
    [Serializable]
    public sealed class SocketSettings
    {
        // 해치 개방 이벤트를 받은 뒤 첫 발까지 기다릴 시간이다.
        [SerializeField, Min(0f)]
        private float _firstDelay;

        // 이 소켓에서 연속으로 발사하는 두 미사일 사이의 시간이다.
        [SerializeField, Min(0.01f)]
        private float _interval = 0.2f;

        // 이번 패턴에서 이 소켓이 발사할 총수다. 0이면 발사하지 않는다.
        [SerializeField, Min(0)]
        private int _shotCount = 3;

        // 첫 번째 제어점: 발사 위치에 더할 월드 좌표 오프셋이다.
        // X 양수는 오른쪽, Y 양수는 위쪽으로 휘게 한다.
        [SerializeField]
        private Vector2 _startControlOffset = new Vector2(1f, 2f);

        // 두 번째 제어점: 목표 위치에 더할 월드 좌표 오프셋이다.
        // 목표에 접근하는 방향과 곡선의 후반 모양을 결정한다.
        [SerializeField]
        private Vector2 _endControlOffset = new Vector2(3f, 2f);

        // 실행 코드가 읽을 소켓별 설정이다.
        public float FirstDelay => _firstDelay;
        public float Interval => _interval;
        public int ShotCount => _shotCount;
        public Vector2 StartControlOffset => _startControlOffset;
        public Vector2 EndControlOffset => _endControlOffset;
    }
}
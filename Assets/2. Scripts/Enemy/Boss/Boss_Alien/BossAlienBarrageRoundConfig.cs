using System;
using UnityEngine;

// 포격 한 회차의 예고 시간과 미사일별 설정을 보관한다.
// 생성된 장판이나 실행 타이머는 이 에셋에 저장하지 않는다.
[CreateAssetMenu(
    menuName = "Boss/Configs/Alien Barrage Round",
    fileName = "BossAlienBarrageRoundConfig")]
public sealed class BossAlienBarrageRoundConfig : ScriptableObject
{
    // 이 회차의 장판을 추가한 뒤 다음 회차를 예고하기까지의 시간이다.
    // 마지막 회차에서도 이 시간만큼 보여준 후 전체 예고를 끝낸다.
    // 시간이 지나도 장판을 제거하거나 미사일을 발사하지 않는다.
    [SerializeField, Min(0.1f)]
    private float _previewDuration = 1f;

    // 이 회차에 떨어질 미사일 목록이다.
    // Inspector에서 배열 크기를 늘려 미사일을 추가한다.
    [SerializeField]
    private MissileSettings[] _missiles =
    {
        new MissileSettings()
    };

    // 패턴 실행 코드가 읽을 회차 설정이다.
    public float PreviewDuration => _previewDuration;

    // 목록이 없으면 미사일이 없는 회차로 판단한다.
    public int MissileCount => _missiles == null ? 0 : _missiles.Length;

    // 지정한 미사일 한 발의 설정을 반환한다.
    /// <summary>
    /// 지정한 미사일 한 발의 설정을 반환한다.
    /// </summary>
    public MissileSettings GetMissileSettings(int index)
    {
        return _missiles[index];
    }

    // 미사일 한 발의 폭발 위치와 공격·외형 설정이다.
    [Serializable]
    public sealed class MissileSettings
    {
        // 미사일이 도착해 폭발할 월드 X, Y 좌표다.
        // 보스나 플레이어의 위치에 더하는 상대 좌표가 아니다.
        [SerializeField]
        private Vector2 _worldPosition;

        // 폭발 중심에서 실제 피해를 검사할 반경이다.
        // 경고 원도 같은 반경을 사용한다. 지름은 이 값의 두 배다.
        [SerializeField, Min(0.01f)]
        private float _radius = 1f;

        // 이 미사일의 폭발 피해량이다.
        // 같은 회차의 폭발이 겹칠 때 피해를 합산할지는 실행 단계에서 정한다.
        [SerializeField, Min(0)]
        private float _damage = 10;

        // 피해 반경에 맞춘 폭발 외형에 추가로 곱할 크기 배율이다.
        // 1이면 기준 크기, 1.2이면 외형만 20% 커진다.
        // 실제 피해 반경과 경고 원 크기는 바뀌지 않는다.
        [SerializeField, Min(0.01f)]
        private float _explosionVisualScale = 1f;

        // 이 미사일의 외형이다.
        // 생성한 아래 방향 Missile_01~03 중 하나를 연결한다.
        [SerializeField]
        private Sprite _missileSprite;

        // 실행 코드가 읽을 미사일별 설정이다.
        public Vector2 WorldPosition => _worldPosition;
        public float Radius => _radius;
        public float Damage => _damage;
        public float ExplosionVisualScale => _explosionVisualScale;
        public Sprite MissileSprite => _missileSprite;
    }
}
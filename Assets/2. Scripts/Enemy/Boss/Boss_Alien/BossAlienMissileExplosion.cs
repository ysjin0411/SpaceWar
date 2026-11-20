using System;
using UnityEngine;

// 폭발 반경에 맞춰 시각 효과의 크기를 조절한다.
public sealed class BossAlienMissileExplosion : MonoBehaviour
{
    // 폭발 애니메이션이 재생되는 자식 오브젝트다.
    [SerializeField] private Transform _visual;

    // Visual의 Scale이 1일 때 최대 폭발 이미지의 기준 반경이다.
    // 현재 스프라이트는 PPU600에서 지름이 약 1.96유닛이다.
    [SerializeField, Min(0.01f)]
    private float _referenceRadius = 0.98f;

    private void Awake()
    {
        // 크기 변경 대상이 이 프리팹의 자식인지 확인한다.
        if (_visual == null ||
            _visual == transform ||
            !_visual.IsChildOf(transform))
        {
            throw new InvalidOperationException(
                $"{name}: Visual에 폭발 애니메이션 자식을 연결하세요.");
        }

        ValidateRadius(_referenceRadius);
    }

    // 생성 직후 미사일이 Config의 폭발 반경을 전달한다.
    public void Initialize(float explosionRadius)
    {
        ValidateRadius(explosionRadius);

        // 기준 반경에 대한 비율로 이미지 크기를 맞춘다.
        float scale = explosionRadius / _referenceRadius;
        _visual.localScale = new Vector3(scale, scale, 1f);
        GameAudio.Play(GameSound.AlienBarrageBlast); // 폭발 이펙트 생성당 한 번.
    }

    // 잘못된 반경으로 이펙트가 사라지거나 뒤집히는 것을 막는다.
    private static void ValidateRadius(float radius)
    {
        if (float.IsNaN(radius) ||
            float.IsInfinity(radius) ||
            radius <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(radius), "폭발 반경은 0보다 커야 합니다.");
        }
    }
}
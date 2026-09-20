using System;
using UnityEngine;

// 폭발 애니메이션이 끝나면 연결된 이펙트 루트를 제거한다.
public sealed class BossAlienExplosionAnimationEvent : MonoBehaviour
{
    // Visual만 남거나 빈 루트가 남지 않도록
    // 폭발 프리팹의 최상위 오브젝트를 연결한다.
    [SerializeField] private GameObject _effectRoot;

    private void Awake()
    {
        // 잘못된 오브젝트를 제거하지 않도록 연결 관계를 검사한다.
        if (_effectRoot == null ||
            _effectRoot == gameObject ||
            !transform.IsChildOf(_effectRoot.transform))
        {
            throw new InvalidOperationException(
                $"{name}: Effect Root에 폭발 프리팹의 부모 루트를 연결하세요.");
        }
    }

    // 폭발 클립의 마지막 Animation Event에서 호출한다.
    public void NotifyExplosionFinished()
    {
        if (_effectRoot != null)
        {
            Destroy(_effectRoot);
        }
    }
}
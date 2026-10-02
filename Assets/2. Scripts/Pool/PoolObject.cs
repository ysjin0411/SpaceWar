using UnityEngine;
using UnityEngine.Pool;

public sealed class PoolObject : MonoBehaviour
{
    // 이 오브젝트가 돌아갈 원래 풀.
    private IObjectPool<GameObject> _ownerPool;

    // 같은 오브젝트를 두 번 반납하는 일을 방지한다.
    private bool _isBorrowed;

    // 최초 생성 시 소속 풀을 연결한다.
    public void Bind(IObjectPool<GameObject> ownerPool)
    {
        _ownerPool = ownerPool;
    }

    // 풀에서 꺼냈다는 사실을 기록한다.
    public void MarkBorrowed()
    {
        _isBorrowed = true;
    }

    // 사용이 끝난 오브젝트를 원래 풀에 반납한다.
    public void ReturnToPool()
    {
        if (!_isBorrowed)
        {
            return;
        }

        // 비활성화 중 다른 코드가 반납을 요청해도 중복 처리하지 않는다.
        _isBorrowed = false;

        _ownerPool.Release(gameObject);
    }
}
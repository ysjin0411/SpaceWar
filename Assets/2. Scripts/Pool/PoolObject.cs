using UnityEngine;
using UnityEngine.Pool;

public sealed class PoolObject : MonoBehaviour
{
    // 이 오브젝트가 돌아갈 원래 풀.
    private IObjectPool<GameObject> _ownerPool;

    // 같은 오브젝트를 두 번 반납하는 일을 방지한다.
    private bool _isBorrowed;

    // 현재 풀에서 빌려 사용 중인지 외부에서 확인한다.
    public bool IsBorrowed => _isBorrowed;

    // 같은 오브젝트라도 대여할 때마다 다른 번호를 부여한다.
    // 외부에서는 읽기만 가능하고, 번호 변경은 이 클래스에서만 수행한다.
    public ulong BorrowVersion { get; private set; }

    // 최초 생성 시 소속 풀을 연결한다.
    public void Bind(IObjectPool<GameObject> ownerPool)
    {
        _ownerPool = ownerPool;
    }

    // 새로운 대여 번호를 부여하고 사용 중인 상태로 전환한다.
    public void MarkBorrowed()
    {
        // 이전 대여의 참조와 이번 대여를 구분한다.
        // checked는 번호가 범위를 넘을 때 과거 번호로 순환하는 것을 막는다.
        BorrowVersion = checked(BorrowVersion + 1UL);

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
using UnityEngine;

public class Gold : MonoBehaviour
{
    [Header("컴포넌트")]
    [SerializeField] GameObject _currentGold;

    [Header("연출")]
    [SerializeField] float _moveSpeed;

    [Header("데이터 설정")]
    [SerializeField] int _goldAmount;

    Vector3 endPos;

    private void Update()
    {
        Move();
        if (gameObject.transform.position == endPos)
        {
            GetGoldAndDestroy();
        }

    }

    private void Move()
    {
        endPos = _currentGold.transform.position;
        transform.position = Vector3.MoveTowards(transform.position, endPos, _moveSpeed * Time.deltaTime);
    }

    private void GetGoldAndDestroy()
    {
        // 골드가 endPos 도착시 현재 골드량 텍스트가 자연스레 바뀜
        SaveManager.Instance.AddGold(_goldAmount);

        // 소멸
        Destroy(gameObject);
    }

    public void Init(int amount)
    {
        _goldAmount = amount;
    }

}

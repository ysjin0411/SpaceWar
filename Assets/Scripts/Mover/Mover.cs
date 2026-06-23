using UnityEngine;

public class Mover : MonoBehaviour
{
    [Header("이동")]
    [SerializeField] float _moveSpeed = 5f;

    [Header("이동 제한")]
    [SerializeField] float _minX = -8f;
    [SerializeField] float _maxX = 8f;
    [SerializeField] float _minY = -4f;
    [SerializeField] float _maxY = 4f;

    Rigidbody2D _rigid;

    private void Awake()
    {
        _rigid = GetComponent<Rigidbody2D>();
    }

    private void FixedUpdate()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector2 dir = new Vector2(h, v).normalized;

        _rigid.linearVelocity = dir * _moveSpeed;

        Vector2 pos = _rigid.position;
        pos.x = Mathf.Clamp(pos.x, _minX, _maxX);
        pos.y = Mathf.Clamp(pos.y, _minY, _maxY);

        _rigid.position = pos;
    }
}

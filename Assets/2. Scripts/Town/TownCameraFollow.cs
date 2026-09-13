using UnityEngine;

// 플레이어를 따라가면서 카메라 화면 전체를 맵 범위 안에 유지한다.
[RequireComponent(typeof(Camera))]
public class TownCameraFollow : MonoBehaviour
{
    // 따라갈 TownPlayer의 부모 Transform.
    [SerializeField] private Transform _target;

    // 카메라가 벗어나면 안 되는 맵 이미지의 SpriteRenderer.
    [SerializeField] private SpriteRenderer _mapRenderer;

    // 플레이어를 기준으로 화면 중심을 이동시킬 거리.
    [SerializeField] private Vector2 _offset = new Vector2(0f, 1f);

    // 작을수록 플레이어를 빠르게 따라간다.
    [SerializeField, Min(0.01f)]
    private float _smoothTime = 0.12f;

    // 화면 크기를 계산할 카메라.
    private Camera _camera;

    // 부드러운 추적에 사용하는 속도.
    private Vector2 _followVelocity;

    // 카메라의 원래 Z 위치.
    private float _cameraZ;

    // 필수 연결과 카메라 방식을 확인한다.
    private void Awake()
    {
        _camera = GetComponent<Camera>();
        _cameraZ = transform.position.z;

        if (_target == null ||
            _mapRenderer == null ||
            _mapRenderer.sprite == null ||
            !_camera.orthographic)
        {
            Debug.LogError(
                "Target과 Map Renderer를 연결하고, 카메라를 Orthographic으로 설정해주세요.",
                this);

            enabled = false;
        }
    }

    // 시작부터 제한 범위 안의 플레이어 위치에 카메라를 맞춘다.
    private void Start()
    {
        SnapToTarget();
    }

    // 이동 후의 플레이어 위치를 따라가고 화면 경계를 제한한다.
    private void LateUpdate()
    {
        if (_target == null ||
            _mapRenderer == null ||
            _mapRenderer.sprite == null)
        {
            return;
        }

        // Transform 위치와 Scale이 반영된 월드 기준 맵 범위.
        Bounds bounds = _mapRenderer.bounds;

        FitViewInsideMap(bounds);

        // 목표 위치부터 제한해서 맵 바깥을 향해 계속 이동하지 않게 한다.
        Vector2 desiredPosition = ClampPosition(
            (Vector2)_target.position + _offset, bounds);

        Vector2 nextPosition = transform.position;

        if (Time.deltaTime > 0f)
        {
            nextPosition = Vector2.SmoothDamp(
                nextPosition,
                desiredPosition,
                ref _followVelocity,
                _smoothTime);
        }

        // 화면 비율이나 Size가 바뀌면 현재 위치도 범위 밖이 될 수 있다.
        // 보간 결과를 한 번 더 제한해 이를 방지한다.
        Vector2 clampedPosition = ClampPosition(nextPosition, bounds);

        if (clampedPosition.x != nextPosition.x)
        {
            _followVelocity.x = 0f;
        }

        if (clampedPosition.y != nextPosition.y)
        {
            _followVelocity.y = 0f;
        }

        transform.position = new Vector3(
            clampedPosition.x, clampedPosition.y, _cameraZ);
    }

    // 화면 자체가 맵보다 크면 위치 제한만으로는 해결할 수 없다.
    // 이 경우에만 카메라 Size를 맵 안에 들어오는 크기로 줄인다.
    private void FitViewInsideMap(Bounds bounds)
    {
        float maximumSize = Mathf.Min(
            bounds.extents.y,
            bounds.extents.x / _camera.aspect);

        _camera.orthographicSize = Mathf.Min(
            _camera.orthographicSize, maximumSize);
    }

    // 화면 가장자리가 맵을 넘지 않도록 카메라 중심을 제한한다.
    private Vector2 ClampPosition(Vector2 position, Bounds bounds)
    {
        // Orthographic Size는 화면의 세로 절반 길이다.
        float halfHeight = _camera.orthographicSize;
        float halfWidth = halfHeight * _camera.aspect;

        float minimumX = bounds.min.x + halfWidth;
        float maximumX = bounds.max.x - halfWidth;
        float minimumY = bounds.min.y + halfHeight;
        float maximumY = bounds.max.y - halfHeight;

        // 화면과 맵 크기가 같거나 미세한 오차로 범위가 뒤집히면
        // 해당 축은 맵 중심에 고정한다.
        position.x = minimumX >= maximumX
            ? bounds.center.x
            : Mathf.Clamp(position.x, minimumX, maximumX);

        position.y = minimumY >= maximumY
            ? bounds.center.y
            : Mathf.Clamp(position.y, minimumY, maximumY);

        return position;
    }

    // 시작하거나 포탈로 이동한 뒤 카메라 위치를 즉시 맞춘다.
    public void SnapToTarget()
    {
        if (_target == null ||
            _mapRenderer == null ||
            _mapRenderer.sprite == null ||
            _camera == null)
        {
            return;
        }

        Bounds bounds = _mapRenderer.bounds;

        FitViewInsideMap(bounds);

        Vector2 position = ClampPosition(
            (Vector2)_target.position + _offset, bounds);

        _followVelocity = Vector2.zero;
        transform.position = new Vector3(
            position.x, position.y, _cameraZ);
    }
}
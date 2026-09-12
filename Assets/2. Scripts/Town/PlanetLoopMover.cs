using UnityEngine;

// 행성을 오른쪽으로 일정 시간 이동시킨 뒤 시작점으로 되돌린다.
public class PlanetLoopMover : MonoBehaviour
{
    // 반복 이동을 시작할 위치. 부모가 있다면 부모 기준 좌표다.
    [SerializeField] private Vector2 _startPosition = new Vector2(-36f, 4f);

    // 한 번의 반복에서 X축 양의 방향으로 이동할 거리.
    [SerializeField, Min(0f)] private float _moveDistance = 72f;

    // 위 거리를 이동하는 데 걸리는 시간(초).
    [SerializeField, Min(0.01f)] private float _duration = 240f;

    // 현재 반복에서 경과한 시간.
    private float _elapsedTime;

    // 기존 Z 위치는 이동 중에도 유지한다.
    private float _localZ;

    // 활성화되면 시작 위치에서 새 반복을 시작한다.
    private void OnEnable()
    {
        _elapsedTime = 0f;
        _localZ = transform.localPosition.z;

        ApplyPosition(0f);
    }

    // 프레임 속도와 무관하게 일정한 속도로 이동한다.
    private void Update()
    {
        // Inspector 값이 잘못 들어와도 0으로 나누지 않도록 보장한다.
        float duration = Mathf.Max(0.01f, _duration);

        // 시간이 duration을 넘으면 초과분을 남겨 다음 반복으로 이어간다.
        _elapsedTime = Mathf.Repeat(
            _elapsedTime + Time.deltaTime,
            duration);

        // 진행률: 0은 시작점, 1은 끝점이다.
        float progress = _elapsedTime / duration;

        ApplyPosition(progress);
    }

    // X만 증가시키고 Y와 Z는 고정한다.
    private void ApplyPosition(float progress)
    {
        float distance = Mathf.Max(0f, _moveDistance);

        transform.localPosition = new Vector3(
            _startPosition.x + distance * progress,
            _startPosition.y,
            _localZ);
    }
}
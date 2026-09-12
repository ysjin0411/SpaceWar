using UnityEngine;

// 카메라 위에서 Meteor를 시간차로 출발시키고 재사용한다.
public class TownMeteorSpawner : MonoBehaviour
{
    // 유성 생성·회수 범위로 사용할 맵 그림의 SpriteRenderer.
    [SerializeField] private SpriteRenderer _mapRenderer;

    // Project 창에 저장한 Meteor 프리팹의 SpriteRenderer.
    [SerializeField] private SpriteRenderer _meteorPrefab;

    // 미리 준비할 Meteor 수. 동시에 표시할 수 있는 최대 개수이기도 하다.
    [SerializeField, Min(1)] private int _poolSize = 8;

    // 다음 Meteor가 출발하기까지의 최소 시간과 최대 시간.
    [SerializeField] private Vector2 _spawnInterval = new Vector2(1f, 3f);

    // 초당 이동 거리.
    [SerializeField, Min(0.01f)] private float _moveSpeed = 4f;

    // 오른쪽이 0도, 아래쪽이 -90도, 왼쪽 아래가 약 -145도다.
    [SerializeField, Range(-170f, -10f)]
    private float _fallAngle = -145f;

    // 카메라 위쪽 경계와 Meteor 사이에 확보할 여유 거리.
    [SerializeField, Min(0f)] private float _spawnMargin = 0.5f;

    // 화면 아래쪽이나 진행 방향의 옆 경계를 벗어난 뒤 회수할 여유 거리.
    [SerializeField, Min(0f)] private float _despawnMargin = 1f;

    // 원본 그림에서 Meteor 머리가 향하는 방향.
    private const float SpriteHeadingAngle = -145f;

    // 생성한 Meteor와 각각의 이동 속도를 보관한다.
    private SpriteRenderer[] _meteors;
    private Vector3[] _velocities;

    // 다음 출발까지 남은 시간.
    private float _remainingTime;

    // 사용 중인 Meteor를 반복 생성·파괴하지 않도록 미리 준비한다.
    private void Awake()
    {
        if (_mapRenderer == null ||
            _mapRenderer.sprite == null ||
            _meteorPrefab == null ||
            _meteorPrefab.sprite == null)
        {
            Debug.LogError(
                "TownMeteorSpawner의 Map Renderer와 Meteor Prefab을 연결해주세요.",
                this);

            enabled = false;
            return;
        }

        int count = Mathf.Max(1, _poolSize);
        _meteors = new SpriteRenderer[count];
        _velocities = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            SpriteRenderer meteor = Instantiate(_meteorPrefab, transform);
            meteor.name = $"Meteor_{i + 1}";
            meteor.gameObject.SetActive(false);

            _meteors[i] = meteor;
        }
    }

    // 다시 활성화해도 여러 개가 즉시 출발하지 않도록 대기 시간을 설정한다.
    private void OnEnable()
    {
        _remainingTime = GetNextInterval();
    }

    // 사용 중인 Meteor를 이동시키고, 시간이 되면 한 개를 출발시킨다.
    private void Update()
    {
        // 일시정지 중에는 이동과 생성 모두 멈춘다.
        if (Time.deltaTime <= 0f)
        {
            return;
        }

        MoveMeteors();

        _remainingTime -= Time.deltaTime;

        if (_remainingTime > 0f)
        {
            return;
        }

        // while로 밀린 생성을 처리하지 않아 한 프레임에 여러 개가 나오지 않는다.
        SpawnOne();
        _remainingTime = GetNextInterval();
    }

    // 최소값과 최대값 순서가 바뀌어도 정상적인 시간 범위를 만든다.
    private float GetNextInterval()
    {
        float min = Mathf.Max(
            0.05f, Mathf.Min(_spawnInterval.x, _spawnInterval.y));

        float max = Mathf.Max(
            min, Mathf.Max(_spawnInterval.x, _spawnInterval.y));

        return Random.Range(min, max);
    }

    // 쉬고 있는 Meteor 하나를 맵 위쪽의 랜덤한 위치에서 출발시킨다.
    private void SpawnOne()
    {
        for (int i = 0; i < _meteors.Length; i++)
        {
            SpriteRenderer meteor = _meteors[i];

            if (meteor.gameObject.activeSelf)
            {
                continue;
            }

            float angle = Mathf.Clamp(_fallAngle, -170f, -10f);
            float radians = angle * Mathf.Deg2Rad;

            Vector3 direction = new Vector3(
                Mathf.Cos(radians),
                Mathf.Sin(radians),
                0f);

            _velocities[i] = direction * Mathf.Max(0.01f, _moveSpeed);

            // 그림의 머리 방향과 실제 이동 방향을 맞춘다.
            meteor.transform.rotation = Quaternion.Euler(
                0f, 0f, angle - SpriteHeadingAngle);

            // 맵의 위치와 Scale이 반영된 실제 월드 영역을 가져온다.
            Bounds mapBounds = _mapRenderer.bounds;

            // 맵 전체 너비에서 출발 위치를 선택한다.
            float x = Random.Range(
                mapBounds.min.x,
                mapBounds.max.x);

            meteor.transform.position = new Vector3(
                x,
                mapBounds.max.y,
                transform.position.z);

            meteor.gameObject.SetActive(true);

            // Meteor 전체가 맵 위쪽 바깥에서 출발하도록 보정한다.
            Bounds meteorBounds = meteor.bounds;
            float targetBottom = mapBounds.max.y + _spawnMargin;

            meteor.transform.position +=
                Vector3.up * (targetBottom - meteorBounds.min.y);

            return;
        }

        // 모두 사용 중이면 이번 출발은 건너뛰고 다음 시간을 기다린다.
    }

    // Meteor를 이동시키고 맵을 완전히 벗어나면 회수한다.
    private void MoveMeteors()
    {
        Bounds mapBounds = _mapRenderer.bounds;

        float bottom = mapBounds.min.y - _despawnMargin;
        float left = mapBounds.min.x - _despawnMargin;
        float right = mapBounds.max.x + _despawnMargin;

        for (int i = 0; i < _meteors.Length; i++)
        {
            SpriteRenderer meteor = _meteors[i];

            if (!meteor.gameObject.activeSelf)
            {
                continue;
            }

            meteor.transform.position +=
                _velocities[i] * Time.deltaTime;

            Bounds meteorBounds = meteor.bounds;

            // Meteor 전체가 맵 아래쪽을 벗어났는지 확인한다.
            bool belowMap = meteorBounds.max.y < bottom;

            // 진행 방향의 옆 경계를 완전히 벗어났는지 확인한다.
            bool passedSide =
                (_velocities[i].x < 0f && meteorBounds.max.x < left) ||
                (_velocities[i].x > 0f && meteorBounds.min.x > right);

            if (belowMap || passedSide)
            {
                meteor.gameObject.SetActive(false);
            }
        }
    }

    // 연출을 끌 때 사용 중이던 Meteor도 함께 정리한다.
    private void OnDisable()
    {
        if (_meteors == null)
        {
            return;
        }

        for (int i = 0; i < _meteors.Length; i++)
        {
            if (_meteors[i] != null)
            {
                _meteors[i].gameObject.SetActive(false);
            }
        }
    }
}
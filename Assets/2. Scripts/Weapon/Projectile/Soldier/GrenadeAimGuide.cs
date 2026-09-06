using System;
using UnityEngine;

/// <summary>
/// Grenade를 현재 무기로 선택한 동안
/// 부채꼴 각도 안을 왕복하며 점 형태의 포물선 궤적을 표시한다.
/// </summary>
public class GrenadeAimGuide : MonoBehaviour
{
    [Header("조준 기준점")]

    // 실제 수류탄과 조준 점이 시작되는 위치다.
    [SerializeField] Transform _firePoint;

    [Header("부채꼴 왕복 설정")]

    // 수류탄이 아래쪽으로 직접 발사되지 않도록
    // 부채꼴의 아래쪽 각도도 위를 향하게 설정한다.
    [SerializeField] float _minimumAngle = 20f;

    // 부채꼴 위쪽 반지름에 해당하는 최대 발사 각도다.
    [SerializeField] float _maximumAngle = 70f;

    // 최소 각도에서 최대 각도까지 이동하는 시간이다.
    [Min(0.01f)]
    [SerializeField] float _oneWaySweepDuration = 1.2f;

    [Header("점 형태 포물선")]

    // 궤적에 표시할 점의 원본 SpriteRenderer다.
    // Awake에서 필요한 개수만큼 복제한 뒤 계속 재사용한다.
    [SerializeField] SpriteRenderer _trajectoryDotTemplate;

    // 포물선에 표시할 점 개수다.
    [Min(3)]
    [SerializeField] int _trajectoryDotCount = 9;

    // 조준 방향의 초기 속도 기준으로 표시할 거리다.
    // 실제 폭발 지점까지 표시하지 않고 앞부분에서 끊는다.
    [Min(0.1f)]
    [SerializeField] float _previewDistance = 3.5f;

    // 실제 수류탄이 중력 없이 직진하는 시간이다.
    float _straightFlightDuration;

    // 적과 먼저 충돌하는 위치를 찾기 위한 레이어다.
    [SerializeField] LayerMask _trajectoryStopLayerMask;

    // 시작점에 가까운 점의 투명도다.
    [Range(0f, 1f)]
    [SerializeField] float _startDotAlpha = 0.9f;

    // 착탄점에 가까운 점의 투명도다.
    [Range(0f, 1f)]
    [SerializeField] float _endDotAlpha = 0.25f;

    // 시작할 때 한 번 생성하고 계속 재사용하는 궤적 점 배열이다.
    SpriteRenderer[] _trajectoryDots;

    // Grenade 프리팹에서 읽어온 실제 투척 속도다.
    float _launchSpeed;

    // Grenade Rigidbody2D에서 읽어온 실제 중력 배율이다.
    float _gravityScale;

    // 수류탄이 자동 폭발하기까지의 시간이다.
    float _fuseTime;

    // 부채꼴 왕복에 사용한 누적 시간이다.
    float _sweepTimer;

    // 현재 Grenade 조준선을 표시하고 있는지 확인한다.
    bool _isVisible;

    // Z키를 누른 순간 Grenade에 전달할 현재 발사 방향이다.
    public Vector2 CurrentDirection { get; private set; } =
        Vector2.right;

    private void Awake()
    {
        ValidateReferences();
        CreateTrajectoryDots();

        // Grenade가 선택되기 전에는 모든 표시를 숨긴다.
        Hide();
    }

    private void Update()
    {
        if (!_isVisible)
        {
            return;
        }

        // 게임 시간이 멈추면 조준선 왕복도 멈춘다.
        if (Time.timeScale <= 0f)
        {
            return;
        }

        _sweepTimer += Time.deltaTime;

        UpdateCurrentDirection();
        DrawTrajectoryDots();
    }

    /// <summary>
    /// Grenade를 선택했을 때 실제 프리팹의 물리값을 받아
    /// 조준점 표시를 시작한다.
    /// </summary>
    public void Show(Grenade grenadePrefab)
    {
        if (grenadePrefab == null)
        {
            throw new ArgumentNullException(
                nameof(grenadePrefab),
                "조준선을 표시하려면 Grenade 프리팹이 필요합니다.");
        }

        _launchSpeed =
            grenadePrefab.LaunchSpeed;

        _gravityScale =
            grenadePrefab.GravityScale;

        _fuseTime =
            grenadePrefab.FuseTime;

        // 실제 수류탄과 동일한 거리와 속도로 중력 시작 시간을 계산한다.
        if (_launchSpeed <= 0f)
        {
            throw new InvalidOperationException(
                "Grenade의 Launch Speed는 0보다 커야 합니다.");
        }

        _straightFlightDuration =
            grenadePrefab.GravityStartDistance / _launchSpeed;

        // Grenade를 선택할 때 최소 각도부터 왕복을 시작한다.
        _sweepTimer = 0f;
        _isVisible = true;

        UpdateCurrentDirection();
        DrawTrajectoryDots();
    }

    /// <summary>
    /// Grenade가 아닌 무기로 교체하면
    /// 궤적 점과 착탄 마커를 모두 숨긴다.
    /// </summary>
    public void Hide()
    {
        _isVisible = false;

        if (_trajectoryDots != null)
        {
            for (int index = 0;
                 index < _trajectoryDots.Length;
                 index++)
            {
                _trajectoryDots[index].gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 표시할 점을 게임 시작 때 한 번만 생성한다.
    /// 조준 방향이 바뀔 때는 생성하지 않고 위치만 옮긴다.
    /// </summary>
    private void CreateTrajectoryDots()
    {
        _trajectoryDots =
            new SpriteRenderer[_trajectoryDotCount];

        for (int index = 0;
             index < _trajectoryDotCount;
             index++)
        {
            SpriteRenderer trajectoryDot =
                Instantiate(
                    _trajectoryDotTemplate,
                    transform);

            trajectoryDot.name =
                $"TrajectoryDot_{index + 1}";

            trajectoryDot.gameObject.SetActive(false);

            _trajectoryDots[index] =
                trajectoryDot;
        }

        // 복제용 원본은 게임 화면에 표시하지 않는다.
        _trajectoryDotTemplate.gameObject.SetActive(false);
    }

    /// <summary>
    /// 최소 각도와 최대 각도 사이를 왕복하며
    /// 현재 수류탄 발사 방향을 계산한다.
    /// </summary>
    private void UpdateCurrentDirection()
    {
        float sweepProgress = Mathf.PingPong(
            _sweepTimer / _oneWaySweepDuration,
            1f);

        float currentAngle = Mathf.Lerp(
            _minimumAngle,
            _maximumAngle,
            sweepProgress);

        Vector3 rotatedDirection =
            Quaternion.Euler(
                0f,
                0f,
                currentAngle) *
            _firePoint.right;

        CurrentDirection =
            ((Vector2)rotatedDirection).normalized;
    }

    /// <summary>
    /// 실제 궤적의 앞부분만 짧게 표시한다.
    /// 적 뒤쪽 점은 숨기고, 탄착 마커는 표시하지 않는다.
    /// </summary>
    private void DrawTrajectoryDots()
    {
        Vector2 startPosition = _firePoint.position;
        Vector2 initialVelocity = CurrentDirection * _launchSpeed;
        Vector2 gravity = Physics2D.gravity * _gravityScale;

        // 표시 거리를 시간으로 변환한다.
        // 신관 이후의 궤적은 표시하지 않는다.
        float previewTime = Mathf.Min(
            _previewDistance / _launchSpeed,
            _fuseTime);

        Vector2 previousPosition = startPosition;

        for (int index = 0;
             index < _trajectoryDots.Length;
             index++)
        {
            // 시작점부터 표시 끝까지 일정 시간 간격으로 배치한다.
            float progress =
                (index + 1f) / _trajectoryDots.Length;

            float elapsedTime = progress * previewTime;

            Vector2 nextPosition = CalculateTrajectoryPosition(
                startPosition,
                initialVelocity,
                gravity,
                elapsedTime);

            // 표시 구간 안에 적이 있으면 그 뒤쪽 점은 숨긴다.
            RaycastHit2D hit = Physics2D.Linecast(
                previousPosition,
                nextPosition,
                _trajectoryStopLayerMask);

            if (hit.collider != null)
            {
                HideDotsFrom(index);
                break;
            }

            SpriteRenderer dot = _trajectoryDots[index];
            dot.transform.position = nextPosition;

            // 마지막 점으로 갈수록 흐려져 자연스럽게 끊어진다.
            Color dotColor = dot.color;
            dotColor.a = Mathf.Lerp(
                _startDotAlpha,
                _endDotAlpha,
                progress);

            dot.color = dotColor;
            dot.gameObject.SetActive(true);

            previousPosition = nextPosition;
        }
    }

    /// <summary>
    /// 직진 구간 이후에만 중력에 의한 위치 변화를 더한다.
    /// Rigidbody2D 물리 궤적의 예상 위치를 계산한다.
    /// </summary>
    private Vector2 CalculateTrajectoryPosition(
        Vector2 startPosition,
        Vector2 initialVelocity,
        Vector2 gravity,
        float elapsedTime)
    {
        // 직진 시간이 끝나기 전에는 낙하 시간이 0이다.
        float fallingTime = Mathf.Max(
            0f,
            elapsedTime - _straightFlightDuration);

        // 초기 속도 이동은 계속 유지하고,
        // 중력이 시작된 이후의 낙하만 추가한다.
        return startPosition
            + initialVelocity * elapsedTime
            + gravity * (0.5f * fallingTime * fallingTime);
    }

    /// <summary>
    /// 적과 충돌한 지점 뒤에 있는 궤적 점들을 숨긴다.
    /// </summary>
    private void HideDotsFrom(
        int startIndex)
    {
        for (int index = startIndex;
             index < _trajectoryDots.Length;
             index++)
        {
            _trajectoryDots[index].gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Inspector 연결 상태와 점 개수를 확인한다.
    /// </summary>
    private void ValidateReferences()
    {
        if (_firePoint == null)
        {
            throw new InvalidOperationException(
                $"{name}의 GrenadeAimGuide에 FirePoint가 연결되지 않았습니다.");
        }

        if (_trajectoryDotTemplate == null)
        {
            throw new InvalidOperationException(
                $"{name}의 GrenadeAimGuide에 Trajectory Dot Template이 연결되지 않았습니다.");
        }

        if (_trajectoryDotCount < 3)
        {
            throw new InvalidOperationException(
                $"{name}의 Trajectory Dot Count는 3 이상이어야 합니다.");
        }
    }
}
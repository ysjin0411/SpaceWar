using System;
using UnityEngine;

/// <summary>
/// Grenade를 현재 무기로 선택한 동안
/// 부채꼴 각도 안을 왕복하며 점 형태의 직선 궤적을 표시한다.
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

    [Header("점 형태 직선")]

    // 궤적에 표시할 점의 원본 SpriteRenderer다.
    // Awake에서 필요한 개수만큼 복제한 뒤 계속 재사용한다.
    [SerializeField] SpriteRenderer _trajectoryDotTemplate;

    // 직선에 표시할 점 개수다.
    [Min(3)]
    [SerializeField] int _trajectoryDotCount = 9;

    // 조준 방향의 초기 속도 기준으로 표시할 거리다.
    // 실제 폭발 지점까지 표시하지 않고 앞부분에서 끊는다.
    [Min(0.1f)]
    [SerializeField] float _previewDistance = 3.5f;


    // 적과 먼저 충돌하는 위치를 찾기 위한 레이어다.
    [SerializeField] LayerMask _trajectoryStopLayerMask;

    // 시작점에 가까운 점의 투명도다.
    [Range(0f, 1f)]
    [SerializeField] float _startDotAlpha = 0.9f;

    // 착탄점에 가까운 점의 투명도다.
    [Range(0f, 1f)]
    [SerializeField] float _endDotAlpha = 0.25f;

    // 현재 플레이어의 좌우 방향을 확인한다.
    PlayerFacing _playerFacing;

    // 계산된 수류탄 발사 방향을 보관한다.
    Vector2 _currentDirection = Vector2.right;

    // 시작할 때 한 번 생성하고 계속 재사용하는 궤적 점 배열이다.
    SpriteRenderer[] _trajectoryDots;




    // 부채꼴 왕복에 사용한 누적 시간이다.
    float _sweepTimer;

    // 현재 Grenade 조준선을 표시하고 있는지 확인한다.
    bool _isVisible;

    // 발사 순간 다시 계산하여 같은 프레임의 방향 전환까지 반영한다.
    public Vector2 CurrentDirection
    {
        get
        {
            UpdateCurrentDirection();
            return _currentDirection;
        }
    }

    private void Awake()
    {
        // GrenadeAimGuide는 캐릭터의 자식이므로 부모에서 방향 정보를 찾는다.
        _playerFacing = GetComponentInParent<PlayerFacing>();

        if (_playerFacing == null)
        {
            throw new MissingComponentException(
                $"{name}: 부모 플레이어에 PlayerFacing이 필요합니다.");
        }

        // 기존 조준선 초기화는 유지한다.
        ValidateReferences();
        CreateTrajectoryDots();
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
    /// Grenade를 선택했을 때 프리팹 연결을 확인하고
    /// 조준점 표시를 시작한다.
    /// </summary>
    public void Show(Grenade grenadePrefab)
    {
        // Weapon의 기존 호출 형식은 유지하며 프리팹 누락을 검사한다.
        if (grenadePrefab == null) throw new ArgumentNullException(nameof(grenadePrefab));
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

    // 오른쪽과 왼쪽 모두 위쪽을 향하는 수류탄 조준 방향을 계산한다.
    private void UpdateCurrentDirection()
    {
        float sweepProgress = Mathf.PingPong(
            _sweepTimer / _oneWaySweepDuration,
            1f);

        float currentAngle = Mathf.Lerp(
            _minimumAngle,
            _maximumAngle,
            sweepProgress);

        // 오른쪽에서는 반시계 방향, 왼쪽에서는 시계 방향으로 올린다.
        // 따라서 좌우 전환 후에도 위로 던지는 각도가 유지된다.
        if (_playerFacing.IsFacingLeft)
        {
            currentAngle = -currentAngle;
        }

        Vector3 rotatedDirection =
            Quaternion.Euler(0f, 0f, currentAngle) * _firePoint.right;

        _currentDirection = ((Vector2)rotatedDirection).normalized;
    }

    /// <summary>
    /// 실제 궤적의 앞부분만 짧게 표시한다.
    /// 적 뒤쪽 점은 숨기고, 탄착 마커는 표시하지 않는다.
    /// </summary>
    private void DrawTrajectoryDots()
    {
        // 시간과 중력 없이 방향과 표시 거리만 사용한다.
        Vector2 start = _firePoint.position;
        Vector2 direction = CurrentDirection;
        Vector2 previous = start;
        for (int index = 0; index < _trajectoryDots.Length; index++)
        {
            float progress = (index + 1f) / _trajectoryDots.Length;
            Vector2 next = start + direction * (_previewDistance * progress);
            // 앞에 Collider가 있으면 그 뒤쪽 점은 숨긴다.
            RaycastHit2D hit = Physics2D.Linecast(previous, next, _trajectoryStopLayerMask);
            if (hit.collider != null)
            {
                HideDotsFrom(index);
                break;
            }
            SpriteRenderer dot = _trajectoryDots[index];
            dot.transform.position = next;
            Color color = dot.color;
            color.a = Mathf.Lerp(_startDotAlpha, _endDotAlpha, progress);
            dot.color = color;
            dot.gameObject.SetActive(true);
            previous = next;
        }
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
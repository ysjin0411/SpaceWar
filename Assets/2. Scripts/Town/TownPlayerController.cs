using UnityEngine;

// 마을 캐릭터는 몸체 하나와 캡슐 충돌체 하나로 이동한다.
[RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
public class TownPlayerController : MonoBehaviour
{
    // 자식 Visual의 SpriteRenderer. 이동 방향에 따라 그림을 뒤집는다.
    [SerializeField] private SpriteRenderer _visual;

    // 착지할 수 있는 레이어. TownGround와 TownPlatform을 함께 지정한다.
    [SerializeField] private LayerMask _groundLayers;

    // 아래점프로 통과할 수 있는 레이어. TownPlatform만 지정한다.
    [SerializeField] private LayerMask _dropThroughLayers;

    // 초당 좌우 이동 거리.
    [SerializeField] private float _moveSpeed = 5f;

    // 점프 순간의 위쪽 속도.
    [SerializeField] private float _jumpSpeed = 10f;

    // 아래점프를 시작할 때의 아래쪽 속력.
    [SerializeField] private float _dropSpeed = 3f;

    // 발밑의 바닥을 검사할 거리.
    [SerializeField] private float _groundCheckDistance = 0.05f;

    // 걸어서 올라갈 수 있는 최대 경사각. 현재 계단은 약 45도다.
    [SerializeField, Range(0f, 80f)]
    private float _maxSlopeAngle = 50f;

    // 몸 전체가 발판 아래로 빠진 뒤 충돌을 복구하기 위한 여유 거리.
    [SerializeField] private float _dropClearance = 0.05f;

    // 실제 이동과 충돌에 사용하는 컴포넌트.
    private Rigidbody2D _body;
    private CapsuleCollider2D _bodyCollider;

    // 바닥 검사 조건과 결과 배열을 재사용한다.
    private ContactFilter2D _groundFilter;
    private readonly RaycastHit2D[] _groundHits = new RaycastHit2D[16];

    // 현재 발밑의 바닥과 아래점프로 통과 중인 발판.
    private Collider2D _groundCollider;
    private Collider2D _ignoredPlatform;

    // 현재 바닥의 수직 방향. 이 값으로 경사면의 이동 방향을 구한다.
    private Vector2 _groundNormal = Vector2.up;

    // 공중에 있거나 스크립트가 꺼지면 복원할 원래 중력값.
    private float _defaultGravityScale;

    // 실제 점프로 상승하는 상태와 계단을 걸어 올라가는 상태를 구분한다.
    private bool _isJumpAscending;

    // Update에서 읽고 FixedUpdate에서 사용할 입력.
    private float _horizontalInput;
    private bool _jumpRequested;
    private bool _dropRequested;

    // 이후 NPC 대화창에서 조작을 잠글 때 사용한다.
    private bool _canControl = true;

    // 필요한 컴포넌트와 원래 중력값, 바닥 검사 조건을 준비한다.
    private void Awake()
    {
        _body = GetComponent<Rigidbody2D>();
        _bodyCollider = GetComponent<CapsuleCollider2D>();

        // 착지 중에는 중력을 끄므로 공중에서 사용할 값을 보관한다.
        _defaultGravityScale = _body.gravityScale;

        if (_visual == null)
        {
            Debug.LogError(
                "TownPlayerController의 Visual을 연결해주세요.", this);

            enabled = false;
            return;
        }

        _groundFilter = new ContactFilter2D();
        _groundFilter.SetLayerMask(_groundLayers);
        _groundFilter.useTriggers = false;
    }

    // 버튼을 누른 순간을 놓치지 않도록 입력은 Update에서 읽는다.
    private void Update()
    {
        if (!_canControl || Time.timeScale == 0f)
        {
            ClearInput();
            return;
        }

        _horizontalInput = Input.GetAxisRaw("Horizontal");

        if (_horizontalInput != 0f)
        {
            _visual.flipX = _horizontalInput < 0f;
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            _jumpRequested = true;

            // C를 누른 순간 아래 방향도 누르고 있었는지 저장한다.
            _dropRequested =
                Input.GetKey(KeyCode.S) ||
                Input.GetKey(KeyCode.DownArrow);
        }
    }

    // 바닥 상태에 맞춰 이동, 점프, 아래점프를 처리한다.
    private void FixedUpdate()
    {
        RestoreCollisionAfterPassing();
        FindGround();

        float horizontalSpeed =
            _canControl ? _horizontalInput * _moveSpeed : 0f;

        Vector2 velocity = _body.linearVelocity;

        if (_groundCollider != null)
        {
            // 바닥 법선을 90도 돌려 오른쪽을 향하는 표면 방향을 만든다.
            // 평지에서는 (1, 0), 계단에서는 경사를 따라가는 방향이 된다.
            Vector2 groundDirection =
                new Vector2(_groundNormal.y, -_groundNormal.x);

            // 표면을 따라 일정한 속도로 이동한다.
            // 입력이 없으면 X와 Y속도가 모두 0이 된다.
            velocity = groundDirection * horizontalSpeed;
        }
        else
        {
            // 공중에서는 좌우만 조절하고 점프·낙하 Y속도는 유지한다.
            velocity.x = horizontalSpeed;
        }

        if (_canControl &&
            _jumpRequested &&
            _groundCollider != null)
        {
            if (_dropRequested)
            {
                // 아래점프 가능한 발판에서만 충돌을 잠시 무시한다.
                if (_ignoredPlatform == null &&
                    IsDropThroughPlatform(_groundCollider))
                {
                    _ignoredPlatform = _groundCollider;

                    Physics2D.IgnoreCollision(
                        _bodyCollider, _ignoredPlatform, true);

                    velocity = new Vector2(horizontalSpeed, -_dropSpeed);
                    _groundCollider = null;
                    _isJumpAscending = false;
                }
            }
            else
            {
                // 일반 점프는 경사와 관계없이 위쪽으로 출발한다.
                velocity = new Vector2(horizontalSpeed, _jumpSpeed);
                _groundCollider = null;
                _isJumpAscending = true;
            }
        }

        // 바닥에서는 중력에 의한 경사면 미끄러짐을 막는다.
        // 점프·아래점프·낙하 중에는 원래 중력을 즉시 복원한다.
        _body.gravityScale =
            _groundCollider != null ? 0f : _defaultGravityScale;

        _body.linearVelocity = velocity;

        // 입력 한 번을 여러 물리 프레임에서 중복 처리하지 않는다.
        _jumpRequested = false;
        _dropRequested = false;
    }

    // 캐릭터 아래의 바닥과 그 바닥의 경사 방향을 찾는다.
    private void FindGround()
    {
        _groundCollider = null;
        _groundNormal = Vector2.up;

        // 실제 점프로 상승하는 동안에는 가까운 바닥에 다시 붙지 않는다.
        // 계단을 걸어 올라갈 때는 이 상태가 아니므로 검사를 계속한다.
        if (_isJumpAscending)
        {
            if (_body.linearVelocity.y > 0f)
            {
                return;
            }

            // 점프 정점에 도달하거나 천장에 막혔다면 착지 검사를 재개한다.
            _isJumpAscending = false;
        }

        int hitCount = _bodyCollider.Cast(
            Vector2.down,
            _groundFilter,
            _groundHits,
            _groundCheckDistance);

        float nearestDistance = float.PositiveInfinity;

        // 바닥의 법선 Y값이 이 값 이상이면 허용된 경사각 안에 있다.
        float minimumNormalY =
            Mathf.Cos(_maxSlopeAngle * Mathf.Deg2Rad);

        Bounds bounds = _bodyCollider.bounds;

        // 현재 세로형 캡슐의 아래쪽 둥근 부분까지 바닥 접촉으로 허용한다.
        // 경사면에서는 발끝보다 조금 높은 옆부분이 먼저 닿기 때문이다.
        float maximumGroundPointY =
            bounds.min.y + bounds.extents.x + _groundCheckDistance;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = _groundHits[i];

            // 아래점프로 통과 중인 발판은 바닥으로 선택하지 않는다.
            if (hit.collider == null ||
                hit.collider == _ignoredPlatform)
            {
                continue;
            }

            // 너무 가파른 벽과 몸통 높이에서 만난 표면은 제외한다.
            if (hit.normal.y < minimumNormalY ||
                hit.point.y > maximumGroundPointY)
            {
                continue;
            }

            // 가장 가까운 바닥의 콜라이더와 법선을 함께 저장한다.
            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                _groundCollider = hit.collider;
                _groundNormal = hit.normal;
            }
        }
    }

    // 레이어와 Effector 설정을 모두 확인한다.
    private bool IsDropThroughPlatform(Collider2D platform)
    {
        int layerBit = 1 << platform.gameObject.layer;

        if ((_dropThroughLayers.value & layerBit) == 0 ||
            !platform.usedByEffector)
        {
            return false;
        }

        PlatformEffector2D effector =
            platform.GetComponent<PlatformEffector2D>();

        return effector != null &&
               effector.enabled &&
               effector.useOneWay;
    }

    // 평평한 발판과 경사진 계단 모두 실제 표면을 통과하면 충돌을 복원한다.
    private void RestoreCollisionAfterPassing()
    {
        if (_ignoredPlatform == null)
        {
            return;
        }

        // 통과 중이던 발판이 꺼졌다면 무시 상태를 정리한다.
        if (!_ignoredPlatform.isActiveAndEnabled)
        {
            RestoreIgnoredCollision();
            return;
        }

        // A는 캐릭터, B는 통과 중인 발판이다.
        ColliderDistance2D separation =
            _bodyCollider.Distance(_ignoredPlatform);

        // 유효하지 않은 검사 결과로 충돌을 복원하지 않는다.
        if (!separation.isValid)
        {
            return;
        }

        // 아직 표면과 겹쳐 있거나 충분히 떨어지지 않았다면 기다린다.
        if (separation.isOverlapped ||
            separation.distance <= _dropClearance)
        {
            return;
        }

        // 캐릭터 쪽 최근접 지점이 발판 쪽 지점보다 아래에 있어야 한다.
        // 아래점프 직전, 아직 발판 위에 있을 때 복원되는 것을 막는다.
        bool passedBelow =
            separation.pointA.y < separation.pointB.y;

        if (passedBelow)
        {
            RestoreIgnoredCollision();
        }
    }

    // 통과하던 발판과의 충돌을 원래대로 되돌린다.
    private void RestoreIgnoredCollision()
    {
        if (_bodyCollider != null && _ignoredPlatform != null)
        {
            Physics2D.IgnoreCollision(
                _bodyCollider, _ignoredPlatform, false);
        }

        _ignoredPlatform = null;
    }

    // 대화창 등에서 입력을 잠가도 중력은 계속 적용되게 한다.
    public void SetControlEnabled(bool canControl)
    {
        _canControl = canControl;
        ClearInput();

        if (_body != null)
        {
            Vector2 velocity = _body.linearVelocity;
            velocity.x = 0f;
            _body.linearVelocity = velocity;
        }
    }

    // 이전 프레임의 입력이 나중에 실행되지 않도록 비운다.
    private void ClearInput()
    {
        _horizontalInput = 0f;
        _jumpRequested = false;
        _dropRequested = false;
    }

    // 비활성화할 때 충돌 무시 상태, 입력, 중력 설정을 정리한다.
    private void OnDisable()
    {
        RestoreIgnoredCollision();
        ClearInput();

        _groundCollider = null;
        _groundNormal = Vector2.up;
        _isJumpAscending = false;

        // 바닥에서 꺼졌더라도 원래 중력으로 돌려놓는다.
        if (_body != null)
        {
            _body.gravityScale = _defaultGravityScale;
        }
    }
}
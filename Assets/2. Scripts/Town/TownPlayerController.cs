using UnityEngine;

// 마을 캐릭터는 몸체 하나와 캡슐 충돌체 하나로 이동한다.
[RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
public class TownPlayerController : MonoBehaviour
{
    // 자식 Visual의 SpriteRenderer. 이동 방향에 따라 그림을 뒤집는다.
    [SerializeField] private SpriteRenderer _visual;

    // Visual에 있는 Animator.
    // 이동 속도와 접지 상태를 애니메이션에 전달한다.
    [SerializeField] private Animator _animator;

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

    // 실제 물리 접촉을 저장한다.
    // 배열을 재사용해서 바닥 검사마다 새 배열을 만들지 않는다.
    private readonly ContactPoint2D[] _groundContacts =
        new ContactPoint2D[16];

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

    // 이동에 필요한 컴포넌트와 애니메이션 연결을 확인한다.
    private void Awake()
    {
        _body = GetComponent<Rigidbody2D>();
        _bodyCollider = GetComponent<CapsuleCollider2D>();

        // 공중에서 사용할 원래 중력값을 보관한다.
        _defaultGravityScale = _body.gravityScale;

        if (_visual == null ||
            _animator == null ||
            _animator.runtimeAnimatorController == null)
        {
            Debug.LogError(
                "TownPlayerController의 Visual, Animator, Animator Controller를 연결해주세요.",
                this);

            enabled = false;
            return;
        }

        // 지정한 지형 레이어만 바닥 검사에 포함한다.
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

        // 이동과 점프 계산 결과를 애니메이션에 반영한다.
        UpdateAnimation();
    }

    // 이동 계산이 끝난 상태를 Animator에 전달한다.
    private void UpdateAnimation()
    {
        // 좌우 방향과 관계없이 속도의 크기만 전달한다.
        // 대화 등으로 조작이 잠기면 이동 애니메이션을 멈춘다.
        float moveSpeed = _canControl
            ? Mathf.Abs(_body.linearVelocity.x)
            : 0f;

        _animator.SetFloat("MoveSpeed", moveSpeed);

        // 점프와 아래점프를 시작하면 groundCollider가 비워지므로
        // 같은 물리 프레임에 공중 상태를 전달할 수 있다.
        _animator.SetBool("IsGrounded", _groundCollider != null);
    }

    // 실제로 발을 받치는 접촉을 먼저 찾는다.
    // 접촉이 없다면 바로 아래의 가까운 바닥을 검사한다.
    private void FindGround()
    {
        _groundCollider = null;
        _groundNormal = Vector2.up;

        // 점프 상승 중에는 가까운 바닥에 다시 붙지 않는다.
        if (_isJumpAscending)
        {
            if (_body.linearVelocity.y > 0f)
            {
                return;
            }

            _isJumpAscending = false;
        }

        float minimumNormalY =
            Mathf.Cos(_maxSlopeAngle * Mathf.Deg2Rad);

        Bounds bounds = _bodyCollider.bounds;

        // 캡슐 아래쪽의 둥근 부분에서 발생한 접촉까지만
        // 발밑의 접촉으로 인정한다.
        float maximumGroundPointY =
            bounds.min.y + bounds.extents.x + _groundCheckDistance;

        int contactCount = _bodyCollider.GetContacts(
            _groundFilter, _groundContacts);

        float bestNormalY = -1f;

        for (int i = 0; i < contactCount; i++)
        {
            ContactPoint2D contact = _groundContacts[i];

            // Effector 등에 의해 실제 충돌이 꺼진 접촉은 제외한다.
            if (!contact.enabled)
            {
                continue;
            }

            // 접촉한 두 콜라이더 중 캐릭터가 아닌 쪽을 찾는다.
            Collider2D ground = contact.collider;

            if (ground == _bodyCollider)
            {
                ground = contact.otherCollider;
            }

            if (ground == null || ground == _ignoredPlatform)
            {
                continue;
            }

            // 벽, 천장, 몸 위쪽에서 발생한 접촉은 바닥이 아니다.
            if (contact.normal.y < minimumNormalY ||
                contact.point.y > maximumGroundPointY)
            {
                continue;
            }

            // 여러 면이 동시에 받치고 있다면 더 평평한 면을 선택한다.
            if (contact.normal.y > bestNormalY)
            {
                bestNormalY = contact.normal.y;
                _groundCollider = ground;
                _groundNormal = contact.normal;
            }
        }

        // 실제로 발을 받치는 바닥을 찾았다면 Cast로 덮어쓰지 않는다.
        if (_groundCollider != null)
        {
            return;
        }

        int hitCount = _bodyCollider.Cast(
            Vector2.down,
            _groundFilter,
            _groundHits,
            _groundCheckDistance);

        float nearestDistance = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = _groundHits[i];

            if (hit.collider == null ||
                hit.collider == _ignoredPlatform)
            {
                continue;
            }

            // 시작부터 겹치거나 거리가 0인 결과는
            // 실제 접촉 검사에서만 판단한다.
            if (hit.distance <= 0f)
            {
                continue;
            }

            if (hit.normal.y < minimumNormalY ||
                hit.point.y > maximumGroundPointY)
            {
                continue;
            }

            // 몸과 겹친 계단을 가까운 바닥으로 오인하지 않는다.
            ColliderDistance2D separation =
                _bodyCollider.Distance(hit.collider);

            if (!separation.isValid || separation.isOverlapped)
            {
                continue;
            }

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

    // 다른 기능에서 현재 플레이어가 조작 가능한 상태인지 확인한다.
    public bool CanControl => _canControl;

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
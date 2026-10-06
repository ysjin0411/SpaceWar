using System;
using UnityEngine;

/// <summary>
/// 패턴 Config에서 블랙홀에 전달할 설정이다.
/// </summary>
[Serializable]
public struct BossBlackHoleSettings
{
    // FirePoint에서 목적지까지 날아가는 시간이다.
    [Min(0.01f)] public float TravelDuration;

    // 완전히 펼쳐진 크기에 대한 비행 중 크기의 비율이다.
    // 0.15면 최종 크기의 15%로 날아간다.
    [Range(0.01f, 1f)] public float FlightScaleRatio;

    // 확대가 완료된 후 흡인과 피해를 유지하는 시간이다.
    [Min(0.01f)] public float ActiveDuration;


    // 플레이어에게 더할 중심 방향의 이동 속도다.
    [SerializeField][Min(0f)] public float PullSpeed;

    // 같은 블랙홀이 피해를 주는 최소 시간 간격이다.
    [Min(0.01f)] public float DamageInterval;

    // 피해 간격마다 적용할 피해량이다.
    [SerializeField][Min(1)] public float Damage;

    // 유지·소멸 중 초당 회전 각도다. 음수는 시계 방향이다.
    public float RotationSpeed;
}

/// <summary>
/// 목적지로 이동한 뒤 플레이어를 흡인하고 주기적으로 피해를 준다.
/// 플레이어보다 먼저 외부 이동 속도를 계산한다.
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class BossBlackHole : MonoBehaviour
{
    // 비행과 공격 사이에 확대 단계를 둔다.
    enum HoleState
    {
        None,
        Flying,
        Expanding,
        Active,
        Fading
    }

    // Renderer 자식에 붙어 있는 블랙홀 Animator다.
    [SerializeField] Animator _animator;

    // 등장 애니메이션 상태의 전체 경로다.
    static readonly int AppearStateHash =
        Animator.StringToHash("Base Layer.Boss_Head_BlackHole_Appear");

    // 활성 애니메이션 상태의 전체 경로다.
    static readonly int ActiveStateHash =
        Animator.StringToHash("Base Layer.Boss_Head_BlackHole_Active");

    // 소멸 애니메이션 상태의 전체 경로다.
    static readonly int DisappearStateHash =
        Animator.StringToHash("Base Layer.Boss_Head_BlackHole_Disappear");

    // 회전·크기·투명도 연출을 적용할 자식 SpriteRenderer다.
    [SerializeField] SpriteRenderer _spriteRenderer;

    // 이미지 전체 너비 대비 바깥 흡인 원의 반지름 비율이다.
    // 현재 생성한 이미지의 여백을 고려한 초기값이다.
    [Range(0.01f, 0.5f)]
    [SerializeField] float _spriteOuterRadiusRatio = 0.47f;

    // 흡인 반지름 대비 안쪽 피해 반지름의 비율이다.
    // 스프라이트의 안쪽 자홍색 테두리에 맞춰 조절한다.
    [Range(0.01f, 1f)]
    [SerializeField] float _damageRadiusRatio = 0.3f;

    // 에디터에서 흡인 원과 피해 원을 표시한다.
    [SerializeField] bool _showRangeGizmos = true;

    // 현재 발사한 보스와 대상 플레이어다.
    BossEnemy _owner;
    PlayerController _player;

    // 안쪽 피해 원에 실제로 닿았는지 확인할 플레이어 Collider들이다.
    Collider2D[] _playerColliders;

    // 패턴에서 복사한 설정이다.
    BossBlackHoleSettings _settings;

    // 발사 위치와 발사 시 확정한 목적지다.
    Vector3 _launchPosition;
    Vector3 _destination;

    // 프리팹에 설정된 이미지 자식의 원래 크기다.
    // 확대가 끝나면 이 크기로 복원한다.
    Vector3 _expandedLocalScale;

    // 현재 단계와 단계별 경과 시간이다.
    HoleState _state;
    float _elapsed;

    // 재진입으로 피해 간격을 우회하지 않도록 다음 피해 시간을 보관한다.
    float _nextDamageTime;

    // 스프라이트 중심을 실제 판정 중심으로 사용한다.
    private Vector2 RangeCenter =>
        _spriteRenderer.transform.TransformPoint(
            _spriteRenderer.sprite.bounds.center);

    // 이미지의 원본 크기에 현재 Transform 배율을 적용한다.
    // 부모인 루트의 Scale도 포함되며 매번 현재 크기로 계산한다.
    private float WorldSuctionRadius
    {
        get
        {
            float localRadius =
                _spriteRenderer.sprite.bounds.size.x *
                _spriteOuterRadiusRatio;

            return _spriteRenderer.transform.TransformVector(
                Vector3.right * localRadius).magnitude;
        }
    }

    // 피해 범위도 같은 크기 기준을 사용한다.
    private float WorldDamageRadius =>
        WorldSuctionRadius * _damageRadiusRatio;

    /// <summary>
    /// 컴포넌트 참조와 초기 실행 상태를 준비한다.
    /// </summary>
    private void Awake()
    {
        // 이미지가 루트에 있으면 판정 위치까지 회전·축소될 수 있다.
        if (_spriteRenderer == null ||
            _spriteRenderer.sprite == null ||
            _spriteRenderer.transform == transform ||
            _spriteRenderer.transform.parent != transform)
        {
            throw new InvalidOperationException(
                $"{name}: 루트의 바로 아래 자식 SpriteRenderer를 연결하세요.");
        }

        // 애니메이션이 SpriteRenderer와 같은 자식에서 재생되는지 검사한다.
        if (_animator == null ||
            _animator.runtimeAnimatorController == null ||
            _animator.gameObject != _spriteRenderer.gameObject)
        {
            throw new InvalidOperationException(
                $"{name}: Renderer 자식의 Animator와 Controller를 연결하세요.");
        }

        // 코드에서 재생할 상태가 실제 Controller에 존재하는지 검사한다.
        if (!_animator.HasState(0, AppearStateHash) ||
            !_animator.HasState(0, ActiveStateHash) ||
            !_animator.HasState(0, DisappearStateHash))
        {
            throw new InvalidOperationException(
                $"{name}: BlackHole의 Appear, Active, Disappear 상태를 확인하세요.");
        }
    }

    /// <summary>
    /// 패턴이 블랙홀 생성 직후 호출한다.
    /// 프리팹에 설정한 루트와 이미지의 크기를 유지한다.
    /// </summary>
    public void Initialize(
        BossEnemy owner,
        PlayerController player,
        Vector2 destination,
        BossBlackHoleSettings settings)
    {
        if (owner == null || player == null)
        {
            throw new ArgumentException("보스와 플레이어를 전달하세요.");
        }

        _owner = owner;
        _player = player;
        _settings = settings;

        _playerColliders =
            player.GetComponentsInChildren<Collider2D>();

        // 부모에서 분리하더라도 현재 월드 크기는 유지한다.
        transform.SetParent(null, true);
        transform.rotation = Quaternion.identity;

        _launchPosition = transform.position;
        _destination = new Vector3(
            destination.x, destination.y, _launchPosition.z);

        // 이미지를 루트 중심에 배치한다.
        _spriteRenderer.transform.localPosition = Vector3.zero;
        _spriteRenderer.transform.localRotation = Quaternion.identity;

        // 프리팹의 원래 크기를 보관하고 비행할 때만 작게 만든다.
        // 루트 Scale은 변경하지 않는다.
        _expandedLocalScale = _spriteRenderer.transform.localScale;
        _spriteRenderer.transform.localScale =
            _expandedLocalScale * _settings.FlightScaleRatio;

        // 비행과 확대 중에는 흡인·피해·회전을 시작하지 않는다.
        _elapsed = 0f;
        _nextDamageTime = 0f;
        _state = HoleState.Flying;

        // 이동 중에는 프리팹에 지정한 스프라이트를 작은 크기로 표시한다.
        // 목적지에 도착하기 전에는 애니메이션을 재생하지 않는다.
        _animator.enabled = false;
    }

    // 이동, 등장, 활성 유지, 소멸의 순서를 관리한다.
    private void Update()
    {
        if (_state == HoleState.None)
        {
            return;
        }

        // 기존처럼 활성 상태와 소멸 상태에서 회전한다.
        if (_state == HoleState.Active || _state == HoleState.Fading)
        {
            _spriteRenderer.transform.Rotate(
                0f, 0f,
                _settings.RotationSpeed * Time.deltaTime,
                Space.Self);
        }

        // Disappear를 끝까지 재생한 뒤 블랙홀을 삭제한다.
        if (_state == HoleState.Fading)
        {
            if (HasAnimationFinished(DisappearStateHash))
            {
                _state = HoleState.None;
                Destroy(gameObject);
            }

            return;
        }

        // 보스가 사라지면 이동 또는 등장 중이라도 소멸을 시작한다.
        if (!IsOwnerAlive())
        {
            BeginFadeOut();
            return;
        }

        if (_state == HoleState.Flying)
        {
            // 작은 크기를 유지하며 목적지까지 이동한다.
            _elapsed += Time.deltaTime;

            float ratio = Mathf.Clamp01(
                _elapsed / _settings.TravelDuration);

            transform.position =
                Vector3.Lerp(_launchPosition, _destination, ratio);

            if (ratio >= 1f)
            {
                // 목적지에 도착한 순간부터 Appear를 재생한다.
                transform.position = _destination;
                _elapsed = 0f;
                _state = HoleState.Expanding;

                _animator.enabled = true;
                _animator.Play(AppearStateHash, 0, 0f);
            }

            return;
        }

        if (_state == HoleState.Expanding)
        {
            AnimatorStateInfo animation =
                _animator.GetCurrentAnimatorStateInfo(0);

            // Play 요청이 Animator에 반영되기 전에는 기다린다.
            if (animation.fullPathHash != AppearStateHash)
            {
                return;
            }

            // Appear의 재생 진행률에 맞춰 기존 확대 연출을 진행한다.
            float ratio = Mathf.Clamp01(animation.normalizedTime);
            float easedRatio = Mathf.SmoothStep(0f, 1f, ratio);

            _spriteRenderer.transform.localScale = Vector3.Lerp(
                _expandedLocalScale * _settings.FlightScaleRatio,
                _expandedLocalScale,
                easedRatio);

            if (animation.normalizedTime >= 1f)
            {
                // 등장 완료 후에만 활성 시간과 피해 판정을 시작한다.
                _spriteRenderer.transform.localScale = _expandedLocalScale;
                _elapsed = 0f;
                _nextDamageTime = Time.time;
                _state = HoleState.Active;

                _animator.Play(ActiveStateHash, 0, 0f);
            }

            return;
        }

        if (_state == HoleState.Active)
        {
            // 이동과 등장에 걸린 시간은 활성 유지 시간에 포함하지 않는다.
            _elapsed += Time.deltaTime;

            if (_elapsed >= _settings.ActiveDuration)
            {
                BeginFadeOut();
            }
        }
    }

    // 지정한 상태가 실제로 재생 중이며 한 번의 재생을 마쳤는지 확인한다.
    private bool HasAnimationFinished(int stateHash)
    {
        AnimatorStateInfo animation =
            _animator.GetCurrentAnimatorStateInfo(0);

        return animation.fullPathHash == stateHash &&
               animation.normalizedTime >= 1f;
    }

    /// <summary>
    /// 물리 갱신 주기에 맞춰 이동과 충돌 처리를 수행한다.
    /// </summary>
    private void FixedUpdate()
    {
        if (_state != HoleState.Active)
        {
            return;
        }

        if (!IsOwnerAlive())
        {
            BeginFadeOut();
            return;
        }

        // 이동 연출이나 사망 중인 플레이어에게는 영향을 주지 않는다.
        if (_player == null || _player.IsDead || !_player.CanControl)
        {
            ReleasePull();
            return;
        }

        // 스프라이트의 실제 중심과 현재 크기를 사용한다.
        Vector2 center = RangeCenter;
        Vector2 offset = center - (Vector2)_player.transform.position;
        float distance = offset.magnitude;

        if (distance <= WorldSuctionRadius)
        {
            // 중심을 지나쳐 반대편으로 튀지 않도록 한 프레임 이동량을 제한한다.
            float speed = Mathf.Min(
                _settings.PullSpeed,
                distance / Time.fixedDeltaTime);

            _player.SetExternalVelocity(
                this, offset.normalized * speed);
        }
        else
        {
            ReleasePull();
        }

        // 피해는 바깥 흡인 원이 아닌 안쪽 코어 접촉으로 판단한다.
        if (Time.time >= _nextDamageTime &&
            TouchesDamageArea(center))
        {
            _nextDamageTime = Time.time + _settings.DamageInterval;
            _player.TakeHit(_settings.Damage);
        }
    }

    /// <summary>
    /// 플레이어 Collider 중 하나라도 피해 원에 닿으면 true다.
    /// </summary>
    private bool TouchesDamageArea(Vector2 center)
    {
        // 현재 루트 Scale이 반영된 피해 반지름이다.
        float radius = WorldDamageRadius;

        foreach (Collider2D playerCollider in _playerColliders)
        {
            if (playerCollider == null ||
                !playerCollider.enabled ||
                !playerCollider.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector2 closest = playerCollider.ClosestPoint(center);

            if ((closest - center).sqrMagnitude <= radius * radius)
            {
                return true;
            }
        }

        return false;
    }

    // 흡입과 피해를 중단하고 소멸 애니메이션을 시작한다.
    public void BeginFadeOut()
    {
        // 소멸 중 반복 호출되어도 애니메이션을 처음부터 재생하지 않는다.
        if (_state == HoleState.Fading)
        {
            return;
        }

        _state = HoleState.Fading;
        ReleasePull();

        // 비활성 오브젝트는 애니메이션을 진행할 수 없으므로 정리한다.
        if (!isActiveAndEnabled)
        {
            Destroy(gameObject);
            return;
        }

        // 이동 중 패턴이 중단되었을 때도 소멸 애니메이션을 재생한다.
        _animator.enabled = true;
        _animator.Play(DisappearStateHash, 0, 0f);
    }

    // 오브젝트가 비활성화되어도 플레이어에게 흡입 속도가 남지 않게 한다.
    private void OnDisable()
    {
        ReleasePull();
    }

    /// <summary>
    /// 이 블랙홀이 적용한 흡인 속도만 제거한다.
    /// </summary>
    private void ReleasePull()
    {
        if (_player != null)
        {
            _player.RemoveExternalVelocity(this);
        }
    }

    /// <summary>
    /// IsOwnerAlive 동작에 필요한 상태와 처리 흐름을 실행한다.
    /// </summary>
    private bool IsOwnerAlive()
    {
        return _owner != null &&
               !_owner.IsDead &&
               _owner.isActiveAndEnabled;
    }

    /// <summary>
    /// 편집 중에는 조절할 범위를 보여준다.
    /// 실행 중에는 실제 공격이 활성화된 동안만 표시한다.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!_showRangeGizmos ||
            _spriteRenderer == null ||
            _spriteRenderer.sprite == null)
        {
            return;
        }

        if (Application.isPlaying && _state != HoleState.Active)
        {
            return;
        }

        // 기존 기즈모 상태가 다른 그림에 영향을 주지 않도록 보관한다.
        Color previousColor = Gizmos.color;
        Matrix4x4 previousMatrix = Gizmos.matrix;

        Gizmos.matrix = Matrix4x4.identity;

        Vector3 center = RangeCenter;
        center.z = _spriteRenderer.transform.position.z;

        // 실제 흡인 판정과 동일한 반지름을 사용한다.
        Gizmos.color = Color.cyan;
        DrawRangeCircle(center, WorldSuctionRadius);

        // 실제 피해 판정과 동일한 반지름을 사용한다.
        Gizmos.color = Color.magenta;
        DrawRangeCircle(center, WorldDamageRadius);

        Gizmos.color = previousColor;
        Gizmos.matrix = previousMatrix;
    }

    /// <summary>
    /// XY 평면에 원을 그린다. 구 형태의 보조선은 표시하지 않는다.
    /// </summary>
    private static void DrawRangeCircle(Vector3 center, float radius)
    {
        const int segments = 96;

        Vector3 previous = center + Vector3.right * radius;

        for (int i = 1; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;

            Vector3 next = center + new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius,
                0f);

            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
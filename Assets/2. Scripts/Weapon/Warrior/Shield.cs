using UnityEngine;
using System.Collections.Generic;

public class Shield : MonoBehaviour
{
    // 워리어 Renderer에 있는 Animator를 연결한다.
    [SerializeField] Animator _animator;

    // Attack 1부터 실드를 완전히 펼치기까지 걸리는 시간.
    [SerializeField, Min(0.01f)] float _raiseDuration = 0.2f;

    // Attack 4 자세를 보여준 뒤 Idle로 돌아오기까지 걸리는 시간.
    [SerializeField, Min(0.01f)] float _lowerDuration = 0.12f;

    // 실드가 포함된 캐릭터 SpriteRenderer.
    [SerializeField] SpriteRenderer _spriteRenderer;

    // Physics Shape를 설정한 Shield Attack 3 스프라이트.
    [SerializeField] Sprite _guardSprite;

    // ShieldGuard에 붙인 방어 영역 콜라이더.
    [SerializeField] PolygonCollider2D _guardCollider;

    // 스프라이트에 저장된 원본 윤곽.
    readonly List<Vector2> _sourceShape = new();

    // 콜라이더 좌표로 변환한 윤곽.
    Vector2[] _colliderPoints;

    // 실드의 현재 자세를 구분한다.
    enum ShieldPose
    {
        Idle,
        Raising,
        Holding,
        Lowering
    }

    ShieldPose _pose;

    // 현재 자세가 시작된 뒤 흐른 시간.
    float _poseTime;

    // 이 실드가 사용 중인 무기의 실제 체력 데이터.
    WeaponRuntimeState _weapon;

    // 체력이 소진되면 Z를 한 번 놓아야 다시 펼칠 수 있다.
    // 자연회복 직후 아주 적은 체력으로 자동 재방어하는 것을 막는다.
    bool _requiresRelease;

    // 실드가 Animator를 제어하는 동안에만 초기화를 수행한다.
    bool _ownsAnimation;

    static readonly int AttackHash =
        Animator.StringToHash("Attack");

    static readonly int AttackSpeedHash =
        Animator.StringToHash("AttackSpeed");

    static readonly int AttackStateHash =
        Animator.StringToHash("Base Layer.Attack");

    static readonly int IdleStateHash =
        Animator.StringToHash("Base Layer.Idle");

    // 완전히 펼쳐졌고 체력이 남아 있을 때만 방어 가능한 상태다.
    // 다음 단계에서 총알 차단 여부를 판단할 때 사용한다.
    public bool IsGuarding =>
        isActiveAndEnabled &&
        Time.timeScale > 0f &&
        _pose == ShieldPose.Holding &&
        _weapon != null &&
        _weapon.HasShieldHealth;

    private void Awake()
    {
        // 필요한 참조가 빠졌다면 실행 초기에 확인한다.
        if (_animator == null ||
            _spriteRenderer == null ||
            _guardSprite == null ||
            _guardCollider == null)
        {
            throw new MissingReferenceException(
                $"{name}: 실드의 Animator, Renderer, Sprite, Collider를 연결해야 합니다.");
        }

        // 실드 컴포넌트와 콜라이더는 같은 오브젝트에 둔다.
        if (_guardCollider.gameObject != gameObject)
        {
            throw new System.InvalidOperationException(
                $"{name}: PolygonCollider2D는 ShieldGuard에 있어야 합니다.");
        }

        // 플레이어 몸과 구분할 전용 레이어를 검사한다.
        int shieldLayer = LayerMask.NameToLayer("PlayerShield");

        if (shieldLayer < 0 || gameObject.layer != shieldLayer)
        {
            throw new System.InvalidOperationException(
                $"{name}: ShieldGuard의 레이어를 PlayerShield로 설정해야 합니다.");
        }

        _guardCollider.enabled = false;
        _guardCollider.isTrigger = true;
        _guardCollider.offset = Vector2.zero;

        // 현재 실드는 하나의 닫힌 윤곽으로 방어 영역을 구성한다.
        if (_guardSprite.GetPhysicsShapeCount() != 1)
        {
            throw new System.InvalidOperationException(
                $"{name}: Attack 3에 하나의 Physics Shape를 설정해야 합니다.");
        }

        _guardSprite.GetPhysicsShape(0, _sourceShape);

        if (_sourceShape.Count < 3)
        {
            throw new System.InvalidOperationException(
                $"{name}: 방어 윤곽에는 최소 세 개의 점이 필요합니다.");
        }

        _colliderPoints = new Vector2[_sourceShape.Count];
        _guardCollider.pathCount = 1;
    }

    private void LateUpdate()
    {
        // 실제 표시되는 스프라이트도 Attack 3일 때만 영역을 켠다.
        // 무기 교체 직후나 애니메이션이 아직 펼쳐지기 전에는 켜지지 않는다.
        bool canGuard =
            IsGuarding &&
            _spriteRenderer.sprite == _guardSprite;

        if (!canGuard)
        {
            _guardCollider.enabled = false;
            return;
        }

        for (int i = 0; i < _sourceShape.Count; i++)
        {
            Vector2 point = _sourceShape[i];

            // SpriteRenderer의 좌우·상하 반전을 판정에도 적용한다.
            if (_spriteRenderer.flipX)
            {
                point.x = -point.x;
            }

            if (_spriteRenderer.flipY)
            {
                point.y = -point.y;
            }

            // 스프라이트 좌표 → 월드 좌표 → 콜라이더 좌표로 변환한다.
            // 부모나 Renderer의 크기를 변경해도 같은 영역을 따라간다.
            Vector3 worldPoint =
                _spriteRenderer.transform.TransformPoint(point);

            _colliderPoints[i] =
                _guardCollider.transform.InverseTransformPoint(worldPoint);
        }

        _guardCollider.SetPath(0, _colliderPoints);
        _guardCollider.enabled = true;
    }

    // 공격을 실제로 막았을 때만 true를 반환한다.
    public bool TryBlockDamage(float damage)
    {
        if (!IsGuarding || !_guardCollider.enabled)
        {
            return false;
        }

        if (!_weapon.TryAbsorbShieldDamage(damage))
        {
            return false;
        }

        // 체력이 소진되는 순간 바로 방어를 종료한다.
        // 다음 프레임의 자연회복보다 먼저 재입력 제한을 설정한다.
        if (!_weapon.HasShieldHealth)
        {
            _requiresRelease = true;
            BeginLowering();
        }

        return true;
    }

    // Weapon에서 실드가 선택된 동안 매 프레임 호출한다.
    public void Tick(
        WeaponRuntimeState weapon,
        bool isHeld,
        float deltaTime)
    {
        // 실드 이외의 무기 데이터는 처리하지 않는다.
        if (weapon == null ||
            weapon.Data.Type != WeaponType.Shield)
        {
            CancelGuard();
            return;
        }

        // 다른 슬롯의 실드로 바뀌었다면 이전 자세를 정리한다.
        if (_weapon != weapon)
        {
            CancelGuard();
            _weapon = weapon;
        }

        // 키를 놓으면 체력 소진 후의 재입력 제한을 해제한다.
        if (!isHeld)
        {
            _requiresRelease = false;
        }
        else if (!_weapon.HasShieldHealth)
        {
            _requiresRelease = true;
        }

        bool wantsGuard =
            isHeld &&
            !_requiresRelease &&
            _weapon.HasShieldHealth;

        switch (_pose)
        {
            case ShieldPose.Idle:
                if (wantsGuard)
                {
                    _ownsAnimation = true;
                    _pose = ShieldPose.Raising;
                    _poseTime = 0f;

                    ShowAttackPose(0f);
                }
                break;

            case ShieldPose.Raising:
                if (!wantsGuard)
                {
                    BeginLowering();
                    break;
                }

                _poseTime += deltaTime;

                // 현재 Attack 클립의 앞 절반이 Attack 1과 2다.
                float raiseProgress = Mathf.Clamp01(
                    _poseTime / Mathf.Max(0.01f, _raiseDuration));

                ShowAttackPose(raiseProgress * 0.5f);

                if (raiseProgress >= 1f)
                {
                    _pose = ShieldPose.Holding;
                }
                break;

            case ShieldPose.Holding:
                if (!wantsGuard)
                {
                    BeginLowering();
                    break;
                }

                // 4컷 클립의 50% 위치가 Attack 3이다.
                ShowAttackPose(0.5f);
                break;

            case ShieldPose.Lowering:
                _poseTime += deltaTime;

                if (_poseTime >= Mathf.Max(0.01f, _lowerDuration))
                {
                    ReturnToIdle();
                }
                break;
        }
    }

    // Attack 상태의 재생 위치만 지정한다.
    // Animator 전체 속도는 건드리지 않아 피격 레이어는 계속 작동한다.
    private void ShowAttackPose(float normalizedTime)
    {
        _animator.SetBool(AttackHash, true);
        _animator.SetFloat(AttackSpeedHash, 0f);
        _animator.Play(AttackStateHash, 0, normalizedTime);
    }

    // 방어 영역을 즉시 끄고 Attack 4 자세로 접는다.
    private void BeginLowering()
    {
        _guardCollider.enabled = false;

        _pose = ShieldPose.Lowering;
        _poseTime = 0f;

        ShowAttackPose(0.75f);
    }

    // 실드가 사용하던 AttackSpeed를 원래 값으로 복구한다.
    private void ReturnToIdle()
    {
        _pose = ShieldPose.Idle;
        _poseTime = 0f;

        if (!_ownsAnimation)
        {
            return;
        }

        _animator.SetFloat(AttackSpeedHash, 1f);
        _animator.SetBool(AttackHash, false);
        _animator.Play(IdleStateHash, 0, 0f);

        _ownsAnimation = false;
    }

    // 무기 변경이나 비활성화 시 방어 영역과 자세를 정리한다.
    public void CancelGuard()
    {
        if (_guardCollider != null)
        {
            _guardCollider.enabled = false;
        }

        ReturnToIdle();

        _weapon = null;
        _requiresRelease = false;
    }

    private void OnDisable()
    {
        CancelGuard();
    }
}
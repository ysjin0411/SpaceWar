using System;
using UnityEngine;

/// <summary>
/// 실드 전개, 유지, 해제와 방어 판정 및 시각 효과를 관리한다.
/// </summary>
public class Shield : MonoBehaviour
{
    // 캐릭터 Renderer의 애니메이터와 스프라이트 렌더러.
    [SerializeField] private Animator _animator;
    [SerializeField] private SpriteRenderer _spriteRenderer;
    // 완전히 펼치거나 완전히 접는 데 걸리는 시간.
    [SerializeField, Min(0.01f)] private float _raiseDuration = 0.2f;
    [SerializeField, Min(0.01f)] private float _lowerDuration = 0.12f;
    // 유지 애니메이션 한 바퀴의 재생 시간.
    [SerializeField, Min(0.01f)] private float _guardCycleDuration = 0.5f;
    // ShieldGuard 오브젝트에 부착된 실제 방어 판정.
    [SerializeField] private PolygonCollider2D _guardCollider;
    // 펼친 방패 판금 영역만 나타내는 Renderer 로컬 좌표.
    // 캐릭터나 전방 발광 영역은 방어 판정에 포함하지 않는다.
    [SerializeField]
    private Vector2[] _guardOutline =
    {
        new Vector2(2.5f, 8.4f), new Vector2(3.3f, 8.8f),
        new Vector2(6.2f, 7.8f), new Vector2(6.2f, -2.8f),
        new Vector2(4.8f, -4.4f), new Vector2(2.5f, -2.8f)
    };
    // 방패를 펼칠 때 한 번 재생할 효과.
    [SerializeField] private SpriteOneShotEffect _deployEffectPrefab;
    // 방패 유지 중 반복 재생할 효과. Animator가 포함된 프리팹이다.
    [SerializeField] private GameObject _guardEffectPrefab;
    // 실제 방어 성공 시 방패 전면에 재생할 효과.
    // 최신 v4는 본체와 동일한 캔버스/피벗으로 정렬된 전체 방패 효과다.
    [SerializeField] private SpriteOneShotEffect _hitEffectPrefab;

    private enum ShieldPose { Idle, Raising, Holding, Lowering }
    private ShieldPose _pose;
    private float _poseTime;
    private float _extension;
    private WeaponRuntimeState _weapon;
    private bool _requiresRelease;
    private bool _ownsAnimation;
    private Vector2[] _colliderPoints;
    private SpriteOneShotEffect _deployEffect;
    private GameObject _guardEffect;
    private SpriteOneShotEffect _blockEffect;

    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int AttackSpeedHash = Animator.StringToHash("AttackSpeed");
    private static readonly int DeployStateHash = Animator.StringToHash("Base Layer.ShieldDeploy");
    private static readonly int GuardStateHash = Animator.StringToHash("Base Layer.ShieldGuard");
    private static readonly int LowerStateHash = Animator.StringToHash("Base Layer.ShieldLower");
    private static readonly int IdleStateHash = Animator.StringToHash("Base Layer.Idle");

    // 완전히 펼친 상태이고 방패 체력이 남아 있을 때만 방어할 수 있다.
    public bool IsGuarding => isActiveAndEnabled && Time.timeScale > 0f &&
        _ownsAnimation && _pose == ShieldPose.Holding &&
        _weapon != null && _weapon.HasShieldHealth;

    /// <summary>방어 판정에 필요한 참조와 좌표를 초기화한다.</summary>
    private void Awake()
    {
        if (_animator == null || _spriteRenderer == null || _guardCollider == null)
            throw new MissingReferenceException($"{name}: Animator, Renderer, Guard Collider를 연결하세요.");
        if (_guardCollider.gameObject != gameObject)
            throw new InvalidOperationException($"{name}: Guard Collider는 Shield와 같은 오브젝트에 있어야 합니다.");
        int layer = LayerMask.NameToLayer("PlayerShield");
        if (layer < 0 || gameObject.layer != layer)
            throw new InvalidOperationException($"{name}: ShieldGuard 레이어를 PlayerShield로 설정하세요.");
        if (_guardOutline == null || _guardOutline.Length < 3)
            throw new InvalidOperationException($"{name}: 방패 판정에는 최소 세 점이 필요합니다.");
        if (!ValidDuration(_raiseDuration) || !ValidDuration(_lowerDuration) ||
            !ValidDuration(_guardCycleDuration))
            throw new InvalidOperationException($"{name}: 실드 재생 시간은 유효한 양수여야 합니다.");
        foreach (Vector2 point in _guardOutline)
            if (float.IsNaN(point.x) || float.IsNaN(point.y) ||
                float.IsInfinity(point.x) || float.IsInfinity(point.y))
                throw new InvalidOperationException($"{name}: Guard Outline 좌표가 올바르지 않습니다.");
        _colliderPoints = new Vector2[_guardOutline.Length];
        _guardCollider.enabled = false;
        _guardCollider.isTrigger = true;
        _guardCollider.offset = Vector2.zero;
        _guardCollider.pathCount = 1;
    }

    /// <summary>현재 입력에 따라 방패 전개율과 애니메이션을 진행한다.</summary>
    public void Tick(WeaponRuntimeState weapon, bool isHeld, float deltaTime)
    {
        if (!isActiveAndEnabled || Time.timeScale <= 0f) return;
        if (weapon == null || weapon.Data.Type != WeaponType.Shield)
        {
            CancelGuard();
            return;
        }
        if (_weapon != weapon)
        {
            CancelGuard();
            _weapon = weapon;
        }
        // 체력이 소진되면 키를 한 번 놓기 전까지 자동 재전개하지 않는다.
        if (!isHeld) _requiresRelease = false;
        else if (!_weapon.HasShieldHealth) _requiresRelease = true;
        bool wantsGuard = isHeld && !_requiresRelease && _weapon.HasShieldHealth;
        float dt = Mathf.Max(0f, deltaTime);
        switch (_pose)
        {
            case ShieldPose.Idle:
                if (wantsGuard) BeginRaising();
                break;
            case ShieldPose.Raising:
                if (!wantsGuard) { BeginLowering(); break; }
                _extension = Mathf.MoveTowards(_extension, 1f, dt / _raiseDuration);
                ShowAttackPose(_extension);
                if (_extension >= 1f) BeginHolding();
                break;
            case ShieldPose.Holding:
                if (!wantsGuard) { BeginLowering(); break; }
                _poseTime = Mathf.Repeat(_poseTime + dt, _guardCycleDuration);
                ShowAttackPose(_poseTime / _guardCycleDuration);
                break;
            case ShieldPose.Lowering:
                // 전개 도중 놓았다면 그 크기부터 접는다. 접기가 끝난 뒤 재전개한다.
                _extension = Mathf.MoveTowards(_extension, 0f, dt / _lowerDuration);
                ShowAttackPose(1f - _extension);
                if (_extension <= 0f) ReturnToIdle();
                break;
        }
    }

    /// <summary>실드 상태 이름을 확인한 뒤 전개 애니메이션을 시작한다.</summary>
    private void BeginRaising()
    {
        if (!_animator.HasState(0, DeployStateHash) || !_animator.HasState(0, GuardStateHash) ||
            !_animator.HasState(0, LowerStateHash))
            throw new InvalidOperationException($"{name}: ShieldDeploy, ShieldGuard, ShieldLower 상태가 필요합니다.");
        _ownsAnimation = true;
        _pose = ShieldPose.Raising;
        _poseTime = 0f;
        _extension = 0f;
        ShowAttackPose(0f);
        if (_deployEffectPrefab != null)
        {
            _deployEffect = Instantiate(_deployEffectPrefab, _spriteRenderer.transform, false);
            AlignEffect(_deployEffect.gameObject);
            _deployEffect.SetPlaybackDuration(_raiseDuration);
        }
    }

    /// <summary>전개 완료 후 방어 상태와 유지 효과를 시작한다.</summary>
    private void BeginHolding()
    {
        _pose = ShieldPose.Holding;
        _poseTime = 0f;
        ShowAttackPose(0f);
        if (_deployEffect != null) Destroy(_deployEffect.gameObject);
        _deployEffect = null;
        if (_guardEffectPrefab != null)
        {
            _guardEffect = Instantiate(_guardEffectPrefab, _spriteRenderer.transform, false);
            AlignEffect(_guardEffect);
        }
        UpdateGuardCollider();
    }

    /// <summary>현재 방패 상태의 지정 시점을 표시한다.</summary>
    private void ShowAttackPose(float normalizedTime)
    {
        int state = _pose == ShieldPose.Holding ? GuardStateHash :
            _pose == ShieldPose.Lowering ? LowerStateHash : DeployStateHash;
        // Tick에서 시간을 진행하므로 기본 레이어의 자동 재생만 정지한다.
        _animator.SetBool(AttackHash, true);
        _animator.SetFloat(AttackSpeedHash, 0f);
        _animator.Play(state, 0, Mathf.Min(Mathf.Clamp01(normalizedTime), 0.99999f));
        _animator.Update(0f);
    }

    /// <summary>본체의 반전과 이동을 판정과 효과에 반영한다.</summary>
    private void LateUpdate()
    {
        UpdateGuardCollider();
        if (_deployEffect != null) AlignEffect(_deployEffect.gameObject);
        if (_guardEffect != null) AlignEffect(_guardEffect);
        if (_blockEffect != null) AlignEffect(_blockEffect.gameObject);
        // 유지 효과도 본체 유지 주기와 같은 진행률로 표시한다.
        if (_guardEffect != null && _pose == ShieldPose.Holding)
        {
            Animator effectAnimator = _guardEffect.GetComponent<Animator>();
            if (effectAnimator != null)
            {
                effectAnimator.speed = 0f;
                effectAnimator.Play("Loop", 0, _poseTime / _guardCycleDuration);
                effectAnimator.Update(0f);
            }
        }
    }

    /// <summary>완전히 펼친 방패 영역에만 방어 Collider를 활성화한다.</summary>
    private void UpdateGuardCollider()
    {
        if (_guardCollider == null) return;
        bool canGuard = IsGuarding &&
            _animator.GetCurrentAnimatorStateInfo(0).fullPathHash == GuardStateHash;
        if (!canGuard) { _guardCollider.enabled = false; return; }
        for (int i = 0; i < _guardOutline.Length; i++)
        {
            Vector2 point = _guardOutline[i];
            if (_spriteRenderer.flipX) point.x = -point.x;
            if (_spriteRenderer.flipY) point.y = -point.y;
            Vector3 world = _spriteRenderer.transform.TransformPoint(point);
            _colliderPoints[i] = _guardCollider.transform.InverseTransformPoint(world);
        }
        _guardCollider.SetPath(0, _colliderPoints);
        _guardCollider.enabled = true;
    }

    /// <summary>방어 성공 시 방패 체력을 소모하고 별도 피격 효과를 재생한다.</summary>
    public bool TryBlockDamage(float damage, Vector2 hitPoint)
    {
        if (!IsGuarding || !_guardCollider.enabled ||
            _animator.GetCurrentAnimatorStateInfo(0).fullPathHash != GuardStateHash)
            return false;
        if (float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0f) return false;
        if (!_weapon.TryAbsorbShieldDamage(damage)) return false;
        // hitPoint를 받는 기존 API는 유지한다. v4 효과는 방패 전체에 정렬된 이미지이므로
        // 접촉점에 이미지 중심을 놓지 않고 본체의 동일한 피벗에 배치한다.
        if (_hitEffectPrefab != null)
        {
            if (_blockEffect != null) Destroy(_blockEffect.gameObject);
            _blockEffect = Instantiate(_hitEffectPrefab, _spriteRenderer.transform, false);
            AlignEffect(_blockEffect.gameObject);
            _blockEffect.SetPlaybackDuration(_lowerDuration);
        }
        if (!_weapon.HasShieldHealth)
        {
            _requiresRelease = true;
            BeginLowering();
        }
        return true;
    }

    /// <summary>현재 크기를 유지한 채 방어를 해제하고 접기 시작한다.</summary>
    private void BeginLowering()
    {
        _guardCollider.enabled = false;
        _pose = ShieldPose.Lowering;
        _poseTime = 0f;
        StopEffects(false);
        ShowAttackPose(1f - _extension);
    }

    /// <summary>공통 캔버스로 제작된 효과를 본체와 같은 위치와 반전으로 정렬한다.</summary>
    private void AlignEffect(GameObject effect)
    {
        effect.transform.localPosition = Vector3.zero;
        effect.transform.localRotation = Quaternion.identity;
        effect.transform.localScale = Vector3.one;
        SpriteRenderer renderer = effect.GetComponent<SpriteRenderer>();
        if (renderer == null) return;
        renderer.flipX = _spriteRenderer.flipX;
        renderer.flipY = _spriteRenderer.flipY;
        renderer.sortingLayerID = _spriteRenderer.sortingLayerID;
        renderer.sortingOrder = _spriteRenderer.sortingOrder + 1;
    }

    /// <summary>실드에 연결된 전개 및 유지 효과를 정리한다.</summary>
    private void StopEffects(bool includeBlock = true)
    {
        if (_deployEffect != null) Destroy(_deployEffect.gameObject);
        if (_guardEffect != null) Destroy(_guardEffect);
        if (includeBlock && _blockEffect != null) Destroy(_blockEffect.gameObject);
        _deployEffect = null;
        _guardEffect = null;
        if (includeBlock) _blockEffect = null;
    }

    /// <summary>기본 자세로 돌아가고 실드가 사용한 애니메이션 제어를 해제한다.</summary>
    private void ReturnToIdle()
    {
        _pose = ShieldPose.Idle;
        _poseTime = 0f;
        _extension = 0f;
        if (_guardCollider != null) _guardCollider.enabled = false;
        StopEffects();
        if (_ownsAnimation && _animator != null && _animator.isActiveAndEnabled)
        {
            _animator.SetFloat(AttackSpeedHash, 1f);
            _animator.SetBool(AttackHash, false);
            _animator.Play(IdleStateHash, 0, 0f);
        }
        _ownsAnimation = false;
    }

    /// <summary>무기 교체나 제어 불가 상태에서 즉시 실드를 해제한다.</summary>
    public void CancelGuard()
    {
        ReturnToIdle();
        _weapon = null;
        _requiresRelease = false;
    }

    /// <summary>재생 시간이 0보다 큰 유한한 값인지 검사한다.</summary>
    private static bool ValidDuration(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }

    /// <summary>비활성화 시 남은 방어 판정과 효과를 정리한다.</summary>
    private void OnDisable()
    {
        CancelGuard();
    }
}



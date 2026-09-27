using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 자이언트소드와 해머의 3연격, 무기 궤적 판정 및 효과를 관리한다.
/// </summary>
[RequireComponent(typeof(PolygonCollider2D))]
public class SpriteMeleeAttack : MonoBehaviour
{
    /// <summary>한 프레임의 무기 판정 위치를 Renderer 로컬 좌표로 저장한다.</summary>
    [Serializable]
    private class HitPose
    {
        // 무기 판정 사각형의 중심. PPU가 적용된 Renderer 로컬 단위다.
        public Vector2 Center;
        // 오른쪽 방향이 0도이며 반시계 방향이 양수다.
        public float Angle;
        // 판정 사각형의 길이와 두께. 캐릭터 전체 크기가 아니다.
        public Vector2 Size = Vector2.one;
    }

    /// <summary>각 타격의 재생 시간, 유효 구간, 판정 궤적과 효과를 저장한다.</summary>
    [Serializable]
    private class ComboStep
    {
        // Base Layer 안에서 재생할 상태 이름.
        public string StateName;
        // 이 타격의 원본 애니메이션 클립.
        public AnimationClip Clip;
        // 준비, 휘두르기, 회수를 포함한 총 동작 시간.
        [Min(0.01f)] public float Duration = 0.3f;
        // 피해 판정을 시작하고 끝낼 정규화 시간.
        [Range(0f, 1f)] public float HitStart = 1f / 6f;
        [Range(0f, 1f)] public float HitEnd = 5f / 6f;
        // 원본 애니메이션 순서와 동일한 여섯 프레임의 무기 판정.
        public HitPose[] HitPoses = new HitPose[6];
        // 몸체와 같은 캔버스/피벗으로 제작된 전체 동작의 궤적 효과.
        public SpriteOneShotEffect TrailEffect;
        // 내려찍기 등 특정 순간에 생성할 효과. 없으면 생성하지 않는다.
        public SpriteOneShotEffect ImpactEffect;
        // 전체 동작 중 ImpactEffect를 생성할 시점.
        [Range(0f, 1f)] public float ImpactTime = 0.65f;
        // ImpactEffect의 생성 위치. Renderer 로컬 좌표 기준이다.
        public Vector2 ImpactOffset;
    }

    // 캐릭터 본체와 무기 그림을 표시하는 Renderer와 Animator.
    [SerializeField] private SpriteRenderer _characterRenderer;
    [SerializeField] private Animator _animator;
    // 1타, 2타, 3타 순서로 설정한다.
    [SerializeField]
    private ComboStep[] _comboSteps =
    {
        new ComboStep { StateName = "Attack" },
        new ComboStep { StateName = "Attack2" },
        new ComboStep { StateName = "Attack3" }
    };
    // 적의 피격 Collider가 속한 레이어만 검사한다.
    [SerializeField] private LayerMask _enemyLayerMask;
    // 실제 적에게 피해를 줄 때 접촉 위치에 생성할 효과.
    [SerializeField] private SpriteOneShotEffect _hitEffectPrefab;

    private PolygonCollider2D _hitCollider;
    private ContactFilter2D _contactFilter;
    private readonly HashSet<IDamageable> _hitTargets = new HashSet<IDamageable>();
    private readonly List<Collider2D> _overlapResults = new List<Collider2D>();
    private readonly List<Vector2> _hitPoints = new List<Vector2>();
    private readonly Vector2[] _colliderPoints = new Vector2[4];
    private int _nextComboIndex;
    private int _currentComboIndex;
    private int _currentAttackStateHash;
    private float _damage;
    private float _previousNormalizedTime;
    // Z키를 유지하는 동안 현재 타격 직후 다음 타격을 연결한다.
    private bool _attackHeld;
    // 다음 타격 시작 시 확정할 최신 공격력이다.
    private float _requestedDamage;
    // 기본 쿨타임 / 현재 쿨타임. 1보다 크면 각 타격이 빨라진다.
    private float _requestedAttackSpeedMultiplier = 1f;
    private bool _isAttackActive;
    private bool _resetComboOnFinish;
    private bool _impactPlayed;
    private SpriteOneShotEffect _activeTrailEffect;
    private SpriteRenderer _trailRenderer;

    [Header("Hitbox Gizmos")]
    // 끄면 플레이 중 히트박스 표시를 숨긴다.
    [SerializeField] private bool _showHitboxGizmos = true;
    // 판정 활성 구간의 색상. A를 0으로 설정하면 이 구간의 표시를 숨긴다.
    [SerializeField] private Color _activeHitboxColor = new Color(1f, 0.2f, 0.1f, 1f);
    // 준비·회수 구간의 색상. A를 0으로 설정하면 이 구간의 표시를 숨긴다.
    [SerializeField] private Color _inactiveHitboxColor = new Color(0.6f, 0.6f, 0.6f, 0.7f);

    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int AttackSpeedHash = Animator.StringToHash("AttackSpeed");
    private static readonly int IdleStateHash = Animator.StringToHash("Base Layer.Idle");

    // 공격 동작 중에만 true. 대기시간은 CanBeginAttack으로 별도 확인한다.
    public bool IsAttacking => _isAttackActive;
    // Weapon은 이 값이 true일 때만 새로운 타격을 요청한다.
    public bool CanBeginAttack => isActiveAndEnabled && Time.timeScale > 0f &&
        !_isAttackActive;

    /// <summary>공통 참조와 피해 검색 조건을 초기화한다.</summary>
    private void Awake()
    {
        _hitCollider = GetComponent<PolygonCollider2D>();
        _hitCollider.enabled = false;
        _hitCollider.isTrigger = true;
        _hitCollider.offset = Vector2.zero;
        _hitCollider.pathCount = 1;
        if (_characterRenderer == null || _animator == null || _enemyLayerMask.value == 0)
            throw new InvalidOperationException($"{name}: Renderer, Animator, Enemy Layer Mask를 설정하세요.");
        _contactFilter = new ContactFilter2D();
        _contactFilter.SetLayerMask(_enemyLayerMask);
        _contactFilter.useTriggers = true;
    }

    /// <summary>Z키 유지 여부를 저장하고, 키를 놓으면 다음 시작 타격을 초기화한다.</summary>
    public void SetAttackHeld(bool isHeld)
    {
        _attackHeld = isHeld;
        if (isHeld) return;

        // 진행 중인 한 타격은 마치되, 다음 입력은 1타부터 시작한다.
        _nextComboIndex = 0;
        if (_isAttackActive) _resetComboOnFinish = true;
    }

    /// <summary>최신 공격 수치를 저장하고, 공격 가능한 상태라면 다음 타격을 시작한다.</summary>
    public void BeginAttack(float damage, float attackSpeedMultiplier)
    {
        if (!IsFinite(damage) || damage < 0f)
            throw new ArgumentOutOfRangeException(nameof(damage));
        if (!IsFinite(attackSpeedMultiplier) || attackSpeedMultiplier <= 0f)
            throw new ArgumentOutOfRangeException(nameof(attackSpeedMultiplier));

        // Weapon이 매 프레임 전달한다. 진행 중인 타격은 그대로 두고 다음 타격에 반영한다.
        _requestedDamage = damage;
        _requestedAttackSpeedMultiplier = attackSpeedMultiplier;
        if (!CanBeginAttack) return;

        ValidateComboSteps();
        ComboStep step = _comboSteps[_nextComboIndex];
        float duration = step.Duration / attackSpeedMultiplier;
        if (!IsFinite(duration) || duration <= 0f)
            throw new InvalidOperationException($"{name}: 공격 재생 시간이 유효하지 않습니다.");

        _currentComboIndex = _nextComboIndex;
        _currentAttackStateHash = Animator.StringToHash($"Base Layer.{step.StateName}");
        _damage = damage;
        _previousNormalizedTime = 0f;
        _resetComboOnFinish = false;
        _impactPlayed = false;
        _hitTargets.Clear();
        _isAttackActive = true;

        // 쿨타임 대기 대신, 준비·휘두르기·회수가 포함된 한 타격 전체를 빠르게 재생한다.
        _animator.SetFloat(AttackSpeedHash, step.Clip.length / duration);
        _animator.SetBool(AttackHash, true);
        _animator.Play(_currentAttackStateHash, 0, 0f);
        _animator.Update(0f);

        if (step.TrailEffect != null)
        {
            _activeTrailEffect = Instantiate(step.TrailEffect, _characterRenderer.transform, false);
            _activeTrailEffect.transform.localPosition = Vector3.zero;
            _activeTrailEffect.transform.localRotation = Quaternion.identity;
            _activeTrailEffect.transform.localScale = Vector3.one;
            // 몸체와 궤적에 동일한 시간을 적용하여 강화 후에도 위치가 어긋나지 않게 한다.
            _activeTrailEffect.SetPlaybackDuration(duration);
            _trailRenderer = _activeTrailEffect.GetComponent<SpriteRenderer>();
            SyncTrailVisual();
        }
    }

    /// <summary>세 타격의 클립, 시간, 판정 좌표가 유효한지 시작 전에 검사한다.</summary>
    private void ValidateComboSteps()
    {
        if (_comboSteps == null || _comboSteps.Length != 3)
            throw new InvalidOperationException($"{name}: Combo Steps는 세 개여야 합니다.");
        foreach (ComboStep step in _comboSteps)
        {
            if (step == null || step.Clip == null || string.IsNullOrWhiteSpace(step.StateName) ||
                !IsFinite(step.Duration) || step.Duration <= 0f || step.Clip.length <= 0f ||
                !IsFinite(step.HitStart) || !IsFinite(step.HitEnd) ||
                step.HitStart < 0f || step.HitEnd > 1f || step.HitStart >= step.HitEnd ||
                step.HitPoses == null || step.HitPoses.Length != 6)
                throw new InvalidOperationException($"{name}: 연격 시간, 클립, 여섯 Hit Poses를 확인하세요.");
            if (!_animator.HasState(0, Animator.StringToHash($"Base Layer.{step.StateName}")))
                throw new InvalidOperationException($"{name}: Animator에 {step.StateName} 상태가 없습니다.");
            if (!IsFinite(step.ImpactTime) || step.ImpactTime < 0f || step.ImpactTime > 1f ||
                !IsFinite(step.ImpactOffset.x) || !IsFinite(step.ImpactOffset.y))
                throw new InvalidOperationException($"{name}: Impact 설정을 확인하세요.");
            foreach (HitPose pose in step.HitPoses)
                if (pose == null || !IsFinite(pose.Center.x) || !IsFinite(pose.Center.y) ||
                    !IsFinite(pose.Angle) || !IsFinite(pose.Size.x) || !IsFinite(pose.Size.y) ||
                    pose.Size.x <= 0f || pose.Size.y <= 0f)
                    throw new InvalidOperationException($"{name}: Hit Pose의 위치, 각도, 크기를 확인하세요.");
        }
    }

    /// <summary>애니메이션 진행 구간을 따라 피해 판정과 효과를 처리한다.</summary>
    private void LateUpdate()
    {
        if (Time.timeScale <= 0f) return;
        // 타격 사이에 별도 대기 타이머를 두지 않는다.
        if (!_isAttackActive) return;
        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
        if (state.fullPathHash != _currentAttackStateHash)
        {
            CancelAttack();
            return;
        }
        ComboStep step = _comboSteps[_currentComboIndex];
        float current = Mathf.Clamp01(state.normalizedTime);
        SyncTrailVisual();
        float from = Mathf.Max(_previousNormalizedTime, step.HitStart);
        float to = Mathf.Min(current, step.HitEnd);
        // 프레임이 건너뛰어도 지나간 유효 구간 전체를 검사한다.
        if (current > _previousNormalizedTime && to >= from && current >= step.HitStart &&
            _previousNormalizedTime < step.HitEnd)
            CheckDamage(from, to);
        if (!_isAttackActive) return;
        if (!_impactPlayed && current >= step.ImpactTime)
        {
            _impactPlayed = true;
            PlayImpact(step);
        }
        _previousNormalizedTime = current;
        if (state.normalizedTime >= 1f) CompleteAttack();
    }

    /// <summary>경과 구간을 프레임 경계와 작은 이동 구간으로 나눠 검사한다.</summary>
    private void CheckDamage(float from, float to)
    {
        ComboStep step = _comboSteps[_currentComboIndex];
        int count = step.HitPoses.Length;
        // 여섯 키는 각각 0, 1/6, ... 5/6에 위치하며 마지막 키는 끝까지 유지된다.
        for (int frame = 0; frame < count && _isAttackActive; frame++)
        {
            float start = Mathf.Max(from, frame / (float)count);
            float end = Mathf.Min(to, (frame + 1f) / count);
            if (end < start) continue;
            EvaluateHitPose(start, out Vector2 c0, out float a0, out Vector2 s0);
            EvaluateHitPose(end, out Vector2 c1, out float a1, out Vector2 s1);
            float thickness = Mathf.Min(Mathf.Min(s0.x, s0.y), Mathf.Min(s1.x, s1.y));
            float distance = Vector2.Distance(c0, c1) + Vector2.Distance(s0, s1);
            int samples = Mathf.Max(1, Mathf.CeilToInt(distance / (thickness * 0.25f)));
            samples = Mathf.Max(samples, Mathf.CeilToInt(Mathf.Abs(Mathf.DeltaAngle(a0, a1)) / 5f));
            for (int i = 0; i <= samples && _isAttackActive; i++)
            {
                float time = Mathf.Lerp(start, end, i / (float)samples);
                ApplyHitboxShape(time);
                CollectAndApplyDamage();
            }
        }
    }

    /// <summary>정규화 시간에 맞는 무기 중심, 회전, 크기를 보간한다.</summary>
    private void EvaluateHitPose(float time, out Vector2 center, out float angle, out Vector2 size)
    {
        EvaluateHitPose(_comboSteps[_currentComboIndex].HitPoses, time, out center, out angle, out size);
    }

    /// <summary>실제 판정과 Gizmos 표시가 같은 보간 계산으로 사각형을 구한다.</summary>
    private static void EvaluateHitPose(HitPose[] poses, float time, out Vector2 center, out float angle, out Vector2 size)
    {
        // 기준 자세 사이를 보간하며 마지막 자세 이후에는 마지막 값을 유지한다.
        float cursor = Mathf.Clamp(time * poses.Length, 0f, poses.Length - 1f);
        int first = Mathf.FloorToInt(cursor);
        int second = Mathf.Min(first + 1, poses.Length - 1);
        float t = cursor - first;
        center = Vector2.Lerp(poses[first].Center, poses[second].Center, t);
        angle = Mathf.LerpAngle(poses[first].Angle, poses[second].Angle, t);
        size = Vector2.Lerp(poses[first].Size, poses[second].Size, t);
    }

    /// <summary>스프라이트 Physics Shape 대신 무기 사각형으로 판정 영역을 만든다.</summary>
    private void ApplyHitboxShape(float time)
    {
        EvaluateHitPose(time, out Vector2 center, out float angle, out Vector2 size);
        Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
        for (int i = 0; i < 4; i++)
        {
            Vector2 corner = new Vector2(i == 0 || i == 3 ? -0.5f : 0.5f, i < 2 ? -0.5f : 0.5f);
            Vector2 point = center + (Vector2)(rotation * Vector2.Scale(corner, size));
            _colliderPoints[i] = transform.InverseTransformPoint(RendererPointToWorld(point));
        }
        _hitCollider.SetPath(0, _colliderPoints);
    }

    /// <summary>현재 판정에 겹친 적을 수집하고 한 타격당 한 번만 피해를 준다.</summary>
    private void CollectAndApplyDamage()
    {
        _overlapResults.Clear();
        _hitPoints.Clear();
        try
        {
            _hitCollider.enabled = true;
            Physics2D.SyncTransforms();
            _hitCollider.Overlap(_contactFilter, _overlapResults);
            foreach (Collider2D hit in _overlapResults)
                _hitPoints.Add(hit == null ? Vector2.zero : HitEffectUtility.GetSurfacePoint(_hitCollider, hit));
        }
        finally { _hitCollider.enabled = false; }
        for (int i = 0; i < _overlapResults.Count && _isAttackActive; i++)
        {
            Collider2D hit = _overlapResults[i];
            if (hit == null) continue;
            IDamageable target = hit.GetComponentInParent<IDamageable>();
            if (target == null || target.IsDead || !_hitTargets.Add(target)) continue;
            if (_hitEffectPrefab != null) Instantiate(_hitEffectPrefab, _hitPoints[i], Quaternion.identity);
            target.TakeHit(_damage);
        }
    }

    /// <summary>Renderer의 좌우 반전과 Transform을 판정 위치에 반영한다.</summary>
    private Vector3 RendererPointToWorld(Vector2 point)
    {
        if (_characterRenderer.flipX) point.x = -point.x;
        if (_characterRenderer.flipY) point.y = -point.y;
        return _characterRenderer.transform.TransformPoint(point);
    }

    /// <summary>무기 궤적의 반전과 정렬 순서를 본체에 맞춘다.</summary>
    private void SyncTrailVisual()
    {
        if (_trailRenderer == null) return;
        _trailRenderer.flipX = _characterRenderer.flipX;
        _trailRenderer.flipY = _characterRenderer.flipY;
        _trailRenderer.sortingLayerID = _characterRenderer.sortingLayerID;
        _trailRenderer.sortingOrder = _characterRenderer.sortingOrder + 1;
    }

    /// <summary>내려찍기 효과를 생성 위치에 남겨 캐릭터를 따라가지 않게 한다.</summary>
    private void PlayImpact(ComboStep step)
    {
        if (step.ImpactEffect == null) return;
        SpriteOneShotEffect effect = Instantiate(step.ImpactEffect,
            RendererPointToWorld(step.ImpactOffset), _characterRenderer.transform.rotation);
        effect.transform.localScale = _characterRenderer.transform.lossyScale;
        SpriteRenderer sr = effect.GetComponent<SpriteRenderer>();
        sr.flipX = _characterRenderer.flipX;
        sr.flipY = _characterRenderer.flipY;
        sr.sortingLayerID = _characterRenderer.sortingLayerID;
        sr.sortingOrder = _characterRenderer.sortingOrder + 1;
    }

    /// <summary>현재 타격을 정리하고, Z키를 유지 중이면 같은 프레임에서 다음 타격을 시작한다.</summary>
    private void CompleteAttack()
    {
        if (!_isAttackActive) return;

        bool continueCombo = _attackHeld && isActiveAndEnabled;
        _nextComboIndex = _resetComboOnFinish
            ? 0
            : (_currentComboIndex + 1) % _comboSteps.Length;

        // 연속 공격 중에는 EndAttack이 Idle로 전환하지 않는다.
        EndAttack(false);
        if (continueCombo)
            BeginAttack(_requestedDamage, _requestedAttackSpeedMultiplier);
    }

    /// <summary>강제 취소 시 다음 공격을 1타로 초기화한다.</summary>
    public void CancelAttack()
    {
        EndAttack(true);
    }

    /// <summary>판정과 효과를 정리하고, 공격을 실제로 멈출 때만 Idle로 돌아간다.</summary>
    private void EndAttack(bool resetCombo)
    {
        bool wasAttacking = _isAttackActive;
        _isAttackActive = false;
        _previousNormalizedTime = 0f;
        _currentAttackStateHash = 0;
        _hitTargets.Clear();
        if (_hitCollider != null) _hitCollider.enabled = false;
        if (_activeTrailEffect != null) Destroy(_activeTrailEffect.gameObject);
        _activeTrailEffect = null;
        _trailRenderer = null;

        if (resetCombo)
        {
            // 무기 변경·사망·제어 불가 등의 취소는 다음 공격을 1타로 되돌린다.
            _nextComboIndex = 0;
            _currentComboIndex = 0;
            _resetComboOnFinish = false;
            _attackHeld = false;
        }

        // Z 유지 중 정상 종료는 다음 타격으로 직접 이어지므로 Idle을 끼우지 않는다.
        if (wasAttacking && !_attackHeld && _animator != null && _animator.isActiveAndEnabled)
        {
            _animator.SetBool(AttackHash, false);
            _animator.SetFloat(AttackSpeedHash, 1f);
            _animator.Play(IdleStateHash, 0, 0f);
        }
    }

    /// <summary>플레이 중 진행 중인 공격의 히트박스를 선택 여부와 관계없이 표시한다.</summary>
    private void OnDrawGizmos()
    {
        // Collider는 검사 순간에만 켜지므로 enabled 대신 공격 상태를 확인한다.
        if (Application.isPlaying && isActiveAndEnabled && _isAttackActive)
            DrawHitboxGizmo();
    }

    /// <summary>판정과 동일한 좌표 변환으로 사각형을 그리며 알파 0이면 표시하지 않는다.</summary>
    private void DrawHitboxGizmo()
    {
        if (!_showHitboxGizmos || _characterRenderer == null || _comboSteps == null)
            return;

        int index = _currentComboIndex;
        if (index < 0 || index >= _comboSteps.Length) return;
        ComboStep step = _comboSteps[index];
        // Inspector에서 배열을 수정하는 도중에도 표시 오류가 발생하지 않게 한다.
        if (step == null || step.HitPoses == null || step.HitPoses.Length == 0) return;
        foreach (HitPose pose in step.HitPoses)
            if (pose == null || !IsFinite(pose.Center.x) || !IsFinite(pose.Center.y) ||
                !IsFinite(pose.Angle) || !IsFinite(pose.Size.x) || !IsFinite(pose.Size.y)) return;

        // 현재 공격 애니메이션의 진행률만 사용한다.
        if (_animator == null || !_animator.isActiveAndEnabled) return;
        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
        if (state.fullPathHash != _currentAttackStateHash) return;
        float time = state.normalizedTime;
        if (!IsFinite(time)) return;
        time = Mathf.Clamp01(time);
        bool hitActive = time >= step.HitStart && time <= step.HitEnd;
        Color color = hitActive ? _activeHitboxColor : _inactiveHitboxColor;
        // 완전 투명일 때는 그리기 자체를 생략한다. 공격 판정에는 영향을 주지 않는다.
        if (color.a <= 0f) return;

        EvaluateHitPose(step.HitPoses, time, out Vector2 center, out float angle, out Vector2 size);
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        try
        {
            // 실제 RendererPointToWorld와 동일하게 좌우 반전 및 부모 Transform을 반영한다.
            Vector3 flip = new Vector3(_characterRenderer.flipX ? -1f : 1f,
                _characterRenderer.flipY ? -1f : 1f, 1f);
            Gizmos.matrix = _characterRenderer.transform.localToWorldMatrix *
                Matrix4x4.Scale(flip) * Matrix4x4.TRS(center, Quaternion.Euler(0f, 0f, angle), Vector3.one);
            Gizmos.color = color;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, size.y, 0f));
        }
        finally
        {
            // 다른 오브젝트의 Gizmos에 색상이나 좌표계가 남지 않도록 복구한다.
            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
    }

    /// <summary>NaN과 무한대를 제외한 정상적인 수치인지 확인한다.</summary>
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>비활성화 시 공격 및 연격 진행 상태를 정리한다.</summary>
    private void OnDisable()
    {
        CancelAttack();
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PolygonCollider2D))]
public class SpriteMeleeAttack : MonoBehaviour
{
    // 실제 워리어의 현재 스프라이트를 표시하는 Renderer.
    [SerializeField] SpriteRenderer _characterRenderer;

    // Attack_1부터 Attack_4까지 순서대로 연결한다.
    [SerializeField] Sprite[] _attackSprites = new Sprite[4];

    // 워리어 Renderer에서 사용하는 Animator.
    [SerializeField] Animator _animator;

    // Warrior Override에 연결된 Attack4 클립.
    // 원본 재생 시간을 읽어 공격 속도를 계산한다.
    [SerializeField] AnimationClip _attackClip;

    // 검 공격으로 피해를 줄 적 콜라이더의 레이어.
    [SerializeField] LayerMask _enemyLayerMask;

    // 현재 휘두르기의 공격력. 공격 시작 시 런타임 무기에서 받는다.
    int _damage;

    // 직전 검사 시점의 애니메이션 진행도.
    float _previousNormalizedTime;

    // 한 번의 휘두르기에 이미 피해를 준 대상.
    readonly HashSet<IDamageable> _hitTargets =
        new HashSet<IDamageable>();

    // 겹친 콜라이더를 받을 목록. 적이 많아도 고정 개수로 잘리지 않는다.
    readonly List<Collider2D> _overlapResults =
        new List<Collider2D>();

    // 피해를 줄 레이어와 Trigger 포함 여부.
    ContactFilter2D _contactFilter;

    // 반복 사용하는 Animator 파라미터와 상태 ID.
    static readonly int AttackHash = Animator.StringToHash("Attack");
    static readonly int AttackSpeedHash = Animator.StringToHash("AttackSpeed");
    static readonly int AttackStateHash = Animator.StringToHash("Base Layer.Attack");
    static readonly int IdleStateHash = Animator.StringToHash("Base Layer.Idle");

    // Weapon이 공격 진행 여부를 확인한다.
    public bool IsAttacking => _isAttackActive;

    // 검날의 공격 범위를 표현하는 콜라이더.
    PolygonCollider2D _hitCollider;

    // 스프라이트에 저장된 윤곽 좌표를 가져올 때 재사용한다.
    readonly List<Vector2> _shapePoints = new List<Vector2>();

    // Weapon이 실제 공격을 시작했을 때만 true가 된다.
    bool _isAttackActive;

    private void Awake()
    {
        _hitCollider = GetComponent<PolygonCollider2D>();

        // 공격 판정이 캐릭터나 적을 물리적으로 밀지 않도록 한다.
        _hitCollider.isTrigger = true;
        _hitCollider.offset = Vector2.zero;
        _hitCollider.enabled = false;

        // 연결 누락은 실행 시작 시 확인한다.
        if (_characterRenderer == null)
        {
            throw new InvalidOperationException(
                $"{name}: Character Renderer를 연결해야 합니다.");
        }

        if (_attackSprites == null || _attackSprites.Length != 4)
        {
            throw new InvalidOperationException(
                $"{name}: Attack Sprites에는 공격 스프라이트 4장이 필요합니다.");
        }

        for (int i = 0; i < _attackSprites.Length; i++)
        {
            if (_attackSprites[i] == null)
            {
                throw new InvalidOperationException(
                    $"{name}: Attack Sprites의 Element {i}가 비어 있습니다.");
            }
        }

        // 공격 애니메이션 제어에 필요한 연결을 확인한다.
        if (_animator == null || _attackClip == null)
        {
            throw new InvalidOperationException(
                $"{name}: Animator와 Attack Clip을 연결해야 합니다.");
        }

        if (_enemyLayerMask.value == 0)
        {
            throw new InvalidOperationException(
                $"{name}: Enemy Layer Mask에 Enemy를 선택해야 합니다.");
        }

        // Trigger로 구성된 적의 피격 콜라이더도 검색한다.
        _contactFilter = new ContactFilter2D();
        _contactFilter.SetLayerMask(_enemyLayerMask);
        _contactFilter.useTriggers = true;
    }

    // 한 번의 휘두르기를 시작한다.
    public void BeginAttack(int damage, float duration)
    {
        if (_isAttackActive)
        {
            return;
        }

        if (duration <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        _damage = damage;
        _previousNormalizedTime = 0f;
        _hitTargets.Clear();
        _isAttackActive = true;

        // 공격 클립 전체가 현재 무기 쿨타임 안에 재생되도록 한다.
        _animator.SetFloat(
            AttackSpeedHash,
            _attackClip.length / duration);

        // 키를 떼더라도 시작한 휘두르기는 끝까지 진행한다.
        _animator.SetBool(AttackHash, true);
        _animator.Play(AttackStateHash, 0, 0f);
    }

    // 공격 종료, 무기 교체, 비활성화 시 호출한다.
    public void CancelAttack()
    {
        bool wasAttacking = _isAttackActive;

        _isAttackActive = false;
        _hitTargets.Clear();

        // 부모의 Awake에서 호출되어 아직 초기화 전인 경우도 처리한다.
        if (_hitCollider != null)
        {
            _hitCollider.enabled = false;
        }

        // 실제 검 공격을 제어하고 있었을 때만 Animator를 복구한다.
        if (wasAttacking && _animator != null &&
            _animator.isActiveAndEnabled)
        {
            _animator.SetBool(AttackHash, false);
            _animator.SetFloat(AttackSpeedHash, 1f);
            _animator.Play(IdleStateHash, 0, 0f);
        }
    }

    private void LateUpdate()
    {
        if (!_isAttackActive || Time.timeScale <= 0f)
        {
            _hitCollider.enabled = false;
            return;
        }

        AnimatorStateInfo state =
            _animator.GetCurrentAnimatorStateInfo(0);

        // 다른 상태로 강제 전환됐다면 검 공격도 종료한다.
        if (state.fullPathHash != AttackStateHash)
        {
            CancelAttack();
            return;
        }

        float currentTime = Mathf.Clamp01(state.normalizedTime);

        // 현재 Attack4는 각 프레임이 전체 시간의 25%씩 차지한다.
        // 2번째 그림: 0.25~0.5 / 3번째 그림: 0.5~0.75.
        for (int frameIndex = 1; frameIndex <= 2; frameIndex++)
        {
            float frameStart = frameIndex * 0.25f;
            float frameEnd = (frameIndex + 1) * 0.25f;

            // 지난 검사 이후 이 공격 프레임을 거쳤는지 확인한다.
            // 공격이 빨라져 화면에서 한 프레임을 건너뛰어도
            // 해당 프레임의 피해 검사 자체는 누락하지 않는다.
            if (_previousNormalizedTime < frameEnd &&
                currentTime >= frameStart)
            {
                CheckDamage(_attackSprites[frameIndex]);

                // 피해 처리 중 공격이 취소됐다면 즉시 종료한다.
                if (!_isAttackActive)
                {
                    return;
                }
            }
        }

        _previousNormalizedTime = currentTime;

        // 루프 클립이어도 한 바퀴가 끝나면 이번 공격은 종료한다.
        if (state.normalizedTime >= 1f)
        {
            CancelAttack();
        }
    }

    // 지정한 공격 그림의 검날과 겹친 적을 검색한다.
    private void CheckDamage(Sprite sprite)
    {
        _overlapResults.Clear();

        try
        {
            // 기존 함수가 스케일과 반전을 반영해 윤곽을 적용한다.
            ApplySpriteShape(sprite);

            if (!_hitCollider.enabled)
            {
                return;
            }

            // 이번 프레임의 이동과 스케일 변경을 물리 검색에 반영한다.
            Physics2D.SyncTransforms();

            _hitCollider.Overlap(
                _contactFilter,
                _overlapResults);
        }
        finally
        {
            // 검색 직후 콜라이더를 끈다.
            // 적이 검날을 플레이어의 몸으로 인식하는 것을 방지한다.
            _hitCollider.enabled = false;
        }

        for (int i = 0; i < _overlapResults.Count; i++)
        {
            Collider2D hitCollider = _overlapResults[i];

            if (hitCollider == null)
            {
                continue;
            }

            // 여러 피격 콜라이더가 있어도 같은 적으로 묶어 처리한다.
            IDamageable target =
                hitCollider.GetComponentInParent<IDamageable>();

            if (target == null || target.IsDead)
            {
                continue;
            }

            // 이번 휘두르기에 처음 맞은 대상에게만 피해를 준다.
            if (_hitTargets.Add(target))
            {
                target.TakeHit(_damage);

                if (!_isAttackActive)
                {
                    return;
                }
            }
        }
    }

    // 현재 스프라이트의 검날 윤곽을 공격 콜라이더에 적용한다.
    private void ApplySpriteShape(Sprite sprite)
    {
        int shapeCount = sprite.GetPhysicsShapeCount();

        if (shapeCount == 0)
        {
            _hitCollider.enabled = false;
            return;
        }

        _hitCollider.pathCount = shapeCount;

        for (int pathIndex = 0; pathIndex < shapeCount; pathIndex++)
        {
            _shapePoints.Clear();

            // Unity가 피벗과 Pixels Per Unit을 반영한 좌표를 반환한다.
            sprite.GetPhysicsShape(pathIndex, _shapePoints);

            for (int pointIndex = 0;
                 pointIndex < _shapePoints.Count;
                 pointIndex++)
            {
                Vector2 point = _shapePoints[pointIndex];

                // SpriteRenderer의 반전은 Transform에 반영되지 않으므로
                // 윤곽 좌표에도 직접 같은 반전을 적용한다.
                if (_characterRenderer.flipX)
                {
                    point.x = -point.x;
                }

                if (_characterRenderer.flipY)
                {
                    point.y = -point.y;
                }

                // Renderer의 위치, 회전, 스케일을 적용한 월드 좌표.
                Vector3 worldPoint =
                    _characterRenderer.transform.TransformPoint(point);

                // 월드 좌표를 공격 콜라이더의 로컬 좌표로 바꾼다.
                // 따라서 부모의 스케일이 달라도 그림 위치에 맞는다.
                Vector3 localPoint =
                    transform.InverseTransformPoint(worldPoint);

                _shapePoints[pointIndex] =
                    new Vector2(localPoint.x, localPoint.y);
            }

            _hitCollider.SetPath(pathIndex, _shapePoints);
        }

        _hitCollider.enabled = true;
    }

    private void OnDisable()
    {
        // 비활성화되면 공격 상태와 피해 기록을 함께 정리한다.
        CancelAttack();
    }
}
using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// FirePoint에서 시작하여 오른쪽으로 뻗는 펄스형 레이저다.
/// 가장 가까운 적의 Collider 앞에서 멈추며,
/// 적이 사라지면 최대 사거리까지 다시 길어진다.
/// </summary>
public class Lazer : MonoBehaviour
{
    [Header("레이저 렌더러")]
    [Tooltip("레이저 바깥쪽의 발광 효과")]
    [SerializeField] LineRenderer _glowRenderer;

    [Tooltip("레이저 중심의 밝은 선")]
    [SerializeField] LineRenderer _coreRenderer;

    [Header("충돌 설정")]
    [Tooltip("레이저가 충돌할 Enemy 레이어")]
    [SerializeField] LayerMask _enemyLayerMask;

    [Tooltip("적이 없을 때 레이저가 도달할 최대 거리")]
    [Min(0.1f)]
    [SerializeField] float _maxDistance = 20f;

    [Header("레이저 크기")]
    [Tooltip("바깥쪽 발광선의 최대 굵기")]
    [Min(0.01f)]
    [SerializeField] float _glowWidth = 0.25f;

    [Tooltip("가운데 중심선의 최대 굵기")]
    [Min(0.01f)]
    [SerializeField] float _coreWidth = 0.08f;

    [Header("레이저 시간")]
    [Tooltip("레이저가 나타나서 사라질 때까지의 전체 시간")]
    [Min(0.01f)]
    [SerializeField] float _beamDuration = 0.18f;

    [Tooltip("레이저가 빠르게 굵어지는 시간")]
    [Min(0.01f)]
    [SerializeField] float _appearDuration = 0.04f;

    [Tooltip("레이저가 가늘어지며 사라지는 시간")]
    [Min(0.01f)]
    [SerializeField] float _disappearDuration = 0.05f;

    // 레이저가 명중한 위치에 생성할 공통 히트 이펙트다.
    [SerializeField] private SpriteOneShotEffect _hitEffectPrefab;

    // 현재 레이저를 발사한 캐릭터의 FirePoint
    Transform _firePoint;

    // WeaponRuntimeState에서 전달받은 현재 공격력
    int _damage;

    // Weapon에서 초기화가 끝났는지 확인한다.
    bool _isInitialized;

    // 레이저 굵기 연출을 보관한다.
    Sequence _beamTween;

    private void Awake()
    {
        if (_glowRenderer == null)
        {
            throw new InvalidOperationException(
                $"{name}의 Lazer에 Glow Renderer가 연결되지 않았습니다.");
        }

        if (_coreRenderer == null)
        {
            throw new InvalidOperationException(
                $"{name}의 Lazer에 Core Renderer가 연결되지 않았습니다.");
        }

        if (_beamDuration < _appearDuration + _disappearDuration)
        {
            throw new InvalidOperationException(
                $"{name}의 Beam Duration은 " +
                "Appear Duration과 Disappear Duration의 합보다 길어야 합니다.");
        }

        // LineRenderer가 월드 좌표의 시작점과 끝점을 사용하게 한다.
        _glowRenderer.useWorldSpace = true;
        _coreRenderer.useWorldSpace = true;

        // 레이저는 시작점과 끝점 두 개로 그린다.
        _glowRenderer.positionCount = 2;
        _coreRenderer.positionCount = 2;

        // 발사 전에는 보이지 않게 굵기를 0으로 만든다.
        SetWidthRatio(0f);
    }

    /// <summary>
    /// Weapon이 레이저를 생성한 직후 호출한다.
    /// </summary>
    public void Initialize(int damage, Transform firePoint)
    {
        if (damage <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(damage),
                damage,
                "레이저 공격력은 0보다 커야 합니다.");
        }

        if (firePoint == null)
        {
            throw new ArgumentNullException(nameof(firePoint));
        }

        // 현재 공격력을 저장한다.
        _damage = damage;

        // 레이저가 시작될 FirePoint를 저장한다.
        _firePoint = firePoint;

        // 초기화가 완료됐다고 표시한다.
        _isInitialized = true;

        // 생성된 즉시 레이저의 시작점과 끝점을 계산한다.
        RaycastHit2D firstHit = UpdateBeamPosition();

        // 이번 펄스에서 가장 가까운 적 하나에게만 피해를 준다.
        DamageFirstEnemy(firstHit);

        // 레이저가 나타났다가 사라지는 연출을 시작한다.
        PlayBeamAnimation();
    }

    private void LateUpdate()
    {
        if (!_isInitialized)
        {
            return;
        }

        // 캐릭터가 제거됐다면 레이저도 유지할 수 없다.
        if (_firePoint == null)
        {
            Destroy(gameObject);
            return;
        }

        // 플레이어가 움직이거나 적이 죽었을 때
        // 레이저의 시작점과 끝점을 매 프레임 다시 계산한다.
        UpdateBeamPosition();
    }

    /// <summary>
    /// 가장 가까운 적을 탐색하고
    /// 두 LineRenderer의 시작점과 끝점을 갱신한다.
    /// </summary>
    private RaycastHit2D UpdateBeamPosition()
    {
        // 레이저는 항상 현재 FirePoint에서 시작한다.
        Vector2 origin = _firePoint.position;

        // FirePoint가 바라보는 오른쪽이 레이저 발사 방향이다.
        Vector2 direction = _firePoint.right.normalized;

        // Enemy 레이어에 속한 가장 가까운 Collider를 찾는다.
        RaycastHit2D hit = Physics2D.Raycast(
            origin,
            direction,
            _maxDistance,
            _enemyLayerMask);

        // 적에게 부딪혔다면 충돌 지점을 끝점으로 사용한다.
        // 적이 없다면 최대 사거리 위치를 끝점으로 사용한다.
        Vector2 endPosition =
            hit.collider != null
                ? hit.point
                : origin + direction * _maxDistance;

        // 바깥 발광선의 시작점과 끝점을 설정한다.
        _glowRenderer.SetPosition(0, origin);
        _glowRenderer.SetPosition(1, endPosition);

        // 가운데 중심선도 동일한 위치에 그린다.
        _coreRenderer.SetPosition(0, origin);
        _coreRenderer.SetPosition(1, endPosition);

        return hit;
    }

    /// <summary>
    /// 레이저를 처음 발사했을 때 가장 가까운 대상 하나에게 피해를 준다.
    /// </summary>
    private void DamageFirstEnemy(RaycastHit2D hit)
    {
        if (hit.collider == null)
        {
            return;
        }

        // 일반 적과 보스를 공통 인터페이스로 찾는다.
        IDamageable target =
            hit.collider.GetComponentInParent<IDamageable>();

        if (target == null || target.IsDead)
        {
            return;
        }

        // 이번 펄스에서 실제로 피해를 주는 위치에 한 번 생성한다.
        Instantiate(
            _hitEffectPrefab,
            hit.point,
            Quaternion.identity);

        // 기존 피해를 적용한다.
        target.TakeHit(_damage);
    }

    /// <summary>
    /// 레이저가 빠르게 나타났다가 사라지게 한다.
    /// </summary>
    private void PlayBeamAnimation()
    {
        // 이전 연출이 남아 있다면 제거한다.
        _beamTween?.Kill();

        // 레이저가 최대 굵기로 나타난 뒤 유지될 시간이다.
        float holdDuration =
            _beamDuration -
            _appearDuration -
            _disappearDuration;

        _beamTween = DOTween.Sequence();

        // 레이저를 빠르게 최대 굵기로 만든다.
        _beamTween.Append(
            DOVirtual.Float(
                0f,
                1f,
                _appearDuration,
                SetWidthRatio)
            .SetEase(Ease.OutQuad));

        // 설정된 시간 동안 최대 굵기를 유지한다.
        _beamTween.AppendInterval(holdDuration);

        // 레이저를 가늘게 만들면서 사라지게 한다.
        _beamTween.Append(
            DOVirtual.Float(
                1f,
                0f,
                _disappearDuration,
                SetWidthRatio)
            .SetEase(Ease.InQuad));

        _beamTween.OnComplete(() =>
        {
            _beamTween = null;
            Destroy(gameObject);
        });
    }

    /// <summary>
    /// 전달받은 0~1 비율로 발광선과 중심선의 굵기를 변경한다.
    /// </summary>
    private void SetWidthRatio(float ratio)
    {
        ratio = Mathf.Clamp01(ratio);

        float currentGlowWidth = _glowWidth * ratio;
        float currentCoreWidth = _coreWidth * ratio;

        _glowRenderer.startWidth = currentGlowWidth;
        _glowRenderer.endWidth = currentGlowWidth;

        _coreRenderer.startWidth = currentCoreWidth;
        _coreRenderer.endWidth = currentCoreWidth;
    }

    private void OnDestroy()
    {
        _beamTween?.Kill();
        _beamTween = null;
    }
}
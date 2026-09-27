using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 보스 FirePoint에서 화면 밖으로 뻗는 레이저다.
/// 방향에 따라 길이와 충돌 범위를 갱신하고 종료 시 서서히 사라진다.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class BossLaserBeam : MonoBehaviour
{
    // 레이저 그림을 표시하는 자식 SpriteRenderer.
    [SerializeField] SpriteRenderer _beamRenderer;

    // 순서대로 반복 재생할 레이저 스프라이트 4장.
    [SerializeField] Sprite[] _animationFrames = new Sprite[4];

    // 1초 동안 재생하는 프레임 수.
    [SerializeField, Min(0.1f)] float _animationFps = 12f;

    // 실제 시작 위치와 화면 밖 끝 위치를 표시하는 자식 Transform.
    [SerializeField] Transform _startPoint;
    [SerializeField] Transform _endPoint;

    // 실제 피해 판정의 굵기. 경고 범위도 이 값을 사용한다.
    [SerializeField, Min(0.01f)] float _damageWidth = 0.5f;

    // 피해 굵기에 대한 스프라이트 전체 높이의 배율.
    // 스프라이트의 투명 여백과 바깥 전기 효과까지 포함한다.
    [SerializeField, Min(1f)] float _visualWidthMultiplier = 3f;

    // 그림에 곱할 색상. 원본 색상을 사용하려면 흰색으로 둔다.
    [SerializeField] Color _beamColor = Color.white;

    // 화면 바깥으로 추가 연장할 거리.
    [SerializeField, Min(0.1f)] float _outsideMargin = 1f;

    // 공격 종료 후 빛이 완전히 사라지는 시간.
    [SerializeField, Min(0.01f)] float _fadeOutDuration = 0.3f;

    // 같은 플레이어에게 연속으로 피해를 주는 최소 간격.
    [SerializeField, Min(0.01f)] float _damageInterval = 0.2f;

    // 현재 레이저를 사용하는 보스다.
    BossEnemy _owner;

    // 현재 보스의 실제 발사 위치다.
    Transform _firePoint;

    // 게임 화면의 범위를 계산할 카메라다.
    Camera _battleCamera;

    // 현재 발사 방향이다.
    Vector2 _direction;

    // 이번 공격의 피해량이다.
    float _damage;

    // 레이저의 실제 충돌 범위다.
    BoxCollider2D _damageCollider;

    // 초기화가 완료되었는지 확인한다.
    bool _initialized;

    // 현재 피해를 줄 수 있는 상태인지 확인한다.
    bool _isAttacking;

    // 종료 연출을 중복 실행하지 않도록 기록한다.
    bool _isFading;

    // 현재 레이저의 굵기와 투명도 비율이다.
    float _visibility = 1f;

    // 현재 시작점부터 끝점까지의 레이저 길이.
    float _beamLength;

    // 현재 표시하는 애니메이션 프레임.
    int _animationFrameIndex;

    // 다음 프레임으로 넘어가기 위해 누적한 시간.
    float _animationTimer;

    // 종료 연출을 정리하기 위한 Tween 참조다.
    Tween _fadeTween;

    // 여러 Collider가 있는 플레이어도 피해 간격을 공유한다.
    readonly Dictionary<PlayerController, float> _nextDamageTimes =
        new Dictionary<PlayerController, float>();

    // 경고 범위 생성 시 사용할 실제 피해 굵기다.
    public float DamageWidth => _damageWidth;

    // 패턴이 종료 연출 완료를 기다릴 때 사용한다.
    public bool IsFading => _isFading;

    /// <summary>
    /// 컴포넌트 참조와 초기 실행 상태를 준비한다.
    /// </summary>
    private void Awake()
    {
        // 필수 프리팹 연결을 검사한다.
        if (_beamRenderer == null ||
            _startPoint == null ||
            _endPoint == null)
        {
            throw new InvalidOperationException(
                $"{name}: Beam Renderer, StartPoint, EndPoint를 연결하세요.");
        }

        // 그림만 확대해야 하므로 Renderer는 루트의 직접적인 자식으로 둔다.
        if (_beamRenderer.transform.parent != transform)
        {
            throw new InvalidOperationException(
                $"{name}: Beam Renderer는 레이저 루트의 직접적인 자식이어야 합니다.");
        }

        if (_animationFrames == null || _animationFrames.Length != 4)
        {
            throw new InvalidOperationException(
                $"{name}: Animation Frames에 레이저 4컷을 연결하세요.");
        }

        for (int i = 0; i < _animationFrames.Length; i++)
        {
            Sprite frame = _animationFrames[i];

            if (frame == null)
            {
                throw new InvalidOperationException(
                    $"{name}: Animation Frames의 {i}번이 비어 있습니다.");
            }

            // 중심점을 기준으로 길이를 늘리므로 모든 프레임의 Pivot을 검사한다.
            Vector2 centerPivot = frame.rect.size * 0.5f;

            if ((frame.pivot - centerPivot).sqrMagnitude > 0.01f)
            {
                throw new InvalidOperationException(
                    $"{frame.name}: Sprite Pivot을 Center로 설정하세요.");
            }

            // 프레임마다 다른 크기를 잘못 연결하는 것을 방지한다.
            if (i > 0 &&
                (frame.rect.size - _animationFrames[0].rect.size)
                    .sqrMagnitude > 0.01f)
            {
                throw new InvalidOperationException(
                    $"{name}: 레이저 4컷의 Rect 크기를 동일하게 맞춰주세요.");
            }
        }

        _damageCollider = GetComponent<BoxCollider2D>();
        _damageCollider.isTrigger = true;
        _damageCollider.enabled = false;

        // 기존처럼 독립된 Kinematic 레이저로 충돌을 처리한다.
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;

        // 루트가 위치와 회전을 담당하고 자식은 그림 크기만 담당한다.
        _beamRenderer.transform.localPosition = Vector3.zero;
        _beamRenderer.transform.localRotation = Quaternion.identity;
        _beamRenderer.drawMode = SpriteDrawMode.Simple;
        _beamRenderer.flipX = false;
        _beamRenderer.flipY = false;
        _beamRenderer.sprite = _animationFrames[0];

        // Initialize가 호출되기 전에는 보이지 않는다.
        SetVisibility(0f);
    }

    /// <summary>
    /// 패턴이 레이저를 생성한 직후 호출한다.
    /// </summary>
    public void Initialize(
        BossEnemy owner,
        Transform firePoint,
        Camera battleCamera,
        Vector2 direction,
        float damage)
    {
        if (owner == null || firePoint == null || battleCamera == null)
        {
            throw new ArgumentException(
                "레이저의 보스, FirePoint, 카메라를 전달하세요.");
        }

        // 현재 프로젝트의 XY 평면을 바라보는 직교 카메라를 사용한다.
        if (!battleCamera.orthographic ||
            Mathf.Abs(Vector3.Dot(
                battleCamera.transform.forward,
                Vector3.forward)) < 0.9999f)
        {
            throw new InvalidOperationException(
                "보스 레이저는 XY 평면을 바라보는 직교 카메라가 필요합니다.");
        }

        if (damage <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(damage));
        }

        _owner = owner;
        _firePoint = firePoint;
        _battleCamera = battleCamera;
        _damage = damage;

        // 콜라이더 크기를 월드 길이와 일치시키기 위해 독립 배치한다.
        transform.SetParent(null);
        transform.localScale = Vector3.one;

        SetDirection(direction);

        _nextDamageTimes.Clear();
        _initialized = true;
        _isAttacking = true;
        _isFading = false;

        // 새 공격은 첫 번째 프레임부터 재생한다.
        _animationFrameIndex = 0;
        _animationTimer = 0f;
        _beamRenderer.sprite = _animationFrames[0];

        UpdateGeometry();
        SetVisibility(1f);

        _damageCollider.enabled = true;
    }

    /// <summary>
    /// 패턴이 회전 각도에 맞춰 매 프레임 발사 방향을 전달한다.
    /// </summary>
    public void SetDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.000001f)
        {
            throw new ArgumentException(
                "레이저 발사 방향은 0벡터일 수 없습니다.");
        }

        // 종료 연출 중에는 마지막 발사 방향을 유지한다.
        if (_isFading)
        {
            return;
        }

        _direction = direction.normalized;
    }

    /// <summary>
    /// 프레임의 이동 결과에 맞춰 후속 상태를 갱신한다.
    /// </summary>
    private void LateUpdate()
    {
        if (!_initialized)
        {
            return;
        }

        // 공격 중과 소멸 연출 중 모두 애니메이션을 재생한다.
        UpdateAnimation();

        // 소멸 중에는 마지막 위치와 방향을 유지한다.
        if (_isFading)
        {
            return;
        }

        // 보스가 사망하거나 제거되면 피해를 멈추고 소멸한다.
        if (_owner == null || _owner.IsDead ||
            !_owner.isActiveAndEnabled ||
            _firePoint == null || _battleCamera == null)
        {
            BeginFadeOut();
            return;
        }

        UpdateGeometry();
    }

    /// <summary>
    /// UpdateAnimation 동작에 필요한 상태와 처리 흐름을 실행한다.
    /// </summary>
    private void UpdateAnimation()
    {
        // 게임이 일시정지되면 레이저 애니메이션도 함께 멈춘다.
        _animationTimer += Time.deltaTime;

        float frameDuration = 1f / Mathf.Max(0.1f, _animationFps);
        int steps = Mathf.FloorToInt(_animationTimer / frameDuration);

        if (steps == 0)
        {
            return;
        }

        // 프레임이 지연돼도 실제 경과 시간에 맞춰 재생한다.
        _animationTimer -= steps * frameDuration;
        _animationFrameIndex =
            (_animationFrameIndex + steps) % _animationFrames.Length;

        _beamRenderer.sprite = _animationFrames[_animationFrameIndex];

        // 프레임 교체 후에도 현재 길이와 소멸 비율을 유지한다.
        UpdateSpriteAppearance();
    }

    /// <summary>
    /// 화면의 모든 모서리보다 발사 방향상 바깥쪽에 끝점을 배치한다.
    /// 방향과 시작 위치가 바뀌면 필요한 길이도 함께 변한다.
    /// </summary>
    private Vector3 CalculateEndPosition(Vector3 origin)
    {
        // ViewportToWorldPoint에 전달할 카메라 기준 깊이다.
        float depth = Vector3.Dot(
            origin - _battleCamera.transform.position,
            _battleCamera.transform.forward);

        float length = 0f;

        // 각 모서리를 발사 방향에 투영해 화면 전체를 넘는 길이를 구한다.
        for (int y = 0; y <= 1; y++)
        {
            for (int x = 0; x <= 1; x++)
            {
                Vector3 corner = _battleCamera.ViewportToWorldPoint(
                    new Vector3(x, y, depth));

                float projectedDistance = Vector2.Dot(
                    (Vector2)(corner - origin),
                    _direction);

                length = Mathf.Max(length, projectedDistance);
            }
        }

        // 전기 효과까지 화면 밖으로 나가도록 여유 길이를 더한다.
        length += _outsideMargin + _damageWidth * _visualWidthMultiplier;

        return origin + (Vector3)(_direction * length);
    }

    /// <summary>
    /// UpdateGeometry 동작에 필요한 상태와 처리 흐름을 실행한다.
    /// </summary>
    private void UpdateGeometry()
    {
        Vector3 start = _firePoint.position;
        Vector3 end = CalculateEndPosition(start);

        _beamLength = Vector2.Distance(start, end);

        float angle =
            Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;

        // 루트를 구간의 중앙에 배치하고 X축을 발사 방향으로 맞춘다.
        transform.SetPositionAndRotation(
            (start + end) * 0.5f,
            Quaternion.Euler(0f, 0f, angle));

        // 실제 계산한 시작점과 끝점을 표시한다.
        _startPoint.position = start;
        _endPoint.position = end;

        // 피해 판정은 기존과 같은 길이와 굵기를 사용한다.
        _damageCollider.offset = Vector2.zero;
        _damageCollider.size =
            new Vector2(_beamLength, _damageWidth);

        // 그림도 같은 시작점과 끝점 사이에 맞춘다.
        UpdateSpriteAppearance();
    }

    /// <summary>
    /// OnTriggerEnter2D 동작에 필요한 상태와 처리 흐름을 실행한다.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        // 처음 레이저에 닿은 순간에도 피해를 처리한다.
        TryDamage(other);
    }

    /// <summary>
    /// OnTriggerStay2D 동작에 필요한 상태와 처리 흐름을 실행한다.
    /// </summary>
    private void OnTriggerStay2D(Collider2D other)
    {
        // 계속 닿아 있으면 설정한 간격으로 피해를 처리한다.
        TryDamage(other);
    }

    /// <summary>
    /// 플레이어에게만 피해를 주고 중복 Collider의 연속 피해를 막는다.
    /// </summary>
    private void TryDamage(Collider2D other)
    {
        if (!_isAttacking || Time.deltaTime <= 0f)
        {
            return;
        }

        // 물리 처리 시점에도 보스 생존 여부를 확인한다.
        if (_owner == null || _owner.IsDead ||
            !_owner.isActiveAndEnabled)
        {
            BeginFadeOut();
            return;
        }

        PlayerController player =
            other.GetComponentInParent<PlayerController>();

        if (player == null)
        {
            return;
        }

        if (_nextDamageTimes.TryGetValue(player, out float nextTime) &&
            Time.time < nextTime)
        {
            return;
        }

        _nextDamageTimes[player] = Time.time + _damageInterval;
        player.TakeHit(_damage);
    }

    /// <summary>
    /// 피해를 즉시 끄고 마지막 위치에서 빛을 서서히 없앤다.
    /// </summary>
    public void BeginFadeOut()
    {
        if (_isFading)
        {
            return;
        }

        _isAttacking = false;
        _isFading = true;
        _damageCollider.enabled = false;

        // 이후 위치 갱신을 멈춰 마지막 레이저 모양을 유지한다.
        _fadeTween = DOVirtual.Float(
                _visibility,
                0f,
                _fadeOutDuration,
                SetVisibility)
            .SetEase(Ease.InOutSine)
            .OnComplete(() =>
            {
                _fadeTween = null;
                Destroy(gameObject);
            });
    }

    /// <summary>
    /// 보스 패턴이 소멸 연출 완료까지 기다릴 때 사용한다.
    /// </summary>
    public IEnumerator FadeOutAndWait()
    {
        BeginFadeOut();

        // Tween 완료 후 실제 오브젝트가 제거될 때까지 기다린다.
        while (this != null)
        {
            yield return null;
        }
    }

    /// <summary>
    /// SetVisibility 동작에 필요한 상태와 처리 흐름을 실행한다.
    /// </summary>
    private void SetVisibility(float ratio)
    {
        // 기존 DOTween이 전달하는 소멸 비율을 사용한다.
        _visibility = Mathf.Clamp01(ratio);
        UpdateSpriteAppearance();
    }

    /// <summary>
    /// UpdateSpriteAppearance 동작에 필요한 상태와 처리 흐름을 실행한다.
    /// </summary>
    private void UpdateSpriteAppearance()
    {
        Sprite sprite = _beamRenderer.sprite;

        // 스프라이트 원본 크기를 Unity 단위로 변환한다.
        float originalWidth = sprite.rect.width / sprite.pixelsPerUnit;
        float originalHeight = sprite.rect.height / sprite.pixelsPerUnit;

        // 가로는 실제 레이저 길이, 세로는 그림의 표시 굵기에 맞춘다.
        // 소멸할 때 가로 길이는 유지하고 굵기만 줄인다.
        float visualHeight =
            _damageWidth * _visualWidthMultiplier * _visibility;

        _beamRenderer.transform.localScale = new Vector3(
            _beamLength / originalWidth,
            visualHeight / originalHeight,
            1f);

        // 굵기가 줄어드는 동시에 투명해진다.
        Color color = _beamColor;
        color.a *= _visibility;
        _beamRenderer.color = color;
    }

    /// <summary>
    /// 비활성화 시 이벤트 연결과 실행 상태를 정리한다.
    /// </summary>
    private void OnDisable()
    {
        // 비활성화된 레이저가 피해나 Tween을 유지하지 않게 한다.
        _isAttacking = false;

        if (_damageCollider != null)
        {
            _damageCollider.enabled = false;
        }

        if (_fadeTween != null)
        {
            _fadeTween.Kill();
            _fadeTween = null;
        }
    }
}
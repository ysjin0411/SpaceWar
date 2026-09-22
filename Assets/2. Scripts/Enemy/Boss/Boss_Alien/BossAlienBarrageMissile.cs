using System;
using UnityEngine;

// 지정된 시작점에서 예고한 월드 좌표까지 미사일 외형을 이동한다.
// 피해 판정과 폭발 생성은 회차를 관리하는 패턴에서 처리한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class BossAlienBarrageMissile : MonoBehaviour
{
    // 미사일 외형과 전투 종료 여부를 확인할 보스다.
    private SpriteRenderer _visual;
    private Boss_Alien _owner;

    // 발사 시 확정한 출발점과 도착점이다.
    // 낙하 중 플레이어 위치를 따라 변경하지 않는다.
    private Vector3 _startPosition;
    private Vector3 _targetPosition;

    // 전체 낙하 시간과 현재 경과 시간이다.
    private float _fallDuration;
    private float _elapsedTime;

    // 초기화 여부와 현재 낙하 중인지 기록한다.
    private bool _initialized;
    private bool _isFalling;

    // 실제로 목표에 도착했을 때만 true가 된다.
    // 취소된 미사일은 도착한 것으로 처리하지 않는다.
    public bool HasArrived { get; private set; }

    // 패턴이 폭발을 생성할 때 사용할 확정된 도착점이다.
    public Vector3 ImpactPosition => _targetPosition;

    private void Awake()
    {
        // 같은 오브젝트의 외형을 사용한다.
        _visual = GetComponent<SpriteRenderer>();

        // 설정을 전달받기 전에는 미사일을 표시하지 않는다.
        _visual.enabled = false;
    }

    // 낙하 시간과 위치는 이후 패턴 설정에서 계산해 전달한다.
    public void Launch(
        Boss_Alien owner,
        Sprite sprite,
        Vector2 startPosition,
        Vector2 targetPosition,
        float fallDuration)
    {
        if (_initialized ||
            owner == null ||
            sprite == null ||
            !IsFinite(startPosition.x) ||
            !IsFinite(startPosition.y) ||
            !IsFinite(targetPosition.x) ||
            !IsFinite(targetPosition.y) ||
            !IsFinite(fallDuration) ||
            fallDuration <= 0f ||
            (targetPosition - startPosition).sqrMagnitude <= 0f)
        {
            throw new ArgumentException(
                $"{name}: 미사일의 초기화 상태와 낙하 설정을 확인하세요.");
        }

        // 이번 실행에 사용할 값을 저장한다.
        _owner = owner;
        _startPosition = new Vector3(
            startPosition.x, startPosition.y, 0f);
        _targetPosition = new Vector3(
            targetPosition.x, targetPosition.y, 0f);
        _fallDuration = fallDuration;
        _elapsedTime = 0f;
        HasArrived = false;

        // 원본 이미지가 아래쪽(-Y)을 향하므로 90도를 보정한다.
        // 수직 아래로 낙하할 때 최종 회전은 0도가 된다.
        Vector2 direction = targetPosition - startPosition;
        float angle =
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f;

        transform.SetPositionAndRotation(
            _startPosition,
            Quaternion.Euler(0f, 0f, angle));

        // 미사일 종류가 달라도 프리팹 하나로 표시할 수 있다.
        _visual.sprite = sprite;
        _visual.color = Color.white;
        _visual.enabled = true;

        _initialized = true;
        _isFalling = true;
    }

    private void Update()
    {
        if (!_initialized)
        {
            return;
        }

        // 낙하 중이거나 도착 후 대기 중이어도 전투 종료 시 제거한다.
        if (_owner == null || !_owner.CanRunArmPattern)
        {
            Remove();
            return;
        }

        // 도착 후에는 위치를 갱신하지 않는다.
        // 일반적인 일시정지에서는 현재 위치를 유지한다.
        if (!_isFalling || Time.deltaTime <= 0f)
        {
            return;
        }

        _elapsedTime += Time.deltaTime;
        float progress = Mathf.Clamp01(_elapsedTime / _fallDuration);

        // 처음에는 느리고, 목표에 가까워질수록 빨라지는 낙하다.
        float easedProgress = progress * progress;
        transform.position = Vector3.Lerp(
            _startPosition, _targetPosition, easedProgress);

        if (progress >= 1f)
        {
            // 정확한 도착점으로 고정하고 미사일 외형을 숨긴다.
            transform.position = _targetPosition;
            _isFalling = false;
            _visual.enabled = false;

            // 오브젝트는 패턴이 도착을 확인하고 제거할 때까지 유지한다.
            HasArrived = true;
        }
    }

    // 도착 처리 후 또는 패턴 취소 시 호출한다.
    public void Remove()
    {
        _initialized = false;
        _isFalling = false;

        if (_visual != null)
        {
            _visual.enabled = false;
        }

        Destroy(gameObject);
    }

    // 위치와 시간에 사용할 수 없는 숫자를 걸러낸다.
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
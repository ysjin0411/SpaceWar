using System;
using UnityEngine;

// 기록된 위치에서 발생하는 발톱의 표시를 담당한다.
// 단계별 대기 시간과 실제 피해 판정은 패턴 코드에서 처리한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class BossAlienTraceClaw : MonoBehaviour
{
    // 발톱이 지면에서 올라오기 시작하는 이미지다.
    [SerializeField]
    private Sprite _riseSprite;

    // 발톱이 완전히 솟아오른 이미지다.
    [SerializeField]
    private Sprite _peakSprite;

    // 발톱이 사라지는 이미지다.
    [SerializeField]
    private Sprite _fadeSprite;

    // 이 효과를 생성한 보스와 이미지 출력 컴포넌트다.
    private Boss_AlienBody _owner;
    private SpriteRenderer _visual;

    // 초기화가 끝난 효과만 보스 상태를 검사한다.
    private bool _initialized;

    private void Awake()
    {
        // 생성 직후에는 초기화되지 않은 이미지가 보이지 않도록 한다.
        _visual = GetComponent<SpriteRenderer>();
        _visual.drawMode = SpriteDrawMode.Simple;
        _visual.enabled = false;
    }

    // 고정된 공격 좌표에 발톱을 배치하고 첫 이미지를 표시한다.
    public void Initialize(
        Boss_AlienBody owner, Vector2 center, float radius)
    {
        // 부모의 크기와 회전이 월드 기준 효과에 영향을 주지 않게 한다.
        if (transform.parent != null)
        {
            throw new InvalidOperationException(
                $"{name}: 발톱 효과는 부모 없이 생성하세요.");
        }

        // 누락된 이미지나 잘못된 좌표·반경을 조기에 확인한다.
        if (owner == null ||
            _riseSprite == null ||
            _peakSprite == null ||
            _fadeSprite == null ||
            !IsFinite(center.x) ||
            !IsFinite(center.y) ||
            !IsFinite(radius) ||
            radius <= 0f)
        {
            throw new InvalidOperationException(
                $"{name}: 보스, 스프라이트, 좌표, 반경을 확인하세요.");
        }

        float sourceWidth = _peakSprite.bounds.size.x;

        if (sourceWidth <= 0f)
        {
            throw new InvalidOperationException(
                $"{name}: Peak 스프라이트의 너비가 올바르지 않습니다.");
        }

        _owner = owner;

        // 세 이미지의 기준점을 동일한 월드 좌표에 배치한다.
        transform.SetPositionAndRotation(
            new Vector3(center.x, center.y, 0f),
            Quaternion.identity);

        // Peak 이미지의 전체 너비를 공격 지름에 맞춘다.
        // 같은 배율을 유지하여 단계마다 발톱 크기가 튀지 않게 한다.
        float scale = radius * 2f / sourceWidth;
        transform.localScale = new Vector3(scale, scale, 1f);

        _visual.color = Color.white;
        _initialized = true;
        ShowSprite(_riseSprite);
    }

    // 패턴이 피해를 판정하는 순간에 완전히 솟은 모습을 표시한다.
    public void ShowPeak()
    {
        ShowSprite(_peakSprite);
    }

    // 패턴이 마무리 단계에 들어가면 사라지는 모습을 표시한다.
    public void ShowFade()
    {
        ShowSprite(_fadeSprite);
    }

    private void Update()
    {
        // 보스 사망이나 전투 중단 시 남아 있는 효과를 정리한다.
        if (_initialized &&
            (_owner == null || !_owner.CanRunPhaseTwoPattern))
        {
            Remove();
        }
    }

    // 전투가 유효한 경우에만 요청받은 이미지를 표시한다.
    private void ShowSprite(Sprite sprite)
    {
        if (!_initialized)
            return;

        if (_owner == null || !_owner.CanRunPhaseTwoPattern)
        {
            Remove();
            return;
        }

        _visual.sprite = sprite;
        _visual.enabled = true;
    }

    // 패턴 완료 또는 중단 시 즉시 숨긴 뒤 오브젝트를 제거한다.
    public void Remove()
    {
        _initialized = false;

        if (_visual != null)
            _visual.enabled = false;

        Destroy(gameObject);
    }

    // 잘못된 수치가 위치나 크기 계산에 들어가지 않도록 검사한다.
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
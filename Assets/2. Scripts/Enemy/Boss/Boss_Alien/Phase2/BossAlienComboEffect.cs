using System;
using UnityEngine;

// 연속 공격의 시각 효과만 표시한다.
// 피해 판정과 재생 시간은 패턴 코드가 관리한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class BossAlienComboEffect : MonoBehaviour
{
    // 이미지를 표시할 렌더러와 효과를 생성한 본체다.
    private SpriteRenderer _visual;
    private Boss_AlienBody _owner;

    // 초기화된 효과만 상태를 확인하고 표시를 갱신한다.
    private bool _initialized;

    // 이번 타격에서 순서대로 표시할 이펙트 이미지 목록이다.
    [SerializeField]private Sprite[] _frames = Array.Empty<Sprite>();

    private void Awake()
    {
        // 위치와 이미지가 설정되기 전에는 표시하지 않는다.
        _visual = GetComponent<SpriteRenderer>();
        _visual.drawMode = SpriteDrawMode.Simple;
        _visual.enabled = false;
    }

    // 소유자와 재생할 이미지 목록을 설정한다.
    // 위치·방향·크기는 SetHandPose에서 본체를 기준으로 설정한다.
    public void Initialize(
        Boss_AlienBody owner,
        Sprite[] frames)
    {
        // 보스 부모의 Scale이 중복 적용되지 않도록 독립 오브젝트로 사용한다.
        if (transform.parent != null)
        {
            throw new InvalidOperationException(
                $"{name}: 공격 이펙트는 부모 없이 생성하세요.");
        }

        if (owner == null || frames == null || frames.Length == 0)
        {
            throw new ArgumentException(
                $"{name}: 소유자와 이펙트 이미지 목록을 확인하세요.");
        }

        // 재생 도중 이미지가 누락되지 않도록 미리 검사한다.
        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] == null)
            {
                throw new ArgumentException(
                    $"{name}: 이펙트 이미지 {i}번이 비어 있습니다.");
            }
        }

        _owner = owner;
        _frames = frames;

        // 첫 이미지를 준비한다. 생성 직후 패턴에서 손 앞 위치를 적용한다.
        _visual.sprite = _frames[0];
        _visual.flipX = false;
        _visual.flipY = false;
        _visual.color = Color.white;
        _visual.enabled = true;

        _initialized = true;
    }

    // 본체의 손 위치와 좌우 반전을 기준으로 이펙트를 배치한다.
    public void SetHandPose(
        Transform bodyVisual,
        Vector2 handLocalPosition,
        bool flipX,
        bool flipY,
        float scaleRatio)
    {
        if (!_initialized)
            return;

        // 이펙트 중심을 손보다 조금 앞에 놓는다.
        // 원본이 왼쪽을 향하므로 반전 전의 앞쪽은 X 음수 방향이다.
        Vector3 localPosition = new Vector3(
            handLocalPosition.x - 1.5f,
            handLocalPosition.y,
            0f);

        // SpriteRenderer 반전은 Transform에 포함되지 않으므로 직접 반영한다.
        if (flipX)
            localPosition.x = -localPosition.x;

        if (flipY)
            localPosition.y = -localPosition.y;

        // 부모 위치와 Scale을 포함해 손 앞의 월드 위치를 계산한다.
        transform.SetPositionAndRotation(
            bodyVisual.TransformPoint(localPosition),
            bodyVisual.rotation);

        // 경고 사각형 크기 대신 본체 크기를 기준으로 이펙트 비율을 유지한다.
        Vector3 bodyScale = bodyVisual.lossyScale;
        transform.localScale = new Vector3(
            Mathf.Abs(bodyScale.x) * scaleRatio,
            Mathf.Abs(bodyScale.y) * scaleRatio,
            1f);

        // 이펙트 그림도 본체와 같은 방향으로 반전한다.
        _visual.flipX = flipX;
        _visual.flipY = flipY;
    }

    // 실제 공격 구간의 진행률에 맞춰 이펙트 이미지를 표시한다.
    public void SetProgress(float progress)
    {
        if (!_initialized)
            return;

        // 진행률을 이미지 번호로 바꾸고 배열 범위를 넘지 않게 제한한다.
        int frameIndex = Mathf.Clamp(
            Mathf.FloorToInt(Mathf.Clamp01(progress) * _frames.Length),
            0,
            _frames.Length - 1);

        _visual.sprite = _frames[frameIndex];

        // 소멸 표현이 이미지에 포함되어 있으므로 추가 페이드는 적용하지 않는다.
        _visual.color = Color.white;
    }

    private void Update()
    {
        // 전투가 끝나면 패턴의 정리 처리와 별개로 효과도 제거한다.
        if (_initialized &&
            (_owner == null || !_owner.CanRunPhaseTwoPattern))
        {
            Remove();
        }
    }

    // 정상 종료와 패턴 취소 모두 같은 방식으로 제거한다.
    public void Remove()
    {
        _initialized = false;

        if (_visual != null)
            _visual.enabled = false;

        Destroy(gameObject);
    }

    // 잘못된 수치가 Transform 계산에 들어가지 않도록 검사한다.
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
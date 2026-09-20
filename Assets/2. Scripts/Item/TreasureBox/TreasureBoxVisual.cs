using System;
using UnityEngine;
using System.Collections;

public class TreasureBoxVisual : MonoBehaviour
{
    // 상자 중앙에서 회전할 CenterEffect의 Transform이다.
    [SerializeField]
    Transform _centerEffect;

    // 중앙 효과가 1초 동안 회전하는 각도다.
    // 양수는 반시계 방향, 음수는 시계 방향이다.
    [SerializeField]
    float _rotationSpeed = 60f;

    // 주변에서 나타났다 사라질 효과들이다.
    // Box와 CenterEffect는 이 배열에 넣지 않는다.
    [SerializeField]
    SpriteRenderer[] _sparkles = new SpriteRenderer[3];

    // 각 효과가 나타나고 사라진 뒤 다시 반복하기까지의 시간이다.
    [SerializeField, Min(0.1f)]
    float _sparkleCycleSeconds = 1.5f;

    // 상자 본체 그림을 표시하는 자식 Box의 SpriteRenderer다.
    [SerializeField]
    SpriteRenderer _boxRenderer;

    // 현재 등급에 대응하는 열린 상자 스프라이트다.
    [SerializeField]
    Sprite _openedSprite;

    // 열린 상자의 내부에서 골드가 출발할 위치다.
    [SerializeField]
    Transform _rewardPoint;

    // 퇴장 중 스파클 코드가 투명도를 다시 덮어쓰지 않도록 구분한다.
    bool _isFadingOut;

    // 부모의 위치와 크기까지 반영된 실제 월드 위치를 전달한다.
    public Vector3 RewardPosition => _rewardPoint.position;

    // 보상 지급 전에 상자 본체를 열린 모습으로 바꾼다.
    public void ShowOpened()
    {
        // 위치, 크기, 주변 효과는 유지하고 상자 그림만 교체한다.
        _boxRenderer.sprite = _openedSprite;
    }

    // 프리팹에 설정한 원래 색상과 최대 불투명도를 보관한다.
    Color[] _originalColors;

    // 재활성화할 때 중앙 효과의 회전을 초기화할 기준이다.
    Quaternion _initialCenterRotation;

    // 현재 반복 주기 안에서 흐른 시간이다.
    float _cycleTime;

    private void Awake()
    {
        // 보상 위치가 해당 등급 외형의 자식으로 연결되었는지 확인한다.
        if (_rewardPoint == null ||
            _rewardPoint == transform ||
            !_rewardPoint.IsChildOf(transform))
        {
            throw new InvalidOperationException(
                $"{name}: 자식 RewardPoint를 연결하세요.");
        }

        // 필수 외형 연결이 빠졌다면 등장 시점에 설정 오류를 알린다.
        if (_boxRenderer == null || _openedSprite == null)
        {
            throw new InvalidOperationException(
                $"{name}: Box Renderer와 Opened Sprite를 연결하세요.");
        }

        // 필수 연결이 빠졌다면 실행 초기에 설정 오류를 알린다.
        if (_centerEffect == null ||
            _sparkles == null ||
            _sparkles.Length == 0 ||
            _sparkleCycleSeconds <= 0f)
        {
            throw new InvalidOperationException(
                $"{name}: 중앙 효과, 주변 효과, 반복 시간을 확인하세요.");
        }

        _initialCenterRotation = _centerEffect.localRotation;
        _originalColors = new Color[_sparkles.Length];

        // 원래 색상을 저장해 투명도만 바꾸도록 준비한다.
        for (int i = 0; i < _sparkles.Length; i++)
        {
            if (_sparkles[i] == null)
            {
                throw new InvalidOperationException(
                    $"{name}: Sparkles의 {i}번 효과가 비어 있습니다.");
            }

            _originalColors[i] = _sparkles[i].color;
        }
    }

    private void OnEnable()
    {
        // 다시 활성화되면 퇴장 상태를 해제하고 반복 연출을 재개한다.
        _isFadingOut = false;

        // 새 외형이 등장하거나 다시 활성화되면 연출을 처음부터 시작한다.
        _cycleTime = 0f;
        _centerEffect.localRotation = _initialCenterRotation;

        ApplySparkleAlpha();
    }

    private void Update()
    {
        // 퇴장 중에는 FadeOut만 투명도를 변경하도록 반복 연출을 멈춘다.
        if (_isFadingOut)
        {
            return;
        }

        // 프레임 속도와 관계없이 초당 설정한 각도만큼 회전한다.
        // Time.deltaTime을 사용하므로 일시정지하면 효과도 멈춘다.
        _centerEffect.Rotate(
            0f,
            0f,
            _rotationSpeed * Time.deltaTime,
            Space.Self);

        // 시간을 한 주기 안으로 유지해 장시간 실행 시에도 계산을 안정시킨다.
        _cycleTime = Mathf.Repeat(
            _cycleTime + Time.deltaTime,
            _sparkleCycleSeconds);

        ApplySparkleAlpha();
    }

    // 효과마다 시차를 두고 투명도를 변경한다.
    private void ApplySparkleAlpha()
    {
        // 현재 시간을 0~1 범위의 주기 진행률로 바꾼다.
        float cycleProgress = _cycleTime / _sparkleCycleSeconds;

        for (int i = 0; i < _sparkles.Length; i++)
        {
            // 효과들이 동시에 깜빡이지 않도록 시작 지점을 나눈다.
            float phaseOffset = (float)i / _sparkles.Length;
            float phase = (cycleProgress + phaseOffset) * Mathf.PI * 2f;

            // 주기의 절반은 서서히 나타났다 사라지고,
            // 나머지 절반은 완전히 투명한 상태로 유지한다.
            float visibility = Mathf.Max(0f, Mathf.Sin(phase));

            // 색상은 유지하고 원래 불투명도에 가시성을 곱한다.
            Color color = _originalColors[i];
            color.a *= visibility;
            _sparkles[i].color = color;
        }
    }

    private void OnDisable()
    {
        // 비활성화할 때 변경한 회전과 색상을 원래 상태로 되돌린다.
        _centerEffect.localRotation = _initialCenterRotation;

        for (int i = 0; i < _sparkles.Length; i++)
        {
            _sparkles[i].color = _originalColors[i];
        }
    }

    // 상자 본체와 중앙 효과, 주변 스파클을 함께 서서히 숨긴다.
    public IEnumerator FadeOut(float duration)
    {
        _isFadingOut = true;

        // 현재 외형에 속한 모든 스프라이트를 가져온다.
        SpriteRenderer[] renderers =
            GetComponentsInChildren<SpriteRenderer>(true);

        // 현재 색상과 투명도를 기준으로 자연스럽게 사라지도록 저장한다.
        Color[] startColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            startColors[i] = renderers[i].color;
        }

        float elapsed = 0f;

        // 지정한 시간 동안 모든 스프라이트의 투명도를 줄인다.
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(elapsed / duration);
            float visibility = 1f - Mathf.SmoothStep(0f, 1f, progress);

            for (int i = 0; i < renderers.Length; i++)
            {
                Color color = startColors[i];
                color.a *= visibility;
                renderers[i].color = color;
            }

            yield return null;
        }

        // 시간이 0인 경우까지 포함해 마지막에는 완전히 숨긴다.
        for (int i = 0; i < renderers.Length; i++)
        {
            Color color = startColors[i];
            color.a = 0f;
            renderers[i].color = color;
        }
    }
}
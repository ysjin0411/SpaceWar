using UnityEngine;

// 플레이 모드뿐 아니라 프리팹을 편집할 때도 팔 연결을 확인한다.
[ExecuteAlways]
[DefaultExecutionOrder(100)]
public sealed class BossAlienArmRig : MonoBehaviour
{
    // 본체에서 팔이 시작되는 어깨 연결 위치다.
    [SerializeField] private Transform _armSocket;

    // 길이와 방향을 변경할 팔 이미지 오브젝트다.
    // 손은 이 오브젝트의 자식으로 두지 않는다.
    [SerializeField] private Transform _armVisual;

    // 팔 그림에서 어깨 쪽 끝을 표시한다.
    // ArmVisual의 직접 자식이어야 한다.
    [SerializeField] private Transform _armStart;

    // 팔 그림에서 손목 쪽 끝을 표시한다.
    // ArmVisual의 직접 자식이어야 한다.
    [SerializeField] private Transform _armEnd;

    // 손목 위치를 나타내며, 실제 공격에서 이동시킬 오브젝트다.
    [SerializeField] private Transform _handRoot;

    // 열린 손과 잡은 손 이미지를 표시한다.
    [SerializeField] private SpriteRenderer _handRenderer;

    // 플레이어를 잡기 전 표시할 손 이미지다.
    [SerializeField] private Sprite _openHandSprite;

    // 플레이어를 잡은 동안 표시할 손 이미지다.
    [SerializeField] private Sprite _closedHandSprite;

    // 잡힌 플레이어의 중심이 따라갈 손 안의 위치다.
    [SerializeField] private Transform _grabPoint;

    // 팔의 세로 두께 배율이다.
    // 가로 길이는 어깨와 손목 사이의 거리로 자동 계산한다.
    [SerializeField, Min(0.01f)]
    private float _armThicknessScale = 1f;

    // 손 이미지의 기본 방향이 다를 때 보정할 회전 각도다.
    // 현재 왼쪽을 향한 손 이미지는 0으로 사용한다.
    [SerializeField] private float _handAngleOffset;

    // 공격 패턴이 현재 손목 위치를 읽을 때 사용한다.
    public Transform HandRoot => _handRoot;

    // 공격 패턴이 잡힌 플레이어의 목표 위치를 읽을 때 사용한다.
    public Transform GrabPoint => _grabPoint;

    private void LateUpdate()
    {
        // 본체나 손이 움직인 뒤 팔의 연결 상태를 갱신한다.
        RefreshArm();
    }

    // 공격 패턴에서 월드 좌표로 손목을 이동시킨다.
    public void SetHandPosition(Vector3 worldPosition)
    {
        if (_handRoot == null)
        {
            return;
        }

        _handRoot.position = worldPosition;

        // 이동한 순간에도 손의 방향과 팔 길이를 맞춘다.
        RefreshArm();
    }

    // 잡기 상태에 따라 손에 표시할 이미지를 교체한다.
    public void SetHandClosed(bool isClosed)
    {
        if (_handRenderer == null)
        {
            return;
        }

        Sprite sprite = isClosed
            ? _closedHandSprite
            : _openHandSprite;

        if (sprite != null)
        {
            _handRenderer.sprite = sprite;
        }
    }

    // 팔 그림의 양 끝을 실제 어깨와 손목 위치에 맞춘다.
    private void RefreshArm()
    {
        // 프리팹을 연결하는 중에는 미연결 상태를 허용한다.
        if (_armSocket == null ||
            _armVisual == null ||
            _armStart == null ||
            _armEnd == null ||
            _handRoot == null)
        {
            return;
        }

        // 연결점 좌표를 ArmVisual의 로컬 좌표로 사용하므로
        // 반드시 ArmVisual 바로 아래에 배치해야 한다.
        if (_armStart.parent != _armVisual ||
            _armEnd.parent != _armVisual)
        {
            return;
        }

        Transform visualParent = _armVisual.parent;

        if (visualParent == null)
        {
            return;
        }

        // 어깨와 손목을 팔 이미지 부모의 좌표계로 변환한다.
        Vector3 startPosition =
            visualParent.InverseTransformPoint(_armSocket.position);

        Vector3 endPosition =
            visualParent.InverseTransformPoint(_handRoot.position);

        Vector2 direction = endPosition - startPosition;
        float targetLength = direction.magnitude;

        // 이미지 안에 지정한 두 연결점 사이의 원래 가로 길이다.
        float originalLength =
            _armEnd.localPosition.x - _armStart.localPosition.x;

        // 연결점이 겹치면 길이와 방향을 계산할 수 없다.
        if (Mathf.Abs(originalLength) < 0.0001f ||
            targetLength < 0.0001f)
        {
            return;
        }

        // 팔이 수평으로 그려졌다는 전제이므로 연결점 높이를 맞춘다.
        if (Mathf.Abs(
            _armEnd.localPosition.y -
            _armStart.localPosition.y) > 0.001f)
        {
            return;
        }

        // 실제 어깨에서 손목으로 향하는 각도를 구한다.
        float targetAngle =
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // 원본 그림의 연결 방향이 왼쪽이면 180도를 보정한다.
        float originalAngle = originalLength < 0f ? 180f : 0f;

        Quaternion rotation = Quaternion.Euler(
            0f,
            0f,
            targetAngle - originalAngle);

        // 팔 길이만 거리에 맞춰 늘리고 두께는 별도로 유지한다.
        Vector3 scale = new Vector3(
            targetLength / Mathf.Abs(originalLength),
            _armThicknessScale,
            1f);

        _armVisual.localRotation = rotation;
        _armVisual.localScale = scale;

        // 스프라이트 Pivot이나 투명 여백과 관계없이
        // ArmStart가 정확히 어깨 위치에 오도록 이미지 위치를 보정한다.
        Vector3 startOffset = rotation *
            Vector3.Scale(_armStart.localPosition, scale);

        _armVisual.localPosition = startPosition - startOffset;

        // 왼쪽을 향한 손 이미지가 팔의 진행 방향을 바라보게 한다.
        Vector2 worldDirection =
            _handRoot.position - _armSocket.position;

        float handAngle =
            Mathf.Atan2(worldDirection.y, worldDirection.x) *
            Mathf.Rad2Deg;

        _handRoot.rotation = Quaternion.Euler(
            0f,
            0f,
            handAngle - 180f + _handAngleOffset);
    }
}
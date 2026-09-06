using System;
using UnityEngine;

/// <summary>
/// 회전 레이저가 지나갈 전체 범위를 붉은 부채꼴로 표시한다.
/// 피해 판정 없이 경고 외형만 담당한다.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public sealed class BossLaserWarning : MonoBehaviour
{
    // 투명도를 지원하고 _Color 속성이 있는 재질이다.
    [SerializeField] Material _warningMaterial;

    // 경고 범위의 색과 최대 불투명도다.
    [SerializeField]
    Color _color =
        new Color(1f, 0f, 0f, 0.3f);

    // 경고를 표시할 정렬 레이어 이름이다.
    [SerializeField] string _sortingLayerName = "Default";

    // 같은 정렬 레이어 안에서의 표시 순서다.
    [SerializeField] int _sortingOrder = 10;

    // 현재 경고를 사용하는 보스다.
    BossEnemy _owner;

    // 실행 중 생성한 경고 모양이다.
    Mesh _mesh;

    // 다른 경고와 색상이 섞이지 않도록 만든 개별 재질이다.
    Material _material;

    // 초기화 완료 여부다.
    bool _initialized;

    /// <summary>
    /// 현재 화면과 회전 각도, 레이저 피해 굵기로 경고 범위를 만든다.
    /// </summary>
    public void Initialize(
        BossEnemy owner,
        Camera battleCamera,
        Vector3 origin,
        float startAngle,
        float sweepAngle,
        float damageWidth)
    {
        if (owner == null || battleCamera == null ||
            _warningMaterial == null)
        {
            throw new InvalidOperationException(
                $"{name}: 보스, 카메라, 경고 재질이 필요합니다.");
        }

        if (!_warningMaterial.HasProperty("_Color"))
        {
            throw new InvalidOperationException(
                $"{name}: 경고 재질에 _Color 속성이 필요합니다.");
        }

        _owner = owner;

        // 이번 패턴은 1~150도 사이의 회전 범위를 사용한다.
        float halfAngle = Mathf.Abs(sweepAngle) * 0.5f;
        float middleAngle = startAngle + sweepAngle * 0.5f;

        Vector2 middleDirection = Direction(middleAngle);

        // 부채꼴 꼭짓점을 약간 뒤로 이동하여
        // 레이저 중심선 양옆의 피해 굵기도 경고 안에 포함한다.
        float padding = damageWidth * 0.5f + 0.05f;
        float shift = padding /
            Mathf.Sin(halfAngle * Mathf.Deg2Rad);

        Vector3 apex =
            origin - (Vector3)(middleDirection * shift);

        // 화면 전체를 덮을 수 있는 반지름을 계산한다.
        float depth = Vector3.Dot(
            apex - battleCamera.transform.position,
            battleCamera.transform.forward);

        float radius = 0f;

        for (int y = 0; y <= 1; y++)
        {
            for (int x = 0; x <= 1; x++)
            {
                Vector3 corner = battleCamera.ViewportToWorldPoint(
                    new Vector3(x, y, depth));

                radius = Mathf.Max(
                    radius,
                    Vector2.Distance(apex, corner));
            }
        }

        radius += 1f;

        // 부채꼴의 둥근 바깥 경계를 여러 삼각형으로 구성한다.
        int segments = Mathf.Max(
            2, Mathf.CeilToInt(Mathf.Abs(sweepAngle) / 2f));

        float lowAngle = Mathf.Min(
            startAngle, startAngle + sweepAngle);

        float angleRange = Mathf.Abs(sweepAngle);

        // 부채꼴의 꼭짓점과 바깥 경계점을 저장한다.
        Vector3[] vertices = new Vector3[segments + 2];

        // 양면 표시 재질이므로 삼각형은 한 방향으로만 만든다.
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;

        // 부채꼴 바깥 경계를 각도 순서대로 배치한다.
        for (int i = 0; i <= segments; i++)
        {
            float angle = lowAngle + angleRange * i / segments;
            vertices[i + 1] = Direction(angle) * radius;
        }

        // 중심과 이웃한 경계점 두 개를 연결해 부채꼴을 채운다.
        for (int i = 0; i < segments; i++)
        {
            int index = i * 3;

            triangles[index] = 0;
            triangles[index + 1] = i + 1;
            triangles[index + 2] = i + 2;
        }

        transform.SetParent(null);
        transform.SetPositionAndRotation(apex, Quaternion.identity);
        transform.localScale = Vector3.one;

        _mesh = new Mesh();
        _mesh.name = "Boss Laser Warning";
        _mesh.vertices = vertices;
        _mesh.triangles = triangles;
        _mesh.RecalculateBounds();
        _mesh.RecalculateNormals();

        GetComponent<MeshFilter>().sharedMesh = _mesh;

        _material = new Material(_warningMaterial);

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = _material;
        meshRenderer.sortingLayerName = _sortingLayerName;
        meshRenderer.sortingOrder = _sortingOrder;

        _initialized = true;
        SetOpacity(0f);
    }

    /// <summary>
    /// 패턴이 전달한 0~1 비율로 경고의 투명도를 변경한다.
    /// </summary>
    public void SetOpacity(float ratio)
    {
        if (_material == null)
        {
            return;
        }

        Color color = _color;
        color.a *= Mathf.Clamp01(ratio);
        _material.SetColor("_Color", color);
    }

    private void Update()
    {
        // 패턴 도중 보스가 죽거나 제거되면 경고도 제거한다.
        if (_initialized &&
            (_owner == null || _owner.IsDead ||
             !_owner.isActiveAndEnabled))
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Z축 회전 각도를 XY 방향 벡터로 변환한다.
    /// </summary>
    private static Vector2 Direction(float angle)
    {
        float radians = angle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    private void OnDestroy()
    {
        // 실행 중 만든 그래픽 리소스를 정리한다.
        if (_mesh != null)
        {
            Destroy(_mesh);
        }

        if (_material != null)
        {
            Destroy(_material);
        }
    }
}
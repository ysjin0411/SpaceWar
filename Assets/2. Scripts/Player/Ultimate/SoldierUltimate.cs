using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// 항공모함의 진입, 폭격, 퇴장과 생성된 연출의 수명을 관리한다.
public sealed class SoldierUltimate : UltimateAbility
{
    // 항공모함 루트의 SpriteRenderer를 연결한다.
    [SerializeField] private SpriteRenderer _carrierPrefab;

    // 지정 좌표까지 직선으로 이동할 미사일 프리팹이다.
    [SerializeField] private UltimateMissile _missilePrefab;

    // 항공모함이 폭격 중 머무를 화면 비율 위치다.
    [SerializeField]
    private Vector2 _arrivalViewport =
        new Vector2(0.3f, 0.85f);

    // 항공모함 전체가 화면 밖에서 시작하도록 추가할 월드 여백이다.
    [SerializeField, Min(0f)] private float _outsideMargin = 0.5f;

    // 진입에 걸리는 시간이다. 퇴장에도 같은 시간을 사용한다.
    [SerializeField, Min(0.01f)] private float _travelDuration = 1.2f;

    // 항공모함 로컬 좌표 기준의 미사일 발사 위치다.
    [SerializeField]
    private Vector2 _launchLocalPosition =
        new Vector2(1f, -0.8f);

    // 무작위로 목표를 선택할 화면 비율 사각형이다.
    [SerializeField]
    private Rect _targetViewportRect =
        new Rect(0.1f, 0.1f, 0.8f, 0.6f);

    // 폭격 시간 동안 발사할 미사일의 총 개수다.
    [SerializeField, Min(1)] private int _missileCount = 30;

    // 항공모함 도착 후 폭격을 진행할 게임 시간이다.
    [SerializeField, Min(0.01f)] private float _bombardDuration = 3f;

    // 캐릭터 이동과 독립적인 월드 오브젝트들을 묶는 실행 루트다.
    private Transform _runtimeRoot;

    // 현재 실행 중인 필살기 코루틴이다.
    private Coroutine _routine;

    public override bool IsRunning => _runtimeRoot != null;

    public override void ValidateConfiguration()
    {
        if (_carrierPrefab == null ||
            _carrierPrefab.sprite == null ||
            _missilePrefab == null ||
            _travelDuration <= 0f ||
            _bombardDuration <= 0f ||
            _missileCount < 1 ||
            _outsideMargin < 0f ||
            _targetViewportRect.width <= 0f ||
            _targetViewportRect.height <= 0f)
        {
            throw new InvalidOperationException(
                $"{name}: 항공모함 필살기 설정을 확인해주세요.");
        }

        if (_targetViewportRect.xMin < 0f ||
            _targetViewportRect.yMin < 0f ||
            _targetViewportRect.xMax > 1f ||
            _targetViewportRect.yMax > 1f ||
            _arrivalViewport.x < 0f ||
            _arrivalViewport.x > 1f ||
            _arrivalViewport.y < 0f ||
            _arrivalViewport.y > 1f)
        {
            throw new InvalidOperationException(
                $"{name}: 도착 위치와 폭격 범위는 화면 비율 0~1 안에 설정해주세요.");
        }

        _missilePrefab.ValidateConfiguration();
    }

    public override bool TryActivate(Camera worldCamera)
    {
        if (!isActiveAndEnabled || IsRunning)
        {
            return false;
        }

        if (worldCamera == null)
        {
            throw new ArgumentNullException(nameof(worldCamera));
        }

        ValidateConfiguration();

        // 사용 시점의 화면을 기준으로 위치를 고정한다.
        Vector3 topLeft = GetWorldPoint(worldCamera, new Vector2(0f, 1f));
        Vector3 arrival = GetWorldPoint(worldCamera, _arrivalViewport);

        Vector3 targetMin = GetWorldPoint(
            worldCamera,
            _targetViewportRect.min);

        Vector3 targetMax = GetWorldPoint(
            worldCamera,
            _targetViewportRect.max);

        GameObject rootObject = new GameObject("SoldierUltimate_Runtime");

        // 플레이어가 속한 스테이지 씬과 함께 제거되도록 한다.
        SceneManager.MoveGameObjectToScene(rootObject, gameObject.scene);
        _runtimeRoot = rootObject.transform;

        SpriteRenderer carrier = Instantiate(
            _carrierPrefab,
            arrival,
            Quaternion.identity,
            _runtimeRoot);

        // 피벗이 중앙이 아니어도 이미지 전체가 화면 밖에서 시작한다.
        Bounds bounds = carrier.bounds;
        Vector3 centerOffset = bounds.center - carrier.transform.position;

        Vector3 start = arrival;
        start.x =
            topLeft.x - bounds.extents.x - _outsideMargin - centerOffset.x;
        start.y =
            topLeft.y + bounds.extents.y + _outsideMargin - centerOffset.y;

        carrier.transform.position = start;

        _routine = StartCoroutine(
            Execute(carrier, start, arrival, targetMin, targetMax));

        return true;
    }

    private IEnumerator Execute(
        SpriteRenderer carrier,
        Vector3 start,
        Vector3 arrival,
        Vector3 targetMin,
        Vector3 targetMax)
    {
        // 화면 밖 좌측 상단에서 도착점까지 진입한다.
        yield return MoveCarrier(carrier.transform, start, arrival, false);

        float elapsed = 0f;
        int firedCount = 0;
        float interval = _bombardDuration / _missileCount;

        while (elapsed < _bombardDuration)
        {
            // 일시정지 중에는 발사와 진행 시간을 모두 멈춘다.
            if (Time.deltaTime <= 0f)
            {
                yield return null;
                continue;
            }

            // 프레임이 늦어져도 누락 없이 예정된 개수를 발사한다.
            while (firedCount < _missileCount &&
                   elapsed >= firedCount * interval)
            {
                FireMissile(carrier.transform, targetMin, targetMax);
                firedCount++;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 마지막 프레임에서 시간이 크게 증가한 경우 남은 발사를 처리한다.
        while (firedCount < _missileCount)
        {
            FireMissile(carrier.transform, targetMin, targetMax);
            firedCount++;
        }

        // 진입 경로의 진행도를 거꾸로 계산하여 퇴장한다.
        yield return MoveCarrier(carrier.transform, start, arrival, true);

        Destroy(carrier.gameObject);

        // 항공모함이 퇴장해도 이미 발사된 미사일과 연기는 유지한다.
        // 모든 자식 연출이 끝나면 이번 실행 루트까지 정리한다.
        while (_runtimeRoot != null && _runtimeRoot.childCount > 0)
        {
            yield return null;
        }

        if (_runtimeRoot != null)
        {
            Destroy(_runtimeRoot.gameObject);
        }

        _runtimeRoot = null;
        _routine = null;
    }

    private IEnumerator MoveCarrier(
        Transform carrier,
        Vector3 start,
        Vector3 arrival,
        bool reverse)
    {
        float elapsed = 0f;

        while (elapsed < _travelDuration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(elapsed / _travelDuration);
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);

            if (reverse)
            {
                easedProgress = 1f - easedProgress;
            }

            carrier.position = Vector3.Lerp(start, arrival, easedProgress);
            yield return null;
        }

        if (reverse)
        {
            carrier.position = start;
        }
        else
        {
            carrier.position = arrival;
        }
    }

    private void FireMissile(
        Transform carrier,
        Vector3 targetMin,
        Vector3 targetMax)
    {
        // 발사 위치는 항공모함에 설정한 로컬 지점을 사용한다.
        Vector3 launchPosition = carrier.TransformPoint(
            new Vector3(_launchLocalPosition.x, _launchLocalPosition.y, 0f));

        // 각 미사일이 서로 독립적인 무작위 목표 좌표를 받는다.
        Vector3 target = new Vector3(
            UnityEngine.Random.Range(
                Mathf.Min(targetMin.x, targetMax.x),
                Mathf.Max(targetMin.x, targetMax.x)),
            UnityEngine.Random.Range(
                Mathf.Min(targetMin.y, targetMax.y),
                Mathf.Max(targetMin.y, targetMax.y)),
            0f);

        UltimateMissile missile = Instantiate(
            _missilePrefab,
            launchPosition,
            Quaternion.identity,
            _runtimeRoot);

        missile.Initialize(target, _runtimeRoot);
    }

    // 카메라의 화면 비율 위치를 게임의 XY 평면으로 변환한다.
    private static Vector3 GetWorldPoint(Camera camera, Vector2 viewport)
    {
        Ray ray = camera.ViewportPointToRay(
            new Vector3(viewport.x, viewport.y, 0f));

        Plane plane = new Plane(Vector3.forward, Vector3.zero);
        float distance;

        if (!plane.Raycast(ray, out distance))
        {
            throw new InvalidOperationException(
                "필살기 카메라가 게임의 XY 평면을 바라보도록 설정해주세요.");
        }

        return ray.GetPoint(distance);
    }

    public override void Cancel()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        if (_runtimeRoot != null)
        {
            // Destroy를 기다리는 동안 추가 피해가 발생하지 않도록 먼저 끈다.
            _runtimeRoot.gameObject.SetActive(false);
            Destroy(_runtimeRoot.gameObject);
            _runtimeRoot = null;
        }
    }

    private void OnDisable()
    {
        Cancel();
    }
}
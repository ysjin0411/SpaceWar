using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// 화면 왼쪽 밖의 무작위 높이에서 필살기 검을 연속 생성한다.
public sealed class WarriorUltimate : UltimateAbility
{
    // 오른쪽 이동과 관통 피해를 담당하는 검 프리팹이다.
    [SerializeField] private UltimateSword _swordPrefab;

    // 필살기 한 번에 발사할 총 검 개수다.
    [SerializeField, Min(1)] private int _swordCount = 40;

    // 전체 검을 나누어 발사하는 게임 시간이다.
    [SerializeField, Min(0.01f)] private float _fireDuration = 3f;

    // 검이 생성되는 세로 구간의 중심을 화면 비율로 지정한다.
    // X: 0은 화면 왼쪽, 음수는 화면 왼쪽 바깥이다.
    // Y: 0은 화면 아래, 0.5는 중앙, 1은 화면 위다.
    [SerializeField]
    private Vector2 _spawnLineCenterViewport =
        new Vector2(-0.15f, 0.5f);

    // 생성된 검들을 묶는다. 캐릭터 이동에는 영향을 받지 않는다.
    private Transform _runtimeRoot;

    // 사용 순간의 화면을 기준으로 고정한 생성 선분이다.
    private float _spawnX;
    private float _minY;
    private float _maxY;

    private float _elapsed;
    private int _spawnedCount;

    // 발사가 끝나도 남은 검이 사라질 때까지 실행 중으로 취급한다.
    public override bool IsRunning => _runtimeRoot != null;

    public override void ValidateConfiguration()
    {
        if (_swordPrefab == null ||
            _swordCount < 1 ||
            _fireDuration <= 0f)
        {
            throw new InvalidOperationException(
                $"{name}: 워리어 필살기 설정을 확인해주세요.");
        }

        // 현재 검 프리팹은 루트에 SpriteRenderer를 사용하는 구조다.
        SpriteRenderer renderer =
            _swordPrefab.GetComponent<SpriteRenderer>();

        if (renderer == null ||
            renderer.sprite == null ||
            _swordPrefab.transform.localScale.x <= 0f)
        {
            throw new InvalidOperationException(
                "검 프리팹 루트의 SpriteRenderer와 양수 X Scale을 확인해주세요.");
        }

        _swordPrefab.ValidateConfiguration();
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

        // 중심에서 화면 높이의 절반씩 내려가고 올라간 지점을 구한다.
        // 따라서 중심을 옮겨도 세로 구간의 길이는 화면 높이를 유지한다.
        Vector3 bottom = GetWorldPoint(
            worldCamera,
            new Vector2(
                _spawnLineCenterViewport.x,
                _spawnLineCenterViewport.y - 0.5f));

        Vector3 top = GetWorldPoint(
            worldCamera,
            new Vector2(
                _spawnLineCenterViewport.x,
                _spawnLineCenterViewport.y + 0.5f));

        // 사용 순간의 위치를 저장하여 발사 중에는 구간을 고정한다.
        _spawnX = bottom.x;
        _minY = Mathf.Min(bottom.y, top.y);
        _maxY = Mathf.Max(bottom.y, top.y);

        GameObject rootObject =
            new GameObject("WarriorUltimate_Runtime");

        SceneManager.MoveGameObjectToScene(
            rootObject,
            gameObject.scene);

        _runtimeRoot = rootObject.transform;
        _elapsed = 0f;
        _spawnedCount = 0;
        GameAudio.Play(GameSound.UltimateWarriorCast); // 궁극기 활성화 승인.

        return true;
    }

    private void Update()
    {
        if (!IsRunning || Time.deltaTime <= 0f)
        {
            return;
        }

        _elapsed += Time.deltaTime;

        // 총 발사 시간과 개수로 각 검의 발사 간격을 계산한다.
        float interval = _fireDuration / _swordCount;

        // 프레임이 늦어져도 예정된 발사를 누락하지 않는다.
        while (_spawnedCount < _swordCount &&
               _elapsed >= _spawnedCount * interval)
        {
            Vector3 position = new Vector3(
                _spawnX,
                UnityEngine.Random.Range(_minY, _maxY),
                0f);

            Instantiate(
                _swordPrefab,
                position,
                Quaternion.identity,
                _runtimeRoot);

            GameAudio.Play(GameSound.UltimateSwordDrop); // 수평 검 한 발 생성.
            _spawnedCount++;
        }

        // 발사 시간이 끝나고 모든 검이 제거되면 실행을 종료한다.
        if (_elapsed >= _fireDuration &&
            _spawnedCount >= _swordCount &&
            _runtimeRoot.childCount == 0)
        {
            Cancel();
        }
    }

    // 화면 좌표를 Z=0인 전투 평면의 월드 좌표로 바꾼다.
    private static Vector3 GetWorldPoint(
        Camera camera,
        Vector2 viewport)
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
        if (_runtimeRoot == null)
        {
            return;
        }

        // 사망·클리어·구간 이동 시 남은 검의 피해 판정도 즉시 멈춘다.
        _runtimeRoot.gameObject.SetActive(false);
        Destroy(_runtimeRoot.gameObject);
        _runtimeRoot = null;
    }

    private void OnDisable()
    {
        Cancel();
    }
}
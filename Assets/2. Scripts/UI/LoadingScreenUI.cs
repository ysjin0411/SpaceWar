using TMPro;
using UnityEngine;

public class LoadingScreenUI : MonoBehaviour
{
    // 로딩 중 문구를 표시하는 텍스트.
    [SerializeField] private TMP_Text _loadingText;

    // 진행률을 표시하는 Filled 이미지.
    [SerializeField] private UnityEngine.UI.Image _fillImage;

    // 점 개수가 바뀌는 시간 간격.
    [SerializeField, Min(0.05f)] private float _dotInterval = 0.4f;

    // 초당 게이지가 채워지는 양.
    // 1이면 표시값이 0에서 1까지 따라가는 데 약 1초가 걸린다.
    [SerializeField, Min(0.1f)] private float _fillSpeed = 1f;

    // 점 개수에 따라 순서대로 표시할 문구.
    private readonly string[] _messages =
    {
        "불러오는 중.",
        "불러오는 중..",
        "불러오는 중..."
    };

    // 실제 로딩 진행률로부터 전달받은 목표값.
    private float _targetProgress;

    // 현재 화면에 표시하고 있는 진행률.
    private float _displayedProgress;

    // 점 애니메이션의 누적 시간과 현재 문구 번호.
    private float _dotTimer;
    private int _dotIndex;

    // 화면의 게이지가 끝까지 채워졌는지 알려준다.
    public bool IsFillComplete => _displayedProgress >= 1f;

    // 패널이 켜질 때마다 표시 상태를 초기화한다.
    private void OnEnable()
    {
        // 비활성 상태로 시작하는 패널도 켜질 때 연결을 검사한다.
        if (_loadingText == null || _fillImage == null)
        {
            throw new MissingReferenceException(
                "LoadingScreenUI에 텍스트와 게이지를 연결해주세요.");
        }

        if (_fillImage.type != UnityEngine.UI.Image.Type.Filled)
        {
            throw new System.InvalidOperationException(
                "로딩 게이지의 Image Type을 Filled로 설정해주세요.");
        }

        _targetProgress = 0f;
        _displayedProgress = 0f;
        _dotTimer = 0f;
        _dotIndex = 0;

        _loadingText.text = _messages[0];
        _fillImage.fillAmount = 0f;
    }

    // 패널이 활성화된 동안 문구와 게이지를 계속 갱신한다.
    private void Update()
    {
        // 게임 시간 배율과 관계없이 점을 반복한다.
        float interval = Mathf.Max(0.05f, _dotInterval);
        _dotTimer += Time.unscaledDeltaTime;

        if (_dotTimer >= interval)
        {
            // 프레임 지연 후에도 문구는 한 단계씩 보여준다.
            _dotTimer %= interval;
            _dotIndex = (_dotIndex + 1) % _messages.Length;
            _loadingText.text = _messages[_dotIndex];
        }

        // 긴 프레임 정체 직후 게이지가 한 번에 채워지지 않도록
        // 표시 애니메이션에 사용하는 시간만 최대 0.05초로 제한한다.
        float displayDelta = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        // 실제 진행률을 넘지 않으며 목표값까지 부드럽게 이동한다.
        _displayedProgress = Mathf.MoveTowards(
            _displayedProgress,
            _targetProgress,
            Mathf.Max(0.1f, _fillSpeed) * displayDelta);

        _fillImage.fillAmount = _displayedProgress;
    }

    // 로딩 담당자가 전달한 진행률을 목표값으로 저장한다.
    public void SetProgress(float progress)
    {
        // 진행률은 0~1 범위로 제한하고 이전 값보다 뒤로 가지 않게 한다.
        _targetProgress = Mathf.Max(
            _targetProgress,
            Mathf.Clamp01(progress));
    }
}
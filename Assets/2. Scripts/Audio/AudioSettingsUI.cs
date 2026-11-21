using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class AudioSettingsUI : MonoBehaviour
{
    // 통째로 활성화하거나 비활성화할 설정 Canvas 오브젝트.
    [SerializeField] private GameObject _settingsCanvas;

    // 배경음과 효과음을 조절하는 슬라이더.
    [SerializeField] private UnityEngine.UI.Slider _bgmSlider;
    [SerializeField] private UnityEngine.UI.Slider _sfxSlider;

    // 각 슬라이더 오른쪽에 표시할 현재 음량.
    [SerializeField] private TMP_Text _bgmValueText;
    [SerializeField] private TMP_Text _sfxValueText;

    // 로그아웃 후 이동할 메인 메뉴 씬 이름.
    [SerializeField] private string _mainMenuSceneName = "1.MainMenu";

    // 공통 음량 설정 관리자.
    private AudioSettingsManager _settings;

    // 이벤트 연결이 끝났는지 나타낸다.
    private bool _isInitialized;

    // 창을 연 입력이 같은 프레임에 닫기 입력으로 다시 사용되지 않게 한다.
    private int _openedFrame = -1;

    // 설정창의 표시 상태 변경을 알린다. true는 열림, false는 닫힘이다.
    public event Action<bool> VisibilityChanged;

    // 외부에서 설정창의 열림 여부를 확인한다.
    public bool IsOpen => _settingsCanvas != null && _settingsCanvas.activeSelf;

    private void Start()
    {
        if (_settingsCanvas == null ||
            _bgmSlider == null ||
            _sfxSlider == null ||
            _bgmValueText == null ||
            _sfxValueText == null)
        {
            throw new InvalidOperationException(
                "AudioSettingsUI의 Canvas, Slider, Text를 모두 연결해주세요.");
        }

        _settings = AudioSettingsManager.Instance;

        if (_settings == null)
        {
            throw new InvalidOperationException(
                "활성 상태의 AudioSettingsManager가 필요합니다.");
        }

        // 두 슬라이더를 정수 0~100 범위로 통일한다.
        ConfigureSlider(_bgmSlider);
        ConfigureSlider(_sfxSlider);

        // 슬라이더 이벤트는 코드에서 한 번만 연결한다.
        _bgmSlider.onValueChanged.AddListener(HandleBgmChanged);
        _sfxSlider.onValueChanged.AddListener(HandleSfxChanged);

        _isInitialized = true;

        // 저장된 값을 표시하고 처음에는 설정창을 닫아둔다.
        RefreshValues();
        _settingsCanvas.SetActive(false);
    }

    /// <summary>
    /// 모든 씬에서 Esc로 설정창을 닫되, 일시정지 입력 처리 이후에 닫습니다.
    /// </summary>
    private void LateUpdate()
    {
        // PauseManager.Update가 열린 설정창을 먼저 확인하도록 LateUpdate에서 처리한다.
        // 같은 Esc 입력이 설정창 닫기와 게임 재개를 동시에 실행하지 않게 한다.
        if (_isInitialized && IsOpen && Time.frameCount != _openedFrame &&
            Input.GetKeyDown(KeyCode.Escape))
        {
            // 닫기 버튼과 동일하게 음량 저장, 효과음, 표시 변경 이벤트를 처리한다.
            CloseFromButton();
        }
    }

    // UI에서 사용할 슬라이더 범위와 방향을 설정한다.
    private void ConfigureSlider(UnityEngine.UI.Slider slider)
    {
        slider.minValue = 0f;
        slider.maxValue = 100f;
        slider.wholeNumbers = true;
        slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
    }

    // 현재 설정값을 이벤트 재호출 없이 UI에 표시한다.
    private void RefreshValues()
    {
        _bgmSlider.SetValueWithoutNotify(_settings.BgmVolume);
        _sfxSlider.SetValueWithoutNotify(_settings.SfxVolume);

        _bgmValueText.text = _settings.BgmVolume.ToString();
        _sfxValueText.text = _settings.SfxVolume.ToString();
    }

    /// <summary>
    /// 설정창을 전환하고 표시 상태 변경을 알린다.
    /// </summary>
    public void Toggle()
    {
        if (!_isInitialized)
        {
            return;
        }
        // 환경설정 버튼으로 창을 열거나 닫을 때 클릭음을 재생한다.
        UIAudioPlayer.Play(UISound.ButtonClick);

        if (IsOpen)
        {
            Close();
            return;
        }

        RefreshValues();
        // Esc로 연 프레임에는 LateUpdate의 닫기 처리를 건너뛴다.
        _openedFrame = Time.frameCount;
        _settingsCanvas.SetActive(true);

        // 일시정지 화면 등 구독자가 표시 상태를 전환하도록 알린다.
        VisibilityChanged?.Invoke(true);
    }

    /// <summary>
    /// 설정값을 저장하고 창을 닫은 뒤 닫힘 상태를 알린다.
    /// </summary>
    public void Close()
    {
        // 초기화 전 또는 이미 닫힌 상태에서는 중복 처리하지 않는다.
        if (!_isInitialized || !IsOpen)
        {
            return;
        }

        _settings.SaveSettings();
        _settingsCanvas.SetActive(false);

        // 버튼과 ESC 모두 같은 닫힘 이벤트를 전달한다.
        VisibilityChanged?.Invoke(false);
    }

    // 배경음 슬라이더 변경을 실제 음량과 숫자에 반영한다.
    private void HandleBgmChanged(float value)
    {
        _settings.SetBgmVolume(Mathf.RoundToInt(value));
        _bgmValueText.text = _settings.BgmVolume.ToString();
    }

    // 효과음 슬라이더 변경을 실제 음량과 숫자에 반영한다.
    private void HandleSfxChanged(float value)
    {
        _settings.SetSfxVolume(Mathf.RoundToInt(value));
        _sfxValueText.text = _settings.SfxVolume.ToString();
    }

    // 공통 환경설정과 계정 진행 데이터를 저장한 후 로그아웃한다.
    public void Logout()
    {
        if (!_isInitialized)
        {
            return;
        }

        if (SaveManager.Instance == null)
        {
            Debug.LogError("SaveManager가 없습니다.", this);
            return;
        }

        // 이동할 씬이 준비되지 않았다면 로그인 상태를 먼저 지우지 않는다.
        if (!Application.CanStreamedLevelBeLoaded(_mainMenuSceneName))
        {
            Debug.LogError(
                $"Build Profiles의 씬 목록에 '{_mainMenuSceneName}'을 추가해주세요.",
                this);
            return;
        }

        try
        {
            _settings.SaveSettings();

            // 기존 Logout 내부에서 계정 저장을 수행한다.
            SaveManager.Instance.Logout();
        }
        catch (Exception exception)
        {
            // 저장 실패 시 씬을 이동하지 않고 오류를 남긴다.
            Debug.LogException(exception, this);
            return;
        }

        SceneManager.LoadScene(_mainMenuSceneName);
    }

    private void OnDisable()
    {
        // 씬 이동 등으로 UI 관리자가 비활성화될 때도 변경값을 저장한다.
        if (_isInitialized && _settings != null)
        {
            _settings.SaveSettings();
        }
    }

    private void OnDestroy()
    {
        if (!_isInitialized)
        {
            return;
        }

        // 남아 있는 슬라이더의 이벤트 연결을 해제한다.
        if (_bgmSlider != null)
        {
            _bgmSlider.onValueChanged.RemoveListener(HandleBgmChanged);
        }

        if (_sfxSlider != null)
        {
            _sfxSlider.onValueChanged.RemoveListener(HandleSfxChanged);
        }
    }

    /// <summary>설정 창의 닫기 버튼에서 저장과 닫기 효과음을 함께 처리합니다.</summary>
    public void CloseFromButton()
    {
        // 초기화나 비활성화 정리에서는 효과음을 내지 않는다.
        if (!_isInitialized || !_settingsCanvas.activeSelf) return;
        GameAudio.Play(GameSound.UiWindowClose);
        Close();
    }
}

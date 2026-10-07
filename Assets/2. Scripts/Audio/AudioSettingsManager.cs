using System;
using UnityEngine;
using UnityEngine.Audio;

public sealed class AudioSettingsManager : MonoBehaviour
{
    // 씬이 바뀌어도 유지되는 공통 음량 설정 관리자.
    public static AudioSettingsManager Instance { get; private set; }

    // BGM과 SFX 그룹이 들어 있는 믹서.
    [SerializeField] private AudioMixer _audioMixer;

    // 계정과 무관하게 사용하는 환경설정 저장 키.
    private const string BgmKey = "Settings.Audio.Bgm";
    private const string SfxKey = "Settings.Audio.Sfx";

    // Mixer에서 노출한 파라미터 이름.
    private const string BgmParameter = "BgmVolume";
    private const string SfxParameter = "SfxVolume";

    // 아직 디스크에 저장하지 않은 변경이 있는지 나타낸다.
    private bool _isDirty;

    // Mixer에 값을 적용할 수 있는 초기화 단계인지 나타낸다.
    private bool _hasStarted;

    // UI와 저장에 사용하는 0~100 범위의 현재 음량.
    public int BgmVolume { get; private set; }
    public int SfxVolume { get; private set; }

    private void Awake()
    {
        // 다른 씬에서 이미 만들어졌다면 중복 관리자만 제거한다.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (_audioMixer == null)
        {
            throw new InvalidOperationException(
                "AudioSettingsManager에 AudioMixer를 연결해주세요.");
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 저장값이 없을 때만 100을 사용한다. 저장된 0은 유지한다.
        BgmVolume = Mathf.Clamp(PlayerPrefs.GetInt(BgmKey, 100), 0, 100);
        SfxVolume = Mathf.Clamp(PlayerPrefs.GetInt(SfxKey, 100), 0, 100);
    }

    private void Start()
    {
        if (Instance != this)
        {
            return;
        }

        // Mixer 적용은 Awake가 아닌 Start부터 수행한다.
        _hasStarted = true;
        ApplyVolume(BgmParameter, BgmVolume);
        ApplyVolume(SfxParameter, SfxVolume);
    }

    // 배경음 설정을 변경하고 현재 재생 중인 음악에도 반영한다.
    public void SetBgmVolume(int volume)
    {
        volume = Mathf.Clamp(volume, 0, 100);

        if (BgmVolume == volume)
        {
            return;
        }

        BgmVolume = volume;
        _isDirty = true;

        if (_hasStarted)
        {
            ApplyVolume(BgmParameter, BgmVolume);
        }
    }

    // 모든 SFX 그룹 소리에 적용할 효과음 설정을 변경한다.
    public void SetSfxVolume(int volume)
    {
        volume = Mathf.Clamp(volume, 0, 100);

        if (SfxVolume == volume)
        {
            return;
        }

        SfxVolume = volume;
        _isDirty = true;

        if (_hasStarted)
        {
            ApplyVolume(SfxParameter, SfxVolume);
        }
    }

    // UI의 0~100 값을 Mixer가 사용하는 데시벨 값으로 변환한다.
    private void ApplyVolume(string parameter, int volume)
    {
        // 0에서는 로그 계산을 하지 않고 사실상 무음인 -80dB를 적용한다.
        float decibels = volume == 0
            ? -80f
            : 20f * Mathf.Log10(volume / 100f);

        if (!_audioMixer.SetFloat(parameter, decibels))
        {
            Debug.LogError(
                $"Mixer의 노출 파라미터 '{parameter}'를 확인해주세요.",
                this);
        }
    }

    // 실제 변경이 있을 때만 디스크에 환경설정을 저장한다.
    public void SaveSettings()
    {
        if (!_isDirty)
        {
            return;
        }

        PlayerPrefs.SetInt(BgmKey, BgmVolume);
        PlayerPrefs.SetInt(SfxKey, SfxVolume);
        PlayerPrefs.Save();

        _isDirty = false;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        // 설정창을 닫기 전에 다른 앱으로 이동한 경우에도 저장한다.
        if (Instance == this && !hasFocus)
        {
            SaveSettings();
        }
    }

    private void OnApplicationPause(bool isPaused)
    {
        // 앱이 일시 중단되는 환경에서도 변경값을 저장한다.
        if (Instance == this && isPaused)
        {
            SaveSettings();
        }
    }

    private void OnApplicationQuit()
    {
        // 정상 종료 시 남아 있는 변경값을 저장한다.
        if (Instance == this)
        {
            SaveSettings();
        }
    }

    private void OnDestroy()
    {
        // 중복 관리자 파괴가 기존 Instance를 지우지 않도록 한다.
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
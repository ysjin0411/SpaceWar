using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-900)]
public sealed class GameAudio : MonoBehaviour
{
    public struct Handle
    {
        // 재사용된 슬롯이나 다른 재생기를 잘못 정지하지 않기 위한 정보.
        internal GameAudio Runtime;
        internal int Index;
        internal uint Version;
    }
    private sealed class Voice
    {
        // 풀의 오디오 소스와 현재 파일 설정.
        public AudioSource Source;
        public GameAudioCatalog.Entry Entry;
        // 지속음·취소 가능한 연출음의 소유 컴포넌트.
        public MonoBehaviour Owner;
        // 사용 여부, 일시정지 상태, 슬롯의 재사용 버전.
        public bool Owned;
        public bool Active;
        public bool Paused;
        public uint Version;
    }
    // 씬 이동 후에도 유지되는 공용 인스턴스.
    private static GameAudio _instance;
    // 클립 목록과 기존 Mixer 그룹.
    [SerializeField] private GameAudioCatalog _catalog;
    [SerializeField] private AudioMixerGroup _sfxGroup;
    [SerializeField] private AudioMixerGroup _bgmGroup;
    // 효과음 최대 동시 재생 수와 음악 교체 시간.
    [SerializeField, Min(8)] private int _voiceCount = 32;
    [SerializeField, Min(0f)] private float _musicFadeSeconds = 0.25f;
    // 빠른 조회와 재생 간격 제한용 자료.
    private readonly Dictionary<GameSound, GameAudioCatalog.Entry> _entries =
        new Dictionary<GameSound, GameAudioCatalog.Entry>();
    private readonly Dictionary<GameSound, float> _nextTimes =
        new Dictionary<GameSound, float>();
    private Voice[] _voices;
    private AudioSource _music;
    private Coroutine _musicRoutine;
    private GameSound _musicId;
    // 기본 씬 음악과 서비스 화면의 임시 음악 요청을 분리한다.
    private GameSound _baseMusicId;
    private readonly List<MusicOverride> _musicOverrides = new List<MusicOverride>();
    private sealed class MusicOverride
    {
        // 화면의 실제 수명과 표시할 음악.
        public MonoBehaviour Owner;
        public GameSound Sound;
    }

    /// <summary>공용 재생기와 오디오 소스 풀을 초기화합니다.</summary>
    private void Awake()
    {
        // 기존 재생기가 있다면 중복 오브젝트만 제거한다.
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        // 잘못 연결된 설정을 조기에 발견한다.
        if (_catalog == null || _sfxGroup == null || _bgmGroup == null)
            throw new InvalidOperationException("GameAudio의 Catalog, SFX, BGM을 연결하세요.");
        foreach (GameAudioCatalog.Entry entry in _catalog.Entries)
        {
            if (entry == null || entry.Id == GameSound.None ||
                entry.Clip == null || _entries.ContainsKey(entry.Id))
                throw new InvalidOperationException("Audio Catalog 누락/중복을 확인하세요.");
            _entries.Add(entry.Id, entry);
        }
        // 전용 오브젝트를 독립적인 영속 루트로 유지한다.
        transform.SetParent(null);
        _instance = this;
        DontDestroyOnLoad(gameObject);
        // 투사체가 파괴되어도 효과음은 이 풀에서 계속 재생된다.
        _voices = new Voice[_voiceCount];
        for (int i = 0; i < _voices.Length; i++)
            _voices[i] = new Voice { Source = CreateSource("SFX_" + i, _sfxGroup) };
        // 음악은 효과음과 별도의 소스를 사용한다.
        _music = CreateSource("BGM", _bgmGroup);
        _music.loop = true;
        _music.priority = 0;
        _music.ignoreListenerPause = true;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    /// <summary>재생기 아래에 2D 오디오 소스를 생성합니다.</summary>
    private AudioSource CreateSource(string objectName, AudioMixerGroup group)
    {
        // 자식으로 만들어 영속 재생기와 수명을 공유한다.
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(transform, false);
        AudioSource source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.outputAudioMixerGroup = group;
        return source;
    }

    /// <summary>발생 오브젝트의 수명과 무관하게 짧은 효과음을 재생합니다.</summary>
    public static void Play(GameSound id)
    {
        // 오디오 설정 문제로 게임 로직까지 중단시키지는 않는다.
        if (_instance == null)
        {
            Debug.LogError("활성 GameAudioRoot가 필요합니다.");
            return;
        }
        _instance.Begin(id, null, false);
    }

    /// <summary>소유 오브젝트 비활성화와 함께 종료되는 소리를 재생합니다.</summary>
    public static Handle PlayOwned(GameSound id, MonoBehaviour owner, bool loop = false)
    {
        // 지속음을 정리할 실제 소유자가 있어야 한다.
        if (_instance == null || owner == null || !owner.isActiveAndEnabled)
            return default;
        return _instance.Begin(id, owner, loop);
    }

    /// <summary>핸들이 가리키는 현재 재생만 정지합니다.</summary>
    public static void Stop(Handle handle)
    {
        // 슬롯이 다른 소리로 재사용된 경우에는 정지하지 않는다.
        if (_instance == null || handle.Runtime != _instance ||
            handle.Index < 0 || handle.Index >= _instance._voices.Length)
            return;
        Voice voice = _instance._voices[handle.Index];
        if (voice.Active && voice.Version == handle.Version)
            _instance.Release(voice);
    }

    /// <summary>핸들에 해당하는 소리가 살아 있는지 확인합니다.</summary>
    public static bool IsPlaying(Handle handle)
    {
        // 일시정지 중인 소리도 유효한 재생으로 취급한다.
        if (_instance == null || handle.Runtime != _instance ||
            handle.Index < 0 || handle.Index >= _instance._voices.Length)
            return false;
        Voice voice = _instance._voices[handle.Index];
        return voice.Active && voice.Version == handle.Version;
    }

    /// <summary>빈도·동시 재생 수·우선순위를 검사하고 소스에 배정합니다.</summary>
    private Handle Begin(GameSound id, MonoBehaviour owner, bool loop)
    {
        // 선택 사항으로 비워둔 항목은 무시한다.
        if (id == GameSound.None) return default;
        if (!_entries.TryGetValue(id, out GameAudioCatalog.Entry entry))
        {
            Debug.LogError("Audio Catalog에 없는 사운드: " + id, this);
            return default;
        }
        if (!entry.IgnorePause && Time.timeScale <= 0f) return default;
        // 완료된 슬롯과 비활성 소유자의 슬롯을 먼저 회수한다.
        RefreshVoices();
        float now = Time.unscaledTime;
        if (_nextTimes.TryGetValue(id, out float next) && now < next) return default;
        int count = 0;
        int slot = -1;
        int replace = -1;
        for (int i = 0; i < _voices.Length; i++)
        {
            Voice voice = _voices[i];
            if (!voice.Active)
            {
                if (slot < 0) slot = i;
                continue;
            }
            if (voice.Entry.Id == id) count++;
            // 중요한 소리가 들어오면 낮은 우선순위의 단발만 교체한다.
            if (!voice.Source.loop && voice.Entry.Priority > entry.Priority &&
                (replace < 0 || voice.Entry.Priority > _voices[replace].Entry.Priority))
                replace = i;
        }
        if (count >= entry.MaxVoices) return default;
        if (slot < 0) slot = replace;
        if (slot < 0) return default;
        Voice selected = _voices[slot];
        Release(selected);
        selected.Version++;
        selected.Entry = entry;
        selected.Owner = owner;
        selected.Owned = owner != null;
        selected.Active = true;
        selected.Source.clip = entry.Clip;
        selected.Source.loop = loop;
        selected.Source.volume = entry.Volume;
        selected.Source.priority = entry.Priority;
        selected.Source.ignoreListenerPause = entry.IgnorePause;
        selected.Source.Play();
        _nextTimes[id] = now + entry.Interval;
        return new Handle { Runtime = this, Index = slot, Version = selected.Version };
    }

    /// <summary>일시정지·소유 오브젝트 종료·자연 종료를 처리합니다.</summary>
    private void RefreshVoices()
    {
        // UI는 계속 들리고 전투음은 게임 일시정지와 함께 멈춘다.
        foreach (Voice voice in _voices)
        {
            if (!voice.Active) continue;
            if (voice.Owned && (voice.Owner == null || !voice.Owner.isActiveAndEnabled))
            {
                Release(voice);
                continue;
            }
            bool pause = !voice.Entry.IgnorePause && Time.timeScale <= 0f;
            if (!voice.Paused && !voice.Source.isPlaying)
            {
                Release(voice);
                continue;
            }
            if (pause && !voice.Paused) voice.Source.Pause();
            else if (!pause && voice.Paused) voice.Source.UnPause();
            voice.Paused = pause;
        }
    }

    /// <summary>활성 재생기에서 소스 상태를 갱신합니다.</summary>
    private void Update()
    {
        // 제거 대기 중인 중복 재생기는 건너뛴다.
        if (_instance != this) return;
        RefreshVoices();
        RefreshMusicOverrides();
    }

    /// <summary>현재 재생을 중단하고 슬롯을 비웁니다.</summary>
    private void Release(Voice voice)
    {
        // 소스와 참조를 함께 정리한다.
        voice.Source.Stop();
        voice.Source.clip = null;
        voice.Active = false;
        voice.Paused = false;
        voice.Owner = null;
        voice.Owned = false;
        voice.Entry = null;
    }

    /// <summary>전투 종료 시 UI를 제외한 진행 중인 효과음을 정리합니다.</summary>
    public static void StopGameplaySounds()
    {
        // 일시정지 해제 후 이전 전투음이 다시 나오지 않게 한다.
        if (_instance == null) return;
        foreach (Voice voice in _instance._voices)
            if (voice.Active && !voice.Entry.IgnorePause)
                _instance.Release(voice);
    }

    /// <summary>현재 배경음악을 지정한 곡으로 교체합니다.</summary>
    public static void SetMusic(GameSound id)
    {
        // 서비스 화면이 열려 있으면 복귀할 기본 음악만 갱신한다.
        if (_instance == null) return;
        _instance._baseMusicId = id;
        _instance.ApplyMusicSelection();
    }

    /// <summary>서비스 화면을 표시하는 동안 기본 음악 대신 지정한 음악을 사용합니다.</summary>
    public static void PushMusic(MonoBehaviour owner, GameSound id)
    {
        // 같은 화면의 페이지 이동은 기존 요청을 교체한다.
        if (_instance == null || owner == null || !owner.isActiveAndEnabled) return;
        _instance._musicOverrides.RemoveAll(item => item.Owner == owner);
        _instance._musicOverrides.Add(new MusicOverride { Owner = owner, Sound = id });
        _instance.ApplyMusicSelection();
    }

    /// <summary>화면의 임시 음악을 해제하고 이전 화면 또는 기본 씬 음악으로 복귀합니다.</summary>
    public static void PopMusic(MonoBehaviour owner)
    {
        // 이미 제거된 요청은 반복 처리하지 않는다.
        if (_instance == null) return;
        if (_instance._musicOverrides.RemoveAll(item => item.Owner == owner) > 0)
            _instance.ApplyMusicSelection();
    }

    /// <summary>화면이 예외적으로 비활성화돼도 음악 요청이 남지 않게 정리합니다.</summary>
    private void RefreshMusicOverrides()
    {
        // Unity에서 파괴된 컴포넌트도 null 비교로 감지한다.
        if (_musicOverrides.RemoveAll(item => item.Owner == null || !item.Owner.isActiveAndEnabled) > 0)
            ApplyMusicSelection();
    }

    /// <summary>마지막으로 열린 화면의 음악을 우선하여 실제 재생곡을 선택합니다.</summary>
    private void ApplyMusicSelection()
    {
        // 표시 중인 서비스가 없으면 현재 씬의 기본 음악을 선택한다.
        GameSound id = _musicOverrides.Count > 0
            ? _musicOverrides[_musicOverrides.Count - 1].Sound : _baseMusicId;
        ChangeMusicSelection(id);
    }

    /// <summary>선택된 음악이 달라졌을 때 페이드를 시작합니다.</summary>
    private static void ChangeMusicSelection(GameSound id)
    {
        // 같은 곡을 반복 요청해도 재시작하지 않는다.
        if (_instance == null || _instance._musicId == id) return;
        if (id != GameSound.None && !_instance._entries.ContainsKey(id))
        {
            Debug.LogError("Audio Catalog에 없는 BGM: " + id);
            return;
        }
        _instance._musicId = id;
        if (_instance._musicRoutine != null)
            _instance.StopCoroutine(_instance._musicRoutine);
        _instance._musicRoutine = _instance.StartCoroutine(_instance.ChangeMusic(id));
    }

    /// <summary>이전 음악을 감쇠한 뒤 새 곡의 음량을 올립니다.</summary>
    private IEnumerator ChangeMusic(GameSound id)
    {
        // Time.timeScale과 무관하게 음악을 전환한다.
        float startVolume = _music.volume;
        float elapsed = 0f;
        while (_music.isPlaying && elapsed < _musicFadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            _music.volume = Mathf.Lerp(startVolume, 0f, elapsed / _musicFadeSeconds);
            yield return null;
        }
        _music.Stop();
        _music.clip = null;
        if (id == GameSound.None)
        {
            _musicRoutine = null;
            yield break;
        }
        // 개별 곡 음량에 기존 BGM Mixer 음량이 추가로 적용된다.
        GameAudioCatalog.Entry entry = _entries[id];
        _music.clip = entry.Clip;
        _music.volume = 0f;
        _music.Play();
        elapsed = 0f;
        while (elapsed < _musicFadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            _music.volume = Mathf.Lerp(0f, entry.Volume, elapsed / _musicFadeSeconds);
            yield return null;
        }
        _music.volume = entry.Volume;
        _musicRoutine = null;
    }

    /// <summary>단일 씬 이동에서 이전 씬의 소리와 음악을 정리합니다.</summary>
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Town에 추가로 여는 서비스 창에서는 음악을 유지한다.
        if (mode == LoadSceneMode.Additive) return;
        foreach (Voice voice in _voices)
            if (voice.Active && (voice.Owned || !voice.Entry.ContinueAcrossScenes))
                Release(voice);
        _nextTimes.Clear();
        _musicOverrides.Clear();
        _baseMusicId = GameSound.None;
        if (_musicRoutine != null) StopCoroutine(_musicRoutine);
        _musicRoutine = null;
        _music.Stop();
        _music.clip = null;
        _musicId = GameSound.None;
    }

    /// <summary>공용 재생기가 제거될 때 이벤트와 정적 참조를 정리합니다.</summary>
    private void OnDestroy()
    {
        // 중복 재생기 제거가 실제 인스턴스에 영향을 주지 않게 한다.
        if (_instance != this) return;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        _instance = null;
    }
}

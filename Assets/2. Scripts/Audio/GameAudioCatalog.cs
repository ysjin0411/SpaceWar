using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Audio Catalog")]
public sealed class GameAudioCatalog : ScriptableObject
{
    [Serializable]
    public sealed class Group
    {
        // 사용자가 지정하는 그룹 제목.
        public string GroupName;
        // 그룹에 공통으로 적용되는 편집용 분류.
        public GameAudioCategory Category;
        // 이 그룹에 속한 독립적인 사운드 목록.
        public Entry[] Entries = Array.Empty<Entry>();
    }

    [Serializable]
    public sealed class Entry
    {
        // 기존 단일 배열의 이전에만 사용한다. 새 편집은 Group.Category를 사용한다.
        [HideInInspector]
        public GameAudioCategory Category = GameAudioCategory.Unassigned;

        // 코드에서 사용하는 식별자와 실제 파일.
        public GameSound Id;
        public AudioClip Clip;
        // 파일별 상대 음량.
        [Range(0f, 1f)] public float Volume = 0.6f;
        // 동일 소리의 최소 재생 간격과 최대 동시 재생 수.
        [Min(0f)] public float Interval = 0.05f;
        [Min(1)] public int MaxVoices = 3;
        // 숫자가 작을수록 중요한 소리.
        [Range(0, 256)] public int Priority = 128;
        // 일시정지 중에도 재생하는 UI·결과 소리.
        public bool IgnorePause;
        // 씬 전환 후에도 남은 재생을 허용할 단발 소리.
        public bool ContinueAcrossScenes;
    }
    // 그룹마다 이름과 카테고리, 별도 Entries 배열을 저장한다.
    [SerializeField] private Group[] _groups = Array.Empty<Group>();
    // 기존 에셋의 데이터 이전이 끝날 때까지 보존하는 원본 배열.
    [SerializeField, HideInInspector] private Entry[] _entries = Array.Empty<Entry>();

    /// <summary>초기화 시 모든 그룹의 항목을 재생기와 생성기에 제공한다.</summary>
    public IEnumerable<Entry> Entries
    {
        get
        {
            // 아직 이전하지 않은 에셋도 재생할 수 있게 한다.
            if (_groups.Length == 0)
            {
                foreach (Entry entry in _entries) yield return entry;
                yield break;
            }
            // 그룹 내부 순서대로 읽으며 설정 오류는 즉시 알린다.
            foreach (Group group in _groups)
            {
                if (group == null || group.Entries == null)
                    throw new InvalidOperationException("Audio Catalog 그룹 또는 Entries가 null입니다.");
                foreach (Entry entry in group.Entries) yield return entry;
            }
        }
    }

#if UNITY_EDITOR
    /// <summary>기존 항목을 카테고리별 실제 그룹으로 이전한다.</summary>
    public void MigrateLegacyEntries()
    {
        // 기존 그룹이나 빈 원본은 다시 이전하지 않는다.
        if (_groups.Length > 0 || _entries.Length == 0) return;
        var grouped = new SortedDictionary<GameAudioCategory, List<Entry>>();
        foreach (Entry entry in _entries)
        {
            // 손상된 항목을 버리지 않고 이전을 중단한다.
            if (entry == null) throw new InvalidOperationException("카탈로그에 null 항목이 있습니다.");
            if (!grouped.TryGetValue(entry.Category, out List<Entry> list))
            {
                list = new List<Entry>();
                grouped.Add(entry.Category, list);
            }
            list.Add(entry);
        }
        var groups = new List<Group>();
        foreach (var pair in grouped)
        {
            // Entry를 그대로 옮겨 음원과 사용자 설정을 보존한다.
            groups.Add(new Group { GroupName = pair.Key.ToString(),
                Category = pair.Key, Entries = pair.Value.ToArray() });
        }
        // 모든 구성이 성공한 뒤에만 원본을 비운다.
        _groups = groups.ToArray();
        _entries = Array.Empty<Entry>();
    }

    /// <summary>생성 결과를 반영하며 기존 그룹 이름과 항목 소속을 유지한다.</summary>
    public void SetEntries(Entry[] entries)
    {
        // 전체 입력을 검증한 뒤 저장 구조를 갱신한다.
        if (entries == null) throw new ArgumentNullException(nameof(entries));
        var pending = new Dictionary<GameSound, Entry>();
        foreach (Entry entry in entries)
        {
            if (entry == null || !pending.TryAdd(entry.Id, entry))
                throw new InvalidOperationException("생성된 사운드에 null 또는 중복 Id가 있습니다.");
        }
        MigrateLegacyEntries();
        var groups = new List<Group>();
        foreach (Group group in _groups)
        {
            // 기존 그룹 순서와 각 항목의 소속을 유지한다.
            var updated = new List<Entry>();
            foreach (Entry old in group.Entries)
            {
                if (pending.TryGetValue(old.Id, out Entry entry))
                {
                    updated.Add(entry);
                    pending.Remove(old.Id);
                }
            }
            groups.Add(new Group { GroupName = group.GroupName,
                Category = group.Category, Entries = updated.ToArray() });
        }
        if (pending.Count > 0)
        {
            // 새 항목의 분류는 사용자가 결정할 수 있도록 별도로 모은다.
            groups.Add(new Group { GroupName = "새 항목 - 분류 필요",
                Category = GameAudioCategory.Unassigned,
                Entries = new List<Entry>(pending.Values).ToArray() });
        }
        _groups = groups.ToArray();
    }
#endif
}

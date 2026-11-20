#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AudioCatalogBuilder
{
    // 현재 프로젝트에서 확인한 파일 경로.
    private const string Root = "Assets/98.Music/Game_SciFi_Audio";
    // 생성할 카탈로그 경로.
    private const string CatalogPath = Root + "/GameAudioCatalog.asset";

    /// <summary>사용자가 메뉴를 실행하면 WAV 파일을 카탈로그에 연결합니다.</summary>
    [MenuItem("Tools/Audio/Build Game Audio Catalog")]
    public static void Build()
    {
        // 잘못된 경로에서 빈 목록을 생성하지 않는다.
        if (!AssetDatabase.IsValidFolder(Root))
            throw new InvalidOperationException("오디오 폴더를 찾을 수 없습니다: " + Root);
        GameAudioCatalog catalog = AssetDatabase.LoadAssetAtPath<GameAudioCatalog>(CatalogPath);
        Dictionary<GameSound, GameAudioCatalog.Entry> previous =
            new Dictionary<GameSound, GameAudioCatalog.Entry>();
        if (catalog != null)
            foreach (GameAudioCatalog.Entry entry in catalog.Entries)
                if (entry != null) previous[entry.Id] = entry;

        // 미리듣기를 제외하고 파일명에 맞는 식별자로 연결한다.
        List<GameAudioCatalog.Entry> entries = new List<GameAudioCatalog.Entry>();
        HashSet<GameSound> found = new HashSet<GameSound>();
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Root }))
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (assetPath.Contains("/00_Previews/")) continue;
            string name = Path.GetFileNameWithoutExtension(assetPath);
            if (!Enum.TryParse(ToIdentifier(name), out GameSound id) ||
                !Enum.IsDefined(typeof(GameSound), id) || id == GameSound.None)
                throw new InvalidOperationException("GameSound에 없는 파일: " + assetPath);
            if (!found.Add(id))
                throw new InvalidOperationException("중복 사운드: " + name);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            if (clip == null) throw new InvalidOperationException("AudioClip 로드 실패: " + assetPath);

            // 재실행 시 사용자가 조절한 값을 유지한다.
            GameAudioCatalog.Entry entry;
            if (previous.TryGetValue(id, out GameAudioCatalog.Entry old))
            {
                // 검증 실패 시 기존 에셋이 변경되지 않도록 별도 객체에 복사한다.
                entry = new GameAudioCatalog.Entry
                {
                    Id = id,
                    Volume = old.Volume,
                    // 재생성 시 사용자가 지정한 편집용 분류를 보존한다.
                    Category = old.Category,
                    Interval = old.Interval,
                    MaxVoices = old.MaxVoices,
                    Priority = old.Priority,
                    IgnorePause = old.IgnorePause,
                    ContinueAcrossScenes = old.ContinueAcrossScenes
                };
            }
            else
            {
                bool ui = assetPath.Contains("/03_Town_UI/") || assetPath.Contains("/04_Menu_UI/");
                if (name.StartsWith("player_")) ui = false;
                bool result = name == "stage_clear" || name == "stage_failed" ||
                    name == "player_death" || name == "level_up" ||
                    name == "upgrade_select" || name == "ultimate_ready" ||
                    name == "cruiser_death_explosion" || name == "alien_death";
                bool music = assetPath.Contains("/01_BGM/");
                bool loop = name.EndsWith("_loop");
                bool howl = name.StartsWith("alien_howl") || name == "alien_transition_full_5s";
                bool ambient = assetPath.Contains("/02_Ambience/");
                entry = new GameAudioCatalog.Entry
                {
                    Id = id,
                    Volume = ambient ? 0.25f : music ? 0.4f : howl ? 0.8f : 0.6f,
                    Interval = loop || howl ? 0f : 0.05f,
                    MaxVoices = loop ? 8 : howl ? 1 : 3,
                    Priority = howl ? 16 : ui || result ? 48 : 128,
                    IgnorePause = ui || result || music || name.StartsWith("head_confusion") || name == "alien_crack_ready",
                    ContinueAcrossScenes = ui || result
                };
            }
            entry.Clip = clip;
            entries.Add(entry);
        }

        // 누락이 있으면 기존 카탈로그를 변경하지 않는다.
        foreach (GameSound id in Enum.GetValues(typeof(GameSound)))
            if (id != GameSound.None && !found.Contains(id))
                throw new InvalidOperationException("부족한 사운드: " + id);
        entries.Sort((a, b) => a.Id.CompareTo(b.Id));

        // 검증된 목록만 Unity 자산으로 저장한다.
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<GameAudioCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        Undo.RecordObject(catalog, "Build Audio Catalog");
        catalog.SetEntries(entries.ToArray());
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Selection.activeObject = catalog;
        Debug.Log("Audio Catalog 연결 완료: " + entries.Count + "개");
    }

    /// <summary>WAV 파일명을 코드 식별자 형태로 변환합니다.</summary>
    private static string ToIdentifier(string fileName)
    {
        // 각 단어의 첫 글자를 대문자로 변환한다.
        string result = string.Empty;
        foreach (string part in fileName.Split('_'))
            if (part.Length > 0)
                result += char.ToUpperInvariant(part[0]) + part.Substring(1);
        return result;
    }
}
#endif

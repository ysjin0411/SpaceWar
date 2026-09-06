using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// StageData Inspector와 씬 저장 시 자동 갱신을 담당한다.
[CustomEditor(typeof(StageData))]
[InitializeOnLoad]
public class StageDataEditor : Editor
{
    static StageDataEditor()
    {
        // 씬 저장이 끝났을 때 해당 씬을 사용하는 데이터를 갱신한다.
        EditorSceneManager.sceneSaved += HandleSceneSaved;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUI.BeginChangeCheck();

        // 사용자가 직접 관리할 정보만 표시한다.
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("_stageIndex"));

        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("_stageName"));

        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("_stageIcon"));

        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("_sourceScene"));

        bool changed = EditorGUI.EndChangeCheck();
        serializedObject.ApplyModifiedProperties();

        StageData data = (StageData)target;

        // 씬을 연결하거나 정보를 수정하면 자동 추출한다.
        // 플레이 중에는 프로젝트 에셋을 갱신하지 않는다.
        bool canRefresh = !EditorApplication.isPlayingOrWillChangePlaymode;

        if (changed && canRefresh)
        {
            RefreshWithLog(data);
        }

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(!canRefresh))
        {
            // 기존 데이터를 처음 연결하거나 다시 확인할 때 사용한다.
            if (GUILayout.Button("씬에서 보스 정보 새로고침"))
            {
                RefreshWithLog(data);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("씬에서 자동으로 읽은 보스");

        // 결과는 확인만 가능하고 여기에서 직접 바꿀 수 없다.
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField(
                "Mid Boss",
                data.MidBossPrefab,
                typeof(BossEnemy),
                false);

            EditorGUILayout.ObjectField(
                "Final Boss",
                data.FinalBossPrefab,
                typeof(BossEnemy),
                false);
        }
    }

    // Inspector에서는 오류를 Console에 표시한다.
    private static void RefreshWithLog(StageData data)
    {
        try
        {
            RefreshFromSavedScene(data);
            AssetDatabase.SaveAssets();
        }
        catch (Exception exception)
        {
            Debug.LogError(exception.Message, data);
        }
    }

    // 저장한 씬과 연결된 StageData만 갱신한다.
    private static void HandleSceneSaved(Scene scene)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        foreach (StageData data in FindAllStageData())
        {
            if (data.SourceScene == null)
            {
                continue;
            }

            string sourcePath =
                AssetDatabase.GetAssetPath(data.SourceScene);

            if (sourcePath != scene.path)
            {
                continue;
            }

            try
            {
                // 방금 저장한 씬은 이미 열려 있으므로 그대로 읽는다.
                ExtractBosses(data, scene);
            }
            catch (Exception exception)
            {
                Debug.LogError(exception.Message, data);
            }
        }

        AssetDatabase.SaveAssets();
    }

    // 프로젝트에 만들어진 StageData 목록을 찾는다.
    private static IEnumerable<StageData> FindAllStageData()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:StageData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            StageData data = AssetDatabase.LoadAssetAtPath<StageData>(path);

            if (data != null)
            {
                yield return data;
            }
        }
    }

    // 빌드 전 전체 데이터를 최신 씬 기준으로 갱신한다.
    public static void RefreshAll()
    {
        foreach (StageData data in FindAllStageData())
        {
            RefreshFromSavedScene(data);
        }

        AssetDatabase.SaveAssets();
    }

    // 다른 씬을 열어 작업 중이어도 원본 씬을 검사할 수 있다.
    private static void RefreshFromSavedScene(StageData data)
    {
        if (data.SourceScene == null)
        {
            StoreBosses(data, null, null);

            throw new InvalidOperationException(
                $"{data.name}: Source Scene에 스테이지 씬을 연결하세요.");
        }

        string path = AssetDatabase.GetAssetPath(data.SourceScene);
        Scene openedScene = SceneManager.GetSceneByPath(path);

        if (openedScene.IsValid() && openedScene.isLoaded)
        {
            // 저장되지 않은 씬과 빌드 데이터가 달라지는 것을 방지한다.
            if (openedScene.isDirty)
            {
                throw new InvalidOperationException(
                    $"{data.name}: 원본 씬을 저장한 뒤 새로고침하세요.");
            }

            ExtractBosses(data, openedScene);
            return;
        }

        // 작업 중인 씬 구성은 유지하고 검사 전용 씬으로 연다.
        Scene previewScene = EditorSceneManager.OpenPreviewScene(path);

        try
        {
            ExtractBosses(data, previewScene);
        }
        finally
        {
            // 검사 중 오류가 발생해도 미리보기 씬을 닫는다.
            EditorSceneManager.ClosePreviewScene(previewScene);
        }
    }

    // 원본 씬에서 보스 배정을 읽는다.
    private static void ExtractBosses(StageData data, Scene scene)
    {
        var encounters = new List<BossEncounterManager>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            // 비활성 오브젝트에 있는 컴포넌트도 검사한다.
            encounters.AddRange(
                root.GetComponentsInChildren<BossEncounterManager>(true));
        }

        if (encounters.Count != 1)
        {
            StoreBosses(data, null, null);

            throw new InvalidOperationException(
                $"{scene.name}: BossEncounterManager가 정확히 하나 있어야 합니다.");
        }

        BossEnemy midBoss = encounters[0].MidBossPrefab;
        BossEnemy finalBoss = encounters[0].FinalBossPrefab;

        // 미리보기 씬을 닫아도 유지되는 프리팹 에셋 참조만 저장한다.
        if (midBoss == null || finalBoss == null ||
            !EditorUtility.IsPersistent(midBoss) ||
            !EditorUtility.IsPersistent(finalBoss))
        {
            StoreBosses(data, null, null);

            throw new InvalidOperationException(
                $"{scene.name}: 중간·최종보스에는 Project 창의 프리팹을 연결하세요.");
        }

        StoreBosses(data, midBoss, finalBoss);
    }

    // 추출 결과가 바뀌었을 때만 저장 대상으로 표시한다.
    private static void StoreBosses(
        StageData data,
        BossEnemy midBoss,
        BossEnemy finalBoss)
    {
        if (data.SetExtractedBosses(midBoss, finalBoss))
        {
            EditorUtility.SetDirty(data);
        }
    }
}

// 빌드에 오래된 보스 정보가 들어가지 않도록 마지막으로 검사한다.
public class StageDataBuildProcessor : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        try
        {
            StageDataEditor.RefreshAll();
        }
        catch (Exception exception)
        {
            throw new BuildFailedException(
                $"스테이지 정보 갱신 실패: {exception.Message}");
        }
    }
}
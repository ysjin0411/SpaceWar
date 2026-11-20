using UnityEditor;
using UnityEngine;

/// <summary>실제 그룹 배열과 기존 데이터 이전 기능을 표시한다.</summary>
[CustomEditor(typeof(GameAudioCatalog))]
public sealed class GameAudioCatalogEditor : Editor
{
    // 그룹 배열과 이전용 원본 배열의 편집 참조.
    private SerializedProperty _groups;
    private SerializedProperty _legacyEntries;

    /// <summary>Inspector에서 편집할 필드를 연결한다.</summary>
    private void OnEnable()
    {
        // 직렬화 속성으로 Undo와 에셋 저장을 지원한다.
        _groups = serializedObject.FindProperty("_groups");
        _legacyEntries = serializedObject.FindProperty("_entries");
    }

    /// <summary>그룹별 Entries와 데이터 이전 버튼을 표시한다.</summary>
    public override void OnInspectorGUI()
    {
        // 외부 변경 사항을 반영한다.
        serializedObject.Update();
        if (_groups.arraySize == 0 && _legacyEntries.arraySize > 0)
        {
            if (GUILayout.Button("기존 사운드를 그룹별 Entries로 이전"))
            {
                // 이전 작업은 Undo로 되돌릴 수 있다.
                var catalog = (GameAudioCatalog)target;
                Undo.RecordObject(catalog, "Migrate Audio Groups");
                catalog.MigrateLegacyEntries();
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssetIfDirty(catalog);
                serializedObject.Update();
            }
        }
        // 실제 배열을 편집하므로 그룹 추가 및 삭제도 가능하다.
        EditorGUILayout.PropertyField(_groups, new GUIContent("Groups"), true);
        serializedObject.ApplyModifiedProperties();
    }
}

/// <summary>그룹 제목에 이름과 카테고리를 함께 표시한다.</summary>
[CustomPropertyDrawer(typeof(GameAudioCatalog.Group))]
public sealed class GameAudioGroupDrawer : PropertyDrawer
{
    // 표시 순서를 고정하고 GUI 호출마다 배열을 만들지 않는다.
    private static readonly string[] Fields = { "GroupName", "Category", "Entries" };

    /// <summary>펼침 상태와 Entries 크기에 맞춰 높이를 계산한다.</summary>
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // 접힌 그룹은 제목 한 줄만 표시한다.
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded) return height;
        foreach (string field in Fields)
            height += EditorGUIUtility.standardVerticalSpacing +
                EditorGUI.GetPropertyHeight(property.FindPropertyRelative(field), true);
        return height;
    }

    /// <summary>이름, Category 드롭다운, 독립된 Entries를 그린다.</summary>
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        // 비어 있는 이름은 Unity의 기본 Element 제목으로 표시한다.
        string name = property.FindPropertyRelative("GroupName").stringValue;
        var category = (GameAudioCategory)property.FindPropertyRelative("Category").intValue;
        string title = (string.IsNullOrWhiteSpace(name) ? label.text : name) + $" [{category}]";
        Rect row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, title, true);
        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            foreach (string field in Fields)
            {
                // 앞 필드의 높이를 반영해 다음 필드를 아래에 배치한다.
                SerializedProperty child = property.FindPropertyRelative(field);
                row.y += row.height + EditorGUIUtility.standardVerticalSpacing;
                row.height = EditorGUI.GetPropertyHeight(child, true);
                EditorGUI.PropertyField(row, child, true);
            }
            EditorGUI.indentLevel--;
        }
        EditorGUI.EndProperty();
    }
}

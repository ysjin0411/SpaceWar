using UnityEditor;
using UnityEngine;

// WeaponData 에셋의 Inspector 표시를 담당한다.
[CustomEditor(typeof(WeaponData))]
[CanEditMultipleObjects]
public class WeaponDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // 최신 에셋 값을 읽는다.
        serializedObject.Update();

        // 어떤 스크립트의 에셋인지 표시하되 변경은 막는다.
        using (new EditorGUI.DisabledScope(true))
        {
            DrawProperty("m_Script");
        }

        // 모든 무기에 필요한 기본 정보.
        DrawProperty("_id");
        DrawProperty("_name");
        DrawProperty("_type");
        DrawProperty("_icon");
        DrawProperty("_playerSpriteLibrary");
        // 무기 종류에 관계없이 캐릭터 선택 화면의 외형을 조절한다.
        DrawProperty("_characterSelectPreviewScale");
        DrawProperty("_characterSelectPreviewOffset");

        SerializedProperty typeProperty =
            serializedObject.FindProperty("_type");

        // 서로 다른 타입의 에셋을 동시에 선택했다면
        // 공통 항목만 편집하도록 한다.
        if (typeProperty.hasMultipleDifferentValues)
        {
            EditorGUILayout.HelpBox(
                "서로 다른 무기 타입을 선택했습니다. " +
                "타입별 설정은 같은 타입끼리 선택해서 수정하세요.",
                MessageType.Info);
        }
        else
        {
            WeaponType type =
                (WeaponType)typeProperty.intValue;

            DrawTypeSettings(type);
        }

        // 모든 무기가 공유하는 구매 조건.
        DrawProperty("_price");
        DrawProperty("_requiredCharacterLevel");
        DrawProperty("_isInitiallyOwned");

        // 모든 무기가 공유하는 UI 설명.
        DrawProperty("_info");
        DrawProperty("_upgradeSelectionInfo");

        // 변경 내용을 저장한다.
        // SerializedProperty를 사용하므로 Undo도 지원한다.
        serializedObject.ApplyModifiedProperties();
    }

    // 선택한 무기 타입에 필요한 설정만 표시한다.
    private void DrawTypeSettings(WeaponType type)
    {
        bool isShield = type == WeaponType.Shield;
        bool isGrenade = type == WeaponType.Grenade;

        bool isAttachedMelee =
            type == WeaponType.GiantSword ||
            type == WeaponType.Hammer;

        // 실드와 부착형 근접 공격은 투사체 프리팹을 사용하지 않는다.
        if (!isShield && !isAttachedMelee)
        {
            DrawProperty("_projectilePrefab");
        }

        if (isShield)
        {
            // 실드는 공격력과 발사 쿨타임 대신
            // 고유 체력과 회복 수치를 사용한다.
            DrawProperty("_shieldMaxHealth");
            DrawProperty("_shieldRecoveryPerSecond");
            DrawProperty("_shieldRepairAmount");
            DrawProperty("_shieldRecoveryUpgradeValue");

            return;
        }

        // 피해를 주는 무기는 기본 공격력을 사용한다.
        DrawProperty("_baseDamage");

        if (isGrenade)
        {
            // 수류탄은 발사 쿨타임 대신 충전량과 충전 시간을 사용한다.
            DrawProperty("_maximumChargeCount");
            DrawProperty("_chargeRecoveryTime");
            DrawProperty("_chargeRecoveryUpgradeValue");
            DrawProperty("_minimumChargeRecoveryTime");

            return;
        }

        // 일반 무기와 휘두르기 무기의 공격 간격.
        DrawProperty("_baseCooltime");

        // 일반 공격속도 증강 설정.
        DrawProperty("_fireRateUpgradeValue");
        DrawProperty("_minimumCooltime");
    }

    // 직렬화된 필드를 기본 Unity 편집 방식으로 표시한다.
    private void DrawProperty(string propertyName)
    {
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);

        // 필드 이름이 바뀌거나 빠졌다면 Inspector에서 바로 알린다.
        if (property == null)
        {
            EditorGUILayout.HelpBox(
                $"WeaponData에서 {propertyName} 필드를 찾을 수 없습니다.",
                MessageType.Error);

            return;
        }

        EditorGUILayout.PropertyField(property, true);
    }
}
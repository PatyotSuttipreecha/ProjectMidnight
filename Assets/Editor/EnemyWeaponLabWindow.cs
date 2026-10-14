using UnityEditor;
using UnityEngine;

public class EnemyWeaponLabWindow : EditorWindow
{
    private EnemyWeaponSO module;
    private Editor moduleEditor;
    private Vector2 scroll;
    [MenuItem("System Modification/Enemy Lab/Weapon Modules")]
    public static void Open() => GetWindow<EnemyWeaponLabWindow>("Enemy Weapons");
    public static void Open(EnemyWeaponSO selectedModule)
    {
        var window = GetWindow<EnemyWeaponLabWindow>("Enemy Weapons");
        window.module = selectedModule;
        window.scroll = Vector2.zero;
        window.Repaint();
        window.Focus();
    }
    public static void DrawProfileLink(EnemyDataSO profile)
    {
        var weapon = profile != null ? profile.startingWeapon : null;
        using (new EditorGUI.DisabledScope(weapon == null))
        {
            if (GUILayout.Button(new GUIContent("Edit Assigned Weapon Module", "Open this profile's Starting Weapon in Weapon Modules. Changes affect every profile using the same module.")))
                Open(weapon);
            if (GUILayout.Button(new GUIContent("Locate Weapon Asset", "Highlight the assigned module in the Project window.")))
                EditorGUIUtility.PingObject(weapon);
        }
        if (weapon == null)
            EditorGUILayout.HelpBox("No Starting Weapon assigned (unarmed). Assign a Weapon Module to edit it here.", MessageType.Info);
    }
    public static void DrawInlineSettings(EnemyDataSO profile, ref bool expanded)
    {
        EditorGUILayout.Space(8);
        expanded = EditorGUILayout.Foldout(expanded, "Assigned Weapon Module Settings", true);
        if (!expanded) return;
        var weapon = profile != null ? profile.startingWeapon : null;
        if (weapon == null)
        {
            EditorGUILayout.HelpBox("Assign Starting Weapon above to configure its module. Empty = unarmed.", MessageType.Info);
            return;
        }
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(weapon.name, EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Editing the assigned module asset. Every profile using this module shares these values. Duplicate the module for independent settings.", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            var data = new SerializedObject(weapon);
            data.Update();
            var property = data.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.propertyPath == "m_Script") continue;
                EditorGUILayout.PropertyField(property, true);
            }
            data.ApplyModifiedProperties();
            if (GUILayout.Button("Save Weapon Module")) AssetDatabase.SaveAssetIfDirty(weapon);
        }
        DrawProfileLink(profile);
        EditorGUILayout.EndVertical();
    }
    private void OnSelectionChange()
    {
        if (Selection.activeObject is EnemyWeaponSO selected) module = selected;
        Repaint();
    }
    private void OnDisable() { if (moduleEditor != null) DestroyImmediate(moduleEditor); }
    private void OnGUI()
    {
        EditorGUILayout.LabelField("The Lost - Weapon Modules", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Assign a module to Starting Weapon on an Enemy Profile with Use Lost Combat enabled. Empty = unarmed. Runtime drops return to the profile's base attack without changing this asset.", MessageType.Info);
        module = (EnemyWeaponSO)EditorGUILayout.ObjectField("Weapon module", module, typeof(EnemyWeaponSO), false);
        if (GUILayout.Button("Select prototype loadout profiles"))
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<EnemyDataSO>("Assets/Data/Enemies/TheLost/Data_Enemies_TheLost_Unarmed.asset");
        if (module == null) return;
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            Editor.CreateCachedEditor(module, null, ref moduleEditor);
            moduleEditor.OnInspectorGUI();
            EditorGUILayout.HelpBox("Use visual-only prefabs for held models, projectiles and fire effects. Set Animator state names after adding real sweep, axe and throw animations. Current prototype presets use the existing zombie attack as fallback.", MessageType.Warning);
            if (GUILayout.Button("Save Module")) AssetDatabase.SaveAssetIfDirty(module);
            EditorGUILayout.EndScrollView();
        }
    }
}

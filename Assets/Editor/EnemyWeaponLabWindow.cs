using UnityEditor;
using UnityEngine;

public class EnemyWeaponLabWindow : EditorWindow
{
    private EnemyWeaponSO module;
    private Editor moduleEditor;
    private Vector2 scroll;
    [MenuItem("System Modification/Enemy Lab/Weapon Modules")]
    public static void Open() => GetWindow<EnemyWeaponLabWindow>("Enemy Weapons");
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

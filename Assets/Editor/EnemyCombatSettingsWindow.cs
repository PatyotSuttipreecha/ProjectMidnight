using UnityEditor;
using UnityEngine;

public sealed class EnemyCombatSettingsWindow : EditorWindow
{
    private const string SettingsPath = "Assets/Data/Enemies/Resources/EnemyCombatSettings.asset";
    private EnemyCombatSettings settings;
    private Editor settingsEditor;
    private Vector2 scroll;

    [MenuItem("System Modification/Enemy Lab/Shared Combat Settings")]
    public static void Open()
    {
        var window = GetWindow<EnemyCombatSettingsWindow>("Combat Settings");
        window.minSize = new Vector2(360, 280);
        window.Resolve();
        window.Show();
        window.Focus();
    }
    private void Resolve()
    {
        settings = EnemyCombatSettings.Shared;
        if (settings == null) settings = AssetDatabase.LoadAssetAtPath<EnemyCombatSettings>(SettingsPath);
    }
    private void OnEnable() => Resolve();
    private void OnDisable() { if (settingsEditor != null) DestroyImmediate(settingsEditor); }
    private void OnGUI()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("SHARED COMBAT SETTINGS", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Distant enemies rush until the pressure group fills. Owners keep their place through circling and attacks; nearest replacements enter one at a time. Sprinting away pauses running and new admissions. Settings apply across profiles and spawn groups targeting the same player.", MessageType.Info);
        if (settings == null) Resolve();
        if (settings == null)
        {
            EditorGUILayout.HelpBox("Combat settings asset could not be loaded. Expected: " + SettingsPath, MessageType.Error);
            if (GUILayout.Button("Reimport Settings Asset"))
            {
                AssetDatabase.ImportAsset(SettingsPath, ImportAssetOptions.ForceUpdate);
                Resolve();
            }
            return;
        }
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.ObjectField("Shared Asset", settings, typeof(EnemyCombatSettings), false);
        if (Application.isPlaying)
            EditorGUILayout.HelpBox("Editing the shared asset during Play Mode. These values persist after leaving Play Mode; this is not a temporary runtime override.", MessageType.Warning);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        Editor.CreateCachedEditor(settings, null, ref settingsEditor);
        settingsEditor.OnInspectorGUI();
        EditorGUILayout.EndScrollView();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Locate Asset")) EditorGUIUtility.PingObject(settings);
        if (GUILayout.Button("Save Settings")) AssetDatabase.SaveAssetIfDirty(settings);
        EditorGUILayout.EndHorizontal();
    }
}

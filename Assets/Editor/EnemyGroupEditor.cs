using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnemyGroup))]
public class EnemyGroupEditor : Editor
{
    [MenuItem("System Modification/Enemy Lab/Create/Enemy Group")]
    public static void CreateGroup()
    {
        var obj = new GameObject("Enemy Group");
        Undo.RegisterCreatedObjectUndo(obj, "Create Enemy Group");
        Undo.AddComponent<EnemyGroup>(obj);
        Selection.activeGameObject = obj;
    }
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("Group mode overrides Spawn Point and Profile. Assign each enemy/spawn point to one group. Enabling Aggressive does not start paused cutscene actors; call StartPatrol separately.", MessageType.Info);
        var group = (EnemyGroup)target;
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            EditorGUILayout.LabelField("Current Mode", Application.isPlaying ? group.IsAggressive ? "Aggressive" : "Normal" : "Play Mode only");
            if (GUILayout.Button("Enable Aggressive")) group.EnableAggressive();
            if (GUILayout.Button("Disable Aggressive")) group.DisableAggressive();
        }
    }
}

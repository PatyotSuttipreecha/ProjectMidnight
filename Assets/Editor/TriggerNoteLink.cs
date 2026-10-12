using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TriggerNoteLink
{
    public static WorldTrigger Find(PointNote note) => note.GetComponent<WorldTrigger>();
    public static void Sync(WorldTrigger trigger) { }
    public static WorldTrigger Create(PointNote note)
    {
        var trigger = note.GetComponent<WorldTrigger>();
        if (trigger == null)
        {
            trigger = Undo.AddComponent<WorldTrigger>(note.gameObject);
            var volume = trigger.GetComponent<BoxCollider>(); Undo.RecordObject(volume, "Configure Trigger Volume");
            volume.isTrigger = true; volume.size = new Vector3(3, 2, 3); volume.center = Vector3.up;
            var body = trigger.GetComponent<Rigidbody>(); Undo.RecordObject(body, "Configure Trigger Body"); body.isKinematic = true; body.useGravity = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(volume); PrefabUtility.RecordPrefabInstancePropertyModifications(body);
        }
        EditorSceneManager.MarkSceneDirty(note.gameObject.scene); return trigger;
    }
    public static void AddNote(WorldTrigger trigger)
    {
        if (trigger.GetComponent<PointNote>() == null) Undo.AddComponent<PointNote>(trigger.gameObject);
    }
    public static void DrawConfiguration(WorldTrigger trigger)
    {
        var serialized = new SerializedObject(trigger); serialized.Update();
        foreach (var field in new[] { "armed", "triggerOnce", "cooldown", "retryWhileInside", "allowWhileGameplayBlocked", "onPlayerEnter", "onTriggered", "onPlayerExit", "showGizmos", "gizmoColor" }) EditorGUILayout.PropertyField(serialized.FindProperty(field), true);
        if (serialized.ApplyModifiedProperties()) { PrefabUtility.RecordPrefabInstancePropertyModifications(trigger); EditorSceneManager.MarkSceneDirty(trigger.gameObject.scene); }
        var volume = trigger.GetComponent<BoxCollider>(); var volumeData = new SerializedObject(volume); volumeData.Update();
        EditorGUILayout.LabelField("Trigger Volume", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(volumeData.FindProperty("m_Center")); EditorGUILayout.PropertyField(volumeData.FindProperty("m_Size"));
        if (volumeData.ApplyModifiedProperties()) { PrefabUtility.RecordPrefabInstancePropertyModifications(volume); EditorSceneManager.MarkSceneDirty(trigger.gameObject.scene); }
    }
}

[CustomEditor(typeof(WorldTrigger))]
public class WorldTriggerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var trigger = (WorldTrigger)target;
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            TriggerNoteLink.AddNote(trigger);
            TriggerNoteLink.DrawConfiguration(trigger);
        }
    }
}

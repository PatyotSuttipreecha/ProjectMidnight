using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(PointNote))]
public class PointNoteEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var point = (PointNote)target;
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            PointNoteDataTools.DrawBinding(point);
            EditorGUILayout.Space(); EditorGUILayout.LabelField("Local Placement / Gizmos", EditorStyles.boldLabel);
            var local = new SerializedObject(point); local.Update();
            foreach (var field in new[] { "relatedObject", "autoNameInHierarchy", "hierarchyNamePrefix", "showGizmos", "showLabel", "showRelatedNotes", "relatedNotesOnlyWhenSelected", "maxRelatedNoteLabels", "markerSize", "labelSize", "labelOffset", "showObjectiveLinks", "showPrerequisiteLinks", "linksOnlyWhenSelected", "showLinkLabels", "maxVisibleLinks", "overrideColors" })
                EditorGUILayout.PropertyField(local.FindProperty(field), true);
            if (local.FindProperty("overrideColors").boolValue)
                foreach (var field in new[] { "markerColor", "labelBackground", "labelTextColor" }) EditorGUILayout.PropertyField(local.FindProperty(field));
            if (local.ApplyModifiedProperties()) { PrefabUtility.RecordPrefabInstancePropertyModifications(point); EditorSceneManager.MarkSceneDirty(point.gameObject.scene); }
            if (GUILayout.Button("Sync hierarchy name")) ApplyDesignChanges(point);
        }
    }
    public static bool HasMissionDesign(PointNote.PointType type) => type == PointNote.PointType.Mission || type == PointNote.PointType.Objective || type == PointNote.PointType.Puzzle;
    public static void ApplyDesignChanges(PointNote point)
    {
        if (EditorApplication.isPlaying || point == null || EditorUtility.IsPersistent(point)) return;
        if (point.autoNameInHierarchy)
        {
            string prefix = PointNoteNameSync.ResolvePrefix(point);
            if (!string.IsNullOrEmpty(prefix) && point.hierarchyNamePrefix != prefix)
            {
                Undo.RecordObject(point, "Preserve Note Name Prefix"); point.hierarchyNamePrefix = prefix;
                PrefabUtility.RecordPrefabInstancePropertyModifications(point);
            }
            string desired = PointNoteNameSync.HierarchyName(point);
            if (point.name != desired) { Undo.RecordObject(point.gameObject, "Rename Note Point"); point.name = desired; PrefabUtility.RecordPrefabInstancePropertyModifications(point.gameObject); EditorSceneManager.MarkSceneDirty(point.gameObject.scene); }
        }
        if (point.data != null)
        {
            PointNoteDataTools.SyncMission(point.data);
            PointNoteNameSync.SyncDataAssetName(point.data);
        }
        SceneView.RepaintAll();
    }
    public static bool HasDependencyCycle(PointNote point) => point.data != null ? PointNoteDataTools.HasCycle(point.data) : Visit(point, new HashSet<PointNote>(), new HashSet<PointNote>());
    private static bool Visit(PointNote point, HashSet<PointNote> active, HashSet<PointNote> visited)
    {
        if (point == null || point.pointType != PointNote.PointType.Objective) return false;
        if (active.Contains(point)) return true;
        if (!visited.Add(point)) return false;
        active.Add(point);
        foreach (var prerequisite in point.prerequisiteObjectives ?? new PointNote[0]) if (Visit(prerequisite, active, visited)) return true;
        active.Remove(point); return false;
    }
}

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PointNoteDataTools
{
    public const string Folder = "Assets/Data/PointNotes";
    public static PointNoteData Create(PointNote.PointType type, string title)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Data", "PointNotes");
        var data = ScriptableObject.CreateInstance<PointNoteData>(); data.recordId = Guid.NewGuid().ToString("N"); data.pointType = type; data.title = title;
        string safe = PointNoteNameSync.DataAssetName(Folder + "/Note.asset", type, title);
        AssetDatabase.CreateAsset(data, AssetDatabase.GenerateUniqueAssetPath(Folder + "/" + safe + ".asset"));
        return data;
    }
    public static PointNoteData Import(PointNote source)
    {
        ImportMany(new[] { source }); return source.data;
    }
    public static void ImportMany(IEnumerable<PointNote> sources)
    {
        var queue = new Queue<PointNote>(sources);
        var visited = new HashSet<PointNote>();
        var graphs = new Dictionary<PointNote, (PointNote[] objectives, PointNote[] prerequisites)>();
        while (queue.Count > 0)
        {
            var point = queue.Dequeue();
            if (point == null || !visited.Add(point) || point.data != null) continue;
            var children = point.objectives ?? new PointNote[0]; var prerequisites = point.prerequisiteObjectives ?? new PointNote[0];
            graphs.Add(point, (children, prerequisites));
            foreach (var child in children) if (child != null) queue.Enqueue(child);
            foreach (var prerequisite in prerequisites) if (prerequisite != null) queue.Enqueue(prerequisite);
            var record = Create(point.pointType, point.title);
            record.status = point.status; record.note = point.note; record.location = point.gameObject.scene.name; record.plannedScene = point.gameObject.scene.path;
            record.missionId = point.missionId; record.missionDescription = point.missionDescription; record.objectiveId = point.objectiveId;
            record.objectiveType = point.objectiveType; record.objectiveDescription = point.objectiveDescription; record.requiredCount = point.requiredCount; record.optionalObjective = point.optionalObjective;
            record.relatedItem = point.relatedItem; record.relatedDocument = point.relatedDocument;
            if (EditorUtility.IsPersistent(point.relatedPrefab)) record.relatedPrefab = point.relatedPrefab;
            Undo.RecordObject(point, "Bind Note Data"); point.data = record; EditorUtility.SetDirty(point);
            PrefabUtility.RecordPrefabInstancePropertyModifications(point);
            if (point.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(point.gameObject.scene);
        }
        foreach (var pair in graphs)
        {
            pair.Key.data.objectives = Array.ConvertAll(pair.Value.objectives, p => p != null ? p.data : null);
            pair.Key.data.prerequisiteObjectives = Array.ConvertAll(pair.Value.prerequisites, p => p != null ? p.data : null);
            EditorUtility.SetDirty(pair.Key.data);
        }
        AssetDatabase.SaveAssets(); SceneView.RepaintAll();
    }
    public static void DrawBinding(PointNote point)
    {
        var serialized = new SerializedObject(point); serialized.Update();
        EditorGUILayout.PropertyField(serialized.FindProperty("data"), new GUIContent("Primary Note Data"));
        if (serialized.ApplyModifiedProperties()) { PrefabUtility.RecordPrefabInstancePropertyModifications(point); EditorSceneManager.MarkSceneDirty(point.gameObject.scene); }
        if (point.data == null)
        {
            EditorGUILayout.HelpBox("Choose an existing Note Data asset or create a new record in the game-wide catalog. Existing local notes are preserved until imported.", MessageType.Info);
            EditorGUILayout.LabelField("Existing local note", point.title);
            if (!string.IsNullOrWhiteSpace(point.note)) EditorGUILayout.HelpBox(point.note, MessageType.None);
            if (GUILayout.Button("Create Data from This Note")) Import(point);
        }
        else
        {
            EditorGUILayout.HelpBox("Editing this record updates every scene/prefab point referencing it. Position, volume and scene events remain local.", MessageType.Info);
            DrawData(point.data);
        }
    }
    public static void DrawData(PointNoteData record)
    {
        var serialized = new SerializedObject(record); serialized.Update();
        foreach (var field in new[] { "pointType", "title", "status", "note", "location", "plannedScene" }) EditorGUILayout.PropertyField(serialized.FindProperty(field), true);
        var type = (PointNote.PointType)serialized.FindProperty("pointType").enumValueIndex;
        if (PointNoteEditor.HasMissionDesign(type))
        {
            EditorGUILayout.PropertyField(serialized.FindProperty("missionId"));
            if (type == PointNote.PointType.Mission)
            {
                EditorGUILayout.PropertyField(serialized.FindProperty("missionDescription"), true);
                EditorGUILayout.PropertyField(serialized.FindProperty("objectives"), true);
            }
            else
            {
                foreach (var field in new[] { "objectiveId", "objectiveType", "objectiveDescription", "requiredCount", "optionalObjective" }) EditorGUILayout.PropertyField(serialized.FindProperty(field), true);
                if (type == PointNote.PointType.Objective) EditorGUILayout.PropertyField(serialized.FindProperty("prerequisiteObjectives"), true);
            }
        }
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Related Notes", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serialized.FindProperty("relatedNotes"), new GUIContent("Related Note Data"), true);
        EditorGUILayout.HelpBox("Primary data defines the point's name and color. Related records share this design context without requiring another scene point. These links do not complete objectives or play cutscenes.", MessageType.Info);
        foreach (var field in new[] { "relatedItem", "relatedDocument", "relatedPrefab" })
            EditorGUILayout.PropertyField(serialized.FindProperty(field),
                field == "relatedDocument" ? new GUIContent("Related Document / Evidence") : new GUIContent(ObjectNames.NicifyVariableName(field)), true);
        if (serialized.ApplyModifiedProperties()) { SyncMission(record); PointNoteNameSync.SyncDataAssetName(record); PointNote.EditorChangeRequested?.Invoke(); SceneView.RepaintAll(); }
        DrawValidation(record);
        var relatedUnique = new HashSet<PointNoteData>();
        foreach (var related in record.relatedNotes ?? new PointNoteData[0])
        {
            if (related == null || related == record || !relatedUnique.Add(related))
            {
                EditorGUILayout.HelpBox("Related notes should be distinct records, excluding this record and empty slots.", MessageType.Warning);
                continue;
            }
            if (GUILayout.Button("Open " + related.pointType + ": " + related.title))
            {
                Selection.activeObject = related;
                EditorGUIUtility.PingObject(related);
            }
        }
    }
    public static void SyncMission(PointNoteData record)
    {
        if (record.pointType != PointNote.PointType.Mission || string.IsNullOrWhiteSpace(record.missionId)) return;
        var all = AssetDatabase.FindAssets("t:PointNoteData");
        var objectives = record.objectives ?? new PointNoteData[0];
        for (int i = 0; i < objectives.Length; i++)
        {
            var objective = objectives[i]; if (objective == null || objective.pointType != PointNote.PointType.Objective) continue;
            int owners = 0;
            foreach (string guid in all)
            {
                var other = AssetDatabase.LoadAssetAtPath<PointNoteData>(AssetDatabase.GUIDToAssetPath(guid));
                if (other.pointType == PointNote.PointType.Mission && Array.IndexOf(other.objectives ?? new PointNoteData[0], objective) >= 0) owners++;
            }
            if (owners > 1) continue;
            string id = string.IsNullOrWhiteSpace(objective.objectiveId) ? record.missionId.Trim() + "_OBJ_" + (i + 1).ToString("00") : objective.objectiveId;
            if (objective.missionId == record.missionId.Trim() && objective.objectiveId == id) continue;
            Undo.RecordObject(objective, "Sync Mission Data IDs"); objective.missionId = record.missionId.Trim(); objective.objectiveId = id; EditorUtility.SetDirty(objective);
        }
    }
    public static bool HasCycle(PointNoteData record) => Visit(record, new HashSet<PointNoteData>(), new HashSet<PointNoteData>());
    private static bool Visit(PointNoteData record, HashSet<PointNoteData> active, HashSet<PointNoteData> visited)
    {
        if (record == null || record.pointType != PointNote.PointType.Objective) return false;
        if (active.Contains(record)) return true;
        if (!visited.Add(record)) return false;
        active.Add(record);
        foreach (var prerequisite in record.prerequisiteObjectives ?? new PointNoteData[0]) if (Visit(prerequisite, active, visited)) return true;
        active.Remove(record); return false;
    }
    private static void ValidateObjective(PointNoteData record)
    {
        var unique = new HashSet<PointNoteData>();
        foreach (var prerequisite in record.prerequisiteObjectives ?? new PointNoteData[0])
        {
            if (prerequisite == null || prerequisite == record || prerequisite.pointType != PointNote.PointType.Objective || !unique.Add(prerequisite))
                EditorGUILayout.HelpBox("Prerequisites must reference distinct Objective Data records, excluding this record.", MessageType.Warning);
            else if (prerequisite.missionId != record.missionId) EditorGUILayout.HelpBox("Prerequisite Mission ID differs: " + prerequisite.title, MessageType.Warning);
        }
        if (HasCycle(record)) EditorGUILayout.HelpBox("Dependency cycle detected in data records.", MessageType.Error);
    }
    public static void DrawValidation(PointNoteData record)
    {
        if (record.pointType == PointNote.PointType.Objective) ValidateObjective(record);
        if (record.pointType != PointNote.PointType.Mission) return;
        var unique = new HashSet<PointNoteData>(); var ids = new HashSet<string>(); int valid = 0;
        foreach (var objective in record.objectives ?? new PointNoteData[0])
        {
            if (objective == null || objective.pointType != PointNote.PointType.Objective || !unique.Add(objective))
            { EditorGUILayout.HelpBox("Each mission slot must reference a distinct Objective Data record.", MessageType.Warning); continue; }
            valid++; if (!ids.Add(objective.objectiveId)) EditorGUILayout.HelpBox("Duplicate Objective ID: " + objective.objectiveId, MessageType.Warning);
            if (objective.missionId != record.missionId) EditorGUILayout.HelpBox("Objective Mission ID differs: " + objective.title, MessageType.Warning);
            ValidateObjective(objective);
        }
        EditorGUILayout.LabelField("Assigned objectives", valid + " / " + (record.objectives?.Length ?? 0));
    }
}

[CustomEditor(typeof(PointNoteData))]
public class PointNoteDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying)) PointNoteDataTools.DrawData((PointNoteData)target);
    }
}

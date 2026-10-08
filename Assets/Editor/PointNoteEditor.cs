using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(PointNote))]
public class PointNoteEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            DrawPropertiesExcluding(serializedObject, "m_Script", "missionId", "missionDescription", "objectiveId", "objectiveType", "objectiveDescription", "requiredCount", "optionalObjective", "objectives", "showObjectiveLinks", "linksOnlyWhenSelected", "showLinkLabels", "maxVisibleLinks", "prerequisiteObjectives", "showPrerequisiteLinks");
            var note = (PointNote)target;
            var type = (PointNote.PointType)serializedObject.FindProperty("pointType").enumValueIndex;
            if (HasMissionDesign(type))
                DrawDesignFields(serializedObject, type);
            if (type == PointNote.PointType.Mission)
                DrawMissionFields(serializedObject);
            if (type == PointNote.PointType.Objective)
                DrawPrerequisiteFields(serializedObject);
            if (serializedObject.ApplyModifiedProperties()) ApplyDesignChanges(note);
            DrawValidation(note);
            if (GUILayout.Button(HasMissionDesign(type) ? "Sync hierarchy name and mission IDs" : "Sync hierarchy name")) ApplyDesignChanges(note);
        }
    }

    public static bool HasMissionDesign(PointNote.PointType type) =>
        type == PointNote.PointType.Mission || type == PointNote.PointType.Objective || type == PointNote.PointType.Puzzle;

    public static void DrawDesignFields(SerializedObject data, PointNote.PointType type)
    {
        EditorGUILayout.PropertyField(data.FindProperty("missionId"));
        if (type == PointNote.PointType.Mission)
            EditorGUILayout.PropertyField(data.FindProperty("missionDescription"), true);
        else
            foreach (var field in new[] { "objectiveId", "objectiveType", "objectiveDescription", "requiredCount", "optionalObjective" })
                EditorGUILayout.PropertyField(data.FindProperty(field), true);
    }

    public static void DrawPrerequisiteFields(SerializedObject data)
    {
        EditorGUILayout.PropertyField(data.FindProperty("prerequisiteObjectives"), true);
        foreach (var field in new[] { "showPrerequisiteLinks", "linksOnlyWhenSelected", "showLinkLabels", "maxVisibleLinks" })
            EditorGUILayout.PropertyField(data.FindProperty(field));
        EditorGUILayout.HelpBox("Empty = starting objective. All listed prerequisites must happen first. Solid arrows point from prerequisite to this objective; dotted arrows indicate mission membership. Design metadata only.", MessageType.Info);
    }

    public static bool HasDependencyCycle(PointNote start)
    {
        return Visit(start, new HashSet<PointNote>(), new HashSet<PointNote>());
    }
    private static bool Visit(PointNote current, HashSet<PointNote> active, HashSet<PointNote> visited)
    {
        if (current == null || current.pointType != PointNote.PointType.Objective) return false;
        if (active.Contains(current)) return true;
        if (!visited.Add(current)) return false;
        active.Add(current);
        foreach (var prerequisite in current.prerequisiteObjectives ?? new PointNote[0])
            if (Visit(prerequisite, active, visited)) return true;
        active.Remove(current); return false;
    }
    private static void DrawPrerequisiteValidation(PointNote note)
    {
        var unique = new HashSet<PointNote>();
        foreach (var prerequisite in note.prerequisiteObjectives ?? new PointNote[0])
        {
            if (prerequisite == null || prerequisite == note || prerequisite.pointType != PointNote.PointType.Objective || prerequisite.gameObject.scene != note.gameObject.scene || !unique.Add(prerequisite))
            { EditorGUILayout.HelpBox("Prerequisites must be unique Objective points in the same scene, excluding this point.", MessageType.Warning); continue; }
            if (string.IsNullOrWhiteSpace(note.missionId) || prerequisite.missionId != note.missionId)
                EditorGUILayout.HelpBox("Prerequisite should have the same non-empty Mission ID: " + prerequisite.title, MessageType.Warning);
        }
        if (HasDependencyCycle(note)) EditorGUILayout.HelpBox("Dependency cycle detected. Remove the circular link so the sequence has a starting point.", MessageType.Error);
    }

    public static void DrawMissionFields(SerializedObject data)
    {
        EditorGUILayout.PropertyField(data.FindProperty("objectives"), true);
        foreach (var field in new[] { "showObjectiveLinks", "linksOnlyWhenSelected", "showLinkLabels", "maxVisibleLinks" })
            EditorGUILayout.PropertyField(data.FindProperty(field));
        EditorGUILayout.HelpBox("Assign Objective Point Notes to the slots. Links appear when this mission or one of its objectives is selected. Required Count is the amount within an objective, not the number of objectives.", MessageType.Info);
    }

    private static bool Editable(PointNote note) => note != null && !EditorUtility.IsPersistent(note) && !EditorApplication.isPlaying;
    private static void Mark(PointNote note)
    {
        PrefabUtility.RecordPrefabInstancePropertyModifications(note);
        EditorUtility.SetDirty(note);
        if (note.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(note.gameObject.scene);
    }
    public static void ApplyDesignChanges(PointNote note)
    {
        if (!Editable(note)) return;
        if (note.autoNameInHierarchy)
        {
            string title = string.IsNullOrWhiteSpace(note.title) ? "Point Note" : note.title.Trim();
            string desired = "[" + note.pointType + "] " + title.Replace('\n', ' ').Replace('\r', ' ').Replace('/', '-');
            if (note.gameObject.name != desired)
            {
                Undo.RecordObject(note.gameObject, "Rename Point Note");
                note.gameObject.name = desired;
                PrefabUtility.RecordPrefabInstancePropertyModifications(note.gameObject);
            }
        }
        var missions = Object.FindObjectsByType<PointNote>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var mission in missions)
        {
            if (!Editable(mission) || mission.pointType != PointNote.PointType.Mission || mission.objectives == null) continue;
            if (mission != note && System.Array.IndexOf(mission.objectives, note) < 0) continue;
            for (int i = 0; i < mission.objectives.Length; i++)
            {
                var objective = mission.objectives[i];
                if (!Editable(objective) || objective.pointType != PointNote.PointType.Objective || objective.gameObject.scene != mission.gameObject.scene) continue;
                int owners = 0;
                foreach (var other in missions)
                    if (other.pointType == PointNote.PointType.Mission && other.objectives != null && System.Array.IndexOf(other.objectives, objective) >= 0) owners++;
                if (owners != 1 || string.IsNullOrWhiteSpace(mission.missionId)) continue;
                string id = string.IsNullOrWhiteSpace(objective.objectiveId) ? mission.missionId.Trim() + "_OBJ_" + (i + 1).ToString("00") : objective.objectiveId;
                if (objective.missionId == mission.missionId.Trim() && objective.objectiveId == id) continue;
                Undo.RecordObject(objective, "Link Mission Objective IDs");
                objective.missionId = mission.missionId.Trim(); objective.objectiveId = id; Mark(objective);
            }
        }
        Mark(note); SceneView.RepaintAll();
    }

    public static void DrawValidation(PointNote note)
    {
        if (note.pointType == PointNote.PointType.Objective) DrawPrerequisiteValidation(note);
        if (note.pointType != PointNote.PointType.Mission) return;
        var unique = new HashSet<PointNote>(); var ids = new HashSet<string>();
        int valid = 0;
        if (string.IsNullOrWhiteSpace(note.missionId)) EditorGUILayout.HelpBox("Set a meaningful Mission ID, for example M01_ESCAPE_HOSPITAL.", MessageType.Warning);
        foreach (var objective in note.objectives ?? new PointNote[0])
        {
            if (objective == null || objective.pointType != PointNote.PointType.Objective || objective.gameObject.scene != note.gameObject.scene || !unique.Add(objective))
            { EditorGUILayout.HelpBox("Each slot must reference a unique Objective point in the same scene / Prefab Stage.", MessageType.Warning); continue; }
            valid++;
            DrawPrerequisiteValidation(objective);
            if (!ids.Add(objective.objectiveId)) EditorGUILayout.HelpBox("Duplicate Objective ID: " + objective.objectiveId, MessageType.Warning);
            if (objective.missionId != (note.missionId ?? "").Trim()) EditorGUILayout.HelpBox("Objective Mission ID differs. Check whether it belongs to another mission, then sync IDs.", MessageType.Warning);
        }
        EditorGUILayout.LabelField("Assigned objectives", valid + " / " + (note.objectives?.Length ?? 0));
        EditorGUILayout.HelpBox("Design links only; these do not track gameplay mission completion. Existing Objective IDs are preserved when relinking.", MessageType.Info);
    }
}

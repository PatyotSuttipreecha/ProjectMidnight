using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneNotesWindow : EditorWindow
{
    private class Entry
    {
        public PointNote note;
        public string assetPath, hierarchy, location;
        public bool prefab;
    }
    private readonly List<Entry> entries = new List<Entry>();
    private Entry selected;
    private string search = "", prefabFolder = "Assets/Prefab";
    private int scope, statusFilter, typeFilter;
    private PointNote.PointType createType;
    private Vector2 listScroll, detailScroll;
    private bool sceneDirty = true;
    private bool showColors;
    private Vector2 colorScroll;
    [MenuItem("System Modification/Scene Notes")]
    public static void Open() { var window = GetWindow<SceneNotesWindow>("Scene Notes"); window.minSize = new Vector2(700, 440); }
    private void OnEnable() { EditorApplication.hierarchyChanged += HierarchyChanged; Undo.undoRedoPerformed += HierarchyChanged; Refresh(); }
    private void OnDisable() { EditorApplication.hierarchyChanged -= HierarchyChanged; Undo.undoRedoPerformed -= HierarchyChanged; }
    private void HierarchyChanged() { sceneDirty = true; Repaint(); }
    private static string Path(Transform target)
    {
        string path = target.name;
        while (target.parent != null) { target = target.parent; path = target.name + "/" + path; }
        return path;
    }
    private void Refresh()
    {
        entries.Clear(); selected = null; sceneDirty = false;
        if (scope != 1)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++) AddScene(SceneManager.GetSceneAt(i));
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null) AddScene(stage.scene);
        }
        if (scope != 0)
        {
            string folder = prefabFolder.Trim().TrimEnd('/').Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder)) { ShowNotification(new GUIContent("Choose a valid Assets folder.")); return; }
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null) continue;
                foreach (var note in root.GetComponentsInChildren<PointNote>(true))
                    entries.Add(new Entry { note = note, prefab = true, assetPath = path, location = path, hierarchy = Path(note.transform) });
            }
        }
        entries.Sort((a,b)=>string.Compare(a.location+a.hierarchy,b.location+b.hierarchy,StringComparison.OrdinalIgnoreCase));
    }
    private void AddScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var note in root.GetComponentsInChildren<PointNote>(true))
                entries.Add(new Entry { note = note, assetPath = scene.path, location = string.IsNullOrEmpty(scene.path) ? scene.name + " (unsaved)" : scene.path, hierarchy = Path(note.transform) });
    }
    private void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            int nextScope = EditorGUILayout.Popup(scope, new[] { "Open Scenes", "Prefab Assets", "Scenes + Prefabs" }, GUILayout.Width(160));
            if (nextScope != scope) { scope = nextScope; Refresh(); }
            search = GUILayout.TextField(search, EditorStyles.toolbarSearchField);
            statusFilter = EditorGUILayout.Popup(statusFilter, new[] { "All status", "Info", "Todo", "Issue", "Done" }, GUILayout.Width(100));
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(65))) Refresh();
        }
        if (scope != 0)
        {
            prefabFolder = EditorGUILayout.TextField("Prefab search folder", prefabFolder);
            EditorGUILayout.HelpBox("Folder changes apply on Refresh. Prefab assets include notes on inactive children and nested prefabs.", MessageType.Info);
        }
        if (sceneDirty) EditorGUILayout.HelpBox("Scene hierarchy changed. Refresh to update the list.", MessageType.Info);
        showColors = EditorGUILayout.Foldout(showColors, "Point Type Colors", true);
        if (showColors)
        {
            colorScroll = EditorGUILayout.BeginScrollView(colorScroll, GUILayout.MaxHeight(230));
            PointNotePalette.DrawSettings();
            EditorGUILayout.EndScrollView();
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            var types = new List<string> { "All point types" }; types.AddRange(Enum.GetNames(typeof(PointNote.PointType)));
            typeFilter = EditorGUILayout.Popup("Point type filter", typeFilter, types.ToArray());
            createType = (PointNote.PointType)EditorGUILayout.EnumPopup("New point type", createType);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                if (GUILayout.Button("Add Note to Selected Object"))
                {
                    var go = Selection.activeGameObject;
                    if (go == null || EditorUtility.IsPersistent(go)) ShowNotification(new GUIContent("Select a scene object or open a prefab in Prefab Mode."));
                    else { var note = go.GetComponent<PointNote>(); if (note == null) { note = Undo.AddComponent<PointNote>(go); note.pointType = createType; } PointNoteEditor.ApplyDesignChanges(note); Selection.activeObject = note; EditorSceneManager.MarkSceneDirty(go.scene); Refresh(); }
                }
                if (GUILayout.Button("Create Note Point"))
                {
                    var go = new GameObject(createType + " Point"); Undo.RegisterCreatedObjectUndo(go, "Create Note Point");
                    StageUtility.PlaceGameObjectInCurrentStage(go);
                    var note = go.AddComponent<PointNote>(); note.pointType = createType; note.title = createType + " Point";
                    PointNoteEditor.ApplyDesignChanges(note);
                    if (SceneView.lastActiveSceneView != null) go.transform.position = SceneView.lastActiveSceneView.pivot;
                    Selection.activeGameObject = go; EditorSceneManager.MarkSceneDirty(go.scene); Refresh();
                }
            }
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(position.width * .43f)))
            {
                listScroll = EditorGUILayout.BeginScrollView(listScroll);
                int shown = 0;
                foreach (var entry in entries)
                {
                    if (entry.note == null || statusFilter > 0 && (int)entry.note.status != statusFilter - 1) continue;
                    if (typeFilter > 0 && (int)entry.note.pointType != typeFilter - 1) continue;
                    string content = entry.note.title + " " + entry.note.note + " " + entry.location + " " + entry.hierarchy + " " + entry.note.pointType + " " + entry.note.missionId + " " + entry.note.objectiveId + " " + entry.note.objectiveDescription + " " + (entry.note.relatedItem != null ? entry.note.relatedItem.itemName : "") + " " + (entry.note.relatedDocument != null ? entry.note.relatedDocument.title : "");
                    if (!string.IsNullOrWhiteSpace(search) && content.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    shown++;
                    entry.hierarchy = Path(entry.note.transform);
                    var style = new GUIStyle(EditorStyles.miniButton)
                    {
                        alignment = TextAnchor.UpperLeft,
                        wordWrap = true,
                        fixedHeight = 0,
                        fontSize = 12,
                        padding = new RectOffset(8, 8, 6, 6)
                    };
                    string label = "[" + entry.note.pointType + " | " + entry.note.status + "] " + entry.note.title + "\n" + entry.hierarchy + "\n" + (entry.prefab ? "Prefab: " : "Scene: ") + entry.location;
                    // miniButton has a fixed single-line height; allocate the wrapped content explicitly.
                    float rowWidth = Mathf.Max(120, position.width * .43f - 28);
                    float rowHeight = Mathf.Max(64, style.CalcHeight(new GUIContent(label), rowWidth));
                    if (GUILayout.Toggle(selected == entry, label, style, GUILayout.Height(rowHeight))) selected = entry;
                    GUILayout.Space(3);
                }
                EditorGUILayout.EndScrollView();
                EditorGUILayout.LabelField(shown + " / " + entries.Count + " notes", EditorStyles.miniLabel);
            }
            using (new EditorGUILayout.VerticalScope()) DrawDetails();
        }
    }
    private void DrawDetails()
    {
        if (selected == null || selected.note == null) { EditorGUILayout.HelpBox("Select a note to view its text, location and point information.", MessageType.Info); return; }
        var note = selected.note;
        EditorGUILayout.LabelField(selected.prefab ? "Prefab asset" : "Scene object", EditorStyles.boldLabel);
        EditorGUILayout.SelectableLabel(selected.location, GUILayout.Height(34));
        EditorGUILayout.SelectableLabel(selected.hierarchy, GUILayout.Height(34));
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.Vector3Field(selected.prefab ? "Prefab position" : "World position", note.transform.position);
            EditorGUILayout.Vector3Field("Local position", note.transform.localPosition);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(selected.prefab ? "Open Prefab" : "Select / Frame"))
            {
                if (selected.prefab) AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<GameObject>(selected.assetPath));
                else { Selection.activeGameObject = note.gameObject; SceneView.lastActiveSceneView?.FrameSelected(); }
            }
            if (GUILayout.Button("Ping")) EditorGUIUtility.PingObject(selected.prefab ? AssetDatabase.LoadAssetAtPath<GameObject>(selected.assetPath) : note.gameObject);
        }
        if (selected.prefab) EditorGUILayout.HelpBox("Asset notes are read-only here. Open Prefab to edit safely in Prefab Mode, then Refresh.", MessageType.Info);
        detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
        var data = new SerializedObject(note); data.Update();
        using (new EditorGUI.DisabledScope(selected.prefab || EditorApplication.isPlaying))
        {
            foreach (var field in new[] { "pointType", "title", "status", "note" })
                EditorGUILayout.PropertyField(data.FindProperty(field), true);
            EditorGUILayout.PropertyField(data.FindProperty("autoNameInHierarchy"));
            var type = (PointNote.PointType)data.FindProperty("pointType").enumValueIndex;
            if (PointNoteEditor.HasMissionDesign(type))
            {
                EditorGUILayout.LabelField("Mission / Objective Design", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Authoring metadata only. Status tracks design work, not mission progress in gameplay.", MessageType.Info);
                foreach (var field in new[] { "missionId", "objectiveId", "objectiveType", "objectiveDescription", "requiredCount", "optionalObjective" })
                    EditorGUILayout.PropertyField(data.FindProperty(field), true);
                if (type == PointNote.PointType.Mission) PointNoteEditor.DrawMissionFields(data);
                if (type == PointNote.PointType.Objective) PointNoteEditor.DrawPrerequisiteFields(data);
            }
            EditorGUILayout.LabelField("Related Content", EditorStyles.boldLabel);
            foreach (var field in new[] { "relatedItem", "relatedDocument", "relatedPrefab", "relatedObject", "showGizmos", "showLabel", "markerSize", "labelSize", "labelOffset", "overrideColors" })
                EditorGUILayout.PropertyField(data.FindProperty(field), true);
            if (data.FindProperty("overrideColors").boolValue)
                foreach (var field in new[] { "markerColor", "labelBackground", "labelTextColor" }) EditorGUILayout.PropertyField(data.FindProperty(field));
            if (data.ApplyModifiedProperties())
            {
                PointNoteEditor.ApplyDesignChanges(note);
                selected.hierarchy = Path(note.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(note);
                EditorSceneManager.MarkSceneDirty(note.gameObject.scene);
            }
            PointNoteEditor.DrawValidation(note);
            if (GUILayout.Button(PointNoteEditor.HasMissionDesign(type) ? "Sync hierarchy name and mission IDs" : "Sync hierarchy name")) PointNoteEditor.ApplyDesignChanges(note);
        }
        EditorGUILayout.EndScrollView();
    }
}

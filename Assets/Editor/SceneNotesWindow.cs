using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneNotesWindow : EditorWindow
{
    private readonly List<PointNoteData> records = new List<PointNoteData>();
    private PointNoteData selected;
    private string search = "";
    private string locationFilter = "";
    private int statusFilter, typeFilter;
    private PointNote.PointType createType;
    private Vector2 listScroll, detailScroll, colorsScroll;
    private bool showColors;
    [MenuItem("System Modification/Scene Configuration/Scene Notes")]
    public static void Open() { var window = GetWindow<SceneNotesWindow>("Scene Notes"); window.minSize = new Vector2(700, 440); }
    private void OnEnable() { EditorApplication.projectChanged += Refresh; Undo.undoRedoPerformed += Refresh; Refresh(); }
    private void OnDisable() { EditorApplication.projectChanged -= Refresh; Undo.undoRedoPerformed -= Refresh; }
    private void Refresh()
    {
        records.Clear();
        foreach (var guid in AssetDatabase.FindAssets("t:PointNoteData"))
        {
            var record = AssetDatabase.LoadAssetAtPath<PointNoteData>(AssetDatabase.GUIDToAssetPath(guid));
            if (record != null) records.Add(record);
        }
        Repaint();
    }
    private void Sort()
    {
        records.Sort((a,b) =>
        {
            string ma = (a.missionId ?? "").Trim(), mb = (b.missionId ?? "").Trim();
            if ((ma.Length == 0) != (mb.Length == 0)) return ma.Length == 0 ? 1 : -1;
            int result = EditorUtility.NaturalCompare(ma, mb); if (result != 0) return result;
            if ((a.pointType == PointNote.PointType.Mission) != (b.pointType == PointNote.PointType.Mission)) return a.pointType == PointNote.PointType.Mission ? -1 : 1;
            result = EditorUtility.NaturalCompare(a.objectiveId ?? "", b.objectiveId ?? "");
            return result != 0 ? result : EditorUtility.NaturalCompare(a.title ?? "", b.title ?? "");
        });
    }
    private void OnGUI()
    {
        using (new ModificationGUI.WindowScope(this, "Scene Configuration", "Scene notes", "Browse game-wide design records and their placements in loaded scenes.", ModificationGUI.Scene, selected))
            DrawContent();
    }
    private void DrawContent()
    {
        records.RemoveAll(r => r == null); Sort();
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label("All Game Data", GUILayout.Width(105));
            search = GUILayout.TextField(search, EditorStyles.toolbarSearchField);
            statusFilter = EditorGUILayout.Popup(statusFilter, new[] { "All status", "Info", "Todo", "Issue", "Done" }, GUILayout.Width(100));
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(65))) Refresh();
            if (GUILayout.Button(PointNoteVisibility.Visible ? "Hide Notes" : "Show Notes", EditorStyles.toolbarButton, GUILayout.Width(85))) PointNoteVisibility.Toggle();
        }
        var types = new List<string> { "All point types" }; types.AddRange(Enum.GetNames(typeof(PointNote.PointType)));
        using (new EditorGUILayout.HorizontalScope())
        {
            typeFilter = EditorGUILayout.Popup("Point type filter", typeFilter, types.ToArray());
            createType = (PointNote.PointType)EditorGUILayout.EnumPopup("New data type", createType);
        }
        var locations = new List<string> { "All locations" };
        foreach (var record in records) if (!string.IsNullOrWhiteSpace(record.location) && !locations.Contains(record.location)) locations.Add(record.location);
        int locationIndex = string.IsNullOrEmpty(locationFilter) ? 0 : Mathf.Max(0, locations.IndexOf(locationFilter));
        locationIndex = EditorGUILayout.Popup("Location filter", locationIndex, locations.ToArray());
        locationFilter = locationIndex == 0 ? "" : locations[locationIndex];
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Create New Note Data")) { selected = PointNoteDataTools.Create(createType, createType + " Note"); AssetDatabase.SaveAssets(); Refresh(); }
            if (GUILayout.Button("Import Notes from Open Scenes")) { PointNoteDataTools.ImportMany(UnityEngine.Object.FindObjectsByType<PointNote>(FindObjectsInactive.Include, FindObjectsSortMode.None)); Refresh(); }
            if (GUILayout.Button("Import All Saved Scenes / Prefabs")) { PointNoteDataMigration.ImportAll(); Refresh(); }
        }
        showColors = EditorGUILayout.Foldout(showColors, "Point Type Colors", true);
        if (showColors) { colorsScroll = EditorGUILayout.BeginScrollView(colorsScroll, GUILayout.MaxHeight(200)); PointNotePalette.DrawSettings(); EditorGUILayout.EndScrollView(); }
        EditorGUILayout.HelpBox("Game-wide Note Data catalog, including records not placed in a scene. Scene PointNotes reference these assets; positions, trigger volumes and UnityEvents remain local.", MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(position.width * .40f)))
            {
                listScroll = EditorGUILayout.BeginScrollView(listScroll); int shown = 0;
                foreach (var record in records)
                {
                    if (typeFilter > 0 && (int)record.pointType != typeFilter - 1 || statusFilter > 0 && (int)record.status != statusFilter - 1) continue;
                    if (locationFilter.Length > 0 && record.location != locationFilter) continue;
                    string content = record.title + " " + record.note + " " + record.location + " " + record.plannedScene + " " + record.missionId + " " + record.objectiveId + " " + record.missionDescription + " " + record.objectiveDescription;
                    if (!string.IsNullOrWhiteSpace(search) && content.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    shown++;
                    string label = (record.title ?? "").Replace('\n',' ').Replace('\r',' ') + "\n" + record.pointType + " | " + record.status;
                    if (ModificationGUI.ListRow(selected == record, label, content + "\n" + AssetDatabase.GetAssetPath(record), ModificationGUI.Scene)) selected = record;
                }
                EditorGUILayout.EndScrollView(); EditorGUILayout.LabelField(shown + " / " + records.Count + " data records", EditorStyles.miniLabel);
            }
            using (new EditorGUILayout.VerticalScope())
            {
                detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
                if (selected == null) EditorGUILayout.HelpBox("Select a data record, or import existing scene notes to populate this catalog.", MessageType.Info);
                else
                {
                    EditorGUILayout.SelectableLabel(AssetDatabase.GetAssetPath(selected), GUILayout.Height(20));
                    using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                    {
                        PointNoteDataTools.DrawData(selected);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button("Place Note Point")) Place(false);
                            if (selected.pointType == PointNote.PointType.Trigger && GUILayout.Button("Place Runtime Trigger")) Place(true);
                            if (GUILayout.Button("Ping Data")) EditorGUIUtility.PingObject(selected);
                        }
                        using (new EditorGUI.DisabledScope(Selection.activeGameObject == null || EditorUtility.IsPersistent(Selection.activeGameObject)))
                            if (GUILayout.Button("Bind Data to Selected GameObject"))
                            {
                                var go = Selection.activeGameObject;
                                var point = go.GetComponent<PointNote>(); if (point == null) point = Undo.AddComponent<PointNote>(go);
                                Undo.RecordObject(point, "Assign Note Data"); point.data = selected;
                                PrefabUtility.RecordPrefabInstancePropertyModifications(point); PointNoteEditor.ApplyDesignChanges(point); EditorSceneManager.MarkSceneDirty(go.scene);
                            }
                    }
                    EditorGUILayout.Space(); EditorGUILayout.LabelField("Placements in Loaded Scenes / Prefab Stage", EditorStyles.boldLabel);
                    int count = 0;
                    foreach (var point in UnityEngine.Object.FindObjectsByType<PointNote>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        if (point.data != selected) continue; count++;
                        if (GUILayout.Button(point.gameObject.scene.name + " / " + point.name)) { Selection.activeGameObject = point.gameObject; SceneView.lastActiveSceneView?.FrameSelected(); }
                    }
                    if (count == 0) EditorGUILayout.HelpBox("No placement in currently loaded scenes. Data exists independently of placement.", MessageType.Info);
                }
                EditorGUILayout.EndScrollView();
            }
        }
    }
    private void Place(bool runtimeTrigger)
    {
        var go = new GameObject("[" + selected.pointType + "] " + selected.title); Undo.RegisterCreatedObjectUndo(go, "Place Note Data");
        StageUtility.PlaceGameObjectInCurrentStage(go);
        if (SceneView.lastActiveSceneView != null) go.transform.position = SceneView.lastActiveSceneView.pivot;
        var point = Undo.AddComponent<PointNote>(go); point.data = selected;
        if (runtimeTrigger) TriggerNoteLink.Create(point);
        EditorSceneManager.MarkSceneDirty(go.scene); Selection.activeGameObject = go;
    }
}

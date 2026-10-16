#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class DecorationLibraryWindow : EditorWindow
{
    [SerializeField] private DecorationLibrary library;
    [SerializeField] private ProceduralDecorationPlacer placer;
    [SerializeField] private DecorationPlacementData placementData;
    [SerializeField] private string search = "", category = "All";
    [SerializeField] private string batchCategory = "";
    [SerializeField] private bool favoritesOnly, manage, onlyInData;
    private readonly HashSet<string> selected = new HashSet<string>();
    private readonly Dictionary<string, GameObject> loaded = new Dictionary<string, GameObject>();
    private List<DecorationLibrary.Entry> rows = new List<DecorationLibrary.Entry>();
    private string activeGuid, status;
    private Vector2 scroll;
    private DecorationLibraryPreviewCache meshPreviews = new DecorationLibraryPreviewCache();

    [MenuItem("System Modification/TerrainTools/Decorations Library", false, 33)]
    public static void Open() => OpenLibrary(null);
    public static void OpenData(DecorationPlacementData data)
    {
        OpenLibrary(null);
        var window = GetWindow<DecorationLibraryWindow>();
        window.placementData = data;
        window.search = ""; window.category = "All"; window.favoritesOnly = false;
        window.selected.Clear(); window.Repaint();
        Selection.activeObject = data;
    }
    public static void OpenLibrary(DecorationLibrary asset, ProceduralDecorationPlacer target = null)
    {
        var window = GetWindow<DecorationLibraryWindow>("Decorations Library", true, typeof(SceneView)); window.minSize = new Vector2(520, 540);
        if (asset) window.library = asset;
        if (!window.library) window.library = AssetDatabase.LoadAssetAtPath<DecorationLibrary>(DecorationLibraryUtility.DefaultPath);
        if (DecorationLibraryUtility.IsScenePlacer(target)) window.placer = target;
        window.RefreshRows(); window.Show();
    }
    private void OnEnable()
    {
        if (!library) library = AssetDatabase.LoadAssetAtPath<DecorationLibrary>(DecorationLibraryUtility.DefaultPath);
        if (!placer)
        {
            if (Selection.activeGameObject) placer = Selection.activeGameObject.GetComponent<ProceduralDecorationPlacer>();
            if (!placer)
            {
                var candidates = Object.FindObjectsByType<ProceduralDecorationPlacer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(DecorationLibraryUtility.IsScenePlacer).ToArray();
                if (candidates.Length == 1) placer = candidates[0];
            }
        }
        Undo.undoRedoPerformed += RefreshRows; EditorApplication.projectChanged += RefreshRows; RefreshRows();
    }
    private void OnDisable() { Undo.undoRedoPerformed -= RefreshRows; EditorApplication.projectChanged -= RefreshRows; meshPreviews.Dispose(); }
    private void RefreshRows()
    {
        loaded.Clear(); rows = library ? library.entries.Where(e => e != null).ToList() : new List<DecorationLibrary.Entry>();
        meshPreviews.Dispose();
        selected.RemoveWhere(guid => !rows.Any(e => e.guid == guid));
        if (category != "All" && !rows.Any(e => e.category == category)) category = "All";
        Repaint();
    }
    private void OnGUI()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("DECORATIONS LIBRARY", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Search • Pick • Add to Placer", EditorStyles.miniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                var chosen = (DecorationLibrary)EditorGUILayout.ObjectField("Library Asset", library, typeof(DecorationLibrary), false);
                if (chosen != library) { library = chosen; selected.Clear(); RefreshRows(); }
                if (GUILayout.Button("New…", GUILayout.Width(64))) CreateLibrary();
                using (new EditorGUI.DisabledScope(!library))
                    if (GUILayout.Button("Save", GUILayout.Width(50))) AssetDatabase.SaveAssetIfDirty(library);
            }
        }
        if (!library) { EditorGUILayout.HelpBox("Create a Library Asset, then add prefab assets or folders. No source asset is duplicated or changed.", MessageType.Info); return; }
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            search = GUILayout.TextField(search, EditorStyles.toolbarSearchField);
            var categories = new[] { "All" }.Concat(rows.Select(e => string.IsNullOrWhiteSpace(e.category) ? "Uncategorized" : e.category).Distinct().OrderBy(c => c)).ToArray();
            int index = Mathf.Max(0, Array.IndexOf(categories, category));
            category = categories[EditorGUILayout.Popup(index, categories, EditorStyles.toolbarPopup, GUILayout.Width(135))];
            favoritesOnly = GUILayout.Toggle(favoritesOnly, "Favorites", EditorStyles.toolbarButton, GUILayout.Width(70));
            onlyInData = GUILayout.Toggle(onlyInData, "In Data", EditorStyles.toolbarButton, GUILayout.Width(60));
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60))) RefreshRows();
        }
        var dataGuids = placementData ? new HashSet<string>(placementData.entries.Where(e => e != null && e.prefab)
            .Select(e => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(e.prefab)))) : new HashSet<string>();
        var shown = rows.Where(e => (!onlyInData || dataGuids.Contains(e.guid)) && (!favoritesOnly || e.favorite) &&
            (category == "All" || (string.IsNullOrWhiteSpace(e.category) ? "Uncategorized" : e.category) == category) && DecorationLibraryUtility.Matches(e, search)).ToList();
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField($"{shown.Count}/{rows.Count} entries • {selected.Count} selected", EditorStyles.miniLabel);
            if (GUILayout.Button("Select results", GUILayout.Width(100))) foreach (var entry in shown) selected.Add(entry.guid);
            if (GUILayout.Button("Clear selection", GUILayout.Width(100))) selected.Clear();
        }
        DrawGrid(shown);
        DrawDetails();
        DrawTarget();
        manage = EditorGUILayout.Foldout(manage, "Manage library / Add assets", true);
        if (manage) DrawManagement();
        if (!string.IsNullOrEmpty(status)) EditorGUILayout.LabelField(status, EditorStyles.wordWrappedMiniLabel);
        if (AssetPreview.IsLoadingAssetPreviews()) Repaint();
    }
    private GameObject Load(string guid)
    {
        if (!loaded.TryGetValue(guid ?? "", out var prefab)) loaded[guid ?? ""] = prefab = DecorationLibraryUtility.Resolve(guid);
        return prefab;
    }
    private void DrawGrid(List<DecorationLibrary.Entry> entries)
    {
        Rect viewport = GUILayoutUtility.GetRect(100, 10000, 150, 10000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        int columns = Mathf.Max(1, Mathf.FloorToInt((viewport.width - 16) / 160));
        float width = (viewport.width - 16) / columns, height = 165;
        int rowCount = Mathf.CeilToInt((float)entries.Count / columns);
        scroll = GUI.BeginScrollView(viewport, scroll, new Rect(0, 0, viewport.width - 16, rowCount * height));
        int first = Mathf.Max(0, Mathf.FloorToInt(scroll.y / height) * columns);
        int end = Mathf.Min(entries.Count, Mathf.CeilToInt((scroll.y + viewport.height) / height) * columns);
        for (int i = first; i < end; i++)
        {
            var entry = entries[i]; var rect = new Rect((i % columns) * width + 3, (i / columns) * height + 3, width - 6, height - 6);
            GUI.Box(rect, GUIContent.none, selected.Contains(entry.guid) ? "selectionRect" : EditorStyles.helpBox);
            bool ticked = GUI.Toggle(new Rect(rect.x + 5, rect.y + 5, 20, 20), selected.Contains(entry.guid), GUIContent.none);
            if (ticked != selected.Contains(entry.guid)) { if (ticked) selected.Add(entry.guid); else selected.Remove(entry.guid); activeGuid = entry.guid; }
            if (GUI.Button(new Rect(rect.xMax - 26, rect.y + 3, 23, 20), new GUIContent(entry.favorite ? "★" : "☆", "Favorite"), EditorStyles.miniButton))
            { Undo.RecordObject(library, "Favorite Decoration"); entry.favorite = !entry.favorite; EditorUtility.SetDirty(library); }
            var prefab = Load(entry.guid);
            var preview = prefab ? AssetPreview.GetAssetPreview(prefab) ?? meshPreviews.Get(prefab) ?? AssetPreview.GetMiniThumbnail(prefab) : null;
            var picture = new Rect(rect.x + 25, rect.y + 23, rect.width - 50, 80);
            if (preview) GUI.DrawTexture(picture, preview, ScaleMode.ScaleToFit);
            else GUI.Label(picture, prefab ? "Loading preview…" : "Missing asset", EditorStyles.centeredGreyMiniLabel);
            string path = AssetDatabase.GUIDToAssetPath(entry.guid ?? "");
            string label = string.IsNullOrWhiteSpace(entry.label) ? Path.GetFileNameWithoutExtension(path) : entry.label;
            GUI.Label(new Rect(rect.x + 6, rect.y + 106, rect.width - 12, 34), new GUIContent(label, path + "\n" + entry.tags), EditorStyles.wordWrappedMiniLabel);
            GUI.Label(new Rect(rect.x + 6, rect.yMax - 20, rect.width - 12, 18), entry.category, EditorStyles.centeredGreyMiniLabel);
            var evt = Event.current;
            if (evt.type == EventType.MouseDown && evt.button == 0 && rect.Contains(evt.mousePosition))
            {
                bool additive = evt.control || evt.command;
                if (!additive && !selected.Contains(entry.guid)) selected.Clear();
                if (additive && selected.Contains(entry.guid)) selected.Remove(entry.guid); else selected.Add(entry.guid);
                activeGuid = entry.guid;
                if (evt.clickCount == 2 && prefab) EditorGUIUtility.PingObject(prefab);
                evt.Use(); Repaint();
            }
            if (evt.type == EventType.MouseDrag && rect.Contains(evt.mousePosition) && prefab)
            {
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = (selected.Contains(entry.guid) ? selected.Select(Load) : new[] { prefab }).Where(p => p).Cast<Object>().ToArray();
                DragAndDrop.StartDrag("Library decorations"); evt.Use();
            }
        }
        GUI.EndScrollView();
        if (entries.Count == 0) EditorGUI.HelpBox(viewport, "No matches. Clear filters or add prefab assets/folders under Manage library.", MessageType.Info);
    }
    private void DrawDetails()
    {
        if (selected.Count > 1)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"Edit Category — {selected.Count} selected", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Applies to all checked Library entries, including selections hidden by filters. Data SO categories are edited separately.", EditorStyles.wordWrappedMiniLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    batchCategory = EditorGUILayout.TextField("Category", batchCategory);
                    if (GUILayout.Button("Existing…", GUILayout.Width(80)))
                    {
                        var menu = new GenericMenu();
                        foreach (var name in rows.Select(e => e.category).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().OrderBy(c => c))
                        {
                            string choice = name;
                            menu.AddItem(new GUIContent(choice.Replace("/", "∕")), batchCategory == choice, () => { batchCategory = choice; Repaint(); });
                        }
                        menu.ShowAsContext();
                    }
                    using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(batchCategory)))
                        if (GUILayout.Button("Apply to Selected", GUILayout.Width(125)))
                        {
                            int count = DecorationLibraryUtility.SetCategory(library, selected, batchCategory);
                            status = $"Updated Category on {count} Library entries. Undo restores categories; Save Library to persist.";
                            // Keep the changed selection visible after moving it out of the active category.
                            category = "All";
                            Repaint();
                        }
                }
            }
            return;
        }
        var entry = rows.FirstOrDefault(e => e.guid == activeGuid);
        if (entry == null) return;
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(entry.guid)), EditorStyles.boldLabel);
                if (GUILayout.Button("Ping", GUILayout.Width(45))) EditorGUIUtility.PingObject(Load(entry.guid));
            }
            EditorGUI.BeginChangeCheck();
            string label = EditorGUILayout.TextField("Display label", entry.label);
            string group = EditorGUILayout.TextField("Category", entry.category);
            string tags = EditorGUILayout.TextField("Search tags", entry.tags);
            if (EditorGUI.EndChangeCheck())
            { Undo.RecordObject(library, "Edit Library Metadata"); entry.label = label; entry.category = group; entry.tags = tags; EditorUtility.SetDirty(library); }
            EditorGUILayout.SelectableLabel(AssetDatabase.GUIDToAssetPath(entry.guid), EditorStyles.miniLabel, GUILayout.Height(16));
        }
    }
    private void DrawTarget()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            placementData = (DecorationPlacementData)EditorGUILayout.ObjectField("Target Data (SO)", placementData, typeof(DecorationPlacementData), false);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("New Placement Data…"))
                {
                    string path = EditorUtility.SaveFilePanelInProject("Create Placement Data", "Decoration_New", "asset", "Choose a reusable prefab set.",
                        "Assets/Script/TerrainTools/PlacementData");
                    if (!string.IsNullOrEmpty(path))
                    { placementData = CreateInstance<DecorationPlacementData>(); AssetDatabase.CreateAsset(placementData, path); }
                }
                using (new EditorGUI.DisabledScope(!placementData))
                {
                    if (GUILayout.Button("Edit Data")) Selection.activeObject = placementData;
                    if (GUILayout.Button("Save Data")) AssetDatabase.SaveAssetIfDirty(placementData);
                    using (new EditorGUI.DisabledScope(selected.Count == 0))
                        if (GUILayout.Button("Remove selected from Data"))
                        {
                            Undo.RecordObject(placementData, "Remove Decorations from Placement Data");
                            int count = placementData.entries.RemoveAll(e => e != null && e.prefab && selected.Contains(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(e.prefab))));
                            EditorUtility.SetDirty(placementData); status = $"Removed {count} references from Data. Undo restores them.";
                        }
                }
            }
            using (new EditorGUI.DisabledScope(!placementData || selected.Count == 0))
                if (GUILayout.Button($"Add {selected.Count} selected to Data Asset", GUILayout.Height(28)))
                {
                    int count = DecorationPlacementDataEditor.Add(placementData, selected.Select(Load));
                    status = $"Added {count} to {placementData.name}. Duplicates skipped. Save Data to persist.";
                }
            var chosen = (ProceduralDecorationPlacer)EditorGUILayout.ObjectField("Target Placer", placer, typeof(ProceduralDecorationPlacer), true);
            if (chosen != placer && (!chosen || DecorationLibraryUtility.IsScenePlacer(chosen))) placer = chosen;
            bool ready = DecorationLibraryUtility.IsScenePlacer(placer) && !EditorApplication.isPlayingOrWillChangePlaymode && PrefabStageUtility.GetCurrentPrefabStage() == null;
            if (!ready) EditorGUILayout.HelpBox("Choose a scene Placer in Edit Mode to receive the selected prefabs.", MessageType.None);
            using (new EditorGUI.DisabledScope(!ready || selected.Count == 0))
                if (GUILayout.Button($"Add {selected.Count} selected to Placer", GUILayout.Height(28)))
                {
                    int count = placer.placementData ? DecorationPlacementDataEditor.Add(placer.placementData, selected.Select(Load)) : DecorationLibraryUtility.AddToPlacer(placer, selected);
                    status = placer.placementData ? $"Added {count} to shared Data {placer.placementData.name}. Save Data to persist." : $"Added {count}; duplicates/missing assets skipped. Existing weights and generated scene objects kept.";
                }
            using (new EditorGUI.DisabledScope(!ready || !placementData))
                if (GUILayout.Button("Use Data Asset on Target Placer"))
                {
                    Undo.RecordObject(placer, "Assign Decoration Placement Data"); placer.placementData = placementData;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(placer); EditorUtility.SetDirty(placer);
                    EditorSceneManager.MarkSceneDirty(placer.gameObject.scene);
                    status = "Assigned Data. Build Preview / Generate from the Placer. Existing scene objects kept.";
                }
        }
    }
    private void DrawManagement()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            var drop = GUILayoutUtility.GetRect(0, 38, GUILayout.ExpandWidth(true)); GUI.Box(drop, "Drop prefab assets or Project folders here", EditorStyles.helpBox);
            var evt = Event.current;
            if (drop.Contains(evt.mousePosition) && (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (evt.type == EventType.DragPerform) { DragAndDrop.AcceptDrag(); AddAssets(DragAndDrop.objectReferences); }
                evt.Use();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Selected Assets / Folders")) AddAssets(Selection.objects);
                using (new EditorGUI.DisabledScope(selected.Count == 0))
                    if (GUILayout.Button("Remove from Library"))
                    {
                        Undo.RecordObject(library, "Remove Library Entries"); int count = library.entries.RemoveAll(e => e != null && selected.Contains(e.guid));
                        EditorUtility.SetDirty(library); status = $"Removed {count} references. Source prefabs kept. Undo restores entries."; RefreshRows();
                    }
            }
            var so = new SerializedObject(library); so.Update(); EditorGUILayout.PropertyField(so.FindProperty("sourceFolders"), true); so.ApplyModifiedProperties();
            if (GUILayout.Button("Scan Source Folders (add new assets)")) AddAssets(library.sourceFolders.Cast<Object>().ToArray());
        }
    }
    private void AddAssets(Object[] assets)
    {
        try
        {
            int count = DecorationLibraryUtility.AddAssets(library, assets, progress => EditorUtility.DisplayCancelableProgressBar("Decorations Library", "Collecting prefab/model references", progress));
            status = $"Added {count} entries. Save Library to persist metadata changes."; RefreshRows();
        }
        finally { EditorUtility.ClearProgressBar(); }
    }
    private void CreateLibrary()
    {
        string path = EditorUtility.SaveFilePanelInProject("Create Decorations Library", "Decorations Library", "asset", "Choose a location for this catalog.", "Assets/Script/TerrainTools/Editor/Libraries");
        if (string.IsNullOrEmpty(path)) return;
        library = CreateInstance<DecorationLibrary>(); AssetDatabase.CreateAsset(library, path); selected.Clear(); RefreshRows();
    }
}
#endif

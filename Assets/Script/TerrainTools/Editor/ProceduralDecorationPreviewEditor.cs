#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed partial class ProceduralDecorationPlacerEditor
{
    private ProceduralDecorationPlacer.PlacementPlan preview;
    private bool previewStale, drawPreview = true, drawRejected, drawBounds;
    private int tab;
    private bool advanced;
    private string previewSettings;
    private readonly Dictionary<int, (Object obj, int stamp)> sceneDependencies = new Dictionary<int, (Object, int)>();
    private int obstacleComponentCount;

    private void EnablePlanningPreview()
    {
        SceneView.duringSceneGui += DrawPlannedPreview;
        EditorApplication.hierarchyChanged += SceneHierarchyChanged;
        EditorApplication.projectChanged += InvalidatePreview;
        ObjectChangeEvents.changesPublished += ScenePropertiesChanged;
        TerrainCallbacks.heightmapChanged += HeightChanged;
        TerrainCallbacks.textureChanged += PaintChanged;
    }
    private void DisablePlanningPreview()
    {
        SceneView.duringSceneGui -= DrawPlannedPreview;
        EditorApplication.hierarchyChanged -= SceneHierarchyChanged;
        EditorApplication.projectChanged -= InvalidatePreview;
        ObjectChangeEvents.changesPublished -= ScenePropertiesChanged;
        TerrainCallbacks.heightmapChanged -= HeightChanged;
        TerrainCallbacks.textureChanged -= PaintChanged;
    }
    private void HeightChanged(Terrain terrain, RectInt region, bool synced)
    { if (target && ((ProceduralDecorationPlacer)target).terrain == terrain) InvalidatePreview(); }
    private void PaintChanged(Terrain terrain, string texture, RectInt region, bool synced)
    { if (target && ((ProceduralDecorationPlacer)target).terrain == terrain) InvalidatePreview(); }
    private void InvalidatePreview()
    { if (preview != null) previewStale = true; Repaint(); SceneView.RepaintAll(); }
    private void ScenePropertiesChanged(ref ObjectChangeEventStream changes)
    {
        if (preview == null || previewStale || !target) return;
        for (int i = 0; i < changes.length; i++)
            if (changes.GetEventType(i) == ObjectChangeKind.ChangeGameObjectOrComponentProperties)
            {
                changes.GetChangeGameObjectOrComponentPropertiesEvent(i, out var change);
                if (sceneDependencies.TryGetValue(change.instanceId, out var previous) &&
                    (!previous.obj || DependencyStamp(previous.obj) != previous.stamp)) { InvalidatePreview(); return; }
            }
    }
    private static List<Component> ObstacleComponents()
    {
        var components = new List<Component>();
        components.AddRange(Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        components.AddRange(Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        return components;
    }
    private static int DependencyStamp(Object obj)
    {
        if (obj is Transform transform) return transform.localToWorldMatrix.GetHashCode();
        if (obj is GameObject go) return go.layer * 31 + (go.activeInHierarchy ? 1 : 0);
        if (obj is Renderer renderer) return renderer.bounds.GetHashCode() ^ (renderer.enabled ? 17 : 0);
        if (obj is Collider collider) return collider.bounds.GetHashCode() ^ (collider.enabled ? 17 : 0) ^ (collider.isTrigger ? 31 : 0);
        return 0;
    }
    private void CaptureDependencies(ProceduralDecorationPlacer placer)
    {
        sceneDependencies.Clear();
        var components = ObstacleComponents(); obstacleComponentCount = components.Count;
        if (placer.terrain) components.Add(placer.terrain);
        foreach (var component in components)
        {
            sceneDependencies[component.GetInstanceID()] = (component, DependencyStamp(component));
            for (var t = component.transform; t; t = t.parent)
            {
                sceneDependencies[t.GetInstanceID()] = (t, DependencyStamp(t));
                sceneDependencies[t.gameObject.GetInstanceID()] = (t.gameObject, DependencyStamp(t.gameObject));
            }
        }
    }
    private void SceneHierarchyChanged()
    {
        if (preview == null || previewStale) return;
        var components = ObstacleComponents();
        if (components.Count != obstacleComponentCount || components.Exists(c => !sceneDependencies.ContainsKey(c.GetInstanceID())))
        { InvalidatePreview(); return; }
        foreach (var previous in sceneDependencies.Values)
            if (!previous.obj || DependencyStamp(previous.obj) != previous.stamp) { InvalidatePreview(); return; }
    }
    private string SettingsKey(ProceduralDecorationPlacer placer) => EditorJsonUtility.ToJson(placer) +
        (placer.placementData ? EditorJsonUtility.ToJson(placer.placementData) : "") + placer.transform.localToWorldMatrix.ToString("R");
    private void CheckPreview(ProceduralDecorationPlacer placer)
    { if (preview != null && previewSettings != SettingsKey(placer)) previewStale = true; }

    public override void OnInspectorGUI()
    {
        var placer = (ProceduralDecorationPlacer)target;
        serializedObject.Update();
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("DECORATION STUDIO", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Set up → Preview → Generate", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"{placer.amount} requested  •  {(placer.useGroups ? "Grouped" : "Scatter")}  •  {(placer.useLayerEdge ? "Layer edge" : "Whole area")}", EditorStyles.wordWrappedLabel);
        }
        tab = GUILayout.Toolbar(tab, new[] { "Assets / Area", "Distribution", "Terrain / Avoid", "Preview" });
        EditorGUILayout.Space(6);
        switch (tab)
        {
            case 0:
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                { Fields("terrain", "rockContainer"); DrawAreaControls(placer); }
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) DrawPrefabs();
                break;
            case 1: DrawDistribution(placer); break;
            case 2: DrawTerrainFilters(placer); break;
            case 3:
                EditorGUILayout.HelpBox("Preview calculates positions, prefab choices, rotation and scale without creating GameObjects. Cyan/group colors = accepted, yellow = painted-layer contour. Rejected markers show the first 500 trials. Generated output replaces the previous output in Decoration Container.", MessageType.Info);
                drawPreview = EditorGUILayout.Toggle("Show planned positions", drawPreview);
                drawBounds = EditorGUILayout.Toggle("Show geometry bounds", drawBounds);
                drawRejected = EditorGUILayout.Toggle("Show rejected samples", drawRejected);
                Fields("showGizmo", "gizmoAlpha", "gizmoColor");
                SceneView.RepaintAll();
                break;
        }
        if (serializedObject.ApplyModifiedProperties()) { Save(placer); InvalidatePreview(); }
        CheckPreview(placer);
        EditorGUILayout.Space(6);
        DrawPlanActions(placer);
        advanced = EditorGUILayout.Foldout(advanced, "Terrain following / Advanced", true);
        if (advanced)
        {
            var decorations = new List<Transform>();
            if (placer.rockContainer) foreach (Transform child in placer.rockContainer) decorations.Add(child);
            DecorationTerrainFollowService.DrawControls(placer.gameObject, decorations, placer.terrain);
        }
    }

    private void Fields(params string[] names)
    {
        foreach (var name in names)
        {
            string label = name == "rockContainer" ? "Decoration Container" : name == "amount" ? "Total Decorations" :
                name == "randomizeSeedOnGenerate" ? "New Seed Each Preview" : name == "useRendererGrounding" ? "Renderer Grounding" : ObjectNames.NicifyVariableName(name);
            EditorGUILayout.PropertyField(serializedObject.FindProperty(name), new GUIContent(label), true);
        }
    }

    private void DrawDistribution(ProceduralDecorationPlacer placer)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Population", EditorStyles.boldLabel);
            Fields("amount", "minDistance", "useGroups");
            if (serializedObject.FindProperty("useGroups").boolValue)
            {
                Fields("minGroupSize", "maxGroupSize", "groupMinDistance", "groupMaxDistance");
                EditorGUILayout.HelpBox("Amount is the total number of decorations. Within each group, every pair respects Group Min / Max Distance (world metres, horizontal). Min Distance separates members of different groups. Final or constrained groups may be smaller. Groups keep prefab roots intact; group numbers appear in generated names.", MessageType.None);
            }
        }
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Appearance", EditorStyles.boldLabel);
            Fields("randomRotation", "alignToTerrain", "useRendererGrounding", "groundOffset", "randomSize");
            EditorGUILayout.HelpBox("Renderer Grounding lifts/lowers the whole prefab root after rotation and scale. It samples renderer bounds over Terrain so centered/offset pivots do not bury the model. Bounds are conservative; wide models may leave gaps on uneven ground. Ground Offset adds clearance or intentional sinking.", MessageType.None);
            if (serializedObject.FindProperty("randomSize").boolValue)
            {
                Fields("uniformScale");
                if (serializedObject.FindProperty("uniformScale").boolValue) Fields("minUniformScale", "maxUniformScale");
                else Fields("minScale", "maxScale");
            }
        }
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Randomness / Search budget", EditorStyles.boldLabel);
            Fields("seed", "randomizeSeedOnGenerate", "attemptsPerDecoration");
            if (GUILayout.Button("New Seed"))
            {
                serializedObject.ApplyModifiedProperties();
                Undo.RecordObject(placer, "New Decoration Seed"); placer.RandomizeSeed(); Save(placer);
                serializedObject.Update(); InvalidatePreview();
            }
            EditorGUILayout.HelpBox("Build Preview chooses a new seed when Randomize Seed is enabled. Generate This Preview reuses that seed and all planned transforms.", MessageType.None);
        }
    }

    private void DrawTerrainFilters(ProceduralDecorationPlacer placer)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Follow a painted path / layer boundary", EditorStyles.boldLabel);
            Fields("useLayerEdge");
            if (serializedObject.FindProperty("useLayerEdge").boolValue)
            {
                Fields("edgeLayer", "edgeThreshold", "edgeSide", "edgeMinDistance", "edgeMaxDistance");
                EditorGUILayout.HelpBox("Select the layer used to paint your path. Threshold defines its boundary. Outside avoids the path; Inside stays on it; Both uses either side. Distances are from each object's pivot, in world metres. Edge precision follows alphamap resolution (approximately one texel). Existing layer exclusions still apply. This reads painted layers, not spline/mesh roads.", MessageType.Info);
            }
        }
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Ground filters", EditorStyles.boldLabel);
            Fields("maxSlope", "useHeightLimit");
            if (serializedObject.FindProperty("useHeightLimit").boolValue) Fields("minHeight", "maxHeight");
            Fields("useTerrainLayerFilter", "excludeLayerThreshold");
            var rules = serializedObject.FindProperty("terrainLayerRules");
            using (new EditorGUI.DisabledScope(!serializedObject.FindProperty("useTerrainLayerFilter").boolValue))
                for (int i = 0; i < rules.arraySize; i++)
                {
                    var rule = rules.GetArrayElementAtIndex(i);
                    var layer = rule.FindPropertyRelative("terrainLayer").objectReferenceValue;
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("excluded"), new GUIContent("Exclude " + (layer ? layer.name : "Missing layer")));
                }
            if (GUILayout.Button("Sync Terrain Layers"))
            {
                serializedObject.ApplyModifiedProperties(); Undo.RecordObject(placer, "Sync Terrain Layers");
                placer.SyncTerrainLayers(); Save(placer); serializedObject.Update(); InvalidatePreview();
            }
        }
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Avoid existing scene objects", EditorStyles.boldLabel);
            Fields("detectSceneObjects");
            if (serializedObject.FindProperty("detectSceneObjects").boolValue)
                Fields("sceneDetectionMode", "sceneObjectLayers", "sceneObjectCheckRadius", "sceneObjectPadding", "ignoreTriggers", "ignoreTerrain");
        }
    }

    private void BuildPreview(ProceduralDecorationPlacer placer)
    {
        Undo.RecordObject(placer, "Build Decoration Preview");
        try
        {
            var result = placer.BuildPlan(placer.randomizeSeedOnGenerate,
                progress => EditorUtility.DisplayCancelableProgressBar("Planning decorations", "Terrain, groups, boundaries and obstacles", progress));
            if (result.cancelled) { InvalidatePreview(); return; }
            preview = result; previewStale = false;
            Save(placer); previewSettings = SettingsKey(placer);
            CaptureDependencies(placer);
            drawPreview = true;
        }
        finally { EditorUtility.ClearProgressBar(); SceneView.RepaintAll(); }
    }

    private void DrawPlanActions(ProceduralDecorationPlacer placer)
    {
        if (preview != null)
        {
            EditorGUILayout.HelpBox(previewStale ? "Preview is out of date. Rebuild after changing settings, Terrain or scene objects." : preview.report,
                previewStale || !string.IsNullOrEmpty(preview.error) || preview.decorations.Count == 0 ? MessageType.Warning : MessageType.Info);
            if (!previewStale)
            {
                var counts = new Dictionary<GameObject, int>();
                foreach (var entry in preview.decorations) { counts.TryGetValue(entry.prefab, out int count); counts[entry.prefab] = count + 1; }
                foreach (var pair in counts) EditorGUILayout.LabelField(pair.Key.name, pair.Value.ToString(), EditorStyles.miniLabel);
            }
        }
        else if (!string.IsNullOrEmpty(placer.lastGenerationReport)) EditorGUILayout.HelpBox(placer.lastGenerationReport, MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorUtility.IsPersistent(placer) || PrefabStageUtility.GetCurrentPrefabStage() != null))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(preview == null ? "Build Preview" : "Rebuild Preview", GUILayout.Height(32))) BuildPreview(placer);
                using (new EditorGUI.DisabledScope(preview == null || previewStale || !string.IsNullOrEmpty(preview.error) || preview.decorations.Count == 0))
                    if (GUILayout.Button("Generate This Preview", GUILayout.Height(32))) { Run(placer, false, preview); preview = null; }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Hide / Clear Preview")) { preview = null; SceneView.RepaintAll(); }
                if (GUILayout.Button("Clear Generated…") && EditorUtility.DisplayDialog("Clear Generated Decorations", "Delete the generated container's contents? Undo restores them.", "Clear", "Cancel"))
                { Run(placer, true); preview = null; }
            }
            using (new EditorGUI.DisabledScope(!placer.terrain || !placer.rockContainer || placer.rockContainer.childCount == 0))
                if (GUILayout.Button(new GUIContent("Ground Existing Decorations (Renderer)", "Adjust generated prefab roots vertically without rerolling their positions, rotation or scale. Undo restores them.")))
                {
                    Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("Ground Existing Decorations");
                    int moved = placer.GroundExistingDecorations(out int skipped);
                    if (moved > 0)
                    {
                        var follower = placer.GetComponent<DecorationTerrainFollower>();
                        if (follower && follower.autoFollow)
                        {
                            var roots = new List<Transform>();
                            foreach (Transform root in placer.rockContainer) roots.Add(root);
                            DecorationTerrainFollowService.Bind(follower, roots, placer.terrain);
                        }
                        EditorSceneManager.MarkSceneDirty(placer.gameObject.scene);
                    }
                    Undo.CollapseUndoOperations(group); InvalidatePreview();
                    Debug.Log($"Renderer grounding: {moved} roots adjusted, {skipped} skipped (missing geometry / Terrain coverage). Child transforms unchanged. Undo with Ctrl+Z.", placer);
                }
        }
    }

    private void DrawPlannedPreview(SceneView view)
    {
        if (!target || preview == null || !drawPreview || EditorApplication.isPlayingOrWillChangePlaymode || Event.current.type != EventType.Repaint) return;
        var placer = (ProceduralDecorationPlacer)target;
        CheckPreview(placer);
        if (previewStale) return;
        using (new Handles.DrawingScope(Color.cyan))
        {
            int shown = 0;
            foreach (var entry in preview.decorations)
            {
                if (++shown > 5000) break;
                Handles.color = placer.useGroups ? Color.HSVToRGB(Mathf.Repeat(entry.group * .618034f, 1), .65f, 1) : Color.cyan;
                float radius = HandleUtility.GetHandleSize(entry.position) * .07f;
                Handles.DrawWireDisc(entry.position, entry.rotation * Vector3.up, radius);
                Handles.DrawLine(entry.position, entry.position + entry.rotation * Vector3.up * radius * 3);
                if (drawBounds && shown <= 1000) Handles.DrawWireCube(entry.worldBounds.center, entry.worldBounds.size);
            }
            Handles.color = Color.yellow;
            foreach (var point in preview.edgePoints) Handles.DrawLine(point, point + Vector3.up * HandleUtility.GetHandleSize(point) * .04f);
            if (drawRejected)
            {
                Handles.color = new Color(1, .3f, .2f, .65f);
                foreach (var point in preview.rejected) Handles.DrawWireDisc(point, Vector3.up, HandleUtility.GetHandleSize(point) * .025f);
            }
        }
        Handles.BeginGUI();
        GUI.Label(new Rect(12, 12, 400, 42), $"Decoration Preview: {preview.decorations.Count}/{preview.requested}\nMarkers: up to 5000 • Bounds: up to 1000 • No scene objects created", EditorStyles.helpBox);
        Handles.EndGUI();
    }
}
#endif

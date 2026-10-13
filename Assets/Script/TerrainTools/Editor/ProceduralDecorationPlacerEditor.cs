#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

[CustomEditor(typeof(ProceduralDecorationPlacer))]
public sealed class ProceduralDecorationPlacerEditor : Editor
{
    private static ProceduralDecorationPlacerEditor areaEditor;
    private readonly BoxBoundsHandle areaHandle = new BoxBoundsHandle();
    private bool moveArea;

    private void OnEnable()
    {
        SceneView.duringSceneGui += DrawAreaHandles;
        Undo.undoRedoPerformed += RefreshArea;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DrawAreaHandles;
        Undo.undoRedoPerformed -= RefreshArea;
        if (areaEditor == this) areaEditor = null;
        SceneView.RepaintAll();
    }

    private void RefreshArea() { Repaint(); SceneView.RepaintAll(); }

    private void DrawAreaControls(ProceduralDecorationPlacer placer)
    {
        EditorGUILayout.LabelField("Generation Area", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorUtility.IsPersistent(placer)))
        {
            bool editing = areaEditor == this;
            bool next = GUILayout.Toggle(editing, editing ? "Finish Editing Area (Esc)" : "Edit Generation Area", "Button");
            if (next != editing)
            {
                if (areaEditor) areaEditor.Repaint();
                areaEditor = next ? this : null;
                RefreshArea();
            }
            if (next)
            {
                moveArea = GUILayout.Toolbar(moveArea ? 1 : 0, new[] { "Resize Box", "Move Center" }) == 1;
                EditorGUILayout.HelpBox("Drag a face handle to resize the box, like Edit Collider. Move Center moves only the generation area. Esc finishes editing; Ctrl+Z undoes changes.", MessageType.Info);
            }
            if (GUILayout.Button("Frame Generation Area") && SceneView.lastActiveSceneView)
            {
                var bounds = new Bounds(placer.transform.TransformPoint(placer.areaCenter), Vector3.zero);
                for (int i = 0; i < 8; i++)
                    bounds.Encapsulate(placer.transform.TransformPoint(placer.areaCenter + Vector3.Scale(placer.areaSize * .5f,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
                SceneView.lastActiveSceneView.Frame(bounds, false);
            }
        }
        EditorGUILayout.PropertyField(serializedObject.FindProperty("areaCenter"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("areaSize"));
    }

    private void DrawAreaHandles(SceneView sceneView)
    {
        if (areaEditor != this || !target || EditorApplication.isPlayingOrWillChangePlaymode) return;
        var placer = (ProceduralDecorationPlacer)target;
        if (EditorUtility.IsPersistent(placer) || !placer.gameObject.scene.isLoaded) return;
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
        { areaEditor = null; Event.current.Use(); RefreshArea(); return; }
        if (Mathf.Abs(placer.transform.localToWorldMatrix.determinant) < 0.000001f) return;
        var center = placer.areaCenter;
        var size = placer.areaSize;
        EditorGUI.BeginChangeCheck();
        using (new Handles.DrawingScope(new Color(.3f, 1f, .5f), placer.transform.localToWorldMatrix))
        {
            if (!moveArea)
            {
                areaHandle.center = center;
                areaHandle.size = size;
                areaHandle.DrawHandle();
                center = areaHandle.center;
                size = areaHandle.size;
            }
            else Handles.DrawWireCube(center, size);
        }
        if (moveArea)
        {
            var position = Handles.PositionHandle(placer.transform.TransformPoint(center), placer.transform.rotation);
            center = placer.transform.InverseTransformPoint(position);
        }
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(placer, "Edit Decoration Generation Area");
            placer.areaCenter = center;
            placer.areaSize = Vector3.Max(size, Vector3.one * .01f);
            Save(placer);
            EditorSceneManager.MarkSceneDirty(placer.gameObject.scene);
            RefreshArea();
        }
    }

    public override void OnInspectorGUI()
    {
        var placer = (ProceduralDecorationPlacer)target;
        serializedObject.Update();
        EditorGUILayout.LabelField("Procedural Decoration Placer", EditorStyles.boldLabel);
        if (!string.IsNullOrEmpty(placer.lastGenerationReport))
            EditorGUILayout.HelpBox(placer.lastGenerationReport, MessageType.Info);
        DrawAreaControls(placer);
        DrawPrefabs();
        DrawPropertiesExcluding(serializedObject, "m_Script", "rockPrefabs", "rockPrefabPercentages", "terrainLayerRules", "rockContainer", "areaCenter", "areaSize");
        EditorGUILayout.PropertyField(serializedObject.FindProperty("rockContainer"), new GUIContent("Decoration Container"));
        var rules = serializedObject.FindProperty("terrainLayerRules");
        EditorGUILayout.LabelField("Terrain Layers (checked = excluded)", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(!placer.useTerrainLayerFilter))
        {
            for (int i = 0; i < rules.arraySize; i++)
            {
                var rule = rules.GetArrayElementAtIndex(i);
                var layer = rule.FindPropertyRelative("terrainLayer").objectReferenceValue;
                EditorGUILayout.PropertyField(rule.FindPropertyRelative("excluded"), new GUIContent(layer != null ? layer.name : "Missing layer"));
            }
        }
        serializedObject.ApplyModifiedProperties();
        var decorations = new List<Transform>();
        if (placer.rockContainer)
            foreach (Transform child in placer.rockContainer) decorations.Add(child);
        DecorationTerrainFollowService.DrawControls(placer.gameObject, decorations, placer.terrain);
        if (GUILayout.Button("Sync Terrain Layers"))
        {
            Undo.RecordObject(placer, "Sync Terrain Layers");
            placer.SyncTerrainLayers();
            Save(placer);
        }
        EditorGUILayout.HelpBox("The entire decoration bounds must fit inside Generation Area, including Y. Prefab weights are relative chances; actual counts vary.", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorUtility.IsPersistent(placer) || PrefabStageUtility.GetCurrentPrefabStage() != null))
        {
            if (GUILayout.Button("Randomize Seed"))
            {
                Undo.RecordObject(placer, "Randomize Decoration Seed");
                placer.RandomizeSeed();
                Save(placer);
            }
            if (GUILayout.Button("Generate Decorations", GUILayout.Height(36)))
                Run(placer, false);
            if (GUILayout.Button("Clear Generated Decorations"))
            {
                if (EditorUtility.DisplayDialog("Clear Generated Decorations", "Delete the objects inside the generated container? This can be undone.", "Clear", "Cancel"))
                    Run(placer, true);
            }
        }
    }

    private static void Save(ProceduralDecorationPlacer placer)
    {
        PrefabUtility.RecordPrefabInstancePropertyModifications(placer);
        EditorUtility.SetDirty(placer);
    }

    private static void Run(ProceduralDecorationPlacer placer, bool clear)
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(clear ? "Clear Decorations" : "Generate Decorations");
        Undo.RecordObject(placer, clear ? "Clear Decorations" : "Generate Decorations");
        if (clear) placer.ClearRocks(); else placer.Generate();
        var follower = placer.GetComponent<DecorationTerrainFollower>();
        if (!clear && follower && follower.autoFollow && placer.rockContainer)
        {
            var decorations = new List<Transform>();
            foreach (Transform child in placer.rockContainer) decorations.Add(child);
            DecorationTerrainFollowService.Bind(follower, decorations, placer.terrain);
        }
        Save(placer);
        EditorSceneManager.MarkSceneDirty(placer.gameObject.scene);
        Undo.CollapseUndoOperations(group);
    }

    private void DrawPrefabs()
    {
        var prefabs = serializedObject.FindProperty("rockPrefabs");
        var weights = serializedObject.FindProperty("rockPrefabPercentages");
        int previous = weights.arraySize;
        weights.arraySize = prefabs.arraySize;
        for (int i = previous; i < weights.arraySize; i++) weights.GetArrayElementAtIndex(i).floatValue = 100f;
        EditorGUILayout.LabelField("Decoration Prefabs", EditorStyles.boldLabel);
        var drop = GUILayoutUtility.GetRect(0, 52, GUILayout.ExpandWidth(true));
        GUI.Box(drop, "Drop prefabs / models / folders here\nDuplicates are skipped; scene instances use their prefab asset.", EditorStyles.helpBox);
        var evt = Event.current;
        if (drop.Contains(evt.mousePosition) && (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform))
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                AddAssets(prefabs, weights, DragAndDrop.objectReferences);
            }
            evt.Use();
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Selected")) AddAssets(prefabs, weights, Selection.objects);
            if (GUILayout.Button("Add Slot")) Append(prefabs, weights, null);
            if (GUILayout.Button("Remove Empty"))
                for (int i = prefabs.arraySize - 1; i >= 0; i--)
                    if (prefabs.GetArrayElementAtIndex(i).objectReferenceValue == null) Remove(prefabs, weights, i);
        }
        using (new EditorGUI.DisabledScope(prefabs.arraySize == 0))
        {
            if (GUILayout.Button(new GUIContent("Remove All Prefabs", "Clear this prefab list and its weights. Generated scene objects are kept. Undo restores the list.")))
            {
                Undo.SetCurrentGroupName("Remove All Decoration Prefabs");
                prefabs.ClearArray();
                weights.ClearArray();
            }
        }
        float total = 0;
        for (int i = 0; i < prefabs.arraySize; i++)
            if (prefabs.GetArrayElementAtIndex(i).objectReferenceValue != null) total += Mathf.Max(0, weights.GetArrayElementAtIndex(i).floatValue);
        int remove = -1;
        for (int i = 0; i < prefabs.arraySize; i++)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var entry = prefabs.GetArrayElementAtIndex(i);
                    entry.objectReferenceValue = EditorGUILayout.ObjectField(entry.objectReferenceValue, typeof(GameObject), false);
                    if (GUILayout.Button("Remove", GUILayout.Width(65))) remove = i;
                }
                EditorGUILayout.Slider(weights.GetArrayElementAtIndex(i), 0, 100, new GUIContent("Weight"));
                float chance = total > 0 && prefabs.GetArrayElementAtIndex(i).objectReferenceValue != null ? weights.GetArrayElementAtIndex(i).floatValue / total * 100 : 0;
                EditorGUILayout.LabelField("Chance", chance.ToString("0.#") + "%");
            }
        }
        if (remove >= 0) Remove(prefabs, weights, remove);
        if (total <= 0) EditorGUILayout.HelpBox("Add a prefab with Weight greater than zero before generating.", MessageType.Warning);
    }

    private static void Remove(SerializedProperty prefabs, SerializedProperty weights, int index)
    {
        prefabs.GetArrayElementAtIndex(index).objectReferenceValue = null;
        prefabs.DeleteArrayElementAtIndex(index);
        weights.DeleteArrayElementAtIndex(index);
    }

    private static void Append(SerializedProperty prefabs, SerializedProperty weights, GameObject prefab)
    {
        int index = prefabs.arraySize;
        prefabs.arraySize++;
        weights.arraySize++;
        prefabs.GetArrayElementAtIndex(index).objectReferenceValue = prefab;
        weights.GetArrayElementAtIndex(index).floatValue = 100;
    }

    private static void AddAssets(SerializedProperty prefabs, SerializedProperty weights, Object[] objects)
    {
        var candidates = new List<GameObject>();
        foreach (var obj in objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (AssetDatabase.IsValidFolder(path))
            {
                var ids = AssetDatabase.FindAssets("t:GameObject", new[] { path });
                System.Array.Sort(ids, System.StringComparer.Ordinal);
                foreach (var id in ids) candidates.Add(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(id)));
            }
            else if (obj is GameObject go)
            {
                if (!EditorUtility.IsPersistent(go)) go = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (go != null) candidates.Add(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GetAssetPath(go)));
            }
        }
        var seen = new HashSet<Object>();
        for (int i = 0; i < prefabs.arraySize; i++) seen.Add(prefabs.GetArrayElementAtIndex(i).objectReferenceValue);
        foreach (var candidate in candidates)
            if (candidate != null && PrefabUtility.IsPartOfPrefabAsset(candidate) && seen.Add(candidate)) Append(prefabs, weights, candidate);
    }
}
#endif

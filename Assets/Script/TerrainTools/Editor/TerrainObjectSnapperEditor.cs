#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(TerrainObjectSnapper))]
public sealed class TerrainObjectSnapperEditor : Editor
{
    [SerializeField] private bool followSelection = true;
    [SerializeField] private bool showPreview = true;
    private readonly System.Collections.Generic.List<Vector3> previewSamples = new System.Collections.Generic.List<Vector3>();

    private void OnEnable()
    {
        Selection.selectionChanged += RefreshPreview;
        SceneView.duringSceneGui += DrawPreview;
    }
    private void OnDisable()
    {
        Selection.selectionChanged -= RefreshPreview;
        SceneView.duringSceneGui -= DrawPreview;
        SceneView.RepaintAll();
    }
    private void RefreshPreview() { Repaint(); SceneView.RepaintAll(); }

    private void DrawPreview(SceneView sceneView)
    {
        if (!showPreview || target == null || Event.current.type != EventType.Repaint ||
            EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null) return;
        var snapper = (TerrainObjectSnapper)target;
        var roots = new System.Collections.Generic.List<TerrainObjectSnapper.Target>();
        foreach (var go in Selection.gameObjects)
            if (go != snapper.gameObject && !EditorUtility.IsPersistent(go) && go.scene.IsValid() &&
                go.scene.isLoaded && !EditorSceneManager.IsPreviewScene(go.scene))
                roots.Add(new TerrainObjectSnapper.Target { root = go.transform });
        var candidates = followSelection ? snapper.CollectTargets(roots) : snapper.CollectTargets();
        var oldColor = Handles.color;
        try
        {
            int drawn = 0;
            foreach (var item in candidates)
            {
                if (item == null || EditorUtility.IsPersistent(item) || !item.gameObject.scene.isLoaded) continue;
                if (++drawn > 200) break;
                if (!snapper.TryGetPlacement(item, out var position, out var bounds, previewSamples))
                {
                    Handles.color = Color.red;
                    Handles.DrawWireDisc(item.position, Vector3.up, HandleUtility.GetHandleSize(item.position) * .1f);
                    continue;
                }
                Handles.color = Color.cyan;
                Handles.DrawLine(item.position, position);
                Handles.DrawWireCube(bounds.center + position - item.position, bounds.size);
                Handles.DrawWireDisc(position, Vector3.up, HandleUtility.GetHandleSize(position) * .06f);
                Handles.color = Color.yellow;
                foreach (var sample in previewSamples)
                    Handles.DrawWireDisc(sample, Vector3.up, HandleUtility.GetHandleSize(sample) * .025f);
            }
        }
        finally { Handles.color = oldColor; }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("terrain"));
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Objects / Parents", EditorStyles.boldLabel);
        followSelection = EditorGUILayout.ToggleLeft("Follow Scene Selection", followSelection);
        var snapper = (TerrainObjectSnapper)target;
        var selectedTargets = new System.Collections.Generic.List<TerrainObjectSnapper.Target>();
        if (followSelection)
        {
            EditorGUILayout.HelpBox("Targets follow your current selection. Deselect to remove them. Objects only move when you click Snap. Your manual list is preserved while this mode is on.", MessageType.Info);
            foreach (var go in Selection.gameObjects)
            {
                if (go == snapper.gameObject || EditorUtility.IsPersistent(go) ||
                    !go.scene.IsValid() || !go.scene.isLoaded || EditorSceneManager.IsPreviewScene(go.scene)) continue;
                selectedTargets.Add(new TerrainObjectSnapper.Target { root = go.transform });
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField(go, typeof(GameObject), true);
            }
        }
        else
        {
            var entries = serializedObject.FindProperty("targets");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Selected Objects"))
                    foreach (var go in Selection.gameObjects)
                        if (go != ((TerrainObjectSnapper)target).gameObject) AddObject(entries, go);
                using (new EditorGUI.DisabledScope(entries.arraySize == 0))
                    if (GUILayout.Button("Clear List", GUILayout.Width(75))) entries.ClearArray();
            }
            var added = EditorGUILayout.ObjectField("Add Object", null, typeof(GameObject), true) as GameObject;
            if (added != null) AddObject(entries, added);

            var dropArea = GUILayoutUtility.GetRect(0, 58, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "Drag GameObjects or Parents here\nMultiple objects supported", EditorStyles.helpBox);
            var current = Event.current;
            if (dropArea.Contains(current.mousePosition) &&
                (current.type == EventType.DragUpdated || current.type == EventType.DragPerform))
            {
                bool valid = false;
                foreach (var reference in DragAndDrop.objectReferences)
                {
                    var go = reference as GameObject;
                    if (go == null && reference is Component component) go = component.gameObject;
                    if (go != null && !EditorUtility.IsPersistent(go) && go.scene.IsValid()) valid = true;
                }
                DragAndDrop.visualMode = valid ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                if (valid && current.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (var reference in DragAndDrop.objectReferences)
                    {
                        var go = reference as GameObject;
                        if (go == null && reference is Component component) go = component.gameObject;
                        if (go != null) AddObject(entries, go);
                    }
                }
                current.Use();
            }
            for (int i = 0; i < entries.arraySize; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(entries.GetArrayElementAtIndex(i).FindPropertyRelative("root"), GUIContent.none);
                if (GUILayout.Button("Remove", GUILayout.Width(65)))
                {
                    entries.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
        }
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Placement Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("selectionMode"), new GUIContent("Object Mode"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("heightMode"));
        using (new EditorGUI.DisabledScope(snapper.heightMode == TerrainObjectSnapper.HeightMode.Pivot))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("groundSampling"));
        showPreview = EditorGUILayout.ToggleLeft("Show Scene Preview (up to 200 objects)", showPreview);
        EditorGUILayout.HelpBox("Highest Of Nine keeps the bounds bottom above all nine sampled ground points; the lower side may float. Average Of Nine balances gaps but may sink. Samples use world-space bounds, not the mesh shape. Pivot uses one point. Cyan = proposed bounds, yellow = ground samples, red = skipped. Preview never moves objects.", MessageType.Info);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("heightOffset"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("includeInactive"));
        EditorGUILayout.HelpBox("Auto traverses nested folders and moves each prefab/model as a whole. Whole Objects moves each supplied root together. Only Y changes. Ctrl+Z undoes placement.", MessageType.Info);
        if (serializedObject.ApplyModifiedProperties() || GUI.changed) SceneView.RepaintAll();
        var candidates = followSelection ? snapper.CollectTargets(selectedTargets) : snapper.CollectTargets();
        int eligible = 0;
        foreach (var candidate in candidates)
            if (!EditorUtility.IsPersistent(candidate) && candidate.gameObject.scene.IsValid() && snapper.TryGetPosition(candidate, out _)) eligible++;
        EditorGUILayout.LabelField($"Detected: {candidates.Count} objects | On Terrain: {eligible}");
        if (candidates.Count == 0)
            EditorGUILayout.HelpBox(followSelection ? "Select objects in the Scene or Hierarchy to snap." : "No target objects. Drag scene objects from Hierarchy into Add Object or the drop area above.", MessageType.Warning);
        else if (eligible < candidates.Count)
            EditorGUILayout.HelpBox("Some objects lack Terrain coverage at one or more samples, contain Terrain, or have no usable Renderer/Collider for the chosen mode. Leave Terrain empty for multiple tiles, or use Pivot for objects without geometry.", MessageType.Warning);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode ||
            PrefabStageUtility.GetCurrentPrefabStage() != null || eligible == 0))
        {
            if (GUILayout.Button("Snap Objects to Terrain")) Snap(snapper, candidates);
        }
    }

    private static void AddObject(SerializedProperty entries, GameObject go)
    {
        if (EditorUtility.IsPersistent(go) || !go.scene.IsValid()) return;
        for (int i = 0; i < entries.arraySize; i++)
            if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("root").objectReferenceValue == go.transform) return;
        int index = entries.arraySize;
        entries.InsertArrayElementAtIndex(index);
        entries.GetArrayElementAtIndex(index).FindPropertyRelative("root").objectReferenceValue = go.transform;
    }

    private static void Snap(TerrainObjectSnapper snapper, System.Collections.Generic.List<Transform> targets)
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Snap Objects to Terrain");
        int moved = 0, skipped = 0;
        foreach (var item in targets)
        {
            if (EditorUtility.IsPersistent(item) || !item.gameObject.scene.IsValid() ||
                !item.gameObject.scene.isLoaded || !snapper.TryGetPosition(item, out var position))
            {
                skipped++;
                continue;
            }
            if ((item.position - position).sqrMagnitude < 0.00000001f) continue;
            Undo.RecordObject(item, "Snap Object to Terrain");
            item.position = position;
            PrefabUtility.RecordPrefabInstancePropertyModifications(item);
            EditorSceneManager.MarkSceneDirty(item.gameObject.scene);
            moved++;
        }
        Undo.CollapseUndoOperations(group);
        Debug.Log($"Terrain snap: {moved} moved, {skipped} skipped, {targets.Count - moved - skipped} already placed. Skipped objects may be outside Terrain bounds, contain a Terrain, or be project assets. Undo with Ctrl+Z.", snapper);
        SceneView.RepaintAll();
    }
}
#endif

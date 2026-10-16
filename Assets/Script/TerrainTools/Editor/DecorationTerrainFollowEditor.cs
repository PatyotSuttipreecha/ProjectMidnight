#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class DecorationTerrainFollowService
{
    private sealed class Change
    {
        public RectInt region;
        public double due;
    }
    private static readonly Dictionary<Terrain, Change> pending = new Dictionary<Terrain, Change>();
    private static bool applying;
    private static bool resync;
    static DecorationTerrainFollowService()
    {
        TerrainCallbacks.heightmapChanged += Changed;
        EditorApplication.update += Update;
        Undo.undoRedoPerformed += () => { pending.Clear(); resync = true; };
        EditorApplication.playModeStateChanged += _ => { pending.Clear(); resync = false; };
    }
    public static bool SceneObject(Component obj) => obj && !EditorUtility.IsPersistent(obj) &&
        obj.gameObject.scene.IsValid() && obj.gameObject.scene.isLoaded && !EditorSceneManager.IsPreviewScene(obj.gameObject.scene);
    private static bool CanRun => !EditorApplication.isPlayingOrWillChangePlaymode && PrefabStageUtility.GetCurrentPrefabStage() == null;
    private static void Changed(Terrain terrain, RectInt region, bool synched)
    {
        if (applying || !CanRun || !SceneObject(terrain)) return;
        if (!pending.TryGetValue(terrain, out var change))
            pending.Add(terrain, change = new Change { region = region });
        else
            change.region = new RectInt(Mathf.Min(change.region.xMin, region.xMin), Mathf.Min(change.region.yMin, region.yMin),
                Mathf.Max(change.region.xMax, region.xMax) - Mathf.Min(change.region.xMin, region.xMin),
                Mathf.Max(change.region.yMax, region.yMax) - Mathf.Min(change.region.yMin, region.yMin));
        change.due = EditorApplication.timeSinceStartup + .25;
    }
    private static void Update()
    {
        if (!CanRun || applying) return;
        if (resync)
        {
            resync = false;
            foreach (var follower in Object.FindObjectsByType<DecorationTerrainFollower>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (SceneObject(follower) && follower.isActiveAndEnabled && follower.autoFollow) Apply(follower, null, null, false);
        }
        if (pending.Count == 0) return;
        var ready = new List<Terrain>();
        foreach (var pair in pending) if (!pair.Key || pair.Value.due <= EditorApplication.timeSinceStartup) ready.Add(pair.Key);
        foreach (var terrain in ready)
        {
            var change = pending[terrain]; pending.Remove(terrain);
            if (!terrain) continue;
            applying = true;
            try
            {
                terrain.terrainData.SyncHeightmap();
                foreach (var follower in Object.FindObjectsByType<DecorationTerrainFollower>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (SceneObject(follower) && follower.isActiveAndEnabled && follower.autoFollow)
                        Apply(follower, terrain, change.region, false);
                // Following is derived from the saved binding. Terrain Undo/Redo resamples it,
                // avoiding extra Undo steps for every delayed brush update.
            }
            finally { applying = false; }
        }
    }
    public static void Apply(DecorationTerrainFollower follower, Terrain terrain, RectInt? region, bool recordUndo)
    {
        foreach (var binding in follower.bindings)
        {
            if (!SceneObject(binding.target) || !binding.terrain || (terrain && binding.terrain != terrain)) continue;
            // Old child bindings must not keep moving visual geometry independently of its bake root.
            if (PlacementRoot(binding.target, follower.splitNestedPrefabs) != binding.target) continue;
            if (region.HasValue)
            {
                var local = binding.target.position - binding.terrain.transform.position;
                var data = binding.terrain.terrainData;
                float x = local.x / data.size.x * (data.heightmapResolution - 1);
                float z = local.z / data.size.z * (data.heightmapResolution - 1);
                var rect = region.Value;
                // Include neighbors used by bilinear height sampling and terrain normals.
                if (x < rect.xMin - 2 || x > rect.xMax + 2 || z < rect.yMin - 2 || z > rect.yMax + 2) continue;
            }
            if (!follower.TryGetPlacement(binding, out var position, out var rotation)) continue;
            if ((position - binding.target.position).sqrMagnitude < 0.00000001f && Quaternion.Angle(rotation, binding.target.rotation) < .001f) continue;
            if (recordUndo) Undo.RecordObject(binding.target, "Follow Terrain");
            binding.target.SetPositionAndRotation(position, rotation);
            PrefabUtility.RecordPrefabInstancePropertyModifications(binding.target);
            EditorSceneManager.MarkSceneDirty(binding.target.gameObject.scene);
        }
        SceneView.RepaintAll();
    }
    public static int Bind(DecorationTerrainFollower follower, IEnumerable<Transform> targets, Terrain preferred = null)
    {
        if (!CanRun || !SceneObject(follower)) return 0;
        Undo.RecordObject(follower, "Bind Decorations to Terrain");
        follower.bindings.RemoveAll(b => !b.target);
        var candidates = new List<Transform>();
        foreach (var supplied in targets)
        {
            var root = PlacementRoot(supplied, follower.splitNestedPrefabs);
            if (root && !candidates.Contains(root)) candidates.Add(root);
        }
        // Rebinding a prefab root replaces stale visual-child bindings on this tool.
        follower.bindings.RemoveAll(b => candidates.Exists(t => b.target != t && b.target.IsChildOf(t)));
        // Replace a previous container binding when rebinding its individual decorations.
        // Otherwise that ancestor would reject every child as a duplicate.
        follower.bindings.RemoveAll(b => candidates.Exists(t => t && t != b.target && t.IsChildOf(b.target)));
        var candidateSet = new HashSet<Transform>(candidates);
        var occupied = new HashSet<Transform>();
        var occupiedParents = new HashSet<Transform>();
        foreach (var other in Object.FindObjectsByType<DecorationTerrainFollower>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            foreach (var b in other.bindings)
            {
                if (!b.target || (other == follower && candidateSet.Contains(b.target))) continue;
                occupied.Add(b.target);
                for (var parent = b.target.parent; parent; parent = parent.parent) occupiedParents.Add(parent);
            }
        var replacements = new Dictionary<Transform, DecorationTerrainFollower.Binding>();
        int count = 0;
        foreach (var target in candidates)
        {
            if (!SceneObject(target) || target.GetComponentInChildren<Terrain>(true)) continue;
            bool conflict = occupiedParents.Contains(target);
            for (var parent = target; parent && !conflict; parent = parent.parent) conflict = occupied.Contains(parent);
            if (conflict) continue;
            Terrain terrain = preferred;
            if (!terrain)
                foreach (var candidate in Terrain.activeTerrains)
                    if (DecorationTerrainFollower.Sample(candidate, target.position, out _, out _)) { terrain = candidate; break; }
            if (!SceneObject(terrain) || !DecorationTerrainFollower.Sample(terrain, target.position, out float height, out var slope)) continue;
            replacements[target] = new DecorationTerrainFollower.Binding { target = target, terrain = terrain,
                heightOffset = target.position.y - height, relativeRotation = Quaternion.Inverse(slope) * target.rotation };
            occupied.Add(target);
            for (var parent = target.parent; parent; parent = parent.parent) occupiedParents.Add(parent);
            count++;
        }
        follower.bindings.RemoveAll(b => replacements.ContainsKey(b.target));
        follower.bindings.AddRange(replacements.Values);
        PrefabUtility.RecordPrefabInstancePropertyModifications(follower);
        EditorUtility.SetDirty(follower);
        return count;
    }
    public static void DrawControls(GameObject owner, IEnumerable<Transform> targets, Terrain preferred = null)
    {
        var candidates = new List<Transform>(targets);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Auto Follow Terrain", EditorStyles.boldLabel);
        var follower = owner.GetComponent<DecorationTerrainFollower>();
        EditorGUILayout.HelpBox("Bind prefab roots at the intended height: visuals and bake components move together. Nested prefabs stay together by default. Enable Split Nested Prefabs only for level/container groups whose nested objects should move independently. Rebind after changing height or rotation.", MessageType.Info);
        EditorGUILayout.LabelField("Ready to bind", candidates.Count.ToString());
        using (new EditorGUI.DisabledScope(!CanRun || !SceneObject(owner.transform)))
        {
            if (follower)
            {
                var so = new SerializedObject(follower); so.Update();
                EditorGUILayout.PropertyField(so.FindProperty("autoFollow"));
                EditorGUILayout.PropertyField(so.FindProperty("followSlope"));
                EditorGUILayout.PropertyField(so.FindProperty("splitNestedPrefabs"), new GUIContent("Split Nested Prefabs (level groups)"));
                so.ApplyModifiedProperties();
                EditorGUILayout.LabelField("Bound decorations", follower.bindings.FindAll(b => b.target).Count.ToString());
                int unsafeChildren = follower.bindings.FindAll(b => b.target && PlacementRoot(b.target, follower.splitNestedPrefabs) != b.target).Count;
                if (unsafeChildren > 0)
                    EditorGUILayout.HelpBox($"{unsafeChildren} old child bindings are paused to protect prefab/bake roots. Select the intended prefab roots and Bind / Rebind. Existing child transform overrides are preserved; review them before rebinding.", MessageType.Warning);
            }
            if (GUILayout.Button("Bind / Rebind Decorations"))
            {
                if (!follower) follower = Undo.AddComponent<DecorationTerrainFollower>(owner);
                int count = Bind(follower, candidates, preferred);
                Debug.Log($"Terrain Follow: bound {count} decorations. Objects outside Terrain or already covered by another binding were skipped.", owner);
            }
            if (follower && GUILayout.Button("Unbind All (keep objects)"))
            {
                Undo.RecordObject(follower, "Unbind Decorations"); follower.bindings.Clear();
                PrefabUtility.RecordPrefabInstancePropertyModifications(follower); EditorUtility.SetDirty(follower);
            }
        }
    }

    public static Transform PlacementRoot(Transform target, bool splitNestedPrefabs)
    {
        if (!target) return null;
        var prefab = splitNestedPrefabs
            ? PrefabUtility.GetNearestPrefabInstanceRoot(target.gameObject)
            : PrefabUtility.GetOutermostPrefabInstanceRoot(target.gameObject);
        return prefab ? prefab.transform : target;
    }
}

[CustomEditor(typeof(DecorationTerrainFollower))]
public sealed class DecorationTerrainFollowerEditor : Editor
{
    private readonly List<Transform> targets = new List<Transform>();
    private bool selectionDirty = true;
    private void OnEnable() { Selection.selectionChanged += Invalidate; EditorApplication.hierarchyChanged += Invalidate; }
    private void OnDisable() { Selection.selectionChanged -= Invalidate; EditorApplication.hierarchyChanged -= Invalidate; }
    private void Invalidate() { selectionDirty = true; Repaint(); }
    public override void OnInspectorGUI()
    {
        var follower = (DecorationTerrainFollower)target;
        if (selectionDirty)
        {
        selectionDirty = false;
        targets.Clear();
        foreach (var go in Selection.gameObjects)
            if (go != follower.gameObject && DecorationTerrainFollowService.SceneObject(go.transform)) Collect(go.transform, targets, follower.splitNestedPrefabs);
        var selected = new HashSet<Transform>(targets);
        targets.RemoveAll(t => HasSelectedParent(t, selected));
        }
        bool splitBefore = follower.splitNestedPrefabs;
        DecorationTerrainFollowService.DrawControls(follower.gameObject, targets);
        if (splitBefore != follower.splitNestedPrefabs) Invalidate();
        EditorGUILayout.HelpBox("Use System Modification > TerrainTools > Decoration Auto Follow Terrain to keep this panel open while selecting scene decorations.", MessageType.None);
    }
    private static bool HasSelectedParent(Transform target, HashSet<Transform> selected)
    {
        for (var parent = target.parent; parent; parent = parent.parent) if (selected.Contains(parent)) return true;
        return false;
    }
    public static void Collect(Transform root, List<Transform> targets, bool splitNestedPrefabs = false)
    {
        if (root.GetComponent<Terrain>()) return;
        if (!splitNestedPrefabs)
        {
            var prefabRoot = DecorationTerrainFollowService.PlacementRoot(root, false);
            if (PrefabUtility.IsAnyPrefabInstanceRoot(prefabRoot.gameObject))
            {
                if (!prefabRoot.GetComponentInChildren<Terrain>(true) && !targets.Contains(prefabRoot)) targets.Add(prefabRoot);
                return;
            }
        }
        // A level-sized prefab can contain many nested decoration prefabs. It is a
        // container, not a single tree/rock, even though it is a prefab instance root.
        bool nestedPrefabs = false;
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
            if (child != root && PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject))
            { nestedPrefabs = true; break; }
        bool hasGeometry = root.GetComponentInChildren<Renderer>(true) || root.GetComponentInChildren<Collider>(true);
        if (root.GetComponent<Renderer>() || root.GetComponent<Collider>() || root.GetComponent<LODGroup>() ||
            (PrefabUtility.IsAnyPrefabInstanceRoot(root.gameObject) && !nestedPrefabs && hasGeometry))
        { if (!targets.Contains(root)) targets.Add(root); return; }
        foreach (Transform child in root) Collect(child, targets, splitNestedPrefabs);
    }
}

public sealed class DecorationTerrainFollowWindow : EditorWindow
{
    [SerializeField] private DecorationTerrainFollower follower;
    private Editor inspector;
    [MenuItem("System Modification/TerrainTools/Decoration Auto Follow Terrain")]
    public static void Open()
    {
        var window = GetWindow<DecorationTerrainFollowWindow>("Decoration Terrain Follow");
        window.minSize = new Vector2(380, 300);
        window.Show();
    }
    private void OnEnable() { Selection.selectionChanged += Repaint; }
    private void OnDisable() { Selection.selectionChanged -= Repaint; if (inspector) DestroyImmediate(inspector); }
    private void OnGUI()
    {
        follower = (DecorationTerrainFollower)EditorGUILayout.ObjectField("Scene Tool", follower, typeof(DecorationTerrainFollower), true);
        if (!DecorationTerrainFollowService.SceneObject(follower))
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null))
                if (GUILayout.Button("Create Terrain Follow Tool"))
                {
                    var go = new GameObject("Decoration Terrain Follow");
                    Undo.RegisterCreatedObjectUndo(go, "Create Terrain Follow Tool");
                    follower = Undo.AddComponent<DecorationTerrainFollower>(go);
                }
            return;
        }
        Editor.CreateCachedEditor(follower, typeof(DecorationTerrainFollowerEditor), ref inspector);
        inspector.OnInspectorGUI();
    }
}
#endif

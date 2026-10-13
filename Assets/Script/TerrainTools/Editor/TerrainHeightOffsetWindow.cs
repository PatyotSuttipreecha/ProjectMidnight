#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// All coordinates used by the region are Terrain-local X/Z in world units.
public static class TerrainHeightOffsetMath
{
    public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 d = b - a;
        return Vector2.Distance(p, a + d * Mathf.Clamp01(Vector2.Dot(p - a, d) / d.sqrMagnitude));
    }

    public static string ValidatePolygon(IList<Vector2> points)
    {
        if (points == null || points.Count < 3) return "Add at least three polygon points.";
        double area = 0;
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 a = points[i], b = points[(i + 1) % points.Count];
            if (!Finite(a.x) || !Finite(a.y)) return "Polygon coordinates must be finite.";
            if ((b - a).sqrMagnitude < 0.000001f) return "Remove duplicate or overlapping points.";
            area += (double)a.x * b.y - (double)b.x * a.y;
            Vector2 c = points[(i + 2) % points.Count];
            if (Mathf.Abs(Cross(b - a, c - b)) < 0.00001f && Vector2.Dot(b - a, c - b) < 0)
                return "Polygon edges must not double back.";
            for (int j = i + 1; j < points.Count; j++)
            {
                if (j == i + 1 || (i == 0 && j == points.Count - 1)) continue;
                Vector2 u = points[j], v = points[(j + 1) % points.Count];
                float abU = Cross(b - a, u - a), abV = Cross(b - a, v - a);
                float uvA = Cross(v - u, a - u), uvB = Cross(v - u, b - u);
                bool crosses = ((abU > 0 && abV < 0) || (abU < 0 && abV > 0)) &&
                               ((uvA > 0 && uvB < 0) || (uvA < 0 && uvB > 0));
                if (crosses || SegmentDistance(u, a, b) < 0.0001f || SegmentDistance(v, a, b) < 0.0001f ||
                    SegmentDistance(a, u, v) < 0.0001f || SegmentDistance(b, u, v) < 0.0001f)
                    return "Polygon edges must not cross or touch each other.";
            }
        }
        return Math.Abs(area) < 0.0001 ? "Polygon must enclose an area." : null;
    }

    public static float Weight(Vector2 p, IList<Vector2> points, float blend)
    {
        bool inside = false;
        float distance = float.MaxValue;
        for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
        {
            Vector2 a = points[j], b = points[i];
            distance = Mathf.Min(distance, SegmentDistance(p, a, b));
            if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
        }
        if (distance < 0.00001f) return blend > 0 ? 0 : 1;
        if (!inside) return 0;
        return blend <= 0 ? 1 : Mathf.SmoothStep(0, 1, Mathf.Clamp01(distance / blend));
    }

    // Work on a copy: rejection cannot partially alter the source heightmap.
    public static float[,] Build(float[,] original, Vector3 size, IList<Vector2> polygon, float blend, float offset)
    {
        if (!Finite(offset) || !Finite(blend) || blend < 0 || !Finite(size.y) || size.y <= 0)
            throw new ArgumentException("Offset, blend and Terrain height must be valid finite values.");
        if (polygon != null)
        {
            string error = ValidatePolygon(polygon);
            if (error != null) throw new ArgumentException(error);
        }
        var result = (float[,])original.Clone();
        int rows = result.GetLength(0), columns = result.GetLength(1);
        for (int z = 0; z < rows; z++)
        for (int x = 0; x < columns; x++)
        {
            float weight = polygon == null ? 1 : Weight(new Vector2(x * size.x / (columns - 1), z * size.z / (rows - 1)), polygon, blend);
            float height = original[z, x] + offset / size.y * weight;
            if (!Finite(height) || height < 0 || height > 1)
                throw new InvalidOperationException("Offset exceeds the Terrain height range. Nothing changed. Reduce the offset; peaks will not be clamped.");
            result[z, x] = height;
        }
        return result;
    }
}

public sealed class TerrainHeightOffsetWindow : EditorWindow
{
    [SerializeField] private Terrain terrain;
    [SerializeField] private bool wholeTerrain;
    [SerializeField] private float offset = 20;
    [SerializeField] private float blend = 10;
    [SerializeField] private bool copyData = true;
    [SerializeField] private bool editBoundary;
    [SerializeField] private List<Vector2> points = new List<Vector2>();
    [SerializeField] private List<Transform> propRoots = new List<Transform>();
    [SerializeField] private TerrainData recoverySource;
    [SerializeField] private string saveFolder = "Assets";
    [SerializeField] private string saveName = "TerrainHeightOffset";
    private Vector2 scroll;
    private string status;

    [MenuItem("System Modification/TerrainTools/Terrain Height Offset", false, 30)]
    public static void Open()
    {
        var window = GetWindow<TerrainHeightOffsetWindow>("Terrain Height Offset");
        if (Selection.activeGameObject != null)
            window.terrain = Selection.activeGameObject.GetComponent<Terrain>();
        if (window.terrain == null && Terrain.activeTerrains.Length == 1) window.terrain = Terrain.activeTerrains[0];
        window.Show();
    }

    private string SavePreferenceKey => "Thung.TerrainHeightOffset." + Application.dataPath;

    private void OnEnable()
    {
        saveFolder = EditorPrefs.GetString(SavePreferenceKey + ".Folder", "Assets");
        saveName = EditorPrefs.GetString(SavePreferenceKey + ".Name", "TerrainHeightOffset");
        SceneView.duringSceneGui += OnSceneGUI;
    }
    private void OnDisable() => SceneView.duringSceneGui -= OnSceneGUI;

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox("Add the same height to a region while preserving its interior shape. Positive = raise, negative = lower. Each Apply adds the offset again.", MessageType.Info);
        EditorGUI.BeginChangeCheck();
        var nextTerrain = (Terrain)EditorGUILayout.ObjectField("Terrain", terrain, typeof(Terrain), true);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(this, "Change Offset Terrain");
            terrain = nextTerrain;
            points.Clear();
            status = null;
        }
        wholeTerrain = EditorGUILayout.Toggle("Whole Terrain", wholeTerrain);
        offset = EditorGUILayout.FloatField("Height offset (units)", offset);
        if (!wholeTerrain)
        {
            blend = EditorGUILayout.FloatField("Inward blend (units)", blend);
            EditorGUILayout.HelpBox("Shift + left click on Terrain to add vertices in order. Drag handles to move them. The cyan outline is the boundary. Yellow markers show the approximate full-offset interior. Only the inward blend changes the original shape; zero blend creates a steep boundary.", MessageType.Info);
            editBoundary = GUILayout.Toggle(editBoundary, "Edit boundary in Scene", "Button");
            for (int i = 0; i < points.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                Vector2 value = EditorGUILayout.Vector2Field("Point " + (i + 1), points[i]);
                if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(this, "Edit Offset Boundary"); points[i] = value; }
                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    Undo.RecordObject(this, "Remove Offset Point"); points.RemoveAt(i); i--;
                }
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("Clear boundary")) { Undo.RecordObject(this, "Clear Offset Boundary"); points.Clear(); }
        }
        copyData = EditorGUILayout.Toggle("Save to configured path", copyData);
        DrawSavePath();
        EditorGUILayout.HelpBox(copyData
            ? "Apply creates or overwrites the TerrainData at Save Path and assigns it to this Terrain and its collider. All scenes using that output asset see its updates. Undo is available."
            : "Editing the original TerrainData affects EVERY Terrain and scene using that asset. Undo is available; no automatic scene save.", copyData ? MessageType.Info : MessageType.Warning);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Optional prop roots", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Each root moves as one object, based on the ground change at its pivot. For a folder of independent props, add its direct children. Nested selections move only once. Terrain trees follow automatically; grass follows the surface.", MessageType.Info);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add selected roots")) AddSelected(false);
        if (GUILayout.Button("Add selected children")) AddSelected(true);
        EditorGUILayout.EndHorizontal();
        for (int i = 0; i < propRoots.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            propRoots[i] = (Transform)EditorGUILayout.ObjectField(propRoots[i], typeof(Transform), true);
            if (GUILayout.Button("X", GUILayout.Width(25))) { propRoots.RemoveAt(i); i--; }
            EditorGUILayout.EndHorizontal();
        }
        if (GUILayout.Button("Add empty prop slot")) propRoots.Add(null);
        string error = Validate();
        if (error != null) EditorGUILayout.HelpBox(error, MessageType.Warning);
        using (new EditorGUI.DisabledScope(error != null))
            if (GUILayout.Button("Apply height offset", GUILayout.Height(32))) Apply();
        if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Recover paint and vegetation", EditorStyles.boldLabel);
        recoverySource = (TerrainData)EditorGUILayout.ObjectField("Original TerrainData", recoverySource, typeof(TerrainData), false);
        EditorGUILayout.HelpBox("If an earlier copy lost paint or vegetation, choose the original TerrainData here. Recovery creates a new complete copy, keeps the CURRENT terrain heights, and adjusts original trees to that ground. Props are not moved again. The original asset is unchanged.", MessageType.Info);
        using (new EditorGUI.DisabledScope(terrain == null || recoverySource == null || EditorApplication.isPlayingOrWillChangePlaymode))
            if (GUILayout.Button("Recover surface; keep current heights")) RecoverSurface();
        EditorGUILayout.HelpBox("Terrain holes/textures stay unchanged. Rebuild baked navigation or collision data used by your game after adjusting the level.", MessageType.None);
        EditorGUILayout.EndScrollView();
        if (GUI.changed) SceneView.RepaintAll();
    }

    private void DrawSavePath()
    {
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.BeginHorizontal();
        saveFolder = EditorGUILayout.TextField("Save Path (folder)", saveFolder);
        if (GUILayout.Button("Browse", GUILayout.Width(65)))
        {
            string chosen = EditorUtility.OpenFolderPanel("TerrainData save folder", Application.dataPath, "");
            if (!string.IsNullOrEmpty(chosen))
            {
                string relative = FileUtil.GetProjectRelativePath(chosen).Replace('\\', '/');
                if (relative == "Assets" || relative.StartsWith("Assets/", StringComparison.Ordinal))
                    saveFolder = relative;
                else status = "Choose a folder inside this project's Assets folder.";
            }
        }
        EditorGUILayout.EndHorizontal();
        saveName = EditorGUILayout.TextField("File name", saveName);
        if (EditorGUI.EndChangeCheck())
        {
            EditorPrefs.SetString(SavePreferenceKey + ".Folder", saveFolder);
            EditorPrefs.SetString(SavePreferenceKey + ".Name", saveName);
        }
        EditorGUILayout.HelpBox("The save folder and file name are remembered. Apply overwrites the same asset automatically, preserving its GUID. Recovery uses the same name with _Recovered. Scene saving remains manual.", MessageType.None);
    }

    private string GetNextSavePath(bool recovery)
    {
        string folder = (saveFolder ?? "").Trim().Replace('\\', '/').TrimEnd('/');
        if ((folder != "Assets" && !folder.StartsWith("Assets/", StringComparison.Ordinal)) ||
            folder.Contains("/../") || folder.EndsWith("/..", StringComparison.Ordinal) || !AssetDatabase.IsValidFolder(folder))
            throw new InvalidOperationException("Save Path must be an existing folder inside Assets. Use Browse to select it.");
        string name = (saveName ?? "").Trim();
        if (name.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 6);
        if (string.IsNullOrWhiteSpace(name) || name == "." || name == ".." ||
            name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 || name.Contains("/") || name.Contains("\\"))
            throw new InvalidOperationException("Enter a valid file name without a folder path.");
        return folder + "/" + name + (recovery ? "_Recovered" : "") + ".asset";
    }

    private void AddSelected(bool children)
    {
        foreach (Transform root in Selection.transforms)
        {
            if (children) { foreach (Transform child in root) if (!propRoots.Contains(child)) propRoots.Add(child); }
            else if (!propRoots.Contains(root)) propRoots.Add(root);
        }
    }

    private string Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return "Use this tool in Edit Mode.";
        if (PrefabStageUtility.GetCurrentPrefabStage() != null) return "Close Prefab Mode before editing a scene Terrain.";
        if (terrain == null || terrain.terrainData == null) return "Choose a scene Terrain.";
        if (EditorUtility.IsPersistent(terrain) || !terrain.gameObject.scene.IsValid() || !terrain.gameObject.scene.isLoaded)
            return "Choose a Terrain in a loaded scene.";
        // Validate the full world matrix, including scaled/rotated ancestors and shear.
        Matrix4x4 matrix = terrain.transform.localToWorldMatrix;
        if ((matrix.MultiplyVector(Vector3.right) - Vector3.right).sqrMagnitude > 0.000001f ||
            (matrix.MultiplyVector(Vector3.up) - Vector3.up).sqrMagnitude > 0.000001f ||
            (matrix.MultiplyVector(Vector3.forward) - Vector3.forward).sqrMagnitude > 0.000001f)
            return "Terrain world rotation must be zero and world scale must be one.";
        if (!TerrainHeightOffsetMath.Finite(offset) || Mathf.Approximately(offset, 0)) return "Enter a finite, non-zero height offset.";
        if (!TerrainHeightOffsetMath.Finite(blend) || blend < 0) return "Blend must be finite and non-negative.";
        if (!wholeTerrain)
        {
            string error = TerrainHeightOffsetMath.ValidatePolygon(points);
            if (error != null) return error;
            Vector3 size = terrain.terrainData.size;
            foreach (Vector2 p in points)
                if (p.x < 0 || p.y < 0 || p.x > size.x || p.y > size.z) return "All polygon points must be inside the Terrain.";
        }
        foreach (Transform root in propRoots)
        {
            if (root == null) continue;
            if (EditorUtility.IsPersistent(root) || !root.gameObject.scene.IsValid() || !root.gameObject.scene.isLoaded)
                return "Prop roots must be scene objects.";
            if (root.GetComponentInChildren<Terrain>(true) != null || root == terrain.transform || terrain.transform.IsChildOf(root))
                return "Prop roots cannot contain a Terrain.";
        }
        return null;
    }

    private Vector3 Surface(Vector2 p)
    {
        Vector3 size = terrain.terrainData.size;
        return terrain.transform.position + new Vector3(p.x, terrain.terrainData.GetInterpolatedHeight(p.x / size.x, p.y / size.z) + 0.5f, p.y);
    }

    private void OnSceneGUI(SceneView view)
    {
        if (terrain == null || terrain.terrainData == null || wholeTerrain) return;
        Handles.color = Color.cyan;
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 p = Surface(points[i]);
            if (points.Count > 1)
            {
                Vector2 next = points[(i + 1) % points.Count];
                var line = new Vector3[25];
                for (int k = 0; k < line.Length; k++) line[k] = Surface(Vector2.Lerp(points[i], next, k / 24f));
                Handles.DrawAAPolyLine(3, line);
            }
            Handles.Label(p + Vector3.up, (i + 1).ToString());
            if (!editBoundary) continue;
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.PositionHandle(p, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(this, "Move Offset Point");
                Vector3 local = moved - terrain.transform.position, size = terrain.terrainData.size;
                points[i] = new Vector2(Mathf.Clamp(local.x, 0, size.x), Mathf.Clamp(local.z, 0, size.z));
                Repaint();
            }
        }
        if (points.Count >= 3 && TerrainHeightOffsetMath.ValidatePolygon(points) == null && Event.current.type == EventType.Repaint)
        {
            Vector2 min = points[0], max = min;
            foreach (Vector2 p in points) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            for (int z = 0; z <= 18; z++)
            for (int x = 0; x <= 18; x++)
            {
                Vector2 p = new Vector2(Mathf.Lerp(min.x, max.x, x / 18f), Mathf.Lerp(min.y, max.y, z / 18f));
                float w = TerrainHeightOffsetMath.Weight(p, points, Mathf.Max(0, blend));
                if (w <= 0) continue;
                Handles.color = Color.Lerp(Color.cyan, Color.yellow, w);
                Vector3 world = Surface(p);
                Handles.DotHandleCap(0, world, Quaternion.identity, HandleUtility.GetHandleSize(world) * 0.035f, EventType.Repaint);
            }
        }
        Event e = Event.current;
        if (!editBoundary || !e.shift || e.alt) return;
        if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            var collider = terrain.GetComponent<TerrainCollider>();
            if (collider != null && collider.terrainData == terrain.terrainData && collider.Raycast(HandleUtility.GUIPointToWorldRay(e.mousePosition), out RaycastHit hit, float.MaxValue))
            {
                Undo.RecordObject(this, "Add Offset Point");
                Vector3 local = hit.point - terrain.transform.position;
                points.Add(new Vector2(local.x, local.z));
                e.Use(); Repaint();
            }
        }
    }

    public static List<Transform> UniqueRoots(IEnumerable<Transform> candidates)
    {
        var set = new HashSet<Transform>(candidates);
        set.Remove(null);
        var result = new List<Transform>();
        foreach (Transform root in set)
        {
            bool nested = false;
            for (Transform p = root.parent; p != null; p = p.parent)
                if (set.Contains(p)) { nested = true; break; }
            if (!nested) result.Add(root);
        }
        return result;
    }

    private void Apply()
    {
        string error = Validate();
        if (error != null) { status = error; return; }
        try
        {
            TerrainData source = terrain.terrainData;
            float[,] original = source.GetHeights(0, 0, source.heightmapResolution, source.heightmapResolution);
            float[,] changed = TerrainHeightOffsetMath.Build(original, source.size, wholeTerrain ? null : points, blend, offset);
            bool anyChange = false;
            for (int z = 0; z < original.GetLength(0) && !anyChange; z++)
                for (int x = 0; x < original.GetLength(1); x++)
                    if (original[z, x] != changed[z, x]) { anyChange = true; break; }
            if (!anyChange) { status = "No height samples changed. Enlarge the region or increase the offset."; return; }
            string copyPath = null;
            if (copyData)
            {
                copyPath = GetNextSavePath(false);
            }
            ApplyPrepared(terrain, changed, UniqueRoots(propRoots), copyPath);
            if (!string.IsNullOrEmpty(copyPath) && source != terrain.terrainData) recoverySource = source;
            status = "Applied " + offset + " units. " + (copyPath == null ? "Save the scene and assets when ready." : "Saved: " + copyPath + ". Save the scene when ready.") + " Ctrl+Z undoes this operation.";
            SceneView.RepaintAll();
        }
        catch (Exception ex) { status = ex.Message; Debug.LogException(ex); }
    }

    private void RecoverSurface()
    {
        try
        {
            if (terrain == null || terrain.terrainData == null || recoverySource == null ||
                EditorUtility.IsPersistent(terrain) || !terrain.gameObject.scene.IsValid() ||
                PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Choose a scene Terrain and its original TerrainData outside Prefab Mode.");
            TerrainData current = terrain.terrainData;
            if (current == recoverySource) throw new InvalidOperationException("Choose the intact ORIGINAL data, not the currently assigned copy.");
            if (current.heightmapResolution != recoverySource.heightmapResolution || current.size != recoverySource.size)
                throw new InvalidOperationException("Original and current TerrainData must have identical size and heightmap resolution.");
            string path = GetNextSavePath(true);
            float[,] heights = current.GetHeights(0, 0, current.heightmapResolution, current.heightmapResolution);
            ApplyPrepared(terrain, heights, new List<Transform>(), path, recoverySource);
            status = "Recovered paint, trees, details and holes. Current heights preserved. Saved: " + path + ". Save the scene when ready. Ctrl+Z undoes recovery.";
            SceneView.RepaintAll();
        }
        catch (Exception ex) { status = ex.Message; Debug.LogException(ex); }
    }

    // Instantiate/CreateAsset alone does NOT preserve painted alphamaps in Unity 6.
    // Rehydrate every surface/vegetation dataset from the LIVE source (including unsaved edits)
    // after creating the asset, before assigning it to a scene Terrain.
    public static TerrainData CreateCompleteCopy(TerrainData source, string path)
    {
        if (string.IsNullOrEmpty(path) || System.IO.File.Exists(path) || AssetDatabase.LoadMainAssetAtPath(path) != null)
            throw new InvalidOperationException("The TerrainData copy must use a new asset path.");
        var copy = Instantiate(source);
        copy.name = System.IO.Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(copy, path);
        CopySurface(source, copy);
        return copy;
    }

    private static void CopySurface(TerrainData source, TerrainData copy)
    {
        TerrainLayer[] layers = source.terrainLayers;
        TreePrototype[] treePrototypes = source.treePrototypes;
        TreeInstance[] trees = source.treeInstances;
        DetailPrototype[] detailPrototypes = source.detailPrototypes;
        bool[,] holes = source.GetHoles(0, 0, source.holesResolution, source.holesResolution);
        copy.terrainLayers = layers;
        copy.alphamapResolution = source.alphamapResolution;
        // Tile the paint transfer to avoid allocating a full multi-layer float map on large levels.
        const int tile = 128;
        if (layers.Length > 0)
            for (int z = 0; z < source.alphamapHeight; z += tile)
                for (int x = 0; x < source.alphamapWidth; x += tile)
                {
                    int width = Mathf.Min(tile, source.alphamapWidth - x), height = Mathf.Min(tile, source.alphamapHeight - z);
                    copy.SetAlphamaps(x, z, source.GetAlphamaps(x, z, width, height));
                }
        copy.SetDetailScatterMode(source.detailScatterMode);
        copy.SetDetailResolution(source.detailResolution, source.detailResolutionPerPatch);
        copy.detailPrototypes = detailPrototypes;
        for (int i = 0; i < detailPrototypes.Length; i++)
            copy.SetDetailLayer(0, 0, i, source.GetDetailLayer(0, 0, source.detailWidth, source.detailHeight, i));
        copy.treePrototypes = treePrototypes;
        copy.SetTreeInstances(trees, false);
        copy.SetHoles(0, 0, holes);
        if (copy.alphamapLayers != source.alphamapLayers || copy.treeInstanceCount != trees.Length ||
            copy.treePrototypes.Length != treePrototypes.Length || copy.detailPrototypes.Length != detailPrototypes.Length)
            throw new InvalidOperationException("Terrain copy validation failed. The scene Terrain has not been changed.");
        EditorUtility.SetDirty(copy);
    }

    // Capture old ground heights and tree positions before SetHeights; then use actual sampled
    // ground deltas after the write, including Unity heightmap quantization and interpolation.
    public static void ApplyPrepared(Terrain terrain, float[,] heights, List<Transform> roots, string copyPath, TerrainData recoveryData = null)
    {
        TerrainData source = recoveryData != null ? recoveryData : terrain.terrainData;
        TerrainData destination = null;
        if (!string.IsNullOrEmpty(copyPath))
        {
            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(copyPath);
            if (existing != null && !(existing is TerrainData))
                throw new InvalidOperationException("Save Path already contains a different asset type. Choose another file name.");
            destination = existing as TerrainData;
            if (destination == null && System.IO.File.Exists(copyPath))
                throw new InvalidOperationException("The output file exists but is not a loaded TerrainData asset.");
            if (destination != null && (destination.size != source.size || destination.heightmapResolution != source.heightmapResolution ||
                destination.alphamapResolution != source.alphamapResolution || destination.alphamapLayers != source.alphamapLayers ||
                destination.detailResolution != source.detailResolution || destination.detailResolutionPerPatch != source.detailResolutionPerPatch))
                throw new InvalidOperationException("Existing output uses a different Terrain layout. Choose another file name to preserve its data and Undo support.");
            if (recoveryData != null && destination == recoveryData)
                throw new InvalidOperationException("Recovery output cannot overwrite its intact original source.");
        }
        if (recoveryData != null && string.IsNullOrEmpty(copyPath))
            throw new InvalidOperationException("Recovery must create a separate asset.");
        Vector3 size = source.size, origin = terrain.transform.position;
        TreeInstance[] trees = source.treeInstances;
        var oldTreeGround = new float[trees.Length];
        for (int i = 0; i < trees.Length; i++) oldTreeGround[i] = source.GetInterpolatedHeight(trees[i].position.x, trees[i].position.z);
        var props = new List<Transform>();
        var propUV = new List<Vector2>();
        var oldPropGround = new List<float>();
        foreach (Transform root in roots)
        {
            Vector3 p = root.position - origin;
            if (p.x < 0 || p.z < 0 || p.x > size.x || p.z > size.z) continue;
            Vector2 uv = new Vector2(p.x / size.x, p.z / size.z);
            props.Add(root); propUV.Add(uv); oldPropGround.Add(source.GetInterpolatedHeight(uv.x, uv.y));
        }
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        const string operation = "Terrain Height Offset";
        Undo.SetCurrentGroupName(operation);
        TerrainData data = source;
        try
        {
            if (!string.IsNullOrEmpty(copyPath))
            {
                if (destination == null) data = CreateCompleteCopy(source, copyPath);
                else
                {
                    data = destination;
                    Undo.RegisterCompleteObjectUndo(data, operation);
                    if (data != source)
                    {
                        foreach (Texture2D texture in data.alphamapTextures)
                            Undo.RegisterCompleteObjectUndo(texture, operation);
                        CopySurface(source, data);
                    }
                }
                Undo.RecordObject(terrain, operation);
                terrain.terrainData = data;
                PrefabUtility.RecordPrefabInstancePropertyModifications(terrain);
                var collider = terrain.GetComponent<TerrainCollider>();
                if (collider != null)
                {
                    Undo.RecordObject(collider, operation);
                    collider.terrainData = data;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
                }
            }
            Undo.RegisterCompleteObjectUndo(data, operation);
            data.SetHeights(0, 0, heights);
            for (int i = 0; i < trees.Length; i++)
            {
                Vector3 p = trees[i].position;
                p.y += (data.GetInterpolatedHeight(p.x, p.z) - oldTreeGround[i]) / size.y;
                trees[i].position = p;
            }
            // false preserves existing clearance and avoids a second snap/offset.
            data.SetTreeInstances(trees, false);
            data.SetBaseMapDirty();
            for (int i = 0; i < props.Count; i++)
            {
                Vector2 uv = propUV[i];
                float delta = data.GetInterpolatedHeight(uv.x, uv.y) - oldPropGround[i];
                if (delta == 0) continue;
                Undo.RecordObject(props[i], operation);
                props[i].position += Vector3.up * delta;
                PrefabUtility.RecordPrefabInstancePropertyModifications(props[i]);
                EditorSceneManager.MarkSceneDirty(props[i].gameObject.scene);
            }
            EditorUtility.SetDirty(data);
            EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
            terrain.Flush();
            // Persist only the configured output asset, including overwrites; keep its GUID.
            if (!string.IsNullOrEmpty(copyPath)) AssetDatabase.SaveAssetIfDirty(data);
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(group);
        }
        catch
        {
            Undo.FlushUndoRecordObjects();
            Undo.RevertAllDownToGroup(group);
            throw;
        }
    }
}
#endif

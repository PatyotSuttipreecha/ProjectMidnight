#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TerrainSlopeMath
{
    [Serializable]
    public struct Footprint
    {
        public Vector3 a, b;
        public float width, sideBlend, endBlend;
        public bool Contains(Vector2 point)
        {
            Vector2 d = new Vector2(b.x-a.x, b.z-a.z);
            float length = d.magnitude;
            if (length < .01f) return false;
            d /= length;
            Vector2 delta = point-new Vector2(a.x,a.z);
            float along = Vector2.Dot(delta,d), across = Mathf.Abs(delta.x*d.y-delta.y*d.x);
            return along >= -endBlend && along <= length+endBlend && across <= width*.5f+sideBlend;
        }
    }

    public static void PreserveExterior(float[,] ramp, float[,] current, Vector3 size, Footprint next, Footprint previous)
    {
        int rows = ramp.GetLength(0), columns = ramp.GetLength(1);
        for (int z=0; z<rows; z++) for (int x=0; x<columns; x++)
        {
            Vector2 p = new Vector2(x*size.x/(columns-1), z*size.z/(rows-1));
            if (!next.Contains(p) && !previous.Contains(p)) ramp[z,x] = current[z,x];
        }
    }
    public static string Validate(Vector3 a, Vector3 b, float angle, float width, float sideBlend, float endBlend)
    {
        foreach (float value in new[] { a.x, a.y, a.z, b.x, b.y, b.z, angle, width, sideBlend, endBlend })
            if (!TerrainHeightOffsetMath.Finite(value)) return "All ramp values must be finite.";
        if (new Vector2(b.x - a.x, b.z - a.z).magnitude < 0.01f) return "A and B must be different horizontal positions.";
        if (Mathf.Abs(angle) >= 89) return "Slope angle must be between -89 and +89 degrees (exclusive).";
        if (width <= 0 || sideBlend < 0 || endBlend < 0) return "Width must be positive; blend distances must be non-negative.";
        return null;
    }

    public static float Angle(Vector3 a, Vector3 b) => Mathf.Atan2(b.y - a.y, new Vector2(b.x - a.x, b.z - a.z).magnitude) * Mathf.Rad2Deg;

    // The full-grade core spans A..B. Blend bands extend outside that segment and
    // outside its width. The target elevation is held at the nearest endpoint in end bands.
    public static float Height(Vector2 p, float original, Vector3 a, Vector3 b, float angle, float width, float sideBlend, float endBlend)
    {
        Vector2 start = new Vector2(a.x, a.z), direction = new Vector2(b.x - a.x, b.z - a.z);
        float length = direction.magnitude;
        direction /= length;
        Vector2 delta = p - start;
        float along = Vector2.Dot(delta, direction);
        float across = Mathf.Abs(delta.x * direction.y - delta.y * direction.x);
        float sideOutside = Mathf.Max(0, across - width * 0.5f);
        float endOutside = Mathf.Max(0, Mathf.Max(-along, along - length));
        float sideWeight = sideOutside == 0 ? 1 : sideBlend <= 0 ? 0 : 1 - Mathf.SmoothStep(0, 1, sideOutside / sideBlend);
        float endWeight = endOutside == 0 ? 1 : endBlend <= 0 ? 0 : 1 - Mathf.SmoothStep(0, 1, endOutside / endBlend);
        float target = a.y + Mathf.Clamp(along, 0, length) * Mathf.Tan(angle * Mathf.Deg2Rad);
        return Mathf.Lerp(original, target, sideWeight * endWeight);
    }

    public static float[,] Build(float[,] original, Vector3 size, Vector3 a, Vector3 b, float angle, float width, float sideBlend, float endBlend)
    {
        string error = Validate(a, b, angle, width, sideBlend, endBlend);
        if (error != null) throw new ArgumentException(error);
        float endHeight = a.y + new Vector2(b.x - a.x, b.z - a.z).magnitude * Mathf.Tan(angle * Mathf.Deg2Rad);
        if (a.y < 0 || a.y > size.y || endHeight < 0 || endHeight > size.y)
            throw new InvalidOperationException("Ramp exceeds the Terrain height range. Reduce the angle or change the anchor height.");
        int rows = original.GetLength(0), columns = original.GetLength(1);
        var result = (float[,])original.Clone();
        for (int z = 0; z < rows; z++)
        for (int x = 0; x < columns; x++)
        {
            float originalWorld = original[z, x] * size.y;
            float newWorld = Height(new Vector2(x * size.x / (columns - 1), z * size.z / (rows - 1)), originalWorld,
                a, b, angle, width, sideBlend, endBlend);
            float value = newWorld == originalWorld ? original[z, x] : newWorld / size.y;
            if (!TerrainHeightOffsetMath.Finite(value) || value < 0 || value > 1)
                throw new InvalidOperationException("Ramp contains out-of-range heights. Nothing was changed.");
            result[z, x] = value;
        }
        return result;
    }
}

public sealed class TerrainSlopeWindow : EditorWindow
{
    [SerializeField] private Terrain terrain;
    [SerializeField] private TerrainData baseline;
    [SerializeField] private Vector3 a, b;
    [SerializeField] private bool hasPoints, matchEndpoints, drawMode;
    [SerializeField] private float angle = 15, width = 12, sideBlend = 5, endBlend = 5;
    [SerializeField] private string saveFolder = "Assets", saveName = "TerrainSlope";
    [SerializeField] private List<Transform> props = new List<Transform>();
    [SerializeField] private TerrainSlopeMath.Footprint previousBake;
    private Vector2 scroll;
    private string status;
    private bool dragging;
    private string PreferenceKey => "Thung.TerrainSlope." + Application.dataPath;
    private float EffectiveAngle => matchEndpoints ? TerrainSlopeMath.Angle(a, b) : angle;

    [MenuItem("System Modification/TerrainTools/Terrain Slope", false, 31)]
    public static void Open()
    {
        var window = GetWindow<TerrainSlopeWindow>("Terrain Slope");
        if (window.terrain == null)
        {
            var selected = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<Terrain>() : null;
            window.SetTerrain(selected != null ? selected : Terrain.activeTerrains.Length == 1 ? Terrain.activeTerrains[0] : null);
        }
        window.Show();
    }

    private void OnEnable()
    {
        saveFolder = EditorPrefs.GetString(PreferenceKey + ".Folder", "Assets");
        saveName = EditorPrefs.GetString(PreferenceKey + ".Name", "TerrainSlope");
        SceneView.duringSceneGui += OnSceneGUI;
        Undo.undoRedoPerformed += OnUndo;
    }
    private void OnDisable() { SceneView.duringSceneGui -= OnSceneGUI; Undo.undoRedoPerformed -= OnUndo; }
    private void OnDestroy() { if (baseline != null) DestroyImmediate(baseline); }
    private void OnUndo() { Repaint(); SceneView.RepaintAll(); }

    private void SetTerrain(Terrain value)
    {
        terrain = value; hasPoints = false; drawMode = false; status = null;
        if (baseline != null) DestroyImmediate(baseline);
        baseline = null;
        previousBake = default;
    }

    private void CaptureBaseline()
    {
        if (terrain == null || terrain.terrainData == null) return;
        if (baseline != null) DestroyImmediate(baseline);
        var source = terrain.terrainData;
        baseline = new TerrainData { hideFlags = HideFlags.HideAndDontSave, heightmapResolution = source.heightmapResolution, size = source.size };
        baseline.SetHeights(0, 0, source.GetHeights(0, 0, source.heightmapResolution, source.heightmapResolution));
        previousBake = default;
        if (hasPoints) { a = Ground(a); b = Ground(b); }
    }

    private Vector3 Ground(Vector3 point)
    {
        var data = baseline != null ? baseline : terrain.terrainData;
        point.x = Mathf.Clamp(point.x, 0, data.size.x);
        point.z = Mathf.Clamp(point.z, 0, data.size.z);
        point.y = data.GetInterpolatedHeight(point.x / data.size.x, point.z / data.size.z);
        return point;
    }

    private string TerrainError()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null) return "Use a scene Terrain in Edit Mode, outside Prefab Mode.";
        if (terrain == null || terrain.terrainData == null || EditorUtility.IsPersistent(terrain) || !terrain.gameObject.scene.IsValid() || !terrain.gameObject.scene.isLoaded) return "Choose a Terrain in a loaded scene.";
        Matrix4x4 m = terrain.transform.localToWorldMatrix;
        if ((m.MultiplyVector(Vector3.right) - Vector3.right).sqrMagnitude > 0.000001f ||
            (m.MultiplyVector(Vector3.up) - Vector3.up).sqrMagnitude > 0.000001f ||
            (m.MultiplyVector(Vector3.forward) - Vector3.forward).sqrMagnitude > 0.000001f) return "Terrain world rotation must be zero and scale must be one.";
        return null;
    }

    private string Error()
    {
        string error = TerrainError();
        if (error != null) return error;
        if (!hasPoints || baseline == null) return "Enable Draw A > B, then drag on the Terrain in Scene view.";
        if (baseline.size != terrain.terrainData.size || baseline.heightmapResolution != terrain.terrainData.heightmapResolution) return "Terrain layout changed. Capture current heights again.";
        error = TerrainSlopeMath.Validate(a, b, EffectiveAngle, width, sideBlend, endBlend);
        if (error != null) return error;
        Vector3 size = terrain.terrainData.size;
        if (a.x < 0 || a.x > size.x || a.z < 0 || a.z > size.z || b.x < 0 || b.x > size.x || b.z < 0 || b.z > size.z) return "A and B must be inside the Terrain.";
        float targetB = a.y + new Vector2(b.x - a.x, b.z - a.z).magnitude * Mathf.Tan(EffectiveAngle * Mathf.Deg2Rad);
        if (a.y < 0 || a.y > size.y || targetB < 0 || targetB > size.y) return "The target ramp exceeds the Terrain height range.";
        foreach (Transform root in props)
            if (root != null && (EditorUtility.IsPersistent(root) || !root.gameObject.scene.IsValid() || !root.gameObject.scene.isLoaded || root.GetComponentInChildren<Terrain>(true) != null))
                return "Prop roots must be scene objects and cannot contain a Terrain.";
        return null;
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox("Drag from A to B to create a straight ramp. The core follows the target angle; the side and end bands blend back to the original ground.", MessageType.Info);
        var next = (Terrain)EditorGUILayout.ObjectField("Terrain", terrain, typeof(Terrain), true);
        if (next != terrain) SetTerrain(next);
        using (new EditorGUI.DisabledScope(TerrainError() != null))
        {
            drawMode = GUILayout.Toggle(drawMode, "Draw A > B in Scene (drag left mouse)", "Button");
            if (GUILayout.Button("Capture current heights / Start next ramp"))
            {
                CaptureBaseline(); status = "Current heights captured. Future bakes blend against this snapshot.";
            }
        }
        EditorGUILayout.HelpBox("Repeated Bake edits the same ramp without accumulating blend. Moving its footprint restores the previous footprint from the snapshot; exterior edits stay unchanged. Capture current heights before starting another ramp or keeping sculpt edits inside this ramp. Closing this window discards the snapshot.", MessageType.None);
        EditorGUI.BeginChangeCheck();
        Vector3 nextA = EditorGUILayout.Vector3Field("A (Terrain local XYZ)", a);
        Vector3 nextB = EditorGUILayout.Vector3Field("B (Terrain local XYZ)", b);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(this, "Edit Slope Endpoints"); a = nextA; b = nextB;
            if (TerrainError() == null) { if (baseline == null) CaptureBaseline(); hasPoints = true; }
        }
        matchEndpoints = EditorGUILayout.Toggle("Match A/B heights", matchEndpoints);
        using (new EditorGUI.DisabledScope(matchEndpoints)) angle = EditorGUILayout.FloatField("Slope angle (degrees)", angle);
        width = EditorGUILayout.FloatField("Core width (units)", width);
        sideBlend = EditorGUILayout.FloatField("Side blend (units)", sideBlend);
        endBlend = EditorGUILayout.FloatField("End blend (units)", endBlend);
        if (hasPoints)
        {
            float distance = new Vector2(b.x - a.x, b.z - a.z).magnitude;
            float rise = distance * Mathf.Tan(EffectiveAngle * Mathf.Deg2Rad);
            EditorGUILayout.LabelField($"Distance: {distance:F2}   Angle: {EffectiveAngle:F2}°   Rise: {rise:F2}");
            EditorGUILayout.LabelField($"Target B world height: {(terrain != null ? terrain.transform.position.y : 0) + a.y + rise:F2}");
        }
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.BeginHorizontal();
        saveFolder = EditorGUILayout.TextField("Save Path (folder)", saveFolder);
        if (GUILayout.Button("Browse", GUILayout.Width(65)))
        {
            string folder = EditorUtility.OpenFolderPanel("Terrain Slope output", Application.dataPath, "");
            if (!string.IsNullOrEmpty(folder))
            {
                string relative = FileUtil.GetProjectRelativePath(folder).Replace('\\', '/');
                if (relative == "Assets" || relative.StartsWith("Assets/", StringComparison.Ordinal)) saveFolder = relative;
                else status = "Choose a folder inside this project's Assets folder.";
            }
        }
        EditorGUILayout.EndHorizontal();
        saveName = EditorGUILayout.TextField("File name", saveName);
        if (EditorGUI.EndChangeCheck()) { EditorPrefs.SetString(PreferenceKey + ".Folder", saveFolder); EditorPrefs.SetString(PreferenceKey + ".Name", saveName); }
        EditorGUILayout.HelpBox("Bake saves over the configured TerrainData asset, retaining its GUID. Every scene using that output sees the changes. Save your scene separately.", MessageType.None);
        EditorGUILayout.LabelField("Optional prop roots", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add selected roots")) AddProps(false);
        if (GUILayout.Button("Add selected children")) AddProps(true);
        EditorGUILayout.EndHorizontal();
        for (int i = 0; i < props.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            props[i] = (Transform)EditorGUILayout.ObjectField(props[i], typeof(Transform), true);
            if (GUILayout.Button("X", GUILayout.Width(25))) { props.RemoveAt(i); i--; }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.HelpBox("Each root moves as one object at its pivot. Use selected children for a folder of independent props. Terrain trees follow the ground automatically.", MessageType.None);
        string error = Error();
        if (error != null) EditorGUILayout.HelpBox(error, MessageType.Warning);
        using (new EditorGUI.DisabledScope(error != null)) if (GUILayout.Button("Bake Slope", GUILayout.Height(34))) Bake();
        if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);
        EditorGUILayout.EndScrollView();
        if (GUI.changed) SceneView.RepaintAll();
    }

    private void AddProps(bool children)
    {
        foreach (Transform root in Selection.transforms)
            if (children) { foreach (Transform child in root) if (!props.Contains(child)) props.Add(child); }
            else if (!props.Contains(root)) props.Add(root);
    }

    private void Bake()
    {
        try
        {
            string error = Error();
            if (error != null) throw new InvalidOperationException(error);
            string folder = (saveFolder ?? "").Trim().Replace('\\', '/').TrimEnd('/');
            string name = (saveName ?? "").Trim();
            if (name.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 6);
            if ((folder != "Assets" && !folder.StartsWith("Assets/", StringComparison.Ordinal)) || folder.Contains("/../") || folder.EndsWith("/..", StringComparison.Ordinal) || !AssetDatabase.IsValidFolder(folder))
                throw new InvalidOperationException("Select an existing Save Path inside Assets.");
            if (string.IsNullOrWhiteSpace(name) || name == "." || name == ".." || name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 || name.Contains("/") || name.Contains("\\"))
                throw new InvalidOperationException("Enter a valid file name without a folder path.");
            float[,] heights = TerrainSlopeMath.Build(baseline.GetHeights(0, 0, baseline.heightmapResolution, baseline.heightmapResolution), baseline.size, a, b, EffectiveAngle, width, sideBlend, endBlend);
            var footprint = new TerrainSlopeMath.Footprint { a=a, b=b, width=width, sideBlend=sideBlend, endBlend=endBlend };
            TerrainSlopeMath.PreserveExterior(heights, terrain.terrainData.GetHeights(0,0,baseline.heightmapResolution,baseline.heightmapResolution), baseline.size, footprint, previousBake);
            string path = folder + "/" + name + ".asset";
            TerrainHeightOffsetWindow.ApplyPrepared(terrain, heights, TerrainHeightOffsetWindow.UniqueRoots(props), path);
            // Join the same Undo group as the Terrain write so undoing/rebaking keeps footprint tracking coherent.
            Undo.RecordObject(this, "Terrain Height Offset");
            previousBake = footprint;
            Undo.FlushUndoRecordObjects();
            status = "Slope saved: " + path + ". Ctrl+Z undoes the bake. Save your scene when ready.";
            SceneView.RepaintAll();
        }
        catch (Exception ex) { status = ex.Message; Debug.LogException(ex); }
    }

    private bool Hit(Vector2 mouse, out Vector3 local)
    {
        local = default;
        var collider = terrain.GetComponent<TerrainCollider>();
        if (collider == null || collider.terrainData != terrain.terrainData || !collider.Raycast(HandleUtility.GUIPointToWorldRay(mouse), out RaycastHit hit, float.MaxValue)) return false;
        local = Ground(hit.point - terrain.transform.position);
        return true;
    }

    private void OnSceneGUI(SceneView view)
    {
        if (TerrainError() != null) return;
        Event e = Event.current;
        int control = GUIUtility.GetControlID("TerrainSlopeDrag".GetHashCode(), FocusType.Passive);
        if (drawMode && !e.alt)
        {
            if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(control);
            if (e.type == EventType.MouseDown && e.button == 0 && Hit(e.mousePosition, out Vector3 start))
            {
                if (baseline == null) CaptureBaseline();
                Undo.RecordObject(this, "Draw Terrain Slope");
                a = b = start; hasPoints = true; dragging = true; GUIUtility.hotControl = control; e.Use();
            }
            if (dragging && e.type == EventType.MouseDrag && e.button == 0)
            {
                if (Hit(e.mousePosition, out Vector3 end)) b = end;
                e.Use(); Repaint(); view.Repaint();
            }
        }
        if (dragging && e.type == EventType.MouseUp && e.button == 0)
        {
            dragging = false; drawMode = false; GUIUtility.hotControl = 0; e.Use(); Repaint();
        }
        if (!hasPoints) return;
        Vector3 origin = terrain.transform.position;
        if (!drawMode && !dragging)
        {
            EditorGUI.BeginChangeCheck();
            Vector3 movedA = Handles.PositionHandle(origin + a, Quaternion.identity) - origin;
            Vector3 movedB = Handles.PositionHandle(origin + b, Quaternion.identity) - origin;
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(this, "Move Slope Endpoints");
                a = movedA; b = movedB; Repaint();
            }
        }
        Handles.Label(origin + a, "A (anchor)");
        Handles.Label(origin + b, matchEndpoints ? "B" : "B (direction / distance)");
        if (Error() != null) return;
        Vector3 direction = new Vector3(b.x - a.x, 0, b.z - a.z);
        float length = direction.magnitude;
        direction /= length;
        Vector3 side = new Vector3(-direction.z, 0, direction.x);
        float extent = width * 0.5f + sideBlend;
        // Coarse sampled surface preview: baseline + the same blend equation used by Bake.
        for (int lane = 0; lane <= 8; lane++)
        {
            var line = new Vector3[33];
            for (int i = 0; i < line.Length; i++)
            {
                Vector3 p = a + direction * Mathf.Lerp(-endBlend, length + endBlend, i / 32f) + side * Mathf.Lerp(-extent, extent, lane / 8f);
                float ground = baseline.GetInterpolatedHeight(p.x / baseline.size.x, p.z / baseline.size.z);
                p.y = TerrainSlopeMath.Height(new Vector2(p.x, p.z), ground, a, b, EffectiveAngle, width, sideBlend, endBlend);
                line[i] = origin + p + Vector3.up * .15f;
            }
            Handles.color = lane == 4 ? Color.yellow : Color.cyan;
            Handles.DrawAAPolyLine(lane == 4 ? 3 : 1, line);
        }
    }
}
#endif

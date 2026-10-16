using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
#if UNITY_EDITOR
using UnityEditor;
#endif

public partial class ProceduralDecorationPlacer
{
    public enum EdgeSide { Outside, Inside, Both }
    public bool useGroups;
    [Min(1)] public int minGroupSize = 3;
    [Min(1)] public int maxGroupSize = 7;
    [Min(0)] public float groupMinDistance = .75f;
    [Min(0)] public float groupMaxDistance = 3f;
    public bool useLayerEdge;
    public TerrainLayer edgeLayer;
    [Range(.01f, .99f)] public float edgeThreshold = .5f;
    public EdgeSide edgeSide = EdgeSide.Outside;
    [Min(0)] public float edgeMinDistance = .5f;
    [Min(0)] public float edgeMaxDistance = 3f;
    [Tooltip("Ground the rotated/scaled renderer geometry instead of placing only its pivot on Terrain. Moves the whole prefab root.")]
    public bool useRendererGrounding = true;
    [Tooltip("Vertical clearance from sampled Terrain after renderer grounding. Negative values deliberately sink the decoration.")]
    public float groundOffset;

    public sealed class PlannedDecoration
    {
        public GameObject prefab;
        public Vector3 position, scale;
        public Quaternion rotation;
        public Bounds worldBounds;
        public int group;
    }
    public sealed class PlacementPlan
    {
        public readonly List<PlannedDecoration> decorations = new List<PlannedDecoration>();
        public readonly List<Vector3> rejected = new List<Vector3>();
        public readonly List<Vector3> edgePoints = new List<Vector3>();
        public readonly Dictionary<string, int> rejections = new Dictionary<string, int>();
        public string error, report;
        public bool cancelled;
        public int seed, attempts, requested, groups;
        public void Reject(string reason, Vector3 position)
        {
            rejections.TryGetValue(reason, out int count); rejections[reason] = count + 1;
            if (rejected.Count < 500) rejected.Add(position);
        }
    }
    private struct Geometry
    {
        public Bounds bounds;
        public Matrix4x4 toRoot;
        public bool supportsGround;
    }

    // Planning never instantiates, moves or deletes scene objects, and restores global Random state.
    public PlacementPlan BuildPlan(bool chooseNewSeed = false, Func<float, bool> cancel = null)
    {
        var state = Random.state;
        try { return PlanInternal(chooseNewSeed, cancel); }
        finally { Random.state = state; }
    }

    private PlacementPlan PlanInternal(bool chooseNewSeed, Func<float, bool> cancel)
    {
        var plan = new PlacementPlan { requested = amount, seed = seed };
        if (!terrain || !terrain.terrainData) return PlanError(plan, "Assign a Terrain first.");
        string areaError = ValidateGenerationArea();
        if (areaError != null) return PlanError(plan, areaError);
        if (amount <= 0 || (useHeightLimit && minHeight > maxHeight)) return PlanError(plan, "Check Amount and the height range.");
        if (useGroups && (minGroupSize < 1 || maxGroupSize < minGroupSize || groupMinDistance < 0 || groupMaxDistance < groupMinDistance))
            return PlanError(plan, "Group ranges are invalid: Max must be at least Min.");
        if (useLayerEdge && (!edgeLayer || edgeMinDistance < 0 || edgeMaxDistance <= 0 || edgeMaxDistance < edgeMinDistance))
            return PlanError(plan, "Select an edge layer and a valid positive edge distance range.");
        float weight = 0;
        for (int i = 0; i < PrefabCount; i++) weight += GetRockPrefabWeight(i);
        if (weight <= 0) return PlanError(plan, "Add a prefab with a positive weight.");
        var data = terrain.terrainData;
        var size = data.size;
        if (size.x <= 0 || size.z <= 0) return PlanError(plan, "Terrain size is invalid.");
        var layers = data.terrainLayers;
        int edgeIndex = Array.IndexOf(layers, edgeLayer);
        if (useLayerEdge && edgeIndex < 0) return PlanError(plan, "The chosen edge layer is not painted on this Terrain.");
        var alpha = (useLayerEdge || useTerrainLayerFilter) ? data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight) : null;
        LayerEdgeIndex edges = null;
        if (useLayerEdge)
        {
            edges = new LayerEdgeIndex(alpha, edgeIndex, edgeThreshold, terrain.transform.position, size);
            if (edges.Points.Count == 0) return PlanError(plan, "No layer boundary crosses the threshold. Paint a path/patch or change Edge Threshold.");
            // A decimated contour is enough for drawing. The search retains the full contour.
            int step = Mathf.Max(1, edges.Points.Count / 2000);
            for (int i = 0; i < edges.Points.Count; i += step)
            {
                var point = edges.Points[i];
                point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
                plan.edgePoints.Add(point);
            }
        }
        var geometry = new Dictionary<GameObject, List<Geometry>>();
        for (int i = 0; i < PrefabCount; i++)
        {
            var prefab = GetPlacementPrefab(i);
            if (prefab && !geometry.ContainsKey(prefab)) geometry.Add(prefab, ReadGeometry(prefab));
        }
        Physics.SyncTransforms();
        CacheSceneObjects();
        if (chooseNewSeed) RandomizeSeed();
        plan.seed = seed;
        Random.InitState(seed);
        int maxAttempts = (int)Math.Min(1000000L, Math.Max((long)amount * Mathf.Max(1, attemptsPerDecoration), 1000L));
        int group = 0, groupStart = 0, groupTarget = 1, groupTrials = 0;
        while (plan.decorations.Count < amount && plan.attempts < maxAttempts)
        {
            if (plan.attempts % 128 == 0 && cancel != null && cancel((float)plan.attempts / maxAttempts))
            { plan.cancelled = true; return plan; }
            plan.attempts++;
            int members = plan.decorations.Count - groupStart;
            if (!useGroups || members >= groupTarget || (members > 0 && groupTrials >= Mathf.Max(32, attemptsPerDecoration) * groupTarget))
            { group++; groupStart = plan.decorations.Count; groupTrials = 0; members = 0; }
            if (members == 0) groupTarget = useGroups ? Random.Range(minGroupSize, maxGroupSize + 1) : 1;
            groupTrials++;
            Vector3 position;
            if (useGroups && members > 0)
            {
                float angle = Random.value * Mathf.PI * 2;
                float radius = Mathf.Sqrt(Mathf.Lerp(groupMinDistance * groupMinDistance, groupMaxDistance * groupMaxDistance, Random.value));
                position = plan.decorations[groupStart].position + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            }
            else position = SampleGenerationArea();
            if (areaMode == GenerationAreaMode.Polygon && !ContainsAreaPoint(position)) { plan.Reject("Outside polygon", position); continue; }
            float u = (position.x - terrain.transform.position.x) / size.x, v = (position.z - terrain.transform.position.z) / size.z;
            if (u < 0 || u > 1 || v < 0 || v > 1) { plan.Reject("Outside Terrain", position); continue; }
            position.y = data.GetInterpolatedHeight(u, v) + terrain.transform.position.y;
            if (useHeightLimit && (position.y < minHeight || position.y > maxHeight)) { plan.Reject("Height", position); continue; }
            if (data.GetSteepness(u, v) > maxSlope) { plan.Reject("Slope", position); continue; }
            if (!AllowsPaint(alpha, layers, u, v)) { plan.Reject("Excluded layer", position); continue; }
            if (useLayerEdge)
            {
                bool inside = PaintWeight(alpha, u, v, edgeIndex) >= edgeThreshold;
                float distance = edges.Distance(position);
                if ((edgeSide == EdgeSide.Inside && !inside) || (edgeSide == EdgeSide.Outside && inside) || distance < edgeMinDistance || distance > edgeMaxDistance)
                { plan.Reject("Layer edge band", position); continue; }
            }
            bool spaced = true;
            foreach (var other in plan.decorations)
            {
                float d = Vector2.Distance(new Vector2(position.x, position.z), new Vector2(other.position.x, other.position.z));
                bool sameGroup = useGroups && other.group == group;
                if (d < (sameGroup ? groupMinDistance : minDistance) || (sameGroup && d > groupMaxDistance)) { spaced = false; break; }
            }
            if (!spaced) { plan.Reject("Spacing / group diameter", position); continue; }
            var prefab = SelectRockPrefab(weight);
            if (!prefab) { plan.Reject("Invalid prefab", position); continue; }
            var rotation = randomRotation ? Quaternion.Euler(0, Random.Range(0f, 360f), 0) : prefab.transform.rotation;
            if (alignToTerrain) rotation = Quaternion.FromToRotation(Vector3.up, data.GetInterpolatedNormal(u, v)) *
                (randomRotation ? rotation : Quaternion.identity);
            var scale = prefab.transform.localScale;
            if (randomSize)
            {
                if (uniformScale) scale *= Random.Range(Mathf.Min(minUniformScale, maxUniformScale), Mathf.Max(minUniformScale, maxUniformScale));
                else scale = Vector3.Scale(scale, new Vector3(Random.Range(Mathf.Min(minScale.x, maxScale.x), Mathf.Max(minScale.x, maxScale.x)),
                    Random.Range(Mathf.Min(minScale.y, maxScale.y), Mathf.Max(minScale.y, maxScale.y)), Random.Range(Mathf.Min(minScale.z, maxScale.z), Mathf.Max(minScale.z, maxScale.z))));
            }
            var matrix = Matrix4x4.TRS(position, rotation, scale);
            var shapes = geometry[prefab];
            if (useRendererGrounding)
            {
                if (!TryGroundGeometry(shapes, matrix, out float adjustment))
                { plan.Reject("Renderer footprint / Terrain coverage", position); continue; }
                position.y += adjustment + groundOffset;
            }
            else position.y += groundOffset;
            matrix = Matrix4x4.TRS(position, rotation, scale);
            // Obstacle and area tests must use the final grounded root position.
            if (IsBlockedBySceneObject(position)) { plan.Reject("Scene obstacle", position); continue; }
            bool fits = shapes.Count > 0;
            var worldBounds = new Bounds();
            bool firstBounds = true;
            foreach (var shape in shapes)
            {
                if (!ContainsBounds(shape.bounds, transform.worldToLocalMatrix * matrix * shape.toRoot)) fits = false;
                if (firstBounds) { worldBounds = new Bounds((matrix * shape.toRoot).MultiplyPoint3x4(shape.bounds.center), Vector3.zero); firstBounds = false; }
                EncapsulateCorners(ref worldBounds, shape.bounds, matrix * shape.toRoot);
            }
            if (!fits) { plan.Reject("Area bounds / geometry", position); continue; }
            plan.decorations.Add(new PlannedDecoration { prefab = prefab, position = position, rotation = rotation, scale = scale, worldBounds = worldBounds, group = group });
        }
        var uniqueGroups = new HashSet<int>();
        foreach (var entry in plan.decorations) uniqueGroups.Add(entry.group);
        plan.groups = uniqueGroups.Count;
        plan.report = $"Planned {plan.decorations.Count}/{amount} decorations | Seed {plan.seed} | Attempts {plan.attempts}/{maxAttempts}";
        if (useGroups) plan.report += $"\n{plan.groups} groups. Tight filters or the final remainder can produce smaller groups.";
        foreach (var rejection in plan.rejections) plan.report += $"\n{rejection.Key}: {rejection.Value}";
        if (plan.decorations.Count < amount && plan.rejections.Count > 0)
        {
            string main = ""; int most = -1;
            foreach (var rejection in plan.rejections) if (rejection.Value > most) { main = rejection.Key; most = rejection.Value; }
            string tip;
            switch (main)
            {
                case "Excluded layer": tip = "Review excluded Terrain Layers and Exclude Layer Threshold."; break;
                case "Scene obstacle": tip = "Review Scene Object Layers, Check Radius and Padding. Large renderer bounds can block empty-looking ground."; break;
                case "Layer edge band": tip = "Widen the edge distance band, check Inside/Outside, or move the area over the painted boundary."; break;
                case "Spacing / group diameter": tip = "Reduce minimum spacing, increase group diameter, reduce group size or enlarge the area."; break;
                case "Area bounds / geometry": tip = "The full model must fit in the area, including Y. Expand the box or reduce object scale; prefabs need measurable geometry."; break;
                case "Renderer footprint / Terrain coverage": tip = "The renderer footprint needs Terrain coverage at all samples. Move inward from tile edges or check that the prefab has a Mesh/Skinned Renderer."; break;
                case "Slope": tip = "Increase Max Slope or choose flatter terrain."; break;
                case "Height": tip = "Adjust the world-space Min/Max Height range."; break;
                default: tip = "Move or resize the generation box over the assigned Terrain."; break;
            }
            plan.report += "\nMain rejection: " + tip;
        }
        return plan;
    }

    private static PlacementPlan PlanError(PlacementPlan plan, string message)
    { plan.error = message; plan.report = message; return plan; }

    private bool AllowsPaint(float[,,] alpha, TerrainLayer[] layers, float u, float v)
    {
        if (!useTerrainLayerFilter) return true;
        foreach (var rule in terrainLayerRules)
        {
            if (rule == null || !rule.excluded) continue;
            int index = Array.IndexOf(layers, rule.terrainLayer);
            if (index >= 0 && PaintWeight(alpha, u, v, index) >= excludeLayerThreshold) return false;
        }
        return true;
    }
    private static float PaintWeight(float[,,] alpha, float u, float v, int layer)
    {
        float x = Mathf.Clamp01(u) * (alpha.GetLength(1) - 1), z = Mathf.Clamp01(v) * (alpha.GetLength(0) - 1);
        int x0 = Mathf.FloorToInt(x), z0 = Mathf.FloorToInt(z), x1 = Mathf.Min(x0 + 1, alpha.GetLength(1) - 1), z1 = Mathf.Min(z0 + 1, alpha.GetLength(0) - 1);
        return Mathf.Lerp(Mathf.Lerp(alpha[z0, x0, layer], alpha[z0, x1, layer], x - x0), Mathf.Lerp(alpha[z1, x0, layer], alpha[z1, x1, layer], x - x0), z - z0);
    }
    private static List<Geometry> ReadGeometry(GameObject prefab)
    {
        var shapes = new List<Geometry>();
        foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
            shapes.Add(new Geometry { bounds = renderer.localBounds, toRoot = prefab.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix,
                supportsGround = renderer is MeshRenderer || renderer is SkinnedMeshRenderer });
        if (shapes.Count > 0) return shapes;
        foreach (var collider in prefab.GetComponentsInChildren<Collider>(true))
        {
            if (!collider.enabled) continue;
            Bounds bounds;
            if (collider is BoxCollider box) bounds = new Bounds(box.center, box.size);
            else if (collider is SphereCollider sphere) bounds = new Bounds(sphere.center, Vector3.one * sphere.radius * 2);
            else if (collider is CapsuleCollider capsule)
            {
                var extent = Vector3.one * capsule.radius * 2; extent[capsule.direction] = Mathf.Max(capsule.height, capsule.radius * 2);
                bounds = new Bounds(capsule.center, extent);
            }
            else if (collider is MeshCollider mesh && mesh.sharedMesh) bounds = mesh.sharedMesh.bounds;
            else continue;
            shapes.Add(new Geometry { bounds = bounds, toRoot = prefab.transform.worldToLocalMatrix * collider.transform.localToWorldMatrix, supportsGround = true });
        }
        return shapes;
    }

    private bool TryGroundGeometry(List<Geometry> shapes, Matrix4x4 rootToWorld, out float adjustment)
    {
        adjustment = float.NegativeInfinity;
        var size = terrain.terrainData.size;
        var origin = terrain.transform.position;
        foreach (var shape in shapes)
        {
            if (!shape.supportsGround) continue;
            if (!IsFinite(shape.bounds.center) || !IsFinite(shape.bounds.extents)) return false;
            var matrix = rootToWorld * shape.toRoot;
            // Both local Y faces handle rotated/mirrored child models. Nine samples per
            // face cover corners, edge midpoints and center without touching scene transforms.
            for (int face = 0; face < 2; face++)
                for (int x = 0; x < 3; x++)
                    for (int z = 0; z < 3; z++)
                    {
                        var point = matrix.MultiplyPoint3x4(new Vector3(
                            Mathf.Lerp(shape.bounds.min.x, shape.bounds.max.x, x * .5f),
                            face == 0 ? shape.bounds.min.y : shape.bounds.max.y,
                            Mathf.Lerp(shape.bounds.min.z, shape.bounds.max.z, z * .5f)));
                        float u = (point.x - origin.x) / size.x, v = (point.z - origin.z) / size.z;
                        if (!IsFinite(point) || u < 0 || u > 1 || v < 0 || v > 1) return false;
                        float ground = terrain.terrainData.GetInterpolatedHeight(u, v) + origin.y;
                        adjustment = Mathf.Max(adjustment, ground - point.y);
                    }
        }
        return !float.IsNegativeInfinity(adjustment) && !float.IsNaN(adjustment);
    }
    private static void EncapsulateCorners(ref Bounds result, Bounds bounds, Matrix4x4 matrix)
    {
        for (int i = 0; i < 8; i++) result.Encapsulate(matrix.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents,
            new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
    }

    private void GenerateInternal() => ApplyPlan(BuildPlan(randomizeSeedOnGenerate));

    public int GroundExistingDecorations(out int skipped)
    {
        skipped = 0; int moved = 0;
        if (!terrain || !terrain.terrainData || !rockContainer) return 0;
        foreach (Transform root in rockContainer)
        {
            if (root.GetComponentInChildren<Terrain>(true) || !TryGroundGeometry(ReadGeometry(root.gameObject), root.localToWorldMatrix, out float adjustment))
            { skipped++; continue; }
            float delta = adjustment + groundOffset;
            if (Mathf.Abs(delta) < .0001f) continue;
#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.RecordObject(root, "Ground Decoration Renderer");
#endif
            root.position += Vector3.up * delta;
#if UNITY_EDITOR
            if (!Application.isPlaying) PrefabUtility.RecordPrefabInstancePropertyModifications(root);
#endif
            moved++;
        }
        return moved;
    }

    public void ApplyPlan(PlacementPlan plan)
    {
        if (plan == null || plan.cancelled || !string.IsNullOrEmpty(plan.error))
        { Debug.LogWarning(plan?.error ?? "No completed placement plan.", this); return; }
        if (plan.decorations.Count == 0)
        { lastGenerationReport = plan.report + "\nExisting decorations kept: no valid positions."; Debug.LogWarning(lastGenerationReport, this); return; }
        // Check all sources before replacing previous output.
        foreach (var entry in plan.decorations) if (!entry.prefab) { Debug.LogWarning("A planned prefab is missing. Rebuild Preview.", this); return; }
        ClearRocks();
        if (!rockContainer)
        {
            var container = new GameObject("Generated Decorations");
            container.transform.SetParent(transform, false);
            rockContainer = container.transform;
#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(container, "Create Decoration Container");
#endif
        }
        foreach (var entry in plan.decorations)
        {
#if UNITY_EDITOR
            var go = Application.isPlaying ? Instantiate(entry.prefab) : (GameObject)PrefabUtility.InstantiatePrefab(entry.prefab, gameObject.scene);
#else
            var go = Instantiate(entry.prefab);
#endif
            go.transform.SetPositionAndRotation(entry.position, entry.rotation);
            go.transform.localScale = entry.scale;
            go.transform.SetParent(rockContainer, true);
            // A group is metadata in the hierarchy name, not a separate movable parent.
            if (useGroups) go.name = $"[Group {entry.group + 1:000}] {entry.prefab.name}";
#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(go, "Generate Decoration");
#endif
        }
        lastGenerationReport = plan.report.Replace("Planned", "Placed");
        Debug.Log(lastGenerationReport, this);
    }

    // Boundary samples are interpolated crossings of the selected paint threshold.
    // KD nearest search avoids scanning every path edge for every random trial.
    private sealed class LayerEdgeIndex
    {
        public readonly List<Vector3> Points = new List<Vector3>();
        private struct Segment { public Vector3 a, b; public Vector3 Center => (a + b) * .5f; }
        private sealed class Node { public Segment segment; public Bounds bounds; public Node left, right; }
        private readonly Node root;
        public LayerEdgeIndex(float[,,] alpha, int layer, float threshold, Vector3 origin, Vector3 size)
        {
            int w = alpha.GetLength(1), h = alpha.GetLength(0);
            var segments = new List<Segment>();
            var crossings = new List<Vector3>(4);
            for (int z = 0; z < h - 1; z++) for (int x = 0; x < w - 1; x++)
            {
                crossings.Clear();
                Crossing(x, z, x + 1, z); Crossing(x + 1, z, x + 1, z + 1);
                Crossing(x + 1, z + 1, x, z + 1); Crossing(x, z + 1, x, z);
                if (crossings.Count == 2) Add(crossings[0], crossings[1]);
                else if (crossings.Count == 4)
                {
                    // Resolve checkerboard cells using the bilinear center value.
                    bool centerInside = (alpha[z, x, layer] + alpha[z, x + 1, layer] + alpha[z + 1, x, layer] + alpha[z + 1, x + 1, layer]) * .25f >= threshold;
                    if (centerInside == (alpha[z, x, layer] >= threshold))
                    { Add(crossings[0], crossings[1]); Add(crossings[2], crossings[3]); }
                    else { Add(crossings[0], crossings[3]); Add(crossings[1], crossings[2]); }
                }
            }
            root = Build(segments.ToArray(), 0, segments.Count, 0);
            void Add(Vector3 a, Vector3 b)
            { segments.Add(new Segment { a = a, b = b }); Points.Add((a + b) * .5f); }
            void Crossing(int x, int z, int nx, int nz)
            {
                float a = alpha[z, x, layer], b = alpha[nz, nx, layer];
                if ((a >= threshold) == (b >= threshold)) return;
                float t = (threshold - a) / (b - a);
                crossings.Add(origin + new Vector3(Mathf.Lerp(x, nx, t) / (w - 1) * size.x, 0, Mathf.Lerp(z, nz, t) / (h - 1) * size.z));
            }
        }
        private static Node Build(Segment[] segments, int start, int count, int axis)
        {
            if (count == 0) return null;
            Array.Sort(segments, start, count, Comparer<Segment>.Create((a, b) => a.Center[axis].CompareTo(b.Center[axis])));
            int middle = start + count / 2;
            var node = new Node { segment = segments[middle], left = Build(segments, start, middle - start, 2 - axis), right = Build(segments, middle + 1, start + count - middle - 1, 2 - axis) };
            node.bounds = new Bounds(node.segment.a, Vector3.zero); node.bounds.Encapsulate(node.segment.b);
            if (node.left != null) node.bounds.Encapsulate(node.left.bounds);
            if (node.right != null) node.bounds.Encapsulate(node.right.bounds);
            return node;
        }
        public float Distance(Vector3 position)
        { float best = float.PositiveInfinity; Search(root, position, ref best); return Mathf.Sqrt(best); }
        private static void Search(Node node, Vector3 p, ref float best)
        {
            if (node == null || BoundsDistance(node.bounds, p) > best) return;
            var ab = new Vector2(node.segment.b.x - node.segment.a.x, node.segment.b.z - node.segment.a.z);
            var ap = new Vector2(p.x - node.segment.a.x, p.z - node.segment.a.z);
            float t = ab.sqrMagnitude > 0 ? Mathf.Clamp01(Vector2.Dot(ap, ab) / ab.sqrMagnitude) : 0;
            best = Mathf.Min(best, (ap - ab * t).sqrMagnitude);
            float left = node.left == null ? float.PositiveInfinity : BoundsDistance(node.left.bounds, p);
            float right = node.right == null ? float.PositiveInfinity : BoundsDistance(node.right.bounds, p);
            Search(left < right ? node.left : node.right, p, ref best);
            Search(left < right ? node.right : node.left, p, ref best);
        }
        private static float BoundsDistance(Bounds bounds, Vector3 p)
        {
            float dx = Mathf.Max(0, Mathf.Max(bounds.min.x - p.x, p.x - bounds.max.x));
            float dz = Mathf.Max(0, Mathf.Max(bounds.min.z - p.z, p.z - bounds.max.z));
            return dx * dx + dz * dz;
        }
    }
}

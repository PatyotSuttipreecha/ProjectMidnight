using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Level Design/Terrain Object Snapper")]
public sealed class TerrainObjectSnapper : MonoBehaviour
{
    public enum TargetMode { ObjectItself, DirectChildren, LeafDescendants }
    public enum SelectionMode { Auto, WholeObjects, DirectChildren, LeafDescendants }
    public enum HeightMode { Pivot, RendererBottom, ColliderBottom }
    public enum GroundSampling { Center, HighestOfNine, AverageOfNine }

    [System.Serializable]
    public sealed class Target
    {
        public Transform root;
        [Tooltip("DirectChildren moves each immediate child as a whole, preserving its model hierarchy. LeafDescendants is for folders containing independent leaf objects.")]
        public TargetMode mode = TargetMode.DirectChildren;
    }

    [Tooltip("Optional. When empty, find the active Terrain underneath each object.")]
    public Terrain terrain;
    public List<Target> targets = new List<Target>();
    [Tooltip("Auto: move immediate children when present, otherwise move the object itself. WholeObjects keeps each supplied hierarchy together.")]
    public SelectionMode selectionMode = SelectionMode.Auto;
    public HeightMode heightMode = HeightMode.RendererBottom;
    [Tooltip("World-space vertical gap above the terrain. Negative values sink objects into it.")]
    public float heightOffset;
    public bool includeInactive = true;
    public GroundSampling groundSampling = GroundSampling.HighestOfNine;

    public List<Transform> CollectTargets() => CollectTargets(targets);

    public List<Transform> CollectTargets(IEnumerable<Target> source)
    {
        var unique = new HashSet<Transform>();
        foreach (var entry in source)
        {
            if (entry == null || entry.root == null) continue;
            if (selectionMode == SelectionMode.Auto)
                CollectAuto(entry.root, unique);
            else if (selectionMode == SelectionMode.WholeObjects || entry.root.childCount == 0)
                unique.Add(entry.root);
            else if (selectionMode == SelectionMode.DirectChildren)
            {
                foreach (Transform child in entry.root) unique.Add(child);
            }
            else
            {
                foreach (var child in entry.root.GetComponentsInChildren<Transform>(true))
                    if (child.childCount == 0) unique.Add(child);
            }
        }

        var result = new List<Transform>();
        foreach (var candidate in unique)
        {
            if (!includeInactive && !candidate.gameObject.activeInHierarchy) continue;
            // A listed ancestor moves its entire hierarchy: never move a child twice.
            bool covered = false;
            for (var parent = candidate.parent; parent != null; parent = parent.parent)
                if (unique.Contains(parent) && (includeInactive || parent.gameObject.activeInHierarchy)) { covered = true; break; }
            if (!covered) result.Add(candidate);
        }
        return result;
    }

    private void CollectAuto(Transform node, HashSet<Transform> result)
    {
        if (!includeInactive && !node.gameObject.activeInHierarchy) return;
        // Preserve prefab/model roots, but traverse empty organizational folders.
        bool isObject = node.GetComponent<Renderer>() != null || node.GetComponent<Collider>() != null ||
                        node.GetComponent<LODGroup>() != null;
#if UNITY_EDITOR
        isObject |= UnityEditor.PrefabUtility.IsAnyPrefabInstanceRoot(node.gameObject);
#endif
        if (isObject || node.childCount == 0) result.Add(node);
        else foreach (Transform child in node) CollectAuto(child, result);
    }

    public bool TryGetPosition(Transform target, out Vector3 position) =>
        TryGetPlacement(target, out position, out _, null);

    public bool TryGetPlacement(Transform target, out Vector3 position, out Bounds bounds,
        List<Vector3> groundSamples)
    {
        bounds = default;
        groundSamples?.Clear();
        position = default;
        if (target == null) return false;
        position = target.position;
        // Never reposition the Terrain itself or a parent containing terrain geometry.
        if (target.GetComponentInChildren<Terrain>(true) != null) return false;
        Bounds combined = default;
        float bottom = position.y;
        bool found = false;
        if (heightMode == HeightMode.RendererBottom)
        {
            foreach (var renderer in target.GetComponentsInChildren<Renderer>(includeInactive))
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
                if (!renderer.enabled) continue;
                bottom = found ? Mathf.Min(bottom, renderer.bounds.min.y) : renderer.bounds.min.y;
                if (found) combined.Encapsulate(renderer.bounds); else combined = renderer.bounds;
                found = true;
            }
        }
        else if (heightMode == HeightMode.ColliderBottom)
        {
            foreach (var collider in target.GetComponentsInChildren<Collider>(includeInactive))
            {
                if (!collider.enabled || collider.isTrigger || !collider.gameObject.activeInHierarchy) continue;
                bottom = found ? Mathf.Min(bottom, collider.bounds.min.y) : collider.bounds.min.y;
                if (found) combined.Encapsulate(collider.bounds); else combined = collider.bounds;
                found = true;
            }
        }

        // Offset model pivots can be far from the actual visible geometry.
        Vector3 sample = found ? combined.center : position;
        bounds = found ? combined : new Bounds(position, Vector3.zero);
        if (!found && heightMode != HeightMode.Pivot) return false;
        float groundY;
        if (found && groundSampling != GroundSampling.Center)
        {
            float highest = float.NegativeInfinity, total = 0f;
            // Nine samples across the world-space bounds. All must have Terrain
            // coverage, including when a footprint crosses terrain tile borders.
            for (int x = 0; x < 3; x++)
                for (int z = 0; z < 3; z++)
                {
                    var point = new Vector3(Mathf.Lerp(combined.min.x, combined.max.x, x * .5f),
                        sample.y, Mathf.Lerp(combined.min.z, combined.max.z, z * .5f));
                    if (!TrySampleGround(point, out float y)) return false;
                    groundSamples?.Add(new Vector3(point.x, y, point.z));
                    highest = Mathf.Max(highest, y);
                    total += y;
                }
            groundY = groundSampling == GroundSampling.HighestOfNine ? highest : total / 9f;
        }
        else
        {
            if (!TrySampleGround(sample, out groundY)) return false;
            groundSamples?.Add(new Vector3(sample.x, groundY, sample.z));
        }
        position.y += groundY + heightOffset - bottom;
        return true;
    }

    private bool TrySampleGround(Vector3 sample, out float groundY)
    {
        groundY = 0;
        Terrain surface = terrain;
        if (surface == null)
        {
            foreach (var candidate in Terrain.activeTerrains)
                if (Contains(candidate, sample)) { surface = candidate; break; }
        }
        if (!Contains(surface, sample)) return false;
        groundY = surface.SampleHeight(sample) + surface.transform.position.y;
        return true;
    }

    private static bool Contains(Terrain surface, Vector3 position)
    {
        if (surface == null || surface.terrainData == null) return false;
        Vector3 local = position - surface.transform.position;
        Vector3 size = surface.terrainData.size;
        return local.x >= 0 && local.z >= 0 && local.x <= size.x && local.z <= size.z;
    }
}

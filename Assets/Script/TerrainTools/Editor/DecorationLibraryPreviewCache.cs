#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Mesh-only thumbnails: no prefab behaviours are instantiated/executed in the scene.
public sealed class DecorationLibraryPreviewCache : IDisposable
{
    private readonly Dictionary<int, Texture2D> images = new Dictionary<int, Texture2D>();
    private PreviewRenderUtility preview;
    private Material fallback;
    private struct Part { public Mesh mesh; public Matrix4x4 matrix; public Material[] materials; }

    public Texture2D Get(GameObject prefab)
    {
        if (!prefab) return null;
        if (images.TryGetValue(prefab.GetInstanceID(), out var cached)) return cached;
        var parts = new List<Part>();
        var excluded = new HashSet<Renderer>();
        foreach (var lod in prefab.GetComponentsInChildren<LODGroup>(true))
        {
            var levels = lod.GetLODs();
            for (int i = 1; i < levels.Length; i++) foreach (var renderer in levels[i].renderers) if (renderer) excluded.Add(renderer);
            if (levels.Length > 0) foreach (var renderer in levels[0].renderers) excluded.Remove(renderer);
        }
        var root = Matrix4x4.Scale(prefab.transform.localScale) * prefab.transform.worldToLocalMatrix;
        foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer.enabled || excluded.Contains(renderer)) continue;
            Mesh mesh = null;
            if (renderer is MeshRenderer)
            { var filter = renderer.GetComponent<MeshFilter>(); if (filter) mesh = filter.sharedMesh; }
            else if (renderer is SkinnedMeshRenderer skin) mesh = skin.sharedMesh;
            if (mesh) parts.Add(new Part { mesh = mesh, matrix = root * renderer.transform.localToWorldMatrix, materials = renderer.sharedMaterials });
        }
        if (parts.Count == 0) { images[prefab.GetInstanceID()] = null; return null; }
        var bounds = new Bounds(parts[0].matrix.MultiplyPoint3x4(parts[0].mesh.bounds.center), Vector3.zero);
        foreach (var part in parts)
            for (int i = 0; i < 8; i++) bounds.Encapsulate(part.matrix.MultiplyPoint3x4(part.mesh.bounds.center + Vector3.Scale(part.mesh.bounds.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
        if (preview == null)
        {
            preview = new PreviewRenderUtility(); preview.cameraFieldOfView = 30;
            preview.lights[0].intensity = 1.3f; preview.lights[0].transform.rotation = Quaternion.Euler(40, 35, 0);
            preview.lights[1].intensity = .8f; preview.lights[1].transform.rotation = Quaternion.Euler(340, 218, 0);
            preview.ambientColor = new Color(.35f, .35f, .35f);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader) fallback = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }
        float distance = Mathf.Max(.1f, bounds.extents.magnitude) / Mathf.Sin(15 * Mathf.Deg2Rad) * 1.15f;
        var camera = preview.camera;
        camera.transform.position = bounds.center + new Vector3(1, .65f, -1).normalized * distance;
        camera.transform.rotation = Quaternion.LookRotation(bounds.center - camera.transform.position);
        camera.nearClipPlane = Mathf.Max(.001f, distance * .001f); camera.farClipPlane = distance * 4 + 10;
        camera.clearFlags = CameraClearFlags.Color; camera.backgroundColor = new Color(.16f, .17f, .19f);
        bool began = false;
        try
        {
            preview.BeginStaticPreview(new Rect(0, 0, 128, 128)); began = true;
            foreach (var part in parts)
                for (int sub = 0; sub < part.mesh.subMeshCount; sub++)
                {
                    var material = part.materials.Length > 0 ? part.materials[Mathf.Min(sub, part.materials.Length - 1)] : fallback;
                    if (material) preview.DrawMesh(part.mesh, part.matrix, material, sub);
                }
            preview.Render(true);
            cached = preview.EndStaticPreview(); began = false;
            cached.hideFlags = HideFlags.HideAndDontSave;
        }
        catch (Exception e)
        {
            if (began) { var abandoned = preview.EndStaticPreview(); if (abandoned) UnityEngine.Object.DestroyImmediate(abandoned); }
            Debug.LogWarning("Decorations Library: mesh preview unavailable for " + prefab.name + ": " + e.Message);
            cached = null;
        }
        images[prefab.GetInstanceID()] = cached;
        return cached;
    }
    public void Dispose()
    {
        foreach (var image in images.Values) if (image) UnityEngine.Object.DestroyImmediate(image);
        images.Clear(); preview?.Cleanup(); preview = null;
        if (fallback) UnityEngine.Object.DestroyImmediate(fallback); fallback = null;
    }
}
#endif

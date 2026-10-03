using System.Linq;
using UnityEditor;
using UnityEngine;

public class M870RealisticImportValidator : AssetPostprocessor
{
    public const string Folder = "Assets/Prefab/Weapon/M870Realistic/";
    public const string PrefabPath = Folder + "Shotgun_M870_Realistic.prefab";
    private static bool queued;

    [InitializeOnLoadMethod]
    private static void AfterReload() => Queue();

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted,
        string[] moved, string[] movedFrom)
    {
        if (imported.Any(p => p.StartsWith(Folder))) Queue();
    }

    private static void Queue()
    {
        if (queued) return;
        queued = true;
        EditorApplication.delayCall += () => { queued = false; Validate(false); };
    }

    [MenuItem("Tools/Project Midnight/Validate Realistic M870")]
    public static void ValidateFromMenu() => Validate(true);

    public static bool Validate(bool report)
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { Queue(); return false; }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) return false;
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        bool changed = false;
        int triangles = 0;
        try
        {
            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length != 35) throw new System.InvalidOperationException("Expected 35 M870 mesh parts.");
            foreach (var filter in filters)
            {
                var meshPath = Folder + "Meshes/" + filter.name + ".obj";
                var mesh = AssetDatabase.LoadAllAssetsAtPath(meshPath).OfType<Mesh>().FirstOrDefault();
                if (mesh == null) throw new System.InvalidOperationException("Missing mesh: " + meshPath);
                if (filter.sharedMesh != mesh) { filter.sharedMesh = mesh; changed = true; }
                triangles += (int)mesh.GetIndexCount(0) / 3;
                if (mesh.uv.Length != mesh.vertexCount || mesh.tangents.Length != mesh.vertexCount)
                    throw new System.InvalidOperationException("UV or tangent data missing: " + filter.name);
                var material = filter.GetComponent<MeshRenderer>()?.sharedMaterial;
                if (material == null || material.shader == null || material.shader.name != "Universal Render Pipeline/Lit")
                    throw new System.InvalidOperationException("Missing URP material: " + filter.name);
                foreach (var property in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap" })
                    if (material.GetTexture(property) == null)
                        throw new System.InvalidOperationException("Missing " + property + ": " + filter.name);
            }
            foreach (var name in new[] { "Visual/Pump", "Visual/Bolt", "FirePoint", "RightHandGrip", "Visual/Pump/LeftHandGrip" })
                if (root.transform.Find(name) == null) throw new System.InvalidOperationException("Missing attachment: " + name);
            if (changed) PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            if (report || changed) Debug.Log("Realistic M870 validated: 35 meshes, " + triangles + " triangles, URP textures, UVs and tangents resolved.");
            return true;
        }
        catch (System.Exception error) { Debug.LogException(error); return false; }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}

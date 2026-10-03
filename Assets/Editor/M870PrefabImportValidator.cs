using System.Linq;
using UnityEditor;
using UnityEngine;

// Resolve imported OBJ mesh IDs through Unity rather than depending on importer versions.
public class M870PrefabImportValidator : AssetPostprocessor
{
    private const string Folder = "Assets/Prefab/Weapon/M870/";
    private const string PrefabPath = Folder + "Shotgun_M870.prefab";
    private static bool queued;

    [InitializeOnLoadMethod]
    private static void AfterReload() => Queue();

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted,
        string[] moved, string[] movedFrom)
    {
        if (imported.Any(p => p.StartsWith(Folder) &&
            (p.EndsWith(".obj") || p == PrefabPath))) Queue();
    }

    private static void Queue()
    {
        if (queued) return;
        queued = true;
        EditorApplication.delayCall += () => { queued = false; Validate(false); };
    }

    [MenuItem("Tools/Project Midnight/Validate M870 Prefab")]
    private static void ValidateFromMenu() => Validate(true);

    private static void Validate(bool report)
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { Queue(); return; }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) return;
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        bool changed = false;
        int triangleCount = 0;
        try
        {
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var meshPath = Folder + filter.name + ".obj";
                var mesh = AssetDatabase.LoadAllAssetsAtPath(meshPath).OfType<Mesh>().FirstOrDefault();
                if (mesh == null)
                {
                    Debug.LogError("M870 mesh has not imported: " + meshPath);
                    return;
                }
                if (filter.sharedMesh != mesh) { filter.sharedMesh = mesh; changed = true; }
                triangleCount += (int)mesh.GetIndexCount(0) / 3;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || renderer.sharedMaterial == null)
                {
                    Debug.LogError("M870 has a missing material: " + filter.name);
                    return;
                }
            }
            if (changed) PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            if (report || changed)
                Debug.Log("M870 prefab validated: " + triangleCount + " triangles, mesh and material references resolved.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}

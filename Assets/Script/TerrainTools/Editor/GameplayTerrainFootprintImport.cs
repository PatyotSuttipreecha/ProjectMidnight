using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Apply the irregular ProBuilder footprint with Unity's native holes API once.
// Later sculpting and hole painting remain the level designer's edits.
[InitializeOnLoad]
public sealed class GameplayTerrainFootprintImport : AssetPostprocessor
{
    private const string TerrainPath = "Assets/Scenes/Terrain/Scenes_Terrain_Gameplay.asset";
    private const string FootprintPath = "Assets/Scenes/Terrain/Scenes_Terrain_GameplayFootprint.bytes";
    private const string Completed = "GameplayTerrainFootprint:v1";
    private static bool queued;

    static GameplayTerrainFootprintImport() => Queue();

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted,
        string[] moved, string[] movedFrom)
    {
        if (imported.Contains(TerrainPath) || imported.Contains(FootprintPath)) Queue();
    }

    private static void Queue()
    {
        if (queued) return;
        queued = true;
        EditorApplication.delayCall += Apply;
    }

    private static void Apply()
    {
        queued = false;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            Queue();
            return;
        }
        var importer = AssetImporter.GetAtPath(TerrainPath);
        if (importer == null || importer.userData == Completed) return;
        var terrain = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainPath);
        var footprint = AssetDatabase.LoadAssetAtPath<TextAsset>(FootprintPath);
        if (terrain == null || footprint == null) return;
        var bytes = footprint.bytes;
        int resolution = terrain.holesResolution;
        if (bytes.Length < 4 || BitConverter.ToInt32(bytes, 0) != resolution ||
            bytes.Length != 4 + (resolution * resolution + 7) / 8)
        {
            Debug.LogError("Gameplay Terrain footprint does not match the heightmap.", terrain);
            return;
        }
        var surface = new bool[resolution, resolution];
        for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                int bit = z * resolution + x;
                surface[z, x] = (bytes[4 + bit / 8] & (1 << (bit % 8))) != 0;
            }
        terrain.enableHolesTextureCompression = false;
        terrain.SetHoles(0, 0, surface);
        EditorUtility.SetDirty(terrain);
        AssetDatabase.SaveAssetIfDirty(terrain);
        importer.userData = Completed;
        AssetDatabase.WriteImportSettingsIfDirty(TerrainPath);
        SceneView.RepaintAll();
    }
}

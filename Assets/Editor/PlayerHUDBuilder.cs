using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PlayerHUDBuilder
{
    public const string Path = "Assets/Prefab/UI/Resources/PlayerHUD.prefab";
    static PlayerHUDBuilder() => EditorApplication.delayCall += EnsurePrefab;
    private static void EnsurePrefab()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && !System.IO.File.Exists(Path)) Build();
    }
    [MenuItem("System Modification/Player HUD/Create or Rebuild Default Prefab")]
    public static void Build()
    {
        System.IO.Directory.CreateDirectory("Assets/Prefab/UI/Resources"); AssetDatabase.Refresh();
        var importer = AssetImporter.GetAtPath("Assets/Prefab/UI/Resources/PlayerHealthRing.png") as TextureImporter;
        if (importer != null) { importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.SaveAndReimport(); }
        var hud = PlayerHUD.CreateDefault();
        PrefabUtility.SaveAsPrefabAsset(hud.gameObject, Path); Object.DestroyImmediate(hud.gameObject); AssetDatabase.SaveAssets();
    }
    [MenuItem("System Modification/Player HUD/Open Prefab")]
    public static void Open() { EnsurePrefab(); AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<GameObject>(Path)); }
}

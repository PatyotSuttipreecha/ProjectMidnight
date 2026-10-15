using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ProjectAssetNaming
{
    public static string BuildName(string assetPath, string title, string type = null)
    {
        string[] folders = FolderSegments(assetPath);
        string prefix = string.Join("_", folders);
        bool containsType = false;
        foreach (string folder in folders)
            if (string.Equals(folder.TrimEnd('s', 'S'), (type ?? "").TrimEnd('s', 'S'), StringComparison.OrdinalIgnoreCase)) containsType = true;
        if (!string.IsNullOrWhiteSpace(type) && !containsType)
            prefix += "_" + type;
        string result = prefix + "_" + (string.IsNullOrWhiteSpace(title) ? "Unnamed" : title.Trim());
        foreach (char invalid in Path.GetInvalidFileNameChars()) result = result.Replace(invalid, '-');
        return result.Replace('\n', ' ').Replace('\r', ' ').TrimEnd('.', ' ');
    }
    private static string[] FolderSegments(string assetPath)
    {
        string directory = (Path.GetDirectoryName((assetPath ?? "").Replace('\\', '/')) ?? "Assets").Replace('\\', '/');
        return directory.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
            ? directory.Substring(7).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries) : new[] { "Assets" };
    }
    public static string ExtractTitle(string assetPath, string fileName)
    {
        string[] folders = FolderSegments(assetPath);
        // Longest match first: handles the full path, old two-folder names, and
        // singular/plural folder variants without stacking prefixes on each sync.
        for (int count = folders.Length; count > 0; count--)
        {
            string prefix = string.Join("_", folders, 0, count) + "_";
            if (fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return fileName.Substring(prefix.Length);
            string singular = string.Join("_", folders, 0, count - 1);
            singular = (singular.Length > 0 ? singular + "_" : "") + folders[count - 1].TrimEnd('s', 'S') + "_";
            if (fileName.StartsWith(singular, StringComparison.OrdinalIgnoreCase)) return fileName.Substring(singular.Length);
        }
        if (fileName.StartsWith("Prefab_", StringComparison.OrdinalIgnoreCase))
        {
            int second = fileName.IndexOf('_', "Prefab_".Length);
            if (second >= 0) return fileName.Substring(second + 1);
        }
        return fileName;
    }

    public static bool Rename(UnityEngine.Object asset, string desired)
    {
        if (asset == null || EditorApplication.isPlayingOrWillChangePlaymode || !AssetDatabase.IsMainAsset(asset)) return false;
        string path = AssetDatabase.GetAssetPath(asset);
        if (!path.StartsWith("Assets/", StringComparison.Ordinal)) return false;
        string current = Path.GetFileNameWithoutExtension(path);
        if (current == desired) return true;
        string candidate = Path.GetDirectoryName(path).Replace('\\', '/') + "/" + desired + Path.GetExtension(path);
        if (current.StartsWith(desired + " ", StringComparison.Ordinal) && int.TryParse(current.Substring(desired.Length + 1), out _) &&
            AssetDatabase.LoadMainAssetAtPath(candidate) != null) return true;
        if (!string.Equals(candidate, path, StringComparison.OrdinalIgnoreCase)) candidate = AssetDatabase.GenerateUniqueAssetPath(candidate);
        string error = AssetDatabase.RenameAsset(path, Path.GetFileNameWithoutExtension(candidate));
        if (!string.IsNullOrEmpty(error)) { Debug.LogWarning("Asset naming: " + error, asset); return false; }
        return true;
    }

    public static bool Sync(UnityEngine.Object asset)
    {
        if (asset == null || !AssetDatabase.IsMainAsset(asset) || EditorApplication.isPlayingOrWillChangePlaymode) return false;
        string path = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) || AssetDatabase.IsValidFolder(path)) return false;
        if (asset is PointNoteData note) return Rename(asset, BuildName(path, note.title, note.pointType.ToString()));
        if (asset is ItemSO item) return Rename(asset, BuildName(path, item.itemName, item.itemType.ToString()));
        if (asset is DocumentSO document) return Rename(asset, BuildName(path, document.title));
        if (asset is GameObject prefab && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            if (PrefabStageUtility.GetCurrentPrefabStage()?.assetPath == path)
            {
                Debug.LogWarning("Close Prefab Mode before syncing this prefab asset name.", prefab); return false;
            }
            string title = ExtractTitle(path, Path.GetFileNameWithoutExtension(path));
            return Rename(asset, BuildName(path, title));
        }
        // Assets without a semantic Title/Item Name retain their existing suffix.
        // Strip only this folder's generated prefix to keep repeated syncs idempotent.
        // Scripts depend on a matching class/file name; do not rename source files
        // without also performing a code-aware refactor.
        if (asset is MonoScript) return false;
        {
            string title = ExtractTitle(path, Path.GetFileNameWithoutExtension(path));
            return Rename(asset, BuildName(path, title));
        }
    }

    [MenuItem("System Modification/Naming/Sync Selected Assets")]
    public static void SyncSelected()
    {
        int renamed = 0, unchanged = 0, skipped = 0;
        foreach (var asset in Selection.objects)
        {
            string before = AssetDatabase.GetAssetPath(asset);
            if (Sync(asset))
            {
                string after = AssetDatabase.GetAssetPath(asset);
                if (before == after) unchanged++;
                else { renamed++; Debug.Log("Asset naming: " + before + " -> " + after, asset); }
            }
            else
            {
                skipped++;
                string reason = EditorApplication.isPlayingOrWillChangePlaymode ? "Exit Play Mode first." :
                    string.IsNullOrEmpty(before) ? "Select an asset in the Project window. Scene objects are not asset files." :
                    !before.StartsWith("Assets/", StringComparison.Ordinal) ? "Only files inside Assets can be renamed." :
                    AssetDatabase.IsValidFolder(before) ? "Select asset files rather than a folder. This command is not recursive." :
                    !AssetDatabase.IsMainAsset(asset) ? "Select the main asset file rather than a sub-asset." :
                    asset is MonoScript ? "Script filenames must match their classes. Rename scripts using a code refactor instead." :
                    asset is GameObject && PrefabStageUtility.GetCurrentPrefabStage()?.assetPath == before ? "Close this prefab's Prefab Mode first." :
                    "Rename failed; check the preceding Console message.";
                Debug.LogWarning("Asset naming: skipped " + asset.name + ". " + reason, asset);
            }
        }
        if (renamed > 0) AssetDatabase.SaveAssets();
        Debug.Log("Asset naming: " + renamed + " renamed, " + unchanged + " already named correctly, " + skipped + " skipped. Format: AllFoldersUnderAssets_(Type)_Title. Folder order and casing follow the current path. Embedded clips/meshes and source scripts are excluded.");
    }
    [MenuItem("System Modification/Naming/Sync Selected Assets", true)]
    private static bool Validate() => !EditorApplication.isPlayingOrWillChangePlaymode && Selection.objects.Length > 0;
}

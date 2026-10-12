using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ProjectAssetNaming
{
    public static string BuildName(string assetPath, string title, string type = null)
    {
        string directory = Path.GetDirectoryName(assetPath).Replace('\\', '/');
        string[] folders = directory.StartsWith("Assets/", StringComparison.Ordinal) ? directory.Substring(7).Split('/') : new[] { "Data" };
        string prefix = folders.Length > 1 ? folders[0] + "_" + folders[1] : folders[0];
        if (!string.IsNullOrWhiteSpace(type) && !string.Equals(folders[Math.Min(1, folders.Length - 1)].TrimEnd('s'), type.TrimEnd('s'), StringComparison.OrdinalIgnoreCase))
            prefix += "_" + type;
        string result = prefix + "_" + (string.IsNullOrWhiteSpace(title) ? "Unnamed" : title.Trim());
        foreach (char invalid in Path.GetInvalidFileNameChars()) result = result.Replace(invalid, '-');
        return result.Replace('\n', ' ').Replace('\r', ' ').TrimEnd('.', ' ');
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
        string path = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(path)) return false;
        if (asset is PointNoteData note) return Rename(asset, BuildName(path, note.title, note.pointType.ToString()));
        if (asset is ItemSO item) return Rename(asset, BuildName(path, item.itemName, item.itemType.ToString()));
        if (asset is DocumentSO document) return Rename(asset, BuildName(path, document.title));
        if (asset is GameObject prefab && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            if (PrefabStageUtility.GetCurrentPrefabStage()?.assetPath == path)
            {
                Debug.LogWarning("Close Prefab Mode before syncing this prefab asset name.", prefab); return false;
            }
            string title = prefab.name;
            string prefix = BuildName(path, "");
            prefix = prefix.Substring(0, prefix.Length - "Unnamed".Length);
            if (title.StartsWith(prefix, StringComparison.Ordinal)) title = title.Substring(prefix.Length);
            else if (title.StartsWith("Prefab_", StringComparison.Ordinal))
            {
                int second = title.IndexOf('_', "Prefab_".Length);
                if (second >= 0) title = title.Substring(second + 1);
            }
            return Rename(asset, BuildName(path, title));
        }
        return false;
    }

    [MenuItem("System Modification/Naming/Sync Selected Assets")]
    public static void SyncSelected()
    {
        int synced = 0;
        foreach (var asset in Selection.objects) if (Sync(asset)) synced++;
        Debug.Log("Asset naming: synced " + synced + " selected assets. Supported: ItemSO, DocumentSO, EvidenceSO, PointNoteData and prefab assets.");
    }
    [MenuItem("System Modification/Naming/Sync Selected Assets", true)]
    private static bool Validate() => !EditorApplication.isPlayingOrWillChangePlaymode && Selection.objects.Length > 0;
}

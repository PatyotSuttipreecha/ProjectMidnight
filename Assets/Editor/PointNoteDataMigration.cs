using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PointNoteDataMigration
{
    public static void ImportAll()
    {
        var temporary = new GameObject("TempNoteScript", typeof(PointNote));
        string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(temporary.GetComponent<PointNote>()));
        UnityEngine.Object.DestroyImmediate(temporary);
        string scriptGuid = AssetDatabase.AssetPathToGUID(scriptPath);
        int count = 0, skipped = 0;
        try
        {
            var paths = Directory.GetFiles("Assets", "*.prefab", SearchOption.AllDirectories);
            foreach (var path in paths)
            {
                string normalized = path.Replace('\\', '/');
                if (!File.ReadAllText(path).Contains(scriptGuid)) continue;
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null && stage.assetPath == normalized) { skipped++; continue; }
                EditorUtility.DisplayProgressBar("Import Note Data", normalized, .25f);
                var root = PrefabUtility.LoadPrefabContents(normalized);
                try { PointNoteDataTools.ImportMany(root.GetComponentsInChildren<PointNote>(true)); PrefabUtility.SaveAsPrefabAsset(root, normalized); count++; }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (var path in Directory.GetFiles("Assets", "*.unity", SearchOption.AllDirectories))
            {
                string normalized = path.Replace('\\', '/');
                if (!File.ReadAllText(path).Contains(scriptGuid)) continue;
                var scene = SceneManager.GetSceneByPath(normalized); bool opened = !scene.IsValid() || !scene.isLoaded;
                if (!opened && scene.isDirty) { skipped++; Debug.LogWarning("Import skipped unsaved scene: " + normalized); continue; }
                EditorUtility.DisplayProgressBar("Import Note Data", normalized, .75f);
                if (opened) scene = EditorSceneManager.OpenScene(normalized, OpenSceneMode.Additive);
                try
                {
                    var points = new System.Collections.Generic.List<PointNote>();
                    foreach (var root in scene.GetRootGameObjects()) points.AddRange(root.GetComponentsInChildren<PointNote>(true));
                    PointNoteDataTools.ImportMany(points);
                    if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Cannot save imported scene: " + normalized);
                    count++;
                }
                finally { if (opened && !scene.isDirty) EditorSceneManager.CloseScene(scene, true); }
            }
        }
        catch (Exception exception) { Debug.LogException(exception); }
        finally { AssetDatabase.SaveAssets(); EditorUtility.ClearProgressBar(); }
        Debug.Log("Note Data import: " + count + " saved scenes/prefabs processed; " + skipped + " open/unsaved assets skipped. Use Import Notes from Open Scenes for unsaved work, then save normally.");
    }
}

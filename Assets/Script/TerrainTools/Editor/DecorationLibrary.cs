#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[CreateAssetMenu(menuName = "System Modification/TerrainTools/Decorations Library", fileName = "Decorations Library")]
public sealed class DecorationLibrary : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        // GUIDs resolve moved assets without eagerly loading every prefab and its textures.
        public string guid;
        public string label;
        public string category = "Uncategorized";
        public string tags;
        public bool favorite;
    }
    public List<Entry> entries = new List<Entry>();
    public List<DefaultAsset> sourceFolders = new List<DefaultAsset>();
}

public static class DecorationLibraryUtility
{
    public const string DefaultPath = "Assets/Script/TerrainTools/Editor/Libraries/THUNG Decorations Library.asset";

    public static GameObject Resolve(string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid ?? "");
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }
    public static bool Matches(DecorationLibrary.Entry entry, string search)
    {
        string path = AssetDatabase.GUIDToAssetPath(entry.guid ?? "");
        string text = $"{entry.label} {entry.category} {entry.tags} {Path.GetFileNameWithoutExtension(path)} {path}";
        return string.IsNullOrWhiteSpace(search) || search.Split((char[])null, StringSplitOptions.RemoveEmptyEntries)
            .All(token => text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
    }
    public static string GuessCategory(string name)
    {
        name = name.ToLowerInvariant();
        if (Has("tree", "bush", "grass", "flower", "plant", "cactus", "leaf", "fern")) return "Vegetation";
        if (Has("coral", "pearl", "shell")) return "Underwater";
        if (Has("rock", "stone")) return "Rocks";
        if (Has("tent", "bag", "barrel", "box", "camp", "canopy", "flag", "lantern", "sleeping")) return "Camp / Props";
        if (Has("wall", "gate", "door", "bridge", "pillar", "building", "ruin")) return "Buildings / Ruins";
        return "Uncategorized";
        bool Has(params string[] words) => words.Any(name.Contains);
    }
    public static int SetCategory(DecorationLibrary library, IEnumerable<string> guids, string category)
    {
        if (!library || guids == null || string.IsNullOrWhiteSpace(category)) return 0;
        category = category.Trim();
        var selected = new HashSet<string>(guids);
        var changed = library.entries.Where(e => e != null && selected.Contains(e.guid) && e.category != category).ToArray();
        if (changed.Length == 0) return 0;
        Undo.RecordObject(library, "Change Selected Decoration Categories");
        foreach (var entry in changed) entry.category = category;
        EditorUtility.SetDirty(library);
        return changed.Length;
    }
    public static int AddAssets(DecorationLibrary library, IEnumerable<Object> assets, Func<float, bool> cancel = null)
    {
        var paths = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var asset in assets.Where(a => a))
        {
            string path = AssetDatabase.GetAssetPath(asset);
            if (AssetDatabase.IsValidFolder(path))
                foreach (string guid in AssetDatabase.FindAssets("t:GameObject", new[] { path })) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            else if (asset is GameObject go)
            {
                if (!EditorUtility.IsPersistent(go))
                {
                    var root = PrefabUtility.GetNearestPrefabInstanceRoot(go);
                    go = root ? PrefabUtility.GetCorrespondingObjectFromSource(root) : null;
                }
                if (go) paths.Add(AssetDatabase.GetAssetPath(go));
            }
        }
        var seen = new HashSet<string>(library.entries.Where(e => e != null).Select(e => e.guid));
        var additions = new List<DecorationLibrary.Entry>();
        int processed = 0;
        foreach (string path in paths)
        {
            if (cancel != null && cancel((float)processed++ / Mathf.Max(1, paths.Count))) return 0;
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid) || seen.Contains(guid)) continue;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab || !PrefabUtility.IsPartOfPrefabAsset(prefab)) continue;
            additions.Add(new DecorationLibrary.Entry { guid = guid, category = GuessCategory(Path.GetFileNameWithoutExtension(path)) });
            seen.Add(guid);
        }
        if (additions.Count == 0) return 0;
        Undo.RecordObject(library, "Add Decorations to Library");
        library.entries.AddRange(additions); EditorUtility.SetDirty(library);
        return additions.Count;
    }
    public static bool IsScenePlacer(ProceduralDecorationPlacer placer) => placer && !EditorUtility.IsPersistent(placer) &&
        placer.gameObject.scene.IsValid() && placer.gameObject.scene.isLoaded && !EditorSceneManager.IsPreviewScene(placer.gameObject.scene);

    public static int AddToPlacer(ProceduralDecorationPlacer placer, IEnumerable<string> guids)
    {
        if (!IsScenePlacer(placer) || EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null) return 0;
        if (placer.placementData) return DecorationPlacementDataEditor.Add(placer.placementData, guids.Distinct().Select(Resolve));
        var prefabs = new List<GameObject>(placer.rockPrefabs ?? Array.Empty<GameObject>());
        var weights = new List<float>();
        for (int i = 0; i < prefabs.Count; i++) weights.Add(placer.rockPrefabPercentages != null && i < placer.rockPrefabPercentages.Length ? placer.rockPrefabPercentages[i] : 100);
        int added = 0;
        foreach (string guid in guids.Distinct())
        {
            var prefab = Resolve(guid);
            if (!prefab || !PrefabUtility.IsPartOfPrefabAsset(prefab) || prefabs.Contains(prefab)) continue;
            prefabs.Add(prefab); weights.Add(100); added++;
        }
        if (added == 0) return 0;
        Undo.RecordObject(placer, "Add Library Decorations to Placer");
        placer.rockPrefabs = prefabs.ToArray(); placer.rockPrefabPercentages = weights.ToArray();
        PrefabUtility.RecordPrefabInstancePropertyModifications(placer); EditorUtility.SetDirty(placer);
        EditorSceneManager.MarkSceneDirty(placer.gameObject.scene);
        return added;
    }
}

[CustomEditor(typeof(DecorationLibrary))]
public sealed class DecorationLibraryInspector : Editor
{
    private bool folders;
    public override void OnInspectorGUI()
    {
        var library = (DecorationLibrary)target;
        EditorGUILayout.LabelField("Decorations Library", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"{library.entries.Count} entries • GUID references • Original prefabs preserved", EditorStyles.wordWrappedLabel);
        if (GUILayout.Button("Open Library Browser", GUILayout.Height(30))) DecorationLibraryWindow.OpenLibrary(library);
        folders = EditorGUILayout.Foldout(folders, "Source folders", true);
        if (folders)
        {
            serializedObject.Update(); EditorGUILayout.PropertyField(serializedObject.FindProperty("sourceFolders"), true); serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("Scan these folders from the Library Browser to add new prefabs. Refresh keeps existing categories, tags and favorites.", MessageType.None);
        }
    }
}
#endif

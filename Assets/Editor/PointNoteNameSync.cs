using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class PointNoteNameSync
{
    private struct Snapshot
    {
        public string name, title, prefix;
        public PointNote.PointType type;
        public bool enabled;
    }
    private static readonly Dictionary<int, Snapshot> snapshots = new Dictionary<int, Snapshot>();
    private static bool queued, resetBaseline = true;

    static PointNoteNameSync()
    {
        EditorApplication.hierarchyChanged += Queue;
        PointNote.EditorChangeRequested += Queue;
        Undo.undoRedoPerformed += () => { resetBaseline = true; Queue(); };
        EditorApplication.playModeStateChanged += _ => { resetBaseline = true; Queue(); };
        Queue();
    }
    private static void Queue()
    {
        if (queued) return;
        queued = true;
        EditorApplication.delayCall += Sync;
    }
    public static string HierarchyName(PointNote.PointType type, string title)
    {
        string clean = string.IsNullOrWhiteSpace(title) ? "Point Note" : title.Trim();
        return "[" + type + "] " + clean.Replace('\n', ' ').Replace('\r', ' ').Replace('/', '-');
    }
    public static string ResolvePrefix(PointNote note, string previousTitle = null)
    {
        if (!string.IsNullOrWhiteSpace(note.hierarchyNamePrefix)) return note.hierarchyNamePrefix;
        string sourceName = note.gameObject.name;
        if (!sourceName.StartsWith("Prefab_", StringComparison.Ordinal))
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(note.gameObject);
            if (source != null) sourceName = source.name;
            else
            {
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null && stage.prefabContentsRoot == note.gameObject)
                    sourceName = System.IO.Path.GetFileNameWithoutExtension(stage.assetPath);
            }
        }
        if (!sourceName.StartsWith("Prefab_", StringComparison.Ordinal)) return "";
        foreach (string title in new[] { previousTitle, note.title })
            if (!string.IsNullOrWhiteSpace(title) && sourceName.EndsWith("_" + title, StringComparison.Ordinal))
                return sourceName.Substring(0, sourceName.Length - title.Length);
        int separator = sourceName.LastIndexOf('_');
        return separator > 0 ? sourceName.Substring(0, separator + 1) : "";
    }
    public static string HierarchyName(PointNote note, string previousTitle = null)
    {
        string prefix = ResolvePrefix(note, previousTitle);
        return string.IsNullOrEmpty(prefix) ? HierarchyName(note.pointType, note.title) :
            prefix + TitleFromName(note.title).Replace('\n', ' ').Replace('\r', ' ').Replace('/', '-');
    }
    public static string DataAssetName(string path, PointNote.PointType type, string title)
    {
        return ProjectAssetNaming.BuildName(path, string.IsNullOrWhiteSpace(title) ? "Point Note" : title, type.ToString());
    }
    public static string TitleFromName(string name)
    {
        string clean = (name ?? "").Trim();
        if (clean.StartsWith("[", StringComparison.Ordinal))
        {
            int end = clean.IndexOf(']');
            if (end > 1)
            {
                string prefix = clean.Substring(1, end - 1).Split('|')[0].Trim();
                if (Enum.TryParse(prefix, out PointNote.PointType type) && Enum.IsDefined(typeof(PointNote.PointType), type))
                    clean = clean.Substring(end + 1).Trim();
            }
        }
        return string.IsNullOrWhiteSpace(clean) ? "Point Note" : clean;
    }
    public static void SyncDataAssetName(PointNoteData data)
    {
        if (data != null) ProjectAssetNaming.Sync(data);
    }
    private static void Sync()
    {
        queued = false;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var alive = new HashSet<int>();
        foreach (var note in UnityEngine.Object.FindObjectsByType<PointNote>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (EditorUtility.IsPersistent(note) || !note.gameObject.scene.IsValid()) continue;
            int id = note.GetInstanceID(); alive.Add(id);
            if (!resetBaseline && note.autoNameInHierarchy && snapshots.TryGetValue(id, out var previous) && previous.enabled)
            {
                bool titleChanged = previous.title != note.title || previous.type != note.pointType || previous.prefix != note.hierarchyNamePrefix;
                bool nameChanged = previous.name != note.gameObject.name;
                if (nameChanged && !titleChanged)
                {
                    string prefix = ResolvePrefix(note, previous.title);
                    string rawName = note.gameObject.name;
                    string title = TitleFromName(!string.IsNullOrEmpty(prefix) && rawName.StartsWith(prefix, StringComparison.Ordinal) ? rawName.Substring(prefix.Length) : rawName);
                    if (note.title != title)
                    {
                        Undo.RecordObject(note.data != null ? (UnityEngine.Object)note.data : note, "Sync Point Note Title");
                        note.title = title;
                        if (note.data != null) EditorUtility.SetDirty(note.data);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(note);
                        EditorUtility.SetDirty(note);
                    }
                }
                if (nameChanged || titleChanged)
                {
                    SyncDataAssetName(note.data);
                    string prefix = ResolvePrefix(note, previous.title);
                    if (!string.IsNullOrEmpty(prefix) && note.hierarchyNamePrefix != prefix)
                    {
                        Undo.RecordObject(note, "Preserve Note Name Prefix");
                        note.hierarchyNamePrefix = prefix;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(note);
                    }
                    string desired = HierarchyName(note, previous.title);
                    if (note.gameObject.name != desired)
                    {
                        Undo.RecordObject(note.gameObject, "Sync Point Note Name");
                        note.gameObject.name = desired;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(note.gameObject);
                    }
                    EditorSceneManager.MarkSceneDirty(note.gameObject.scene);
                    SceneView.RepaintAll();
                }
            }
            snapshots[id] = new Snapshot { name = note.gameObject.name, title = note.title, prefix = note.hierarchyNamePrefix, type = note.pointType, enabled = note.autoNameInHierarchy };
        }
        foreach (int id in new List<int>(snapshots.Keys)) if (!alive.Contains(id)) snapshots.Remove(id);
        resetBaseline = false;
    }
}

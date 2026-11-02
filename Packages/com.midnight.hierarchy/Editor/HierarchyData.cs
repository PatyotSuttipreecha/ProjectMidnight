using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Midnight.Hierarchy
{
    [Serializable]
    internal class Decoration
    {
        public string id;
        public Color color = Color.clear;
        public string icon = "";
        public bool divider;
    }

    [Serializable]
    internal class PaletteEntry
    {
        public string name = "New style";
        public Color color = new Color(.25f, .55f, .9f, .2f);
        // Asset GUIDs survive moves. Built-in icons use the builtin: prefix.
        public string icon = "builtin:GameObject Icon";
    }

    [Serializable]
    internal class PaletteFile
    {
        public List<PaletteEntry> entries = new List<PaletteEntry>();
    }

    [FilePath("ProjectSettings/MidnightHierarchy.asset", FilePathAttribute.Location.ProjectFolder)]
    internal class HierarchyData : ScriptableSingleton<HierarchyData>
    {
        public bool enabled = true;
        public bool colors = true, icons = true, lines = true, stripes = true;
        public bool components = true, activeToggle = true, shortcuts = true, minimal;
        public bool autoGroupColors = true;
        [Range(.1f, 1f)] public float colorStrength = .65f;
        public bool gradientColors = true;
        [Range(0, 1)] public float gradientEndOpacity = .08f;
        [Range(.1f, 1)] public float childColorStrength = .5f;
        public List<Decoration> decorations = new List<Decoration>();
        public List<string> objects = new List<string>();
        public List<string> scenes = new List<string>();
        public List<PaletteEntry> palette = new List<PaletteEntry>
        {
            new PaletteEntry { name = "Systems", icon = "builtin:SettingsIcon" },
            new PaletteEntry { name = "Gameplay", color = new Color(.2f,.8f,.4f,.2f) },
            new PaletteEntry { name = "UI", color = new Color(.8f,.4f,.8f,.2f), icon = "builtin:Canvas Icon" }
        };
        [NonSerialized] private Dictionary<string, Decoration> index;

        public void Persist()
        {
            index = null;
            Save(true);
            HierarchyOverlay.Invalidate();
            EditorApplication.RepaintHierarchyWindow();
        }

        public Decoration Find(string id)
        {
            if (index == null)
            {
                index = new Dictionary<string, Decoration>();
                foreach (var entry in decorations) index[entry.id] = entry;
            }
            return index.TryGetValue(id, out var result) ? result : null;
        }

        public static string Id(GameObject go)
        {
            if (!go) return null;
            var stage = PrefabStageUtility.GetPrefabStage(go);
            if (stage != null)
            {
                if (stage.scene.isDirty) return null;
                // Map the preview scene object to its saved prefab asset, without adding components.
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(stage.assetPath);
                if (!root) return null;
                var indices = new Stack<int>();
                for (var t = go.transform; t != stage.prefabContentsRoot.transform; t = t.parent)
                {
                    if (!t || !t.parent) return null;
                    indices.Push(t.GetSiblingIndex());
                }
                var assetTransform = root.transform;
                while (indices.Count > 0)
                {
                    int index = indices.Pop();
                    if (index >= assetTransform.childCount) return null;
                    assetTransform = assetTransform.GetChild(index);
                }
                // Newly added children in Prefab Mode need saving before they have an asset ID.
                if (assetTransform.name != go.name) return null;
                return GlobalObjectId.GetGlobalObjectIdSlow(assetTransform.gameObject).ToString();
            }
            if (!go.scene.IsValid() || string.IsNullOrEmpty(go.scene.path)) return null;
            return GlobalObjectId.GetGlobalObjectIdSlow(go).ToString();
        }

        public static GameObject Resolve(string id)
        {
            return GlobalObjectId.TryParse(id, out var global)
                ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(global) as GameObject : null;
        }

        public Decoration For(GameObject go)
        {
            var id = Id(go);
            var own = id == null ? null : Find(id);
            if (own != null) return own;
            var source = PrefabUtility.GetCorrespondingObjectFromSource(go);
            if (source)
            {
                var sourceId = GlobalObjectId.GetGlobalObjectIdSlow(source).ToString();
                return Find(sourceId);
            }
            if (autoGroupColors && go.name.TrimStart().StartsWith("===", StringComparison.Ordinal))
            {
                string label = go.name.ToLowerInvariant();
                Color color = label.Contains("light") ? new Color(.75f,.57f,.15f) :
                    label.Contains("character") || label.Contains("enemy") ? new Color(.65f,.24f,.22f) :
                    label.Contains("interaction") ? new Color(.22f,.52f,.35f) :
                    label.Contains("environment") ? new Color(.25f,.4f,.58f) : new Color(.4f,.33f,.58f);
                return new Decoration { color = color, divider = true, icon = "builtin:Folder Icon" };
            }
            return null;
        }

        public void Apply(GameObject[] targets, PaletteEntry style, bool divider = false)
        {
            if (EditorApplication.isPlaying) return;
            var valid = new List<string>();
            foreach (var go in targets)
            {
                var id = Id(go);
                if (id != null) valid.Add(id);
            }
            if (valid.Count != targets.Length)
                EditorUtility.DisplayDialog("Save scene first", "Objects in unsaved scenes cannot keep persistent styles. Save the scene and try again.", "OK");
            if (valid.Count == 0) return;
            Undo.RecordObject(this, "Style hierarchy objects");
            foreach (var id in valid)
            {
                decorations.RemoveAll(x => x.id == id);
                if (style != null) decorations.Add(new Decoration { id = id, color = style.color, icon = style.icon, divider = divider });
            }
            Persist();
        }

        public int ApplyColor(GameObject[] roots, Color color, bool recursive)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return 0;
            var objects = new HashSet<GameObject>();
            foreach (var root in roots)
            {
                if (!root) continue;
                if (recursive)
                    foreach (var child in root.GetComponentsInChildren<Transform>(true)) objects.Add(child.gameObject);
                else objects.Add(root);
            }
            var changes = new List<Decoration>();
            bool invalid = false;
            foreach (var go in objects)
            {
                string id = Id(go);
                if (id == null) { invalid = true; continue; }
                var old = For(go);
                changes.Add(new Decoration { id = id, color = color, icon = old != null ? old.icon : "", divider = old != null && old.divider });
            }
            if (invalid) EditorUtility.DisplayDialog("Save scene or prefab first", "Some objects do not have saved IDs yet and were skipped. Save the scene or Prefab Mode changes before applying colors.", "OK");
            if (changes.Count == 0) return 0;
            Undo.RecordObject(this, recursive ? "Color hierarchy recursively" : "Color hierarchy objects");
            enabled = colors = true;
            var changedIds = new HashSet<string>();
            foreach (var change in changes) changedIds.Add(change.id);
            decorations.RemoveAll(entry => changedIds.Contains(entry.id));
            decorations.AddRange(changes);
            Persist();
            return changes.Count;
        }

        public static Texture Icon(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (key.StartsWith("editor:", StringComparison.Ordinal)) return HierarchyIconPicker.Load(key);
            if (key.StartsWith("builtin:", StringComparison.Ordinal)) return EditorGUIUtility.IconContent(key.Substring(8)).image;
            return AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(key));
        }
    }
}

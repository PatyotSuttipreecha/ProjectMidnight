using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Midnight.Hierarchy
{
    internal class HierarchyIconPicker : EditorWindow
    {
        internal sealed class Entry { public string key, name; }
        private static readonly List<Entry> builtins = new List<Entry>();
        private static readonly Dictionary<string, Texture> textures = new Dictionary<string, Texture>();
        private static AssetBundle bundle;
        private static bool scanned;
        private readonly List<Entry> project = new List<Entry>();
        private readonly List<Entry> filtered = new List<Entry>();
        private Action<string> selected;
        private string search = "";
        private int tab;
        private Vector2 scroll;
        internal static void Open(Action<string> onSelected)
        {
            var window = CreateInstance<HierarchyIconPicker>();
            window.selected = onSelected;
            window.titleContent = new GUIContent("Hierarchy Icons");
            window.minSize = new Vector2(420, 330);
            Scan();
            window.Filter();
            window.ShowUtility();
        }
        private static void Scan()
        {
            if (scanned) return;
            scanned = true;
            try
            {
                var method = typeof(EditorGUIUtility).GetMethod("GetEditorAssetBundle", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                bundle = method?.Invoke(null, null) as AssetBundle;
                if (bundle != null)
                    foreach (var path in bundle.GetAllAssetNames())
                        if (path.Contains("/icons/") && (path.EndsWith(".png") || path.EndsWith(".asset")))
                            builtins.Add(new Entry { key = "editor:" + path, name = Path.GetFileNameWithoutExtension(path) });
            }
            catch (Exception exception) { Debug.LogWarning("Midnight Hierarchy icon catalog: " + exception.Message); }
            // Public loaded resources provide a fallback if Unity changes the internal bundle API.
            if (builtins.Count == 0)
                foreach (var texture in Resources.FindObjectsOfTypeAll<Texture2D>())
                    if (texture && !string.IsNullOrEmpty(texture.name) && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(texture)))
                    {
                        string key = "builtin:" + texture.name;
                        if (textures.ContainsKey(key)) continue;
                        textures[key] = texture;
                        builtins.Add(new Entry { key = key, name = texture.name });
                    }
            builtins.Sort((a,b) => string.Compare(a.name,b.name,StringComparison.OrdinalIgnoreCase));
        }
        internal static Texture Load(string key)
        {
            Scan();
            if (textures.TryGetValue(key, out var texture)) return texture;
            if (key.StartsWith("editor:", StringComparison.Ordinal) && bundle)
                texture = bundle.LoadAsset<Texture2D>(key.Substring(7));
            textures[key] = texture;
            return texture;
        }
        private void Filter()
        {
            filtered.Clear();
            foreach (var entry in tab == 0 ? builtins : project)
                if (string.IsNullOrEmpty(search) || entry.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                    filtered.Add(entry);
            scroll = Vector2.zero;
        }
        private void ScanProject()
        {
            project.Clear();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                project.Add(new Entry { key = guid, name = path });
            }
        }
        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            int newTab = GUILayout.Toolbar(tab, new[] { "Unity built-in", "Project textures" });
            search = EditorGUILayout.TextField("Search icons", search);
            if (EditorGUI.EndChangeCheck())
            {
                if (newTab != tab && newTab == 1) ScanProject();
                tab = newTab;
                Filter();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(filtered.Count + " icons", EditorStyles.miniLabel);
                if (GUILayout.Button("No icon", GUILayout.Width(75))) Choose("");
            }
            // Only visible rows load textures and receive GUI controls, even for large catalogs.
            Rect viewport = GUILayoutUtility.GetRect(0, 100000, 0, 100000, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            const float cell = 36;
            int columns = Mathf.Max(1, Mathf.FloorToInt((viewport.width - 18) / cell));
            int rows = Mathf.CeilToInt(filtered.Count / (float)columns);
            scroll = GUI.BeginScrollView(viewport, scroll, new Rect(0,0,columns * cell, rows * cell));
            int first = Mathf.Max(0, Mathf.FloorToInt(scroll.y / cell));
            int last = Mathf.Min(rows, Mathf.CeilToInt((scroll.y + viewport.height) / cell) + 1);
            for (int row = first; row < last; row++)
                for (int col = 0; col < columns; col++)
                {
                    int index = row * columns + col;
                    if (index >= filtered.Count) break;
                    var entry = filtered[index];
                    Texture texture = entry.key.StartsWith("editor:") || entry.key.StartsWith("builtin:") ? Load(entry.key) : HierarchyData.Icon(entry.key);
                    if (GUI.Button(new Rect(col * cell, row * cell, 33, 33), new GUIContent(texture, entry.name))) Choose(entry.key);
                }
            GUI.EndScrollView();
        }
        private void Choose(string key)
        {
            selected?.Invoke(key);
            Close();
            GUIUtility.ExitGUI();
        }
    }
}

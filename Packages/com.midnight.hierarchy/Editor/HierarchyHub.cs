using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Midnight.Hierarchy
{
    internal class HierarchyHub : EditorWindow
    {
        private Vector2 scroll;
        private int tab;
        private int paletteIndex;
        private string search = "";
        private string[] sceneGuids = Array.Empty<string>();
        private readonly string[] tabs = { "Style", "Navigate", "Appearance", "Palette" };

        [MenuItem("Tools/Midnight/Hierarchy")]
        internal static void Open()
        {
            var window = GetWindow<HierarchyHub>();
            window.titleContent = new GUIContent("Midnight Hierarchy");
            window.minSize = new Vector2(310, 220);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshScenes();
            EditorApplication.projectChanged += RefreshScenes;
            EditorApplication.hierarchyChanged += Repaint;
            Undo.undoRedoPerformed += Repaint;
            Selection.selectionChanged += Repaint;
        }
        private void OnDisable()
        {
            EditorApplication.projectChanged -= RefreshScenes;
            EditorApplication.hierarchyChanged -= Repaint;
            Undo.undoRedoPerformed -= Repaint;
            Selection.selectionChanged -= Repaint;
        }
        private void RefreshScenes() { sceneGuids = AssetDatabase.FindAssets("t:Scene"); Repaint(); }

        internal static void Bookmark(GameObject go)
        {
            if (EditorApplication.isPlaying) return;
            var id = HierarchyData.Id(go);
            if (id == null)
            {
                EditorUtility.DisplayDialog("Save scene first", "Save the scene before bookmarking this object.", "OK");
                return;
            }
            var data = HierarchyData.instance;
            if (data.objects.Contains(id)) return;
            Undo.RecordObject(data, "Bookmark object");
            data.objects.Add(id);
            data.Persist();
        }

        private void OnGUI()
        {
            tab = GUILayout.Toolbar(tab, tabs);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (tab == 0) Styles();
            else if (tab == 1) Navigation();
            else if (tab == 2) Appearance();
            else Palette();
            EditorGUILayout.EndScrollView();
        }

        private void Styles()
        {
            EditorGUILayout.LabelField("Hierarchy colors & icons", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Select objects and click a color below, or Alt + left-click any Hierarchy row. Named === section headers receive automatic colors; ordinary objects keep their original appearance until styled.", MessageType.Info);
            var targets = Selection.gameObjects;
            EditorGUILayout.LabelField("Selected objects", targets.Length.ToString());
            using (new EditorGUI.DisabledScope(targets.Length == 0 || EditorApplication.isPlayingOrWillChangePlaymode))
                HierarchyStylePopup.DrawControls(targets);
        }

        private void Navigation()
        {
            var data = HierarchyData.instance;
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!Selection.activeGameObject || EditorApplication.isPlaying))
                {
                    if (GUILayout.Button("Bookmark selected")) Bookmark(Selection.activeGameObject);
                    if (GUILayout.Button("Style selected")) HierarchyOverlay.StyleMenu(Selection.activeGameObject);
                }
                if (GUILayout.Button("Collapse all")) HierarchyExpansion.Handle(null, false, true);
            }
            using (new EditorGUILayout.HorizontalScope())
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Create folder")) CreateGroup(false);
                if (GUILayout.Button("Create divider")) CreateGroup(true);
            }
            EditorGUILayout.LabelField("Object bookmarks", EditorStyles.boldLabel);
            if (data.objects.Count == 0) EditorGUILayout.LabelField("Bookmark a saved scene object to begin.");
            for (int i = 0; i < data.objects.Count; i++)
            {
                string id = data.objects[i];
                var go = HierarchyData.Resolve(id);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!go))
                        if (GUILayout.Button(go ? go.name : "Unavailable — load its scene", EditorStyles.miniButton))
                        {
                            Selection.activeGameObject = go;
                            EditorGUIUtility.PingObject(go);
                        }
                    if (GUILayout.Button("×", GUILayout.Width(24)))
                    {
                        Undo.RecordObject(data, "Remove bookmark");
                        data.objects.RemoveAt(i);
                        data.Persist();
                        GUIUtility.ExitGUI();
                    }
                }
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scene selector", EditorStyles.boldLabel);
            search = EditorGUILayout.TextField("Search", search);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                DrawScenes(true);
                DrawScenes(false);
            }
            EditorGUILayout.HelpBox("Alt-right-click a Hierarchy row for styles. Alt-click a component icon for its editor. Dock this tools window beside Hierarchy for navigation.", MessageType.Info);
        }

        private void DrawScenes(bool favorites)
        {
            var data = HierarchyData.instance;
            EditorGUILayout.LabelField(favorites ? "Bookmarked scenes" : "Other scenes", EditorStyles.miniBoldLabel);
            var guids = favorites ? data.scenes.ToArray() : sceneGuids;
            foreach (var guid in guids)
            {
                if (!favorites && data.scenes.Contains(guid)) continue;
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(search) && path.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(favorites ? "★" : "☆", GUILayout.Width(26)))
                    {
                        Undo.RecordObject(data, "Bookmark scene");
                        if (favorites) data.scenes.Remove(guid); else data.scenes.Add(guid);
                        data.Persist();
                        GUIUtility.ExitGUI();
                    }
                    using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(path)))
                    {
                        var label = string.IsNullOrEmpty(path) ? "Missing scene" : Path.GetFileNameWithoutExtension(path);
                        if (GUILayout.Button(new GUIContent(label, path))) OpenScene(path, false);
                        if (GUILayout.Button(new GUIContent("+", "Open additively"), GUILayout.Width(24))) OpenScene(path, true);
                    }
                }
            }
        }

        private static void OpenScene(string path, bool additive)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                EditorUtility.DisplayDialog("Leave Prefab Mode first", "Return to the main stage before switching scenes.", "OK");
                return;
            }
            if (!additive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var loaded = SceneManager.GetSceneByPath(path);
            if (additive && loaded.IsValid() && loaded.isLoaded) { SceneManager.SetActiveScene(loaded); return; }
            EditorSceneManager.OpenScene(path, additive ? OpenSceneMode.Additive : OpenSceneMode.Single);
            GUIUtility.ExitGUI();
        }

        private void Appearance()
        {
            var data = HierarchyData.instance;
            var serialized = new SerializedObject(data);
            serialized.Update();
            foreach (string name in new[] { "enabled", "colors", "autoGroupColors", "colorStrength", "gradientColors", "gradientEndOpacity", "childColorStrength", "icons", "lines", "stripes", "components", "activeToggle", "shortcuts", "minimal" })
                EditorGUILayout.PropertyField(serialized.FindProperty(name));
            if (serialized.ApplyModifiedProperties()) data.Persist();
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Hovered object shortcuts: D default parent, A active, F focus position, X delete (confirmation), E expand/collapse, Shift-E expand only, Ctrl-Shift-E collapse all. Disabled while renaming. Minimal hides lines, stripes and component icons.", MessageType.Info);
            if (GUILayout.Button("Refresh hierarchy cache"))
            {
                HierarchyOverlay.Invalidate();
                EditorApplication.RepaintHierarchyWindow();
            }
        }

        private void Palette()
        {
            var data = HierarchyData.instance;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Export JSON"))
                {
                    string path = EditorUtility.SaveFilePanel("Export palette", "", "MidnightPalette", "json");
                    if (!string.IsNullOrEmpty(path))
                    {
                        try { File.WriteAllText(path, JsonUtility.ToJson(new PaletteFile { entries = data.palette }, true)); }
                        catch (Exception exception) { EditorUtility.DisplayDialog("Export failed", exception.Message, "OK"); }
                    }
                }
                if (GUILayout.Button("Import JSON")) ImportPalette();
            }
            var serialized = new SerializedObject(data);
            serialized.Update();
            EditorGUILayout.PropertyField(serialized.FindProperty("palette"), true);
            if (serialized.ApplyModifiedProperties()) data.Persist();
            EditorGUILayout.HelpBox("Icon: builtin:Camera Icon or a Texture2D asset GUID. Custom textures must also exist in the destination project; JSON does not embed textures.", MessageType.Info);
            EditorGUILayout.LabelField("Assign custom icon", EditorStyles.miniBoldLabel);
            if (data.palette.Count == 0) return;
            paletteIndex = Mathf.Clamp(paletteIndex, 0, data.palette.Count - 1);
            paletteIndex = EditorGUILayout.Popup("Palette entry", paletteIndex, data.palette.ConvertAll(x => x.name).ToArray());
            if (GUILayout.Button("Browse all icons for this palette entry..."))
            {
                var entry = data.palette[paletteIndex];
                HierarchyIconPicker.Open(key => {
                    if (!data.palette.Contains(entry)) return;
                    Undo.RecordObject(data, "Set palette icon");
                    entry.icon = key;
                    data.Persist();
                    Repaint();
                });
            }
            var texture = EditorGUILayout.ObjectField("Texture", null, typeof(Texture2D), false) as Texture2D;
            if (texture && data.palette.Count > 0)
            {
                Undo.RecordObject(data, "Set palette icon");
                data.palette[paletteIndex].icon = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(texture));
                data.Persist();
            }
        }

        private static void CreateGroup(bool divider)
        {
            var parent = Selection.activeTransform;
            var go = new GameObject(divider ? "──────── Section ────────" : "Folder");
            if (parent)
            {
                SceneManager.MoveGameObjectToScene(go, parent.gameObject.scene);
                go.transform.SetParent(parent, false);
            }
            Undo.RegisterCreatedObjectUndo(go, "Create hierarchy group");
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            // Groups are ordinary empty GameObjects. Never move children or remove transforms implicitly.
        }

        private static void ImportPalette()
        {
            string path = EditorUtility.OpenFilePanel("Import palette", "", "json");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                var imported = JsonUtility.FromJson<PaletteFile>(File.ReadAllText(path));
                if (imported?.entries == null || imported.entries.Count == 0 || imported.entries.Exists(x => x == null || string.IsNullOrWhiteSpace(x.name)))
                    throw new FormatException("Expected a non-empty entries list with named palette styles.");
                var data = HierarchyData.instance;
                Undo.RecordObject(data, "Import palette");
                data.palette = imported.entries;
                data.Persist();
            }
            catch (Exception exception) { EditorUtility.DisplayDialog("Import failed", exception.Message, "OK"); }
        }
    }
}

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Midnight.Hierarchy
{
    [InitializeOnLoad]
    internal static class HierarchyOverlay
    {
        private sealed class Row
        {
            public GameObject go;
            public Decoration style;
            public Component[] components;
            public Texture[] componentIcons;
            public Texture icon;
            public int depth;
        }
        private static readonly Dictionary<int, Row> Cache = new Dictionary<int, Row>();
        private static int hovered;
        private static Texture2D gradient;
        private static float gradientEnd = -1;

        private static float RowOpacity(Row row, HierarchyData data)
        {
            float opacity = Mathf.Min(data.colorStrength, Selection.Contains(row.go) ? .12f : .3f);
            if (row.go.transform.parent && row.style != null && !row.style.divider)
            {
                var parent = data.For(row.go.transform.parent.gameObject);
                if (parent != null && parent.color == row.style.color) opacity *= data.childColorStrength;
            }
            return opacity;
        }

        private static Texture2D Gradient(float end)
        {
            if (gradient && Mathf.Approximately(end, gradientEnd)) return gradient;
            if (!gradient)
                gradient = new Texture2D(128, 1, TextureFormat.RGBA32, false) {
                    name = "Midnight Hierarchy Fade", hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
                };
            var pixels = new Color[128];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(1, 1, 1, Mathf.Lerp(1, end, i / 127f));
            gradient.SetPixels(pixels); gradient.Apply(); gradientEnd = end;
            return gradient;
        }

        static HierarchyOverlay()
        {
            EditorApplication.hierarchyWindowItemOnGUI += Draw;
            EditorApplication.hierarchyChanged += Invalidate;
            EditorApplication.projectChanged += Invalidate;
            EditorApplication.playModeStateChanged += _ => Invalidate();
            Undo.undoRedoPerformed += () => HierarchyData.instance.Persist();
        }

        internal static void Invalidate() { Cache.Clear(); }

        private static Row Get(int id)
        {
            if (Cache.TryGetValue(id, out var row)) return row;
            var go = EditorUtility.InstanceIDToObject(id) as GameObject;
            if (!go) return null;
            row = new Row { go = go, style = HierarchyData.instance.For(go) };
            row.icon = row.style == null ? null : HierarchyData.Icon(row.style.icon);
            row.components = go.GetComponents<Component>();
            row.componentIcons = new Texture[row.components.Length];
            for (int i = 0; i < row.components.Length; i++)
                if (row.components[i]) row.componentIcons[i] = EditorGUIUtility.ObjectContent(row.components[i], row.components[i].GetType()).image;
            for (var parent = go.transform.parent; parent; parent = parent.parent) row.depth++;
            Cache[id] = row;
            return row;
        }

        private static void Draw(int id, Rect rect)
        {
            var data = HierarchyData.instance;
            if (!data.enabled) return;
            var row = Get(id);
            if (row == null || !row.go) return;
            var e = Event.current;
            bool over = rect.Contains(e.mousePosition);
            if (over) hovered = id;
            if (e.type == EventType.Repaint)
            {
                if (data.stripes && !data.minimal && ((int)(rect.y / rect.height) & 1) == 0)
                    EditorGUI.DrawRect(new Rect(0, rect.y, rect.xMax, rect.height), new Color(0, 0, 0, .045f));
                if (data.colors && row.style != null && row.style.color.a > 0)
                {
                    var color = row.style.color;
                    // This callback is AFTER Unity draws text, selection and rename controls.
                    // Keep the native foreground visible instead of painting another label on top.
                    color.a = RowOpacity(row, data);
                    var background = new Rect(0, rect.y, rect.xMax, rect.height);
                    if (data.gradientColors)
                    {
                        Color previous = GUI.color;
                        GUI.color = color;
                        GUI.DrawTexture(background, Gradient(data.gradientEndOpacity), ScaleMode.StretchToFill, true);
                        GUI.color = previous;
                    }
                    else EditorGUI.DrawRect(background, color);
                    EditorGUI.DrawRect(new Rect(0, rect.y, 3, rect.height), new Color(color.r, color.g, color.b, 1));
                    if (row.style.divider) EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), new Color(.6f,.6f,.6f,.5f));
                }
                if (data.lines && !data.minimal)
                    for (int i = 0; i < row.depth; i++)
                        EditorGUI.DrawRect(new Rect(rect.x - 21 - i * 14, rect.y, 1, rect.height), new Color(.5f,.5f,.5f,.23f));
                if (data.icons && row.icon && !EditorGUIUtility.editingTextField)
                {
                    // Replace only the native icon's pixels, not the name or foldout.
                    // A solid backing prevents transparent custom icons mixing with the cube.
                    Color backing = EditorGUIUtility.isProSkin ? new Color(.22f,.22f,.22f) : new Color(.76f,.76f,.76f);
                    bool selected = Selection.Contains(row.go);
                    if (selected) backing = EditorGUIUtility.isProSkin ? new Color(.17f,.36f,.53f) : new Color(.24f,.48f,.9f);
                    else if (data.stripes && !data.minimal && ((int)(rect.y / rect.height) & 1) == 0)
                        backing *= .955f;
                    backing.a = 1;
                    if (data.colors && row.style != null && row.style.color.a > 0)
                    {
                        Color tint = row.style.color;
                        tint.a = 1;
                        float opacity = RowOpacity(row, data);
                        if (data.gradientColors) opacity *= Mathf.Lerp(1, data.gradientEndOpacity, Mathf.Clamp01((rect.x + 8) / Mathf.Max(1, rect.xMax)));
                        backing = Color.Lerp(backing, tint, opacity);
                    }
                    Rect iconRect = new Rect(rect.x, rect.y + (rect.height - 16) * .5f, 16, 16);
                    EditorGUI.DrawRect(iconRect, backing);
                    GUI.DrawTexture(iconRect, row.icon, ScaleMode.ScaleToFit, true);
                }
            }
            float right = rect.xMax - 2;
            if (data.activeToggle)
            {
                right -= 18;
                var toggle = new Rect(right, rect.y, 16, rect.height);
                bool active = GUI.Toggle(toggle, row.go.activeSelf, new GUIContent("", "Toggle GameObject active state"));
                if (active != row.go.activeSelf)
                {
                    Undo.RecordObject(row.go, "Toggle GameObject");
                    row.go.SetActive(active);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(row.go);
                    EditorUtility.SetDirty(row.go);
                }
            }
            float nameEnd = rect.x + 20 + GUI.skin.label.CalcSize(new GUIContent(row.go.name)).x + 12;
            if (data.components && !data.minimal)
            {
                // Keep the object name readable in narrow windows; remaining components stay in Inspector.
                for (int i = row.components.Length - 1; i >= 0 && right - 18 > nameEnd; i--)
                {
                    if (!row.components[i] || row.components[i] is Transform) continue;
                    right -= 18;
                    var r = new Rect(right, rect.y, 16, 16);
                    GUI.Label(r, new GUIContent(row.componentIcons[i], row.components[i].GetType().Name + " — Alt-click to inspect"));
                    if (e.type == EventType.MouseDown && e.button == 0 && e.alt && r.Contains(e.mousePosition))
                    {
                        ComponentPopup.Open(row.components[i]);
                        e.Use();
                    }
                }
            }
            if (over && e.type == EventType.MouseDown && e.button == 0 && e.alt)
            {
                HierarchyStylePopup.Show(row.go, new Rect(e.mousePosition, Vector2.zero));
                e.Use();
            }
            if (over && e.type == EventType.MouseDown && e.button == 1 && e.alt)
            {
                StyleMenu(row.go);
                e.Use();
            }
            // Require the pointer to remain over this row and avoid consuming text-editing shortcuts.
            if (!over || hovered != id || !data.shortcuts || e.type != EventType.KeyDown ||
                EditorGUIUtility.editingTextField || e.alt || e.command) return;
            if (e.keyCode == KeyCode.E)
            {
                if (HierarchyExpansion.Handle(row.go, e.shift, e.control)) e.Use();
                return;
            }
            if (e.control || e.shift) return;
            switch (e.keyCode)
            {
                case KeyCode.A:
                    Undo.RecordObject(row.go, "Toggle GameObject");
                    row.go.SetActive(!row.go.activeSelf);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(row.go);
                    EditorUtility.SetDirty(row.go);
                    e.Use();
                    break;
                case KeyCode.F:
                    if (SceneView.lastActiveSceneView)
                    {
                        var bounds = new Bounds(row.go.transform.position, Vector3.one);
                        var renderers = row.go.GetComponentsInChildren<Renderer>();
                        if (renderers.Length > 0)
                        {
                            bounds = renderers[0].bounds;
                            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                        }
                        SceneView.lastActiveSceneView.Frame(bounds, false);
                    }
                    e.Use();
                    break;
                case KeyCode.X:
                    var target = row.go;
                    // Delay destruction until Unity has finished drawing its hierarchy rows.
                    EditorApplication.delayCall += () =>
                    {
                        if (target && EditorUtility.DisplayDialog("Delete GameObject?", "Delete " + target.name + " and its children?", "Delete", "Cancel"))
                            Undo.DestroyObjectImmediate(target);
                    };
                    e.Use();
                    break;
                case KeyCode.D:
                    DefaultParent.Set(row.go);
                    e.Use();
                    break;
            }
        }

        internal static void StyleMenu(GameObject go)
        {
            var menu = new GenericMenu();
            var targets = Selection.Contains(go) ? Selection.gameObjects : new[] { go };
            foreach (var entry in HierarchyData.instance.palette)
            {
                var captured = entry;
                menu.AddItem(new GUIContent("Style/" + entry.name), false, () => HierarchyData.instance.Apply(targets, captured));
                menu.AddItem(new GUIContent("Divider/" + entry.name), false, () => HierarchyData.instance.Apply(targets, captured, true));
            }
            menu.AddItem(new GUIContent("Clear style"), false, () => HierarchyData.instance.Apply(targets, null));
            menu.AddItem(new GUIContent("Bookmark"), false, () => HierarchyHub.Bookmark(go));
            menu.AddItem(new GUIContent("Open tools"), false, HierarchyHub.Open);
            menu.ShowAsContext();
        }
    }

    internal class ComponentPopup : EditorWindow
    {
        [SerializeField] private Component targetComponent;
        private Editor editor;
        private Vector2 scroll;
        internal static void Open(Component target)
        {
            var window = CreateInstance<ComponentPopup>();
            window.targetComponent = target;
            window.titleContent = new GUIContent(target.GetType().Name);
            window.minSize = new Vector2(300, 160);
            window.ShowUtility();
        }
        private void OnGUI()
        {
            if (!targetComponent) { EditorGUILayout.HelpBox("Component no longer exists.", MessageType.Info); return; }
            Editor.CreateCachedEditor(targetComponent, null, ref editor);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            editor.OnInspectorGUI();
            EditorGUILayout.EndScrollView();
        }
        private void OnDisable() { if (editor) DestroyImmediate(editor); }
    }
}

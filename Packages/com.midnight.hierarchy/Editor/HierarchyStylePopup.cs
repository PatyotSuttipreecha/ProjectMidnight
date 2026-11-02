using UnityEditor;
using UnityEngine;

namespace Midnight.Hierarchy
{
    internal class HierarchyStylePopup : PopupWindowContent
    {
        private GameObject[] targets;
        private static Color customColor = new Color(.2f, .5f, .7f, 1);
        private static string customIcon = "builtin:Folder Icon";
        private static bool divider;
        private static bool includeChildren;
        private static readonly Color[] Colors = {
            new Color(.65f,.22f,.24f), new Color(.72f,.4f,.16f), new Color(.65f,.58f,.18f),
            new Color(.22f,.52f,.32f), new Color(.18f,.5f,.55f), new Color(.24f,.38f,.65f),
            new Color(.47f,.3f,.65f), new Color(.65f,.3f,.5f)
        };
        private static readonly string[] Icons = { "Folder Icon", "GameObject Icon", "Camera Icon", "Light Icon", "AudioSource Icon", "Canvas Icon", "SettingsIcon" };

        internal static void Show(GameObject go, Rect anchor)
        {
            PopupWindow.Show(anchor, new HierarchyStylePopup {
                targets = Selection.Contains(go) ? Selection.gameObjects : new[] { go }
            });
        }
        public override Vector2 GetWindowSize() => new Vector2(330, 390);
        public override void OnGUI(Rect rect)
        {
            EditorGUILayout.LabelField("Style " + targets.Length + " object(s)", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode)) DrawControls(targets);
        }
        internal static void DrawControls(GameObject[] targets)
        {
            var data = HierarchyData.instance;
            includeChildren = EditorGUILayout.Toggle(new GUIContent("Include Children (colors)", "Apply colors to every descendant, including inactive objects. Icons remain unchanged. You can also hold Alt when clicking a color."), includeChildren);
            bool recursive = includeChildren || (Event.current != null && Event.current.alt);
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (var color in Colors)
                {
                    Color old = GUI.backgroundColor;
                    GUI.backgroundColor = color;
                    if (GUILayout.Button(new GUIContent(" ", "Apply color. Hold Alt to include all descendants."), GUILayout.Height(27)))
                    {
                        customColor = color;
                        data.ApplyColor(targets, color, recursive);
                    }
                    GUI.backgroundColor = old;
                }
            }
            customColor = EditorGUILayout.ColorField("Custom color", customColor);
            if (GUILayout.Button("Apply custom color")) data.ApplyColor(targets, customColor, recursive);
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
                foreach (string icon in Icons)
                    if (GUILayout.Button(EditorGUIUtility.IconContent(icon), GUILayout.Width(37), GUILayout.Height(26)))
                    {
                        customIcon = "builtin:" + icon;
                        Apply(targets, null, customIcon);
                    }
            if (GUILayout.Button("Browse all icons..."))
            {
                var capturedTargets = (GameObject[])targets.Clone();
                HierarchyIconPicker.Open(key => { customIcon = key; Apply(capturedTargets, null, key); });
            }
            var texture = EditorGUILayout.ObjectField("Custom icon", null, typeof(Texture2D), false) as Texture2D;
            if (texture) Apply(targets, null, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(texture)));
            divider = EditorGUILayout.Toggle("Divider line", divider);
            if (GUILayout.Button("Apply color + icon + divider"))
                data.Apply(targets, new PaletteEntry { color = customColor, icon = customIcon }, divider);
            EditorGUILayout.LabelField("Saved palette", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
                foreach (var style in data.palette)
                    if (style != null && GUILayout.Button(style.name))
                    {
                        if (recursive) data.ApplyColor(targets, style.color, true);
                        else data.Apply(targets, style, divider);
                    }
            if (GUILayout.Button("Clear color (keep icons)")) data.ApplyColor(targets, Color.clear, recursive);
            if (GUILayout.Button("Clear custom style")) data.Apply(targets, null);
        }
        private static void Apply(GameObject[] targets, Color? color, string icon)
        {
            var data = HierarchyData.instance;
            // Enable the feature when the user explicitly applies a style.
            Undo.RecordObject(data, "Enable hierarchy styling");
            data.enabled = true;
            if (color.HasValue) data.colors = true;
            if (icon != null) data.icons = true;
            foreach (var go in targets)
            {
                if (!go) continue;
                var old = data.For(go);
                data.Apply(new[] { go }, new PaletteEntry {
                    color = color ?? (old != null ? old.color : Color.clear),
                    icon = icon ?? (old != null ? old.icon : "")
                }, old != null && old.divider);
            }
        }
    }
}

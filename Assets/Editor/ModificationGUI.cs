using System;
using UnityEditor;
using UnityEngine;

// Shared presentation for System Modification tools. Does not edit gameplay data.
public static class ModificationGUI
{
    public static readonly Color Enemy = new Color(.24f, .78f, .71f);
    public static readonly Color Weapon = new Color(1f, .65f, .32f);
    public static readonly Color Item = new Color(.38f, .68f, 1f);
    public static readonly Color Player = new Color(.71f, .57f, .98f);
    public static readonly Color Scene = new Color(.91f, .73f, .36f);
    public static readonly Color Debug = new Color(.98f, .52f, .53f);
    private static GUIStyle heading, muted, small, card, section, tab, row;
    private static bool dark;
    private static Color Surface => EditorGUIUtility.isProSkin ? new Color(.20f, .22f, .25f) : new Color(.94f, .95f, .97f);

    private static void Styles()
    {
        if (heading != null && dark == EditorGUIUtility.isProSkin) return;
        dark = EditorGUIUtility.isProSkin;
        heading = new GUIStyle(EditorStyles.boldLabel) { fontSize = 22, wordWrap = true };
        muted = new GUIStyle(EditorStyles.wordWrappedLabel) { fontSize = 11 };
        muted.normal.textColor = dark ? new Color(.68f, .73f, .79f) : new Color(.34f, .39f, .46f);
        small = new GUIStyle(muted) { fontSize = 10 };
        card = new GUIStyle { padding = new RectOffset(12, 12, 12, 12), margin = new RectOffset(0, 0, 4, 10) };
        card.normal.background = EditorGUIUtility.whiteTexture;
        section = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, wordWrap = true };
        tab = new GUIStyle(EditorStyles.toolbarButton) { fixedHeight = 29, fontSize = 11 };
        row = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, wordWrap = true,
            padding = new RectOffset(12, 10, 6, 6), fontSize = 12 };
    }

    public sealed class WindowScope : IDisposable
    {
        private readonly float labelWidth;
        private readonly UnityEngine.Object target;
        private readonly string category;
        public WindowScope(EditorWindow window, string category, string name, string description, Color accent, UnityEngine.Object target = null)
        {
            Styles();
            this.category = category;
            this.target = target;
            labelWidth = EditorGUIUtility.labelWidth;
            Vector2 minimum = Vector2.Max(window.minSize, new Vector2(400, 440));
            if (window.minSize != minimum) window.minSize = minimum;
            EditorGUIUtility.labelWidth = Mathf.Clamp(window.position.width * .37f, 145, 220);
            GUILayout.BeginVertical();
            Rect stripe = GUILayoutUtility.GetRect(0, 4, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(stripe, accent);
            GUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(18, 18, 14, 12) });
            GUILayout.BeginHorizontal();
            GUILayout.Label("SYSTEM MODIFICATION  /  " + category.ToUpperInvariant(), small);
            GUILayout.FlexibleSpace();
            GUILayout.Label(Application.isPlaying ? "PLAY MODE" : "EDIT MODE", EditorStyles.miniBoldLabel);
            GUILayout.EndHorizontal();
            GUILayout.Label(name, heading);
            GUILayout.Label(description, muted);
            if (target != null)
            {
                GUILayout.Space(7);
                GUILayout.Label(new GUIContent("CURRENT  /  " + target.name, AssetDatabase.GetAssetPath(target)), small);
            }
            GUILayout.EndVertical();
            GUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(14, 14, 6, 8) });
        }
        public void Dispose()
        {
            GUILayout.EndVertical();
            GUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(25));
            GUILayout.Label(category, EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (target != null && GUILayout.Button("Locate", EditorStyles.toolbarButton, GUILayout.Width(60)))
                EditorGUIUtility.PingObject(target);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            EditorGUIUtility.labelWidth = labelWidth;
        }
    }

    public sealed class Card : IDisposable
    {
        public Card(string name, string description = null)
        {
            Styles();
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = Surface;
            GUILayout.BeginVertical(card);
            GUI.backgroundColor = previous;
            if (!string.IsNullOrEmpty(name)) GUILayout.Label(name, section);
            if (!string.IsNullOrEmpty(description)) GUILayout.Label(description, muted);
            GUILayout.Space(5);
        }
        public void Dispose() => GUILayout.EndVertical();
    }

    public static int Tabs(int selected, string[] labels, Color accent, float availableWidth = -1)
    {
        Styles();
        selected = Mathf.Clamp(selected, 0, labels.Length - 1);
        float width = availableWidth > 0 ? availableWidth : EditorGUIUtility.currentViewWidth - 32;
        int columns = Mathf.Clamp(Mathf.FloorToInt(width / 95), 1, labels.Length);
        int rows = Mathf.CeilToInt((float)labels.Length / columns);
        selected = GUILayout.SelectionGrid(selected, labels, columns, tab, GUILayout.Height(rows * 29));
        Rect area = GUILayoutUtility.GetLastRect();
        float cellWidth = area.width / columns, cellHeight = area.height / rows;
        EditorGUI.DrawRect(new Rect(area.x + selected % columns * cellWidth,
            area.y + (selected / columns + 1) * cellHeight - 2, cellWidth, 2), accent);
        GUILayout.Space(10);
        return selected;
    }

    public static void Section(string name)
    {
        Styles();
        GUILayout.Space(9);
        Rect area = GUILayoutUtility.GetRect(new GUIContent(name), section, GUILayout.MinHeight(27), GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(area, Surface);
        EditorGUI.DrawRect(new Rect(area.x, area.y, 3, area.height), Enemy);
        GUI.Label(new Rect(area.x + 10, area.y + 3, area.width - 16, area.height - 4), name, section);
        GUILayout.Space(5);
    }

    public static bool ListRow(bool selected, string label, string tooltip, Color accent)
    {
        Styles();
        Color previous = GUI.backgroundColor;
        if (selected) GUI.backgroundColor = Color.Lerp(Color.white, accent, .4f);
        bool value = GUILayout.Toggle(selected, new GUIContent(label, tooltip), row, GUILayout.MinHeight(label.Contains("\n") ? 48 : 32));
        GUI.backgroundColor = previous;
        Rect area = GUILayoutUtility.GetLastRect();
        if (selected) EditorGUI.DrawRect(new Rect(area.x, area.y + 3, 3, area.height - 6), accent);
        GUILayout.Space(3);
        return value;
    }
}


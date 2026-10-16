using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DecorationPlacementData))]
public sealed class DecorationPlacementDataEditor : Editor
{
    private string search = "";
    private readonly DecorationLibraryPreviewCache previews = new DecorationLibraryPreviewCache();
    private void OnDisable() => previews.Dispose();
    public static int Add(DecorationPlacementData data, IEnumerable<GameObject> prefabs)
    {
        var additions = prefabs.Where(p => p && EditorUtility.IsPersistent(p) && PrefabUtility.IsPartOfPrefabAsset(p))
            .Distinct().Where(p => data.entries == null || !data.entries.Any(e => e != null && e.prefab == p)).ToArray();
        if (additions.Length == 0) return 0;
        Undo.RecordObject(data, "Add Decorations to Placement Data");
        if (data.entries == null) data.entries = new List<DecorationPlacementData.Entry>();
        foreach (var prefab in additions) data.entries.Add(new DecorationPlacementData.Entry {
            prefab = prefab, category = DecorationLibraryUtility.GuessCategory(prefab.name) });
        EditorUtility.SetDirty(data);
        return additions.Length;
    }
    public static DecorationPlacementData CreateFromPlacer(ProceduralDecorationPlacer placer)
    {
        string path = EditorUtility.SaveFilePanelInProject("Save Placement Data", "Decoration_New", "asset", "Save this reusable prefab set.",
            "Assets/Script/TerrainTools/PlacementData");
        if (string.IsNullOrEmpty(path)) return null;
        var data = CreateInstance<DecorationPlacementData>();
        for (int i = 0; i < (placer.rockPrefabs?.Length ?? 0); i++)
            if (placer.rockPrefabs[i]) data.entries.Add(new DecorationPlacementData.Entry {
                prefab = placer.rockPrefabs[i], weight = placer.GetRockPrefabWeight(i),
                category = DecorationLibraryUtility.GuessCategory(placer.rockPrefabs[i].name) });
        AssetDatabase.CreateAsset(data, path);
        return data;
    }
    public override void OnInspectorGUI()
    {
        var data = (DecorationPlacementData)target;
        EditorGUILayout.LabelField("PLACEMENT DATA", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Assign this SO to a Procedural Decoration Placer. Preview and Generate read its prefabs and weights directly. Editing this set affects every Placer that uses it.", MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Browse / Add Assets", GUILayout.Height(28))) DecorationLibraryWindow.OpenData(data);
            if (GUILayout.Button("Save", GUILayout.Width(55))) AssetDatabase.SaveAssetIfDirty(data);
        }
        var drop = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
        GUI.Box(drop, "Drop prefab / model assets here", EditorStyles.helpBox);
        var evt = Event.current;
        if (drop.Contains(evt.mousePosition) && (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform))
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (evt.type == EventType.DragPerform) { DragAndDrop.AcceptDrag(); Add(data, DragAndDrop.objectReferences.OfType<GameObject>()); }
            evt.Use();
        }
        search = EditorGUILayout.TextField("Search set", search);
        EditorGUILayout.LabelField($"{data.Count} entries", EditorStyles.miniLabel);
        serializedObject.Update();
        var entries = serializedObject.FindProperty("entries");
        float total = 0; for (int i = 0; i < data.Count; i++) total += data.GetWeight(i);
        int remove = -1;
        for (int i = 0; i < entries.arraySize; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i);
            var prefab = entry.FindPropertyRelative("prefab").objectReferenceValue as GameObject;
            string text = $"{(prefab ? prefab.name : "Missing")} {entry.FindPropertyRelative("category").stringValue} {entry.FindPropertyRelative("tags").stringValue}";
            if (!string.IsNullOrWhiteSpace(search) && !search.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries).All(t => text.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0)) continue;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var texture = prefab ? AssetPreview.GetAssetPreview(prefab) ?? previews.Get(prefab) : null;
                    GUILayout.Label(texture, GUILayout.Width(64), GUILayout.Height(64));
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("prefab"), GUIContent.none);
                    if (GUILayout.Button("Remove", GUILayout.Width(65))) remove = i;
                }
                EditorGUILayout.Slider(entry.FindPropertyRelative("weight"), 0, 100);
                EditorGUILayout.LabelField("Chance", (total > 0 ? data.GetWeight(i) / total * 100 : 0).ToString("0.#") + "%");
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("category"));
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("tags"));
            }
        }
        if (remove >= 0) entries.DeleteArrayElementAtIndex(remove);
        if (GUILayout.Button("Add Empty Slot"))
        {
            int index = entries.arraySize++;
            var entry = entries.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("prefab").objectReferenceValue = null;
            entry.FindPropertyRelative("weight").floatValue = 100;
            entry.FindPropertyRelative("category").stringValue = "";
            entry.FindPropertyRelative("tags").stringValue = "";
        }
        serializedObject.ApplyModifiedProperties();
        if (AssetPreview.IsLoadingAssetPreviews()) Repaint();
    }
}

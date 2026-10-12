using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ItemSpawnGroup))]
public class ItemSpawnGroupEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var group = (ItemSpawnGroup)target;
        EditorGUILayout.HelpBox("Place Item Spawn Points under this group in the Hierarchy. Nested groups manage their own points. Inactive points and points without valid items are skipped.\nApply Point Chances OFF: fill the selected locations. ON: selected locations also roll their individual chance, so fewer items may spawn.", MessageType.Info);
        EditorGUILayout.LabelField("Eligible Points", group.GetEligiblePoints().Count.ToString());
        if (Application.isPlaying)
            EditorGUILayout.LabelField("Last Roll", group.LastSpawnedCount + " spawned / " + group.LastSelectedCount + " selected");
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
            if (GUILayout.Button("Spawn Group Now")) group.Spawn();
    }

    [MenuItem("System Modification/ItemLab/Create/Item Spawn Group")]
    private static void Create()
    {
        var go = new GameObject("Item Spawn Group");
        GameObjectUtility.SetParentAndAlign(go, Selection.activeGameObject);
        Undo.RegisterCreatedObjectUndo(go, "Create Item Spawn Group");
        Undo.AddComponent<ItemSpawnGroup>(go);
        Selection.activeGameObject = go;
    }
}

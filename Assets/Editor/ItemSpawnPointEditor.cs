using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ItemSpawnPoint)), CanEditMultipleObjects]
public class ItemSpawnPointEditor : Editor
{
    private string search = "";
    private int typeFilter;
    private ItemSO[] library;

    private void OnEnable() => RefreshLibrary();
    private void RefreshLibrary()
    {
        library = AssetDatabase.FindAssets("t:ItemSO")
            .Select(id => AssetDatabase.LoadAssetAtPath<ItemSO>(AssetDatabase.GUIDToAssetPath(id)))
            .Where(item => item != null).OrderBy(item => item.itemName).ToArray();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("selectionMode"));
        var mode = serializedObject.FindProperty("selectionMode");
        if (mode.hasMultipleDifferentValues)
        {
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("Select points with the same Selection Mode to edit their settings together.", MessageType.Info);
            return;
        }
        bool random = mode.enumValueIndex == (int)ItemSpawnPoint.SelectionMode.RandomByType;
        if (random)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("randomItemType"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("randomPool"), true);
            if (GUILayout.Button("Fill Pool from Project (Matching Type)"))
            {
                serializedObject.ApplyModifiedProperties();
                RefreshLibrary();
                foreach (ItemSpawnPoint spawnPoint in targets)
                {
                    Undo.RecordObject(spawnPoint, "Fill Random Item Pool");
                    spawnPoint.randomPool = library.Where(entry => entry.itemType == spawnPoint.randomItemType).ToArray();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(spawnPoint);
                    EditorUtility.SetDirty(spawnPoint);
                }
                serializedObject.Update();
            }
            EditorGUILayout.HelpBox("Each eligible item has an equal chance. Null entries, duplicate items, other types and items without a usable pickup prefab are skipped. Fill Pool replaces the list; new project items require filling it again.", MessageType.Info);
        }
        else
        {
        EditorGUILayout.PropertyField(serializedObject.FindProperty("item"), new GUIContent("Item Data"));
        var labels = new[] { "All types" }.Concat(Enum.GetNames(typeof(ItemType))).ToArray();
        typeFilter = EditorGUILayout.Popup("Browse Type", typeFilter, labels);
        search = EditorGUILayout.TextField("Search Items", search);
        if (GUILayout.Button("Choose Item from Library"))
        {
            RefreshLibrary();
            var menu = new GenericMenu();
            foreach (var entry in library)
            {
                if (typeFilter > 0 && (int)entry.itemType != typeFilter - 1) continue;
                if (!string.IsNullOrWhiteSpace(search) &&
                    (entry.itemName + " " + entry.name).IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                var selected = entry;
                string title = string.IsNullOrWhiteSpace(entry.itemName) ? entry.name : entry.itemName;
                menu.AddItem(new GUIContent(entry.itemType + "/" + title.Replace('/', '-') + " (" + entry.name + ")"), false, () =>
                {
                    serializedObject.Update();
                    serializedObject.FindProperty("item").objectReferenceValue = selected;
                    serializedObject.ApplyModifiedProperties();
                });
            }
            if (menu.GetItemCount() == 0) menu.AddDisabledItem(new GUIContent("No matching items"));
            menu.ShowAsContext();
        }
        }
        foreach (string field in new[] { "quantity", "pickupPrefabOverride", "spawnOnStart", "spawnChance", "spawnOnce", "localOffset", "rotationOffset", "onSpawned" })
            EditorGUILayout.PropertyField(serializedObject.FindProperty(field), true);
        serializedObject.ApplyModifiedProperties();

        var point = (ItemSpawnPoint)target;
        if (point.Group != null)
            EditorGUILayout.HelpBox("Controlled by parent group: " + point.Group.name + ". Call the group's Spawn(), not this point. Point Spawn On Start is ignored. Spawn Chance applies only if the group enables Apply Point Chances.", MessageType.Info);
        if (!random && point.item != null)
            EditorGUILayout.HelpBox("Type: " + point.item.itemType + (point.item.itemType == ItemType.Weapon || point.item.itemType == ItemType.Ammo ? " | Weapon: " + point.item.weaponType : "") +
                "\nThe selected ItemSO defines the item's type and inventory behavior.", MessageType.Info);
        if (!random && (point.item == null || point.EffectivePrefab == null))
            EditorGUILayout.HelpBox("Select Item Data and assign its Pickup Prefab, or provide a Pickup Prefab Override here.", MessageType.Warning);
        if (random)
        {
            EditorGUILayout.LabelField("Eligible Items", point.GetEligibleItems().Count.ToString());
            if (point.GetEligibleItems().Count == 0)
                EditorGUILayout.HelpBox("No eligible items. Fill/edit the pool and check the type and pickup prefabs.", MessageType.Warning);
        }
        EditorGUILayout.HelpBox("Spawn Once allows only one chance roll per session, including a failed roll. An existing pickup always prevents duplicates.", MessageType.Info);
        using (new EditorGUI.DisabledScope(!Application.isPlaying || targets.Length != 1))
            if (GUILayout.Button("Spawn Now (Play Mode)")) point.Spawn();
    }

    [MenuItem("GameObject/Gameplay/Item Spawn Point", false, 10)]
    private static void Create(MenuCommand command)
    {
        var go = new GameObject("Item Spawn Point");
        GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
        Undo.RegisterCreatedObjectUndo(go, "Create Item Spawn Point");
        Undo.AddComponent<ItemSpawnPoint>(go);
        Selection.activeGameObject = go;
    }
    [MenuItem("System Modification/ItemLab/Create/Item Spawn Point")]
    private static void CreateFromLab() => Create(new MenuCommand(Selection.activeGameObject));
}

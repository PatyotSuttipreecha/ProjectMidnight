using UnityEditor;
using UnityEngine;

public class ItemSpawnConfigurationWindow : EditorWindow
{
    [SerializeField] private ItemSpawnPoint point;
    [SerializeField] private bool followSelection = true;
    private Editor inspector;
    private Vector2 scroll;

    [MenuItem("System Modification/ItemLab/Spawn Points")]
    public static void Open() => GetWindow<ItemSpawnConfigurationWindow>("Item Spawn Points");

    private void OnEnable()
    {
        minSize = new Vector2(440, 480);
        Selection.selectionChanged += FollowSelection;
        FollowSelection();
    }
    private void OnDisable()
    {
        Selection.selectionChanged -= FollowSelection;
        if (inspector != null) DestroyImmediate(inspector);
    }
    private void FollowSelection()
    {
        if (followSelection && Selection.activeGameObject != null &&
            Selection.activeGameObject.TryGetComponent<ItemSpawnPoint>(out var selected)) point = selected;
        Repaint();
    }
    private void OnGUI()
    {
        followSelection = EditorGUILayout.ToggleLeft("Follow selection", followSelection);
        point = (ItemSpawnPoint)EditorGUILayout.ObjectField("Spawn Point", point, typeof(ItemSpawnPoint), true);
        if (GUILayout.Button("Create Item Spawn Point"))
            EditorApplication.ExecuteMenuItem("System Modification/ItemLab/Create/Item Spawn Point");
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Loaded Scene Spawn Points", EditorStyles.boldLabel);
        foreach (var entry in FindObjectsByType<ItemSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!entry.gameObject.scene.IsValid()) continue;
            if (GUILayout.Button(entry.gameObject.scene.name + " / " + entry.name))
            {
                point = entry;
                Selection.activeGameObject = entry.gameObject;
                EditorGUIUtility.PingObject(entry);
            }
        }
        EditorGUILayout.Space();
        if (point == null)
            EditorGUILayout.HelpBox("Select a spawn point or create one to configure fixed items and random item types.", MessageType.Info);
        else
        {
            Editor.CreateCachedEditor(point, null, ref inspector);
            inspector.OnInspectorGUI();
        }
        EditorGUILayout.EndScrollView();
    }
}

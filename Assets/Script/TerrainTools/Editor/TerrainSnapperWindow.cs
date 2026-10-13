#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Keep a stable Inspector for the tool while the scene selection changes.
public sealed class TerrainSnapperWindow : EditorWindow
{
    [SerializeField] private TerrainObjectSnapper snapper;
    private Editor inspector;
    private Vector2 scroll;

    [MenuItem("System Modification/TerrainTools/Terrain Snapper", false, 32)]
    public static void Open()
    {
        var window = GetWindow<TerrainSnapperWindow>("Terrain Snapper");
        window.minSize = new Vector2(300, 360);
        window.Show();
    }

    private void OnEnable()
    {
        Selection.selectionChanged += Repaint;
        Undo.undoRedoPerformed += Repaint;
        if (snapper == null)
        {
            var selected = Selection.activeGameObject;
            if (selected != null) snapper = selected.GetComponent<TerrainObjectSnapper>();
            if (snapper == null)
                foreach (var candidate in Object.FindObjectsByType<TerrainObjectSnapper>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (IsSceneTool(candidate)) { snapper = candidate; break; }
        }
    }

    private static bool IsSceneTool(TerrainObjectSnapper tool) =>
        tool != null && !EditorUtility.IsPersistent(tool) && tool.gameObject.scene.IsValid() &&
        tool.gameObject.scene.isLoaded && !EditorSceneManager.IsPreviewScene(tool.gameObject.scene);

    private void OnDisable()
    {
        Selection.selectionChanged -= Repaint;
        Undo.undoRedoPerformed -= Repaint;
        if (inspector != null) DestroyImmediate(inspector);
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Select scene objects, then click Snap Objects to Terrain. Follow Scene Selection updates targets automatically; turn it off to use your manual list.", MessageType.Info);
        var chosen = (TerrainObjectSnapper)EditorGUILayout.ObjectField("Scene Tool", snapper, typeof(TerrainObjectSnapper), true);
        if (chosen != snapper && (chosen == null || IsSceneTool(chosen))) snapper = chosen;
        if (!IsSceneTool(snapper))
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode ||
                PrefabStageUtility.GetCurrentPrefabStage() != null ||
                !UnityEngine.SceneManagement.SceneManager.GetActiveScene().isLoaded))
            {
                if (GUILayout.Button("Create Terrain Snapper in Scene"))
                {
                    var go = new GameObject("Terrain Object Snapper");
                    Undo.RegisterCreatedObjectUndo(go, "Create Terrain Snapper");
                    snapper = Undo.AddComponent<TerrainObjectSnapper>(go);
                    // Leave Terrain empty to support multiple terrain tiles.
                    EditorSceneManager.MarkSceneDirty(go.scene);
                }
            }
            return;
        }
        Editor.CreateCachedEditor(snapper, typeof(TerrainObjectSnapperEditor), ref inspector);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        inspector.OnInspectorGUI();
        EditorGUILayout.EndScrollView();
    }
}
#endif

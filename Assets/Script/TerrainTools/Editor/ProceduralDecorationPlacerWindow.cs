#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Keep a stable Inspector for the tool while the scene selection changes.
public sealed class ProceduralDecorationPlacerWindow : EditorWindow
{
    [SerializeField] private ProceduralDecorationPlacer snapper;
    private Editor inspector;
    private Vector2 scroll;

    [MenuItem("System Modification/TerrainTools/Procedural Decoration Placer", false, 32)]
    public static void Open()
    {
        var window = GetWindow<ProceduralDecorationPlacerWindow>("Procedural Decoration Placer");
        window.minSize = new Vector2(380, 420);
        window.Show();
    }

    private void OnEnable()
    {
        Selection.selectionChanged += Repaint;
        Undo.undoRedoPerformed += Repaint;
        if (snapper == null)
        {
            var selected = Selection.activeGameObject;
            if (selected != null) snapper = selected.GetComponent<ProceduralDecorationPlacer>();
            if (snapper == null)
                foreach (var candidate in Object.FindObjectsByType<ProceduralDecorationPlacer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (IsSceneTool(candidate)) { snapper = candidate; break; }
        }
    }

    private static bool IsSceneTool(ProceduralDecorationPlacer tool) =>
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
        EditorGUILayout.HelpBox("Add prefabs, set groups or painted-layer edges, then Build Preview → Generate This Preview. Settings stay available while you select assets.", MessageType.Info);
        var chosen = (ProceduralDecorationPlacer)EditorGUILayout.ObjectField("Scene Tool", snapper, typeof(ProceduralDecorationPlacer), true);
        if (chosen != snapper && (chosen == null || IsSceneTool(chosen))) snapper = chosen;
        if (!IsSceneTool(snapper))
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode ||
                PrefabStageUtility.GetCurrentPrefabStage() != null ||
                !UnityEngine.SceneManagement.SceneManager.GetActiveScene().isLoaded))
            {
                if (GUILayout.Button("Create Procedural Decoration Placer in Scene"))
                {
                    var go = new GameObject("Procedural Decoration Placer");
                    Undo.RegisterCreatedObjectUndo(go, "Create Procedural Decoration Placer");
                    snapper = Undo.AddComponent<ProceduralDecorationPlacer>(go);
                    snapper.terrain = Terrain.activeTerrains.Length == 1 ? Terrain.activeTerrains[0] : null;
                    EditorSceneManager.MarkSceneDirty(go.scene);
                }
            }
            return;
        }
        Editor.CreateCachedEditor(snapper, null, ref inspector);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        inspector.OnInspectorGUI();
        EditorGUILayout.EndScrollView();
    }
}
#endif

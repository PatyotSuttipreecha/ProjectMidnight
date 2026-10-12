using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class TriggerConfigurationWindow : EditorWindow
{
    private WorldTrigger trigger;
    private Editor inspector;
    private Vector2 scroll;
    [MenuItem("System Modification/Scene Configuration/Triggers")]
    public static void Open() => GetWindow<TriggerConfigurationWindow>("Trigger Configuration");
    private void OnSelectionChange()
    {
        trigger = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<WorldTrigger>() : null;
        if (trigger == null && Selection.activeGameObject != null) { var note = Selection.activeGameObject.GetComponent<PointNote>(); if (note != null) trigger = TriggerNoteLink.Find(note); }
        Repaint();
    }
    private void OnDisable() { if (inspector != null) DestroyImmediate(inspector); }
    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Player entry activates On Triggered. Wire events to a Timeline, door, enemy spawner or other scene component. This trigger does not manage cutscene input or Mission completion automatically.", MessageType.Info);
        trigger = (WorldTrigger)EditorGUILayout.ObjectField("Trigger", trigger, typeof(WorldTrigger), true);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            if (GUILayout.Button("Create Trigger Point")) Create(false);
            using (new EditorGUI.DisabledScope(Selection.activeGameObject == null || EditorUtility.IsPersistent(Selection.activeGameObject)))
                if (GUILayout.Button("Create Linked Trigger from Selected Object")) Create(true);
        }
        if (trigger == null) return;
        scroll = EditorGUILayout.BeginScrollView(scroll);
        Editor.CreateCachedEditor(trigger, null, ref inspector); inspector.OnInspectorGUI();
        EditorGUILayout.LabelField("Triggered this session", trigger.HasTriggered ? "Yes" : "No");
        if (GUILayout.Button("Select / Frame")) { Selection.activeGameObject = trigger.gameObject; SceneView.lastActiveSceneView?.FrameSelected(); }
        EditorGUILayout.EndScrollView();
    }
    private void Create(bool useSelected)
    {
        var go = useSelected ? Selection.activeGameObject : new GameObject("[Trigger] Event Point");
        if (!useSelected)
        {
            Undo.RegisterCreatedObjectUndo(go, "Create Trigger Note"); StageUtility.PlaceGameObjectInCurrentStage(go);
            if (SceneView.lastActiveSceneView != null) go.transform.position = SceneView.lastActiveSceneView.pivot;
        }
        var note = go.GetComponent<PointNote>();
        if (note == null) { note = Undo.AddComponent<PointNote>(go); note.pointType = PointNote.PointType.Trigger; note.title = useSelected ? go.name : "Event Point"; }
        PointNoteEditor.ApplyDesignChanges(note);
        trigger = TriggerNoteLink.Create(note);
        EditorSceneManager.MarkSceneDirty(go.scene); Selection.activeGameObject = go;
    }
}
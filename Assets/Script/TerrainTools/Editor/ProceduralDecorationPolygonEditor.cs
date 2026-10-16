using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed partial class ProceduralDecorationPlacerEditor
{
    private void DrawPolygonControls(ProceduralDecorationPlacer placer)
    {
        EditorGUILayout.HelpBox("Shift + left click on the assigned Terrain to add pivots in order. Drag pivots to reshape the boundary. Polygon follows Terrain with no box Y limit; complete decoration geometry must fit inside it.", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorUtility.IsPersistent(placer)))
        {
            bool edit = GUILayout.Toggle(areaEditor == this, areaEditor == this ? "Finish Boundary (Esc)" : "Edit Boundary in Scene", "Button");
            if (edit != (areaEditor == this)) { areaEditor = edit ? this : null; RefreshArea(); }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Start from Box"))
                {
                    serializedObject.ApplyModifiedProperties();
                    Undo.RecordObject(placer, "Create Decoration Boundary");
                    var c = placer.areaCenter; var s = placer.areaSize * .5f;
                    placer.areaPoints = new System.Collections.Generic.List<Vector2> {
                        new Vector2(c.x-s.x,c.z-s.z), new Vector2(c.x+s.x,c.z-s.z),
                        new Vector2(c.x+s.x,c.z+s.z), new Vector2(c.x-s.x,c.z+s.z) };
                    PolygonChanged(placer); serializedObject.Update();
                }
                if (GUILayout.Button("Remove Last Pivot"))
                {
                    serializedObject.ApplyModifiedProperties();
                    if (placer.areaPoints.Count > 0) { Undo.RecordObject(placer, "Remove Boundary Pivot"); placer.areaPoints.RemoveAt(placer.areaPoints.Count-1); PolygonChanged(placer); }
                    serializedObject.Update();
                }
                if (GUILayout.Button("Clear Pivots"))
                {
                    serializedObject.ApplyModifiedProperties(); Undo.RecordObject(placer, "Clear Boundary Pivots");
                    placer.areaPoints.Clear(); PolygonChanged(placer); serializedObject.Update();
                }
            }
        }
        EditorGUILayout.PropertyField(serializedObject.FindProperty("areaPoints"), new GUIContent("Boundary Pivots (local X/Z)"), true);
        string error = DecorationPolygon.Validate(placer.areaPoints);
        if (error != null) EditorGUILayout.HelpBox(error, MessageType.Warning);
        if (GUILayout.Button("Frame Boundary") && SceneView.lastActiveSceneView && placer.areaPoints.Count > 0)
        {
            var bounds = new Bounds(PolygonWorld(placer,placer.areaPoints[0]),Vector3.zero);
            foreach (var p in placer.areaPoints) bounds.Encapsulate(PolygonWorld(placer,p));
            bounds.Expand(2); SceneView.lastActiveSceneView.Frame(bounds,false);
        }
    }
    private static Vector3 PolygonWorld(ProceduralDecorationPlacer placer, Vector2 point)
    {
        var world = placer.transform.TransformPoint(new Vector3(point.x,0,point.y));
        if (placer.terrain && placer.terrain.terrainData) world.y = placer.terrain.SampleHeight(world) + placer.terrain.transform.position.y + .1f;
        return world;
    }
    private void PolygonChanged(ProceduralDecorationPlacer placer)
    {
        Save(placer); EditorSceneManager.MarkSceneDirty(placer.gameObject.scene); RefreshArea();
    }
    private void DrawPolygonScene(ProceduralDecorationPlacer placer)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorUtility.IsPersistent(placer)) return;
        var points = placer.areaPoints;
        if (points == null) return;
        bool editing = areaEditor == this;
        var evt = Event.current;
        if (editing && evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
        { areaEditor = null; evt.Use(); RefreshArea(); return; }
        using (new Handles.DrawingScope(Color.cyan))
        {
            for (int i = 0; i < points.Count; i++)
            {
                var a = points[i]; var b = points[(i+1)%points.Count];
                // Sample each edge along the Terrain, so the boundary remains legible on slopes.
                var last = PolygonWorld(placer,a);
                for (int step=1; step<=24; step++)
                { var next=PolygonWorld(placer,Vector2.Lerp(a,b,step/24f)); Handles.DrawLine(last,next); last=next; }
                if (!editing) continue;
                var world = PolygonWorld(placer,a);
                Handles.Label(world,$"P{i+1}");
                EditorGUI.BeginChangeCheck();
                var moved = Handles.PositionHandle(world, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(placer,"Move Boundary Pivot"); var local=placer.transform.InverseTransformPoint(moved);
                    points[i]=new Vector2(local.x,local.z); PolygonChanged(placer);
                }
            }
        }
        if (!editing) return;
        if (evt.shift && !evt.alt) HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        if (evt.type == EventType.MouseDown && evt.button == 0 && evt.shift && !evt.alt)
        {
            var collider = placer.terrain ? placer.terrain.GetComponent<TerrainCollider>() : null;
            var ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
            if (collider && collider.Raycast(ray,out var hit,float.MaxValue))
            {
                Undo.RecordObject(placer,"Add Boundary Pivot"); var local=placer.transform.InverseTransformPoint(hit.point);
                points.Add(new Vector2(local.x,local.z)); PolygonChanged(placer); evt.Use();
            }
            else { SceneView.lastActiveSceneView?.ShowNotification(new GUIContent("Click the assigned Terrain (with TerrainCollider).")); evt.Use(); }
        }
    }
}

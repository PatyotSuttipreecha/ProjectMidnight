using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

[CustomEditor(typeof(PatrolRoute))]
public class PatrolRouteEditor : Editor
{
    private bool place;
    private bool move;
    private int count = 5;
    private float radius = 8f;
    private float spacing = 2f;
    private float snap = 2f;
    private string status;
    private PatrolRoute Route => (PatrolRoute)target;
    private NavMeshQueryFilter Filter => new NavMeshQueryFilter { agentTypeID = Route.agentTypeId, areaMask = Route.areaMask };
    public override void OnInspectorGUI()
    {
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorUtility.IsPersistent(Route) || UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null))
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("This route owns point placement, order and wait times. Point objects only mark positions; no PatrolPoint component is needed.", MessageType.Info);
            DrawWaitTimes();
            place = EditorGUILayout.ToggleLeft("Shift-click Scene View to add points", place);
            move = EditorGUILayout.ToggleLeft("Move points with handles", move);
            count = EditorGUILayout.IntSlider("Generate count", count, 2, 20);
            radius = EditorGUILayout.Slider("Generate radius", radius, 1f, 50f);
            spacing = EditorGUILayout.Slider("Minimum spacing", spacing, 0.1f, 10f);
            snap = EditorGUILayout.Slider("NavMesh snap distance", snap, 0.1f, 5f);
            if (GUILayout.Button("Generate connected patrol")) Generate();
        }
        if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);
    }
    private bool Snap(Vector3 p, out Vector3 result)
    {
        bool found = NavMesh.SamplePosition(p, out NavMeshHit hit, snap, Filter); result = hit.position; return found;
    }
    private bool Connected(Vector3 a, Vector3 b)
    {
        NavMeshPath path = new NavMeshPath(); return NavMesh.CalculatePath(a, b, Filter, path) && path.status == NavMeshPathStatus.PathComplete;
    }
    private Transform Add(Vector3 position, int index)
    {
        GameObject point = new GameObject("Patrol " + index);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(point, Route.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(point, "Create route point");
        Undo.SetTransformParent(point.transform, Route.transform, "Parent route point");
        point.transform.position = position;
        return point.transform;
    }
    private void Assign(List<Transform> points)
    {
        Undo.RecordObject(Route, "Assign patrol route"); Route.points = points.ToArray(); EditorUtility.SetDirty(Route);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(Route.gameObject.scene);
        SceneView.RepaintAll();
    }
    private void Generate()
    {
        if (!Snap(Route.transform.position, out Vector3 origin)) { status = "Move route near a baked NavMesh matching its agent type."; return; }
        List<Vector3> positions = new List<Vector3>();
        for (int i = 0; i < count * 100 && positions.Count < count; i++)
        {
            Vector2 offset = Random.insideUnitCircle * radius;
            if (!Snap(origin + new Vector3(offset.x, 0f, offset.y), out Vector3 candidate) || Vector3.Distance(origin, candidate) > radius || !Connected(origin, candidate)) continue;
            bool valid = true;
            foreach (Vector3 other in positions) if (Vector3.Distance(candidate, other) < spacing || !Connected(other, candidate) || !Connected(candidate, other)) { valid = false; break; }
            if (valid) positions.Add(candidate);
        }
        if (positions.Count < 2) { status = "Not enough reachable points. Existing route is unchanged."; return; }
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Generate shared patrol route");
        List<Transform> points = new List<Transform>();
        for (int i = 0; i < positions.Count; i++) points.Add(Add(positions[i], i + 1));
        Assign(points); Undo.CollapseUndoOperations(group);
        status = "Generated " + points.Count + "/" + count + " points. Existing point objects are preserved. Ctrl+Z undoes generation.";
    }
    private void OnSceneGUI()
    { DrawSceneTools(); }

    public void DrawSceneTools()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorUtility.IsPersistent(Route) || UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null) return;
        if (move && Route.points != null)
            foreach (Transform point in Route.points)
            {
                if (point == null) continue;
                EditorGUI.BeginChangeCheck(); Vector3 position = Handles.PositionHandle(point.position, Quaternion.identity);
                if (EditorGUI.EndChangeCheck() && Snap(position, out Vector3 snapped))
                { Undo.RecordObject(point, "Move route point"); point.position = snapped; EditorUtility.SetDirty(point); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(point.gameObject.scene); }
            }
        Event e = Event.current;
        if (!place || !e.shift || e.alt) return;
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        if (!Physics.Raycast(HandleUtility.GUIPointToWorldRay(e.mousePosition), out RaycastHit hit, 10000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
        bool valid = Snap(hit.point, out Vector3 positionOnMesh) && Snap(Route.transform.position, out Vector3 origin) && Connected(origin, positionOnMesh);
        Handles.color = valid ? Color.green : Color.red; Handles.DrawWireDisc(valid ? positionOnMesh : hit.point, Vector3.up, 0.3f);
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            e.Use();
            if (valid)
            {
                Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
                List<Transform> points = Route.points != null ? new List<Transform>(Route.points) : new List<Transform>();
                points.Add(Add(positionOnMesh, points.Count + 1)); Assign(points); Undo.CollapseUndoOperations(group);
            }
        }
        SceneView.RepaintAll();
    }

    private void DrawWaitTimes()
    {
        if (Route.points == null) return;
        if (Route.pointWaits == null) Route.pointWaits = new List<PatrolRoute.PointWait>();
        for (int i = 0; i < Route.points.Length; i++)
        {
            Transform point = Route.points[i];
            if (point == null) continue;
            PatrolRoute.PointWait entry = Route.pointWaits.Find(p => p != null && p.point == point);
            PatrolPoint legacy = point.GetComponent<PatrolPoint>();
            bool previous = entry != null ? entry.overrideWaitTime : legacy != null && legacy.overrideWaitTime;
            float previousTime = entry != null ? entry.waitTime : legacy != null ? legacy.waitTime : 2f;
            EditorGUI.BeginChangeCheck();
            bool custom = EditorGUILayout.ToggleLeft("P" + (i + 1) + " — " + point.name + ": custom wait", previous);
            float seconds = custom ? Mathf.Max(0f, EditorGUILayout.FloatField("Wait (seconds)", previousTime)) : previousTime;
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(Route, "Edit route wait time");
                if (entry == null) { entry = new PatrolRoute.PointWait { point = point }; Route.pointWaits.Add(entry); }
                entry.overrideWaitTime = custom; entry.waitTime = seconds;
                EditorUtility.SetDirty(Route);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(Route.gameObject.scene);
            }
        }
    }
}

[CustomEditor(typeof(EnemySpawnPoint))]
public class EnemySpawnPointEditor : Editor
{
    private Editor routeEditor;
    private bool editRoute = true;
    private bool showPaths = true;
    private List<string> problems;
    private void OnDisable() { if (routeEditor != null) DestroyImmediate(routeEditor); }
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            using (new EditorGUI.DisabledScope(property.propertyPath == "m_Script"))
                EditorGUILayout.PropertyField(property, property.propertyPath == "target" ? new GUIContent("Target (optional)") : new GUIContent(property.displayName), true);
        }
        serializedObject.ApplyModifiedProperties();
        EnemySpawnPoint spawn = (EnemySpawnPoint)target;
        EditorGUILayout.HelpBox("The profile comes from the Enemy Prefab. Target is optional: leave empty to use the prefab target, or automatically find the object tagged Player.", MessageType.Info);
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.ObjectField("Prefab Profile", spawn.EffectiveProfile, typeof(EnemyDataSO), false);
        if (spawn.EffectiveProfile == null)
            EditorGUILayout.HelpBox("Assign a Profile on the Enemy Prefab.", MessageType.Warning);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorUtility.IsPersistent(spawn) || UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null))
        {
            if (spawn.patrolRoute == null && GUILayout.Button("Create and assign Patrol Route"))
            {
                Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
                GameObject obj = new GameObject(spawn.name + " Route");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(obj, spawn.gameObject.scene);
                obj.transform.position = spawn.transform.position;
                Undo.RegisterCreatedObjectUndo(obj, "Create spawn patrol route");
                PatrolRoute route = Undo.AddComponent<PatrolRoute>(obj);
                NavMeshQueryFilter filter = PatrolPathPreview.Filter(spawn);
                route.agentTypeId = filter.agentTypeID; route.areaMask = filter.areaMask;
                Undo.RecordObject(spawn, "Assign spawn route"); spawn.patrolRoute = route;
                EditorUtility.SetDirty(spawn);
                if (PrefabUtility.IsPartOfPrefabInstance(spawn)) PrefabUtility.RecordPrefabInstancePropertyModifications(spawn);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(spawn.gameObject.scene);
                Undo.CollapseUndoOperations(group);
            }
            showPaths = EditorGUILayout.ToggleLeft("Show NavMesh paths: green reachable / red blocked", showPaths);
            if (GUILayout.Button("Validate Spawn + Patrol paths")) problems = PatrolPathPreview.Validate(spawn);
            if (problems != null)
                EditorGUILayout.HelpBox(problems.Count == 0 ? "All route points and required paths are reachable. Spawn check uses the centre; runtime checks each sampled spawn position. Revalidate after editing or baking." : string.Join("\n", problems), problems.Count == 0 ? MessageType.Info : MessageType.Warning);
            if (spawn.patrolRoute != null)
            {
                EditorGUILayout.HelpBox("This is a shared Route. Editing it also changes the route for every spawn point using it.", MessageType.Info);
                if (GUILayout.Button("Frame assigned Route"))
                {
                    Bounds bounds = new Bounds(spawn.patrolRoute.transform.position, Vector3.one);
                    if (spawn.patrolRoute.points != null) foreach (Transform point in spawn.patrolRoute.points) if (point != null) bounds.Encapsulate(point.position);
                    if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.Frame(bounds, false);
                }
                editRoute = EditorGUILayout.Foldout(editRoute, "Edit assigned Patrol Route here", true);
                if (editRoute)
                {
                    Editor.CreateCachedEditor(spawn.patrolRoute, typeof(PatrolRouteEditor), ref routeEditor);
                    routeEditor.OnInspectorGUI();
                }
            }
        }
        if (Application.isPlaying)
        {
            EditorGUILayout.LabelField("Alive", spawn.AliveCount.ToString());
            if (GUILayout.Button("Spawn now")) spawn.SpawnOne();
        }
    }
    private void OnSceneGUI() { DrawSceneTools(); }
    public void DrawSceneTools()
    {
        EnemySpawnPoint spawn = (EnemySpawnPoint)target;
        if (showPaths && Event.current.type == EventType.Repaint) PatrolPathPreview.Draw(spawn);
        if (editRoute && routeEditor is PatrolRouteEditor editor && editor.target == spawn.patrolRoute) editor.DrawSceneTools();
    }
}

public static class PatrolPathPreview
{
    public static NavMeshQueryFilter Filter(EnemySpawnPoint spawn)
    {
        NavMeshAgent agent = spawn.enemyPrefab != null ? spawn.enemyPrefab.GetComponent<NavMeshAgent>() : null;
        return new NavMeshQueryFilter { agentTypeID = agent != null ? agent.agentTypeID : 0, areaMask = agent != null ? agent.areaMask : NavMesh.AllAreas };
    }
    private static bool Path(Vector3 from, Vector3 to, NavMeshQueryFilter filter, out NavMeshPath path)
    {
        path = new NavMeshPath();
        if (!NavMesh.SamplePosition(from, out NavMeshHit start, 0.2f, filter) || !NavMesh.SamplePosition(to, out NavMeshHit end, 0.2f, filter)) return false;
        return NavMesh.CalculatePath(start.position, end.position, filter, path) && path.status == NavMeshPathStatus.PathComplete;
    }
    private static void Legs(PatrolRoute route, System.Action<Transform, Transform, string> visit)
    {
        Transform[] points = route.points;
        if (points == null) return;
        if (route.mode == EnemyController.PatrolMode.Random)
        {
            for (int i = 0; i < points.Length; i++) for (int j = 0; j < points.Length; j++)
                if (i != j && points[i] != null && points[j] != null) visit(points[i], points[j], "P" + (i + 1) + " → P" + (j + 1));
            return;
        }
        for (int i = 1; i < points.Length; i++)
            if (points[i - 1] != null && points[i] != null)
            {
                visit(points[i - 1], points[i], "P" + i + " → P" + (i + 1));
                if (route.mode == EnemyController.PatrolMode.PingPong) visit(points[i], points[i - 1], "P" + (i + 1) + " → P" + i);
            }
        if (route.mode == EnemyController.PatrolMode.Loop && points.Length > 1 && points[points.Length - 1] != null && points[0] != null)
            visit(points[points.Length - 1], points[0], "P" + points.Length + " → P1");
    }
    public static List<string> Validate(EnemySpawnPoint spawn)
    {
        List<string> issues = new List<string>();
        if (spawn.enemyPrefab == null) issues.Add("Enemy Prefab is missing.");
        PatrolRoute route = spawn.patrolRoute;
        if (route == null || route.points == null || route.points.Length == 0) { issues.Add("Assign a Route and place its patrol points."); return issues; }
        NavMeshQueryFilter filter = Filter(spawn);
        if (route.agentTypeId != filter.agentTypeID || route.areaMask != filter.areaMask) issues.Add("Route NavMesh settings differ from the Enemy Prefab. Set matching agent type and area mask.");
        for (int i = 0; i < route.points.Length; i++)
        {
            Transform point = route.points[i];
            if (point == null) { issues.Add("P" + (i + 1) + " is missing."); continue; }
            if (!NavMesh.SamplePosition(point.position, out _, 0.2f, filter)) issues.Add("P" + (i + 1) + " is off the compatible NavMesh.");
            if (!Path(spawn.transform.position, point.position, filter, out _)) issues.Add("Spawn centre cannot reach P" + (i + 1) + ".");
        }
        Legs(route, (a, b, label) => { if (!Path(a.position, b.position, filter, out _)) issues.Add(label + " is blocked or disconnected."); });
        return issues;
    }
    public static void Draw(EnemySpawnPoint spawn)
    {
        PatrolRoute route = spawn.patrolRoute;
        if (route == null || route.points == null) return;
        NavMeshQueryFilter filter = Filter(spawn);
        Transform first = System.Array.Find(route.points, point => point != null);
        if (first != null) DrawLeg(spawn.transform.position, first.position, filter, "Spawn → Route");
        foreach (Transform point in route.points)
        {
            if (point == null) continue;
            Handles.color = NavMesh.SamplePosition(point.position, out _, 0.2f, filter) ? Color.green : Color.red;
            Handles.DrawWireDisc(point.position + Vector3.up * 0.1f, Vector3.up, route.markerSize * 1.5f);
        }
        Legs(route, (a, b, label) => DrawLeg(a.position, b.position, filter, label));
    }
    private static void DrawLeg(Vector3 from, Vector3 to, NavMeshQueryFilter filter, string label)
    {
        bool valid = Path(from, to, filter, out NavMeshPath path);
        Handles.color = valid ? Color.green : Color.red;
        Vector3 lift = Vector3.up * 0.25f;
        if (valid && path.corners.Length > 1)
        {
            Vector3[] corners = path.corners;
            for (int i = 0; i < corners.Length; i++) corners[i] += lift;
            Handles.DrawAAPolyLine(4f, corners);
        }
        else Handles.DrawDottedLine(from + lift, to + lift, 5f);
        Handles.Label(Vector3.Lerp(from, to, 0.5f) + lift, label + (valid ? "" : " BLOCKED"));
    }
}

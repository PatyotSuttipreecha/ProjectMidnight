using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public class EnemyLabWindow : EditorWindow
{
    [SerializeField] private EnemyController enemy;
    [SerializeField] private EnemyDataSO standaloneProfile;
    [SerializeField] private bool followSelection = true;
    [SerializeField] private bool placePoints;
    [SerializeField] private bool editPoints;
    [SerializeField] private int generateCount = 5;
    [SerializeField] private float generateRadius = 8f;
    [SerializeField] private float minimumSpacing = 2f;
    [SerializeField] private float snapDistance = 2f;
    private Vector2 scroll;
    private string status;
    private Transform pointRoot;
    private int workspace;
    private PatrolRoute selectedRoute;
    private EnemySpawnPoint selectedSpawn;
    private Editor workspaceEditor;


    [MenuItem("System Modification/Enemy Lab/Enemy Settings")]
    public static void Open()
    {
        EnemyLabWindow window = GetWindow<EnemyLabWindow>("Enemy Lab");
        window.minSize = new Vector2(400f, 550f);
    }
    [MenuItem("System Modification/Enemy Lab/Profiles")]
    public static void OpenProfiles() { Open(); var w = GetWindow<EnemyLabWindow>(); w.workspace = 1; w.SelectEnemy(null); }
    [MenuItem("System Modification/Enemy Lab/Patrol Routes")]
    public static void OpenRoutes() { Open(); var w = GetWindow<EnemyLabWindow>(); w.workspace = 2; w.FollowSelection(); }
    [MenuItem("System Modification/Enemy Lab/Spawn Points")]
    public static void OpenSpawns() { Open(); var w = GetWindow<EnemyLabWindow>(); w.workspace = 3; w.FollowSelection(); }

    private void OnEnable()
    {
        Selection.selectionChanged += FollowSelection;
        SceneView.duringSceneGui += DrawSceneTools;
        Undo.undoRedoPerformed += Refresh;
        EditorApplication.playModeStateChanged += PlayModeChanged;
        FollowSelection();
    }

    private void OnDisable()
    {
        if (workspaceEditor != null) DestroyImmediate(workspaceEditor);
        Selection.selectionChanged -= FollowSelection;
        SceneView.duringSceneGui -= DrawSceneTools;
        Undo.undoRedoPerformed -= Refresh;
        EditorApplication.playModeStateChanged -= PlayModeChanged;
    }

    private void Refresh() { Repaint(); SceneView.RepaintAll(); }
    private void PlayModeChanged(PlayModeStateChange state) { placePoints = false; editPoints = false; Refresh(); }
    private void FollowSelection()
    {
        if (followSelection && Selection.activeObject is EnemyWeaponSO module)
        { selectedModule = module; workspace = 5; Refresh(); return; }
        if (followSelection && Selection.activeGameObject != null)
        {
            var group = Selection.activeGameObject.GetComponent<EnemyGroup>();
            if (group != null) { selectedGroup = group; workspace = 4; Refresh(); return; }
            PatrolRoute route = Selection.activeGameObject.GetComponentInParent<PatrolRoute>();
            EnemySpawnPoint spawn = Selection.activeGameObject.GetComponent<EnemySpawnPoint>();
            if (route != null) { selectedRoute = route; workspace = 2; Refresh(); return; }
            if (spawn != null) { selectedSpawn = spawn; workspace = 3; Refresh(); return; }
        }
        if (followSelection && Selection.activeObject is EnemyDataSO profile)
        { SelectEnemy(null); workspace = 1; standaloneProfile = profile; Refresh(); return; }
        if (!followSelection || Selection.activeGameObject == null) return;
        EnemyController candidate = Selection.activeGameObject.GetComponentInParent<EnemyController>();
        if (candidate == null) candidate = Selection.activeGameObject.GetComponentInChildren<EnemyController>(true);
        if (candidate != null) { workspace = 0; if (candidate != enemy) SelectEnemy(candidate); Refresh(); }
    }
    private void SelectEnemy(EnemyController value)
    {
        enemy = value;
        pointRoot = null;
        placePoints = false;
        editPoints = false;
        status = null;
        Refresh();
    }

    private bool CanEditPath => enemy != null && !EditorApplication.isPlayingOrWillChangePlaymode &&
        !EditorUtility.IsPersistent(enemy) && enemy.gameObject.scene.IsValid() &&
        PrefabStageUtility.GetCurrentPrefabStage() == null;

    private readonly EnemyProfileGUI profileGUI = new EnemyProfileGUI();
    private bool patrolExpanded, gizmosExpanded, legacyExpanded;
    private EnemyGroup selectedGroup;
    private EnemyWeaponSO selectedModule;

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Enemy Lab", EditorStyles.boldLabel);
        int previous = workspace;
        workspace = GUILayout.Toolbar(workspace, new[] { "Enemy", "Profiles", "Routes", "Spawns", "Groups", "Modules" });
        if (previous != workspace) { placePoints = editPoints = false; scroll = Vector2.zero; }
        if (workspace >= 2) { DrawWorkspace(); return; }
        followSelection = EditorGUILayout.ToggleLeft("Follow Hierarchy / Project selection", followSelection);
        if (workspace == 0)
        {
            EditorGUI.BeginChangeCheck();
            var selected = (EnemyController)EditorGUILayout.ObjectField("Enemy instance / prefab", enemy, typeof(EnemyController), true);
            if (EditorGUI.EndChangeCheck()) SelectEnemy(selected);
        }
        else
        {
            standaloneProfile = (EnemyDataSO)EditorGUILayout.ObjectField("Enemy Profile", standaloneProfile, typeof(EnemyDataSO), false);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("Create Enemy Profile")) EnemyProfileMigration.CreateProfile();
        }
        if (workspace == 1)
        {
            if (standaloneProfile == null) { EditorGUILayout.HelpBox("Select a profile asset to configure it without a scene enemy.", MessageType.Info); return; }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox("Shared profile: edits affect every enemy using this asset when initialized.", MessageType.Info);
            profileGUI.Draw(standaloneProfile);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("Save Profile")) AssetDatabase.SaveAssetIfDirty(standaloneProfile);
            EditorGUILayout.EndScrollView();
            return;
        }
        if (enemy == null) { EditorGUILayout.HelpBox("Select a scene enemy or prefab. Profiles, routes, spawn points, groups and modules have their own workspaces.", MessageType.Info); return; }
        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (Application.isPlaying) { EnemyDiagnosticsGUI.Draw(enemy); Repaint(); }
        EditorGUILayout.HelpBox("Instance settings: references, patrol assignment and Gizmos. Shared tuning: the Profile below. Route placement: Routes workspace.", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            var controller = new SerializedObject(enemy); controller.Update();
            Property(controller, "enemyData", "Enemy Profile");
            Property(controller, "player", "Target (optional)");
            Property(controller, "startPatrolOnStart", "Start AI / Patrol On Start");
            Property(controller, "patrolRoute", "Patrol Route");
            if (controller.ApplyModifiedProperties()) MarkDirty(enemy);
            if (enemy.enemyData == null || !enemy.enemyData.useSharedSettings)
            {
                EditorGUILayout.HelpBox("Legacy instance settings are preserved. Promote them once to a shared Profile for one place to tune health, navigation and physics.", MessageType.Warning);
                if (GUILayout.Button("Promote current settings to shared Profile")) EnemyProfileMigration.Promote(enemy);
                legacyExpanded = EditorGUILayout.Foldout(legacyExpanded, "Legacy component tuning", true);
                if (legacyExpanded) DrawLegacySettings(controller);
            }
            if (enemy.enemyData != null && GUILayout.Button("Create independent profile copy")) CloneProfile();
        }
        if (enemy.enemyData != null)
        {
            profileGUI.Draw(enemy.enemyData);
            if (!Application.isPlaying)
                foreach (var warning in EnemyProfileGUI.AnimatorWarnings(enemy.GetComponent<Animator>(), enemy.enemyData)) EditorGUILayout.HelpBox(warning, MessageType.Warning);
        }
        else EditorGUILayout.HelpBox("Assign an Enemy Profile before configuring AI.", MessageType.Warning);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            gizmosExpanded = EditorGUILayout.Foldout(gizmosExpanded, "Instance Gizmos / previews", true);
            if (gizmosExpanded)
            {
                var data = new SerializedObject(enemy); data.Update();
                foreach (string field in new[] { "showGizmos", "patrolMarkerSize", "patrolPointColor", "patrolRouteColor", "showHearingGizmos", "hearingPreviewPlayer", "hearingPreviewGun" })
                    Property(data, field, ObjectNames.NicifyVariableName(field));
                if (data.ApplyModifiedProperties()) MarkDirty(enemy);
            }
            if (enemy.Route != null)
            {
                if (GUILayout.Button("Edit assigned Patrol Route")) { selectedRoute = enemy.Route; workspace = 2; placePoints = editPoints = false; }
            }
            else
            {
                patrolExpanded = EditorGUILayout.Foldout(patrolExpanded, "Legacy patrol points / generation", true);
                if (patrolExpanded) DrawLegacyPatrol();
            }
            if (GUILayout.Button("Save Scene / Prefab / Profile")) SaveEnemyChanges();
        }
        if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);
        EditorGUILayout.EndScrollView();
    }
    private void DrawLegacySettings(SerializedObject controller)
    {
        controller.Update();
        foreach (var field in typeof(EnemyDataSO).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            var property = controller.FindProperty(field.Name);
            if (property != null) EditorGUILayout.PropertyField(property, true);
        }
        if (controller.ApplyModifiedProperties()) MarkDirty(enemy);
        var agent = enemy.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            var data = new SerializedObject(agent); data.Update();
            Property(data, "m_Speed", "Legacy walk speed"); Property(data, "m_Acceleration", "Legacy acceleration");
            Property(data, "m_AngularSpeed", "Legacy turn speed"); Property(data, "m_StoppingDistance", "Legacy stopping distance");
            if (data.ApplyModifiedProperties()) MarkDirty(agent);
        }
        var health = enemy.GetComponent<HitBoxManager>();
        if (health != null)
        {
            var data = new SerializedObject(health); data.Update(); Property(data, "health", "Legacy health");
            if (data.ApplyModifiedProperties()) MarkDirty(health);
        }
    }
    private void DrawLegacyPatrol()
    {
        var data = new SerializedObject(enemy); data.Update();
        Property(data, "patrolMode", "Patrol mode");
        EditorGUILayout.PropertyField(data.FindProperty("patrolPoints"), new GUIContent("Assigned points"), true);
        if (data.ApplyModifiedProperties()) MarkDirty(enemy);
        if (CanEditPath && GUILayout.Button("Extract points into shared Route")) ExtractRoute();
        DrawPointSettings();
        using (new EditorGUI.DisabledScope(!CanEditPath))
        {
            placePoints = EditorGUILayout.ToggleLeft("Place points: Shift + click in Scene", placePoints);
            editPoints = EditorGUILayout.ToggleLeft("Move points with Scene handles", editPoints);
            snapDistance = EditorGUILayout.Slider("NavMesh snap distance", snapDistance, .1f, 5);
            generateCount = EditorGUILayout.IntSlider("Point count", generateCount, 2, 20);
            generateRadius = EditorGUILayout.Slider("Generation radius", generateRadius, 1, 50);
            minimumSpacing = EditorGUILayout.Slider("Minimum spacing", minimumSpacing, .1f, 10);
            if (GUILayout.Button("Generate connected patrol points")) GeneratePatrol();
            if (GUILayout.Button("Validate assigned paths")) ValidatePatrol();
            if (GUILayout.Button("Clear point assignments")) AssignPoints(new List<Transform>());
        }
        if (!CanEditPath) EditorGUILayout.HelpBox("Point placement requires a scene instance in Edit Mode, outside Prefab Mode.", MessageType.Info);
    }
    private void DrawWorkspace()
    {
        followSelection = EditorGUILayout.ToggleLeft("Follow selection", followSelection);
        if (workspace == 2)
        {
            selectedRoute = (PatrolRoute)EditorGUILayout.ObjectField("Patrol Route", selectedRoute, typeof(PatrolRoute), true);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("Create Patrol Route")) EnemyProfileMigration.CreateRoute();
        }
        else if (workspace == 3)
        {
            selectedSpawn = (EnemySpawnPoint)EditorGUILayout.ObjectField("Spawn Point", selectedSpawn, typeof(EnemySpawnPoint), true);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("Create Spawn Point")) EnemyProfileMigration.CreateSpawner();
        }
        else if (workspace == 4)
        {
            selectedGroup = (EnemyGroup)EditorGUILayout.ObjectField("Enemy Group", selectedGroup, typeof(EnemyGroup), true);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("Create Enemy Group")) EnemyGroupEditor.CreateGroup();
        }
        else
        {
            selectedModule = (EnemyWeaponSO)EditorGUILayout.ObjectField("Weapon Module", selectedModule, typeof(EnemyWeaponSO), false);
            EditorGUILayout.HelpBox("Weapon settings belong here; the Combat profile category contains the unarmed/base attack. Edits affect every profile using this module.", MessageType.Info);
            if (GUILayout.Button("Open Weapon Module window")) EnemyWeaponLabWindow.Open(selectedModule);
        }
        Object target = workspace == 2 ? (Object)selectedRoute : workspace == 3 ? (Object)selectedSpawn : workspace == 4 ? (Object)selectedGroup : selectedModule;
        if (target == null) { EditorGUILayout.HelpBox("Select an existing object or create one here.", MessageType.Info); return; }
        Editor.CreateCachedEditor(target, null, ref workspaceEditor);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        using (new EditorGUI.DisabledScope(workspace == 5 && EditorApplication.isPlayingOrWillChangePlaymode))
        {
            workspaceEditor.OnInspectorGUI();
            if (workspace == 5 && GUILayout.Button("Save Weapon Module")) AssetDatabase.SaveAssetIfDirty(selectedModule);
        }
        EditorGUILayout.EndScrollView();
    }

    private void ExtractRoute()
    {
        List<Transform> points = AssignedPoints();
        GameObject obj = new GameObject(enemy.name + " Route");
        SceneManager.MoveGameObjectToScene(obj, enemy.gameObject.scene);
        obj.transform.position = enemy.transform.position;
        Undo.RegisterCreatedObjectUndo(obj, "Extract shared patrol route");
        PatrolRoute route = Undo.AddComponent<PatrolRoute>(obj);
        route.points = points.ToArray();
        foreach (Transform point in points)
        {
            if (point == null) continue;
            PatrolPoint old = point.GetComponent<PatrolPoint>();
            if (old != null) route.pointWaits.Add(new PatrolRoute.PointWait { point = point, overrideWaitTime = old.overrideWaitTime, waitTime = old.waitTime });
        }
        route.mode = (EnemyController.PatrolMode)new SerializedObject(enemy).FindProperty("patrolMode").enumValueIndex;
        NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
        if (agent != null) { route.agentTypeId = agent.agentTypeID; route.areaMask = agent.areaMask; }
        SerializedObject data = new SerializedObject(enemy);
        data.FindProperty("patrolRoute").objectReferenceValue = route;
        data.ApplyModifiedProperties();
        MarkDirty(enemy); MarkDirty(route);
        status = "Shared route created. Assign it to spawn points.";
    }
    private void DrawPointSettings()
    {
        if (enemy.Route != null)
        {
            EditorGUILayout.HelpBox("Wait times and point placement belong to the Route. Open the Routes workspace to edit them together.", MessageType.Info);
            if (GUILayout.Button("Edit this Patrol Route")) { selectedRoute = enemy.Route; workspace = 2; placePoints = false; editPoints = false; }
            return;
        }
        Section("Wait Times Per Point");
        List<Transform> points = AssignedPoints();
        for (int i = 0; i < points.Count; i++)
        {
            Transform point = points[i];
            if (point == null) continue;
            PatrolPoint settings = point.GetComponent<PatrolPoint>();
            EditorGUILayout.LabelField("P" + (i + 1) + " — " + point.name, EditorStyles.boldLabel);
            if (settings == null)
            {
                using (new EditorGUI.DisabledScope(!CanEditPath))
                    if (GUILayout.Button("Enable custom wait time for P" + (i + 1)))
                    {
                        settings = Undo.AddComponent<PatrolPoint>(point.gameObject);
                        MarkDirty(settings);
                    }
            }
            if (settings == null) continue;
            SerializedObject data = new SerializedObject(settings);
            data.Update();
            Property(data, "overrideWaitTime", "Override profile wait time");
            if (data.FindProperty("overrideWaitTime").boolValue) Property(data, "waitTime", "Wait (seconds)");
            if (data.ApplyModifiedProperties()) MarkDirty(settings);
        }
    }

    private static void Section(string title) { EditorGUILayout.Space(8); EditorGUILayout.LabelField(title, EditorStyles.boldLabel); }
    private static void Property(SerializedObject data, string path, string label)
    {
        SerializedProperty property = data.FindProperty(path);
        if (property != null) EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip));
    }

    private void CloneProfile()
    {
        string path = EditorUtility.SaveFilePanelInProject("Create Enemy Profile", enemy.enemyData.name + " Copy", "asset", "Choose a location for the independent profile.");
        if (string.IsNullOrEmpty(path)) return;
        EnemyDataSO copy = Instantiate(enemy.enemyData);
        copy.name = System.IO.Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(copy, path);
        Undo.RegisterCreatedObjectUndo(copy, "Create enemy profile");
        Undo.RecordObject(enemy, "Assign enemy profile");
        enemy.enemyData = copy;
        MarkDirty(enemy);
        status = "Independent profile created: " + path;
    }

    private NavMeshQueryFilter QueryFilter()
    {
        NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
        return new NavMeshQueryFilter { agentTypeID = agent != null ? agent.agentTypeID : 0,
            areaMask = agent != null ? agent.areaMask : NavMesh.AllAreas };
    }

    private bool Snap(Vector3 point, out Vector3 snapped)
    {
        bool found = NavMesh.SamplePosition(point, out NavMeshHit hit, snapDistance, QueryFilter());
        snapped = hit.position;
        return found;
    }

    private bool Connected(Vector3 from, Vector3 to)
    {
        NavMeshPath path = new NavMeshPath();
        return NavMesh.CalculatePath(from, to, QueryFilter(), path) && path.status == NavMeshPathStatus.PathComplete;
    }

    private List<Transform> AssignedPoints()
    {
        if (enemy.Route != null) return new List<Transform>(enemy.Route.points ?? new Transform[0]);
        SerializedProperty points = new SerializedObject(enemy).FindProperty("patrolPoints");
        List<Transform> result = new List<Transform>();
        for (int i = 0; i < points.arraySize; i++) result.Add(points.GetArrayElementAtIndex(i).objectReferenceValue as Transform);
        return result;
    }

    private void AssignPoints(List<Transform> points)
    {
        if (enemy.Route != null)
        { Undo.RecordObject(enemy.Route, "Assign route points"); enemy.Route.points = points.ToArray(); MarkDirty(enemy.Route); Refresh(); return; }
        SerializedObject data = new SerializedObject(enemy);
        data.Update();
        SerializedProperty array = data.FindProperty("patrolPoints");
        array.arraySize = points.Count;
        for (int i = 0; i < points.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
        data.ApplyModifiedProperties();
        MarkDirty(enemy);
        Refresh();
    }

    private Transform CreatePoint(Vector3 position, int number)
    {
        if (enemy.Route != null) pointRoot = enemy.Route.transform;
        if (pointRoot == null)
        {
            GameObject group = new GameObject(enemy.name + " Patrol Points");
            SceneManager.MoveGameObjectToScene(group, enemy.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(group, "Create patrol group");
            pointRoot = group.transform;
        }
        GameObject point = new GameObject("Patrol " + number);
        SceneManager.MoveGameObjectToScene(point, enemy.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(point, "Create patrol point");
        if (enemy.Route == null) Undo.AddComponent<PatrolPoint>(point);
        Undo.SetTransformParent(point.transform, pointRoot, "Parent patrol point");
        point.transform.position = position;
        return point.transform;
    }

    private void GeneratePatrol()
    {
        if (!CanEditPath) return;
        if (!Snap(enemy.transform.position, out Vector3 origin))
        {
            status = "No NavMesh near this enemy for its agent type. Bake NavMesh and check the enemy position first.";
            return;
        }
        List<Vector3> positions = new List<Vector3>();
        for (int attempt = 0; attempt < generateCount * 100 && positions.Count < generateCount; attempt++)
        {
            Vector2 random = Random.insideUnitCircle * generateRadius;
            if (!Snap(origin + new Vector3(random.x, 0f, random.y), out Vector3 candidate)) continue;
            if (Vector3.Distance(candidate, origin) > generateRadius || !Connected(origin, candidate)) continue;
            bool valid = true;
            foreach (Vector3 existing in positions)
                if (Vector3.Distance(existing, candidate) < minimumSpacing || !Connected(existing, candidate) || !Connected(candidate, existing))
                { valid = false; break; }
            if (valid) positions.Add(candidate);
        }
        if (positions.Count < 2) { status = "Not enough connected space. Increase radius or reduce spacing. Existing patrol is unchanged."; return; }
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Generate enemy patrol");
        pointRoot = null;
        List<Transform> points = new List<Transform>();
        for (int i = 0; i < positions.Count; i++) points.Add(CreatePoint(positions[i], i + 1));
        AssignPoints(points);
        Undo.CollapseUndoOperations(group);
        status = "Generated " + points.Count + " / " + generateCount + " connected points. Ctrl+Z undoes the entire generation.";
    }

    private void ValidatePatrol()
    {
        List<Transform> points = AssignedPoints();
        int problems = 0;
        if (!Snap(enemy.transform.position, out Vector3 origin)) { status = "Enemy is not near a compatible NavMesh."; return; }
        foreach (Transform point in points)
        {
            if (point == null || !Snap(point.position, out Vector3 snapped) ||
                Vector3.Distance(point.position, snapped) > 0.2f || !Connected(origin, snapped)) { problems++; continue; }
            foreach (Transform other in points)
                if (other != null && other != point && !Connected(snapped, other.position)) problems++;
        }
        status = points.Count == 0 ? "No patrol points assigned." : problems == 0 ? "All assigned points and connecting paths are valid." : problems + " invalid point/path checks. Move points onto a connected NavMesh.";
    }

    private void DrawSceneTools(SceneView view)
    {
        if (workspace >= 2)
        {
            if (workspace == 2 && workspaceEditor is PatrolRouteEditor routeEditor) routeEditor.DrawSceneTools();
            if (workspace == 3 && workspaceEditor is EnemySpawnPointEditor spawnEditor) spawnEditor.DrawSceneTools();
            return;
        }
        if (workspace == 1) return;
        if (!CanEditPath) return;
        List<Transform> points = AssignedPoints();
        if (editPoints)
            foreach (Transform point in points)
            {
                if (point == null) continue;
                Handles.Label(point.position + Vector3.up * 0.3f, point.name);
                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.PositionHandle(point.position, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    if (Snap(moved, out Vector3 snapped))
                    { Undo.RecordObject(point, "Move patrol point"); point.position = snapped; MarkDirty(point); }
                    else status = "Point move rejected: no compatible NavMesh nearby.";
                    Refresh();
                }
            }
        if (!placePoints) return;
        Event current = Event.current;
        if (!current.shift || current.alt) return;
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 10000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
        bool valid = Snap(hit.point, out Vector3 position) && Snap(enemy.transform.position, out Vector3 origin) && Connected(origin, position);
        Handles.color = valid ? Color.green : Color.red;
        Handles.DrawWireDisc(valid ? position : hit.point, Vector3.up, 0.3f);
        Handles.Label(hit.point + Vector3.up * 0.3f, valid ? "Shift-click: add patrol point" : "No reachable NavMesh here");
        if (current.type == EventType.MouseDown && current.button == 0)
        {
            current.Use();
            if (valid)
            {
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Place enemy patrol point");
                points.Add(CreatePoint(position, points.Count + 1));
                AssignPoints(points);
                Undo.CollapseUndoOperations(group);
                status = "Patrol point added.";
            }
            else status = "Cannot place here: point must be on a reachable NavMesh.";
            Refresh();
        }
        view.Repaint();
    }

    private static void MarkDirty(Object target)
    {
        EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        Component component = target as Component;
        if (component != null && !EditorUtility.IsPersistent(component)) EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
    }

    private void SaveEnemyChanges()
    {
        if (enemy.enemyData != null) AssetDatabase.SaveAssetIfDirty(enemy.enemyData);
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (EditorUtility.IsPersistent(enemy)) AssetDatabase.SaveAssetIfDirty(enemy);
        else if (stage != null && enemy.gameObject.scene == stage.scene) PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, stage.assetPath);
        else if (!EditorSceneManager.SaveScene(enemy.gameObject.scene)) { status = "Scene save cancelled or failed."; return; }
        status = "Enemy settings and profile saved.";
    }
}


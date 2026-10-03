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
        if (followSelection && Selection.activeGameObject != null)
        {
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

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Enemy Lab", EditorStyles.boldLabel);
        workspace = GUILayout.Toolbar(workspace, new[] { "Enemy", "Profiles", "Routes", "Spawns" });
        if (workspace >= 2) { DrawWorkspace(); return; }
        if (workspace == 1) enemy = null;
        EditorGUILayout.LabelField("Tune enemy behaviour and build patrol points on the NavMesh.", EditorStyles.wordWrappedLabel);
        EditorGUI.BeginChangeCheck();
        EnemyController selected = (EnemyController)EditorGUILayout.ObjectField("Enemy", enemy, typeof(EnemyController), true);
        if (EditorGUI.EndChangeCheck()) SelectEnemy(selected);
        followSelection = EditorGUILayout.ToggleLeft("Follow Hierarchy / Project selection", followSelection);
        standaloneProfile = (EnemyDataSO)EditorGUILayout.ObjectField("Standalone Profile", standaloneProfile, typeof(EnemyDataSO), false);
        if (workspace == 1 && GUILayout.Button("Create Enemy Profile")) EnemyProfileMigration.CreateProfile();
        if (enemy == null)
        {
            if (standaloneProfile != null)
            {
                EditorGUILayout.HelpBox("Shared settings for all enemies using this profile. Changes apply when enemies initialize.", MessageType.Info);
                scroll = EditorGUILayout.BeginScrollView(scroll);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    SerializedObject settings = new SerializedObject(standaloneProfile);
                    settings.Update();
                    SerializedProperty property = settings.GetIterator();
                    bool enterChildren = true;
                    while (property.NextVisible(enterChildren))
                    {
                        enterChildren = false;
                        if (property.propertyPath == "m_Script" || property.propertyPath == "lootTable" || property.propertyPath == "lootPrefab" || property.propertyPath == "lootDropChance") continue;
                        EditorGUILayout.PropertyField(property, true);
                    }
                    EnemyLootEditor.Draw(settings);
                    if (settings.ApplyModifiedProperties()) EditorUtility.SetDirty(standaloneProfile);
                    if (GUILayout.Button("Save Profile")) AssetDatabase.SaveAssetIfDirty(standaloneProfile);
                }
                EditorGUILayout.EndScrollView();
                return;
            }
            EditorGUILayout.HelpBox("Select an Enemy in the scene or drag its EnemyController here. Place the enemy in a scene with a baked NavMesh to build patrol points.", MessageType.Info);
            return;
        }
        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (EditorApplication.isPlaying)
        {
            GUIStyle stateStyle = new GUIStyle(EditorStyles.helpBox);
            stateStyle.fontSize = 16;
            stateStyle.fontStyle = FontStyle.Bold;
            stateStyle.normal.textColor = enemy.StateColor;
            EditorGUILayout.LabelField("AI State: " + enemy.CurrentState, stateStyle);
            Repaint();
        }
        EditorGUILayout.HelpBox("AI colours: white Idle | green Patrol | yellow Waiting | orange Chase | purple Search | red Attack | grey Dead. Pink patrol points turn yellow when targeted.", MessageType.Info);
        EditorGUILayout.HelpBox(EditorUtility.IsPersistent(enemy) ? "Editing prefab asset: " + AssetDatabase.GetAssetPath(enemy)
            : "Editing scene instance: " + enemy.gameObject.scene.name + " / " + enemy.name, MessageType.Info);
        if (EditorApplication.isPlaying)
            EditorGUILayout.HelpBox("Enemy Lab edits are disabled during Play Mode. Settings and patrol changes are made in Edit Mode.", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            SerializedObject controller = new SerializedObject(enemy);
            controller.Update();
            Section("Behaviour Profile");
            EditorGUILayout.PropertyField(controller.FindProperty("enemyData"), new GUIContent("Enemy data profile"));
            if (controller.ApplyModifiedProperties()) MarkDirty(enemy);
            if (enemy.enemyData != null)
            {
                EditorGUILayout.HelpBox("This profile is shared. Editing it affects every enemy that uses it. Create a copy for independent tuning.", MessageType.Warning);
                if (GUILayout.Button("Create independent profile copy")) CloneProfile();
                DrawProfile();
            }
            else EditorGUILayout.HelpBox("Assign an EnemyDataSO profile before this enemy can run its AI.", MessageType.Warning);

            if (enemy.enemyData == null || !enemy.enemyData.useSharedSettings)
            {
                EditorGUILayout.HelpBox("Legacy per-enemy settings are active. Promote them to a new shared profile to preserve all current tuning.", MessageType.Info);
                if (GUILayout.Button("Promote current settings to shared Profile"))
                { EnemyProfileMigration.Promote(enemy); status = "Profile created. Assign it to any spawn point."; }
            }
            SerializedObject tuning = enemy.enemyData != null && enemy.enemyData.useSharedSettings
                ? new SerializedObject(enemy.enemyData) : controller;
            tuning.Update();
            Section("Movement and Health");
            if (enemy.enemyData != null && enemy.enemyData.useSharedSettings)
            {
                Property(tuning, "walkSpeed", "Walk speed (units/s)");
                Property(tuning, "acceleration", "Acceleration");
                Property(tuning, "turnSpeed", "Turn speed (degrees/s)");
                Property(tuning, "stoppingDistance", "Stopping distance");
                Property(tuning, "startingHealth", "Starting health");
                if (tuning.ApplyModifiedProperties()) EditorUtility.SetDirty(enemy.enemyData);
            }
            else
            {

            NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                SerializedObject navigation = new SerializedObject(agent);
                navigation.Update();
                Property(navigation, "m_Speed", "Walk speed (units/s)");
                Property(navigation, "m_Acceleration", "Acceleration");
                Property(navigation, "m_AngularSpeed", "Turn speed (degrees/s)");
                Property(navigation, "m_StoppingDistance", "Stopping distance");
                if (navigation.ApplyModifiedProperties()) MarkDirty(agent);
            }
            HitBoxManager health = enemy.GetComponent<HitBoxManager>();
            if (health != null)
            {
                SerializedObject hp = new SerializedObject(health);
                hp.Update();
                Property(hp, "health", "Starting health");
                if (hp.ApplyModifiedProperties()) MarkDirty(health);
            }

            }
            Section("Vision and Attack Details");
            controller.Update();
            tuning.Update();
            Property(tuning, "eyeHeight", "Enemy eye height");
            Property(tuning, "playerTargetHeight", "Player target height");
            Property(tuning, "attackWindup", "Attack hit delay (seconds)");
            Property(controller, "showGizmos", "Show enemy Gizmos");
            Property(controller, "patrolMarkerSize", "Patrol point marker size");
            Property(controller, "patrolPointColor", "Patrol point colour");
            Property(controller, "patrolRouteColor", "Patrol route colour");
            if (controller.ApplyModifiedProperties()) MarkDirty(enemy);
            if (tuning.ApplyModifiedProperties()) EditorUtility.SetDirty(tuning.targetObject);
            if (enemy.enemyData != null && enemy.GetComponent<NavMeshAgent>() != null && (enemy.enemyData.useSharedSettings ? enemy.enemyData.stoppingDistance : enemy.GetComponent<NavMeshAgent>().stoppingDistance) >= enemy.enemyData.attackRange)
                EditorGUILayout.HelpBox("Stopping distance should be smaller than attack range so the enemy can reach attack distance.", MessageType.Warning);

            Section("Patrol Points");
            EditorGUILayout.HelpBox("Random picks any point. Loop follows array order and wraps around. Ping-pong follows array order then reverses. Scene arrows show the chosen mode. Pink spheres are patrol points; yellow is the current target.", MessageType.Info);
            controller.Update();
            tuning.Update();
            Property(controller, "patrolRoute", "Shared Patrol Route");
            if (controller.ApplyModifiedProperties()) MarkDirty(enemy);
            if (enemy.Route != null)
            {
                SerializedObject routeData = new SerializedObject(enemy.Route);
                Property(routeData, "mode", "Patrol mode");
                EditorGUILayout.PropertyField(routeData.FindProperty("points"), new GUIContent("Shared route points"), true);
                if (routeData.ApplyModifiedProperties()) MarkDirty(enemy.Route);
                if (GUILayout.Button("Select Route for placement / generation")) Selection.activeGameObject = enemy.Route.gameObject;
            }
            else
            {
                Property(controller, "patrolMode", "Patrol mode");
                EditorGUILayout.PropertyField(controller.FindProperty("patrolPoints"), new GUIContent("Assigned patrol points"), true);
                if (CanEditPath && GUILayout.Button("Extract current points into shared Route")) ExtractRoute();
            }
            if (controller.ApplyModifiedProperties()) MarkDirty(enemy);
            if (tuning.ApplyModifiedProperties()) EditorUtility.SetDirty(tuning.targetObject);
            DrawPointSettings();
            using (new EditorGUI.DisabledScope(!CanEditPath))
            {
                placePoints = EditorGUILayout.ToggleLeft("Place points: Shift + left click in Scene View", placePoints);
                editPoints = EditorGUILayout.ToggleLeft("Move assigned points with Scene handles", editPoints);
                if (placePoints || editPoints) SceneView.RepaintAll();
                snapDistance = EditorGUILayout.Slider("NavMesh snap distance", snapDistance, 0.1f, 5f);
                Section("Generate Patrol");
                generateCount = EditorGUILayout.IntSlider("Point count", generateCount, 2, 20);
                generateRadius = EditorGUILayout.Slider("Radius around enemy", generateRadius, 1f, 50f);
                minimumSpacing = EditorGUILayout.Slider("Minimum point spacing", minimumSpacing, 0.1f, 10f);
                EditorGUILayout.HelpBox("Generation uses the enemy agent type and allowed NavMesh areas. It validates complete paths from the enemy and between generated points. It replaces assigned references; existing point objects are preserved.", MessageType.Info);
                if (GUILayout.Button("Generate connected patrol points")) GeneratePatrol();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Validate assigned paths")) ValidatePatrol();
                    if (GUILayout.Button("Clear point assignments"))
                    {
                        AssignPoints(new List<Transform>());
                        status = "Patrol assignments cleared. Existing point objects are preserved.";
                    }
                }
            }
            if (!CanEditPath) EditorGUILayout.HelpBox("Patrol placement and generation require a scene instance in Edit Mode, outside Prefab Mode.", MessageType.Info);
            Section("Physics Mass");
            controller.Update();
            tuning.Update();
            Property(tuning, "physicsMassMultiplier", "Whole-body mass multiplier");
            if (tuning.FindProperty("physicsMassMultiplier").floatValue > 5f)
                EditorGUILayout.HelpBox("This is a multiplier, not total kilograms. 65 means every bone weighs 65 times its base mass. Start at 1 for the default body weight; use per-bone kg for individual weights.", MessageType.Warning);
            EditorGUILayout.PropertyField(tuning.FindProperty("boneMassOverrides"),
                new GUIContent("Per-bone base mass (kg)"), true);
            if (controller.ApplyModifiedProperties()) MarkDirty(enemy);
            if (tuning.ApplyModifiedProperties()) EditorUtility.SetDirty(tuning.targetObject);
            EditorGUILayout.HelpBox("Effective mass = bone base mass × whole-body multiplier. Add a Humanoid bone override to customize one body part. Empty overrides keep the rig's original masses. Higher mass reduces the response to the same bullet impulse; it does not make gravity fall slower.", MessageType.Info);
            Section("Death and Loot");
            controller.Update();
            tuning.Update();
            Property(tuning, "useRagdoll", "Use ragdoll (auto-build Humanoid)");
            Property(tuning, "deathAnimation", "Death animation state");
            Property(tuning, "deathAnimationLeadIn", "Animation before ragdoll (seconds)");
            Property(tuning, "deathBlendDuration", "Animation / ragdoll blend (seconds)");
            Property(tuning, "deathAnimationDrive", "Animation support strength");
            Property(tuning, "hybridDeathPushScale", "Hybrid death push scale (0–1)");
            EditorGUILayout.HelpBox("Hybrid death starts with animation, then physics follows a fading animation support force during the blend. Increase blend time for a softer release. Set lead-in time to 0 for pure ragdoll.", MessageType.Info);
            Property(tuning, "corpseLifetime", "Corpse lifetime (seconds)");
            Property(tuning, "ragdollGravityScale", "Death gravity scale (lower = slower)");
            Property(tuning, "ragdollLinearDamping", "Death movement damping");
            Property(tuning, "ragdollAngularDamping", "Death rotation damping");
            Property(tuning, "corpseSettleDelay", "Settle time before physics sleep (seconds)");
            Property(tuning, "deathPushImpulse", "Fatal bullet push impulse");
            if (enemy.enemyData != null)
            {
                SerializedObject lootSettings = new SerializedObject(enemy.enemyData);
                lootSettings.Update(); EnemyLootEditor.Draw(lootSettings);
                if (lootSettings.ApplyModifiedProperties()) EditorUtility.SetDirty(enemy.enemyData);
            }
            Property(tuning, "lootOffset", "Loot spawn offset");
            if (controller.ApplyModifiedProperties()) MarkDirty(enemy);
            if (tuning.ApplyModifiedProperties()) EditorUtility.SetDirty(tuning.targetObject);
            EditorGUILayout.HelpBox("Corpse lifetime 0 keeps the corpse. Ragdoll uses the configured rig, or builds bone physics at runtime for a Humanoid Animator. Non-Humanoid models need a manual rig. Assign a pickup prefab for loot.", MessageType.Info);
            if (tuning.FindProperty("useRagdoll").boolValue && enemy.GetComponentsInChildren<Joint>(true).Length == 0)
                EditorGUILayout.HelpBox("No ragdoll joints yet. A Humanoid rig will be generated when the game starts; custom joint limits can still be configured manually.", MessageType.Info);
            Section("Bullet Hit Reaction");
            controller.Update();
            tuning.Update();
            Property(tuning, "enableHitReaction", "Enable partial ragdoll hit reaction");
            Property(tuning, "hitPushImpulse", "Bullet push impulse");
            Property(tuning, "hitPhysicsDuration", "Physics reaction time (seconds)");
            Property(tuning, "hitRecoveryDuration", "Return to animation (seconds)");
            Property(tuning, "hitReactionBlend", "Hit physics blend (0–1)");
            Property(tuning, "hitAnimationSupport", "Hit animation support");
            Property(tuning, "hitMaxAngle", "Maximum hit deflection (degrees)");
            if (controller.ApplyModifiedProperties()) MarkDirty(enemy);
            if (tuning.ApplyModifiedProperties()) EditorUtility.SetDirty(tuning.targetObject);
            EditorGUILayout.HelpBox("Walking and attacking continue during hits. A separate jointed rig absorbs the impulse; only struck-chain rotations blend onto the animation. Proxy physics has no world colliders. Keep impulses small. Death physics uses the full visible ragdoll.", MessageType.Info);
            if (GUILayout.Button("Save Scene / Prefab / Profile")) SaveEnemyChanges();
        }
        if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);
        EditorGUILayout.EndScrollView();
    }

    private void DrawWorkspace()
    {
        followSelection = EditorGUILayout.ToggleLeft("Follow selection", followSelection);
        if (workspace == 2)
        {
            selectedRoute = (PatrolRoute)EditorGUILayout.ObjectField("Patrol Route", selectedRoute, typeof(PatrolRoute), true);
            if (GUILayout.Button("Create Patrol Route")) EnemyProfileMigration.CreateRoute();
        }
        else
        {
            selectedSpawn = (EnemySpawnPoint)EditorGUILayout.ObjectField("Spawn Point", selectedSpawn, typeof(EnemySpawnPoint), true);
            if (GUILayout.Button("Create Spawn Point")) EnemyProfileMigration.CreateSpawner();
        }
        Object target = workspace == 2 ? (Object)selectedRoute : selectedSpawn;
        if (target == null) { EditorGUILayout.HelpBox("Select an existing object or create one here.", MessageType.Info); return; }
        Editor.CreateCachedEditor(target, null, ref workspaceEditor);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        workspaceEditor.OnInspectorGUI();
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
    private void DrawProfile()
    {
        SerializedObject profile = new SerializedObject(enemy.enemyData);
        profile.Update();
        Section("Detection");
        Property(profile, "detectionRadius", "Detection radius");
        Property(profile, "fieldOfView", "Field of view (degrees)");
        Property(profile, "obstacleMask", "Sight-blocking layers");
        Section("Patrol and Alert");
        Property(profile, "waitTimeAtPoint", "Wait at point (seconds)");
        Property(profile, "lostSightCooldown", "Lost sight timeout (seconds)");
        Section("Attack");
        Property(profile, "minAttack", "Minimum damage");
        Property(profile, "maxAttack", "Maximum damage");
        Property(profile, "attackRange", "Attack range");
        Property(profile, "attackCooldown", "Attack interval (seconds)");
        if (profile.FindProperty("maxAttack").floatValue < profile.FindProperty("minAttack").floatValue)
            EditorGUILayout.HelpBox("Maximum damage must be at least minimum damage.", MessageType.Warning);
        if (profile.ApplyModifiedProperties()) { EditorUtility.SetDirty(enemy.enemyData); Refresh(); }
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
        if (property != null) EditorGUILayout.PropertyField(property, new GUIContent(label));
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


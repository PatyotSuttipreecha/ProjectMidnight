using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class EnemyProfileMigration
{
    public static EnemyDataSO Promote(EnemyController enemy)
    {
        const string folder = "Assets/Script/EnemySO/Profiles";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Script/EnemySO", "Profiles");
        EnemyDataSO profile = enemy.enemyData != null ? Object.Instantiate(enemy.enemyData) : ScriptableObject.CreateInstance<EnemyDataSO>();
        profile.name = enemy.name + " Profile";
        SerializedObject source = new SerializedObject(enemy);
        SerializedObject destination = new SerializedObject(profile);
        SerializedProperty iterator = destination.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (iterator.propertyPath.StartsWith("m_")) continue;
            SerializedProperty original = source.FindProperty(iterator.propertyPath);
            if (original != null && original.propertyType == iterator.propertyType) destination.CopyFromSerializedProperty(original);
        }
        destination.ApplyModifiedPropertiesWithoutUndo();
        profile.useSharedSettings = true;
        UnityEngine.AI.NavMeshAgent agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) { profile.walkSpeed = agent.speed; profile.acceleration = agent.acceleration; profile.turnSpeed = agent.angularSpeed; profile.stoppingDistance = agent.stoppingDistance; }
        HitBoxManager health = enemy.GetComponent<HitBoxManager>();
        if (health != null) profile.startingHealth = health.health;
        string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + profile.name + ".asset");
        AssetDatabase.CreateAsset(profile, path);
        Undo.RegisterCreatedObjectUndo(profile, "Create shared enemy profile");
        Undo.RecordObject(enemy, "Assign shared enemy profile");
        enemy.enemyData = profile;
        EditorUtility.SetDirty(enemy);
        if (PrefabUtility.IsPartOfPrefabInstance(enemy)) PrefabUtility.RecordPrefabInstancePropertyModifications(enemy);
        if (!EditorUtility.IsPersistent(enemy)) EditorSceneManager.MarkSceneDirty(enemy.gameObject.scene);
        return profile;
    }

    [MenuItem("System Modification/Enemy Lab/Create/Profile")]
    public static void CreateProfile()
    {
        string path = EditorUtility.SaveFilePanelInProject("Create Enemy Profile", "Enemy Profile", "asset", "Choose where to save the shared settings.");
        if (string.IsNullOrEmpty(path)) return;
        EnemyDataSO profile = ScriptableObject.CreateInstance<EnemyDataSO>();
        profile.useSharedSettings = true;
        AssetDatabase.CreateAsset(profile, path);
        Undo.RegisterCreatedObjectUndo(profile, "Create enemy profile");
        Selection.activeObject = profile;
    }

    [MenuItem("System Modification/Enemy Lab/Create/Spawn Point")]
    public static void CreateSpawner()
    {
        GameObject obj = new GameObject("Enemy Spawn Point");
        Undo.RegisterCreatedObjectUndo(obj, "Create enemy spawn point");
        obj.AddComponent<EnemySpawnPoint>();
        if (SceneView.lastActiveSceneView != null) obj.transform.position = SceneView.lastActiveSceneView.pivot;
        Selection.activeGameObject = obj;
    }
    [MenuItem("System Modification/Enemy Lab/Create/Patrol Route")]
    public static void CreateRoute()
    {
        GameObject obj = new GameObject("Patrol Route");
        Undo.RegisterCreatedObjectUndo(obj, "Create patrol route");
        obj.AddComponent<PatrolRoute>();
        if (SceneView.lastActiveSceneView != null) obj.transform.position = SceneView.lastActiveSceneView.pivot;
        Selection.activeGameObject = obj;
    }
}

[CustomEditor(typeof(EnemyController))]
public class EnemyControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "isAttacking", "isAlert");
        serializedObject.ApplyModifiedProperties();
        EnemyController enemy = (EnemyController)target;
        if (Application.isPlaying) EnemyDiagnosticsGUI.Draw(enemy);
        if (enemy.enemyData != null) EnemyWeaponLabWindow.DrawProfileLink(enemy.enemyData);
        EditorGUILayout.HelpBox("Enemy tuning, including ragdoll, mass and loot, belongs to its shared Profile. Use Enemy Lab to edit it. Patrol points and wait times belong to the Route.", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (enemy.enemyData == null || !enemy.enemyData.useSharedSettings)
            {
                EditorGUILayout.HelpBox("Legacy settings are preserved. Convert once to copy this enemy's existing tuning into a shared Profile.", MessageType.Warning);
                if (GUILayout.Button("Move current settings to Profile")) EnemyProfileMigration.Promote(enemy);
            }
            if (GUILayout.Button("Open Enemy Lab")) { Selection.activeObject = enemy.gameObject; EnemyLabWindow.Open(); }
            if (enemy.enemyData != null && GUILayout.Button("Edit Profile"))
            { Selection.activeObject = enemy.enemyData; EnemyLabWindow.OpenProfiles(); }
        }
    }
}

[CustomEditor(typeof(EnemyDataSO))]
public class EnemyDataProfileEditor : Editor
{
    private readonly EnemyProfileGUI profileGUI = new EnemyProfileGUI();
    public override void OnInspectorGUI()
    {
        profileGUI.Draw((EnemyDataSO)target);
    }
}

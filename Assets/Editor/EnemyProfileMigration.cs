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
        DrawDefaultInspector();
        EnemyController enemy = (EnemyController)target;
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

public static class EnemyLootEditor
{
    public static void Draw(SerializedObject data)
    {
        SerializedProperty table = data.FindProperty("lootTable");
        if (table == null) return;
        SerializedProperty chance = data.FindProperty("lootDropChance");
        chance.floatValue = EditorGUILayout.Slider("Drop chance (%)", chance.floatValue * 100f, 0f, 100f) / 100f;
        EditorGUILayout.HelpBox("First roll the drop chance. If successful, select one item from the 100% Loot Table. Otherwise no item drops.", MessageType.Info);
        EditorGUILayout.PropertyField(table, new GUIContent("Loot Table (one drop per death)"), true);
        if (table.arraySize == 0)
        {
            EditorGUILayout.HelpBox("Add pickup prefabs and percentages totalling 100%. An empty table preserves the old single-loot setup.", MessageType.Info);
            EditorGUILayout.PropertyField(data.FindProperty("lootPrefab"), new GUIContent("Legacy loot prefab"));
            return;
        }
        float total = 0f; bool missing = false;
        for (int i = 0; i < table.arraySize; i++)
        {
            SerializedProperty entry = table.GetArrayElementAtIndex(i);
            float weight = entry.FindPropertyRelative("percent").floatValue;
            total += weight;
            if (weight > 0f && entry.FindPropertyRelative("prefab").objectReferenceValue == null) missing = true;
        }
        EditorGUILayout.HelpBox("Total: " + total.ToString("0.##") + "% / 100%" + (missing ? " — missing prefab." : "") + "\nEach death selects exactly one item. Invalid tables do not drop loot.", Mathf.Abs(total - 100f) <= 0.01f && !missing ? MessageType.Info : MessageType.Warning);
        if (GUILayout.Button("Normalize percentages to 100%"))
        {
            float positiveTotal = 0f;
            for (int i = 0; i < table.arraySize; i++) positiveTotal += Mathf.Max(0f, table.GetArrayElementAtIndex(i).FindPropertyRelative("percent").floatValue);
            float assigned = 0f;
            for (int i = 0; i < table.arraySize; i++)
            {
                SerializedProperty percent = table.GetArrayElementAtIndex(i).FindPropertyRelative("percent");
                percent.floatValue = i == table.arraySize - 1 ? Mathf.Max(0f, 100f - assigned) : positiveTotal > 0f ? Mathf.Max(0f, percent.floatValue) / positiveTotal * 100f : 100f / table.arraySize;
                assigned += percent.floatValue;
            }
        }
    }
}

[CustomEditor(typeof(EnemyDataSO))]
public class EnemyDataProfileEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "lootTable", "lootPrefab", "lootDropChance");
        EnemyLootEditor.Draw(serializedObject);
        serializedObject.ApplyModifiedProperties();
    }
}

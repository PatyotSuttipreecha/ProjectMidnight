using UnityEditor;
using UnityEngine;

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

using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "System Modification/TerrainTools/Decoration Placement Data", fileName = "Decoration_New")]
public sealed class DecorationPlacementData : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        public GameObject prefab;
        [Range(0, 100)] public float weight = 100;
        public string category;
        public string tags;
    }
    public List<Entry> entries = new List<Entry>();
    public int Count => entries == null ? 0 : entries.Count;
    public GameObject GetPrefab(int index) => index >= 0 && index < Count && entries[index] != null ? entries[index].prefab : null;
    public float GetWeight(int index)
    {
        if (!GetPrefab(index)) return 0;
        float value = entries[index].weight;
        return float.IsNaN(value) || float.IsInfinity(value) ? 0 : Mathf.Clamp(value, 0, 100);
    }
}

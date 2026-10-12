using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Gameplay/Item Spawn Group")]
public class ItemSpawnGroup : MonoBehaviour
{
    public bool spawnOnStart = true;
    public bool rollOnce = true;
    [Min(0)] public int pointsToSelect = 4;
    [Tooltip("Selected points roll their Spawn Chance. The final number of pickups may be less than Points To Select.")]
    public bool applyPointChances;
    public bool HasRolled { get; private set; }
    public int LastSelectedCount { get; private set; }
    public int LastSpawnedCount { get; private set; }

    private void Start() { if (spawnOnStart) Spawn(); }

    public List<ItemSpawnPoint> GetEligiblePoints()
    {
        var result = new List<ItemSpawnPoint>();
        foreach (var point in GetComponentsInChildren<ItemSpawnPoint>(true))
            if (point.Group == this && point.CanSpawn) result.Add(point);
        return result;
    }

    public void Spawn()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || (rollOnce && HasRolled)) return;
        HasRolled = true;
        var points = GetEligiblePoints();
        // Shuffle without replacement: every eligible location has the same selection chance.
        for (int i = points.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var swap = points[i]; points[i] = points[j]; points[j] = swap;
        }
        LastSelectedCount = Mathf.Min(Mathf.Max(0, pointsToSelect), points.Count);
        LastSpawnedCount = 0;
        for (int i = 0; i < LastSelectedCount; i++)
            if (points[i].SpawnAllocated(applyPointChances) != null) LastSpawnedCount++;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(.25f, .8f, 1f);
        foreach (var point in GetComponentsInChildren<ItemSpawnPoint>(true))
            if (point.Group == this) Gizmos.DrawLine(transform.position, point.SpawnPosition);
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public class EnemySpawnPoint : MonoBehaviour
{
    public EnemyController enemyPrefab;
    public PatrolRoute patrolRoute;
    [UnityEngine.Serialization.FormerlySerializedAs("player")]
    [Tooltip("Optional target. Leave empty to use the prefab target or automatically find the object tagged Player.")]
    public Transform target;
    public EnemyDataSO EffectiveProfile => enemyPrefab != null ? enemyPrefab.enemyData : null;
    public bool spawnOnStart = true;
    [Min(1)] public int initialCount = 1;
    [Min(1)] public int maxAlive = 3;
    public bool respawn;
    [Min(0.1f)] public float spawnInterval = 5f;
    [Min(0)] public int totalSpawnLimit;
    [Min(0f)] public float spawnRadius = 2f;
    [Min(0.1f)] public float navMeshSnapDistance = 2f;
    private readonly List<EnemyController> living = new List<EnemyController>();
    private int spawnedCount;
    private float nextSpawnTime;
    public int AliveCount { get { living.RemoveAll(e => e == null || e.IsDead); return living.Count; } }

    private void Start()
    {
        if (spawnOnStart) for (int i = 0; i < Mathf.Min(initialCount, maxAlive); i++) SpawnOne();
        nextSpawnTime = Time.time + spawnInterval;
    }
    private void Update()
    {
        if (!respawn || Time.time < nextSpawnTime) return;
        nextSpawnTime = Time.time + Mathf.Max(0.1f, spawnInterval);
        SpawnOne();
    }
    public EnemyController SpawnOne()
    {
        if (!Application.isPlaying) return null;
        if (enemyPrefab == null || AliveCount >= maxAlive ||
            (totalSpawnLimit > 0 && spawnedCount >= totalSpawnLimit)) return null;
        EnemyDataSO resolvedProfile = EffectiveProfile;
        if (resolvedProfile == null)
        {
            Debug.LogWarning("Assign an Enemy Profile on the Enemy Prefab.", this);
            return null;
        }
        NavMeshAgent templateAgent = enemyPrefab.GetComponent<NavMeshAgent>();
        NavMeshQueryFilter filter = new NavMeshQueryFilter {
            agentTypeID = templateAgent != null ? templateAgent.agentTypeID : 0,
            areaMask = templateAgent != null ? templateAgent.areaMask : NavMesh.AllAreas };
        Vector3 spawnPosition = Vector3.zero;
        bool found = false;
        for (int i = 0; i < 20; i++)
        {
            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            Vector3 candidate = transform.position + new Vector3(offset.x, 0f, offset.y);
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSnapDistance, filter)) continue;
            if (patrolRoute != null && patrolRoute.points != null && patrolRoute.points.Length > 0)
            {
                Transform first = System.Array.Find(patrolRoute.points, point => point != null);
                if (first != null)
                {
                    NavMeshPath path = new NavMeshPath();
                    if (!NavMesh.CalculatePath(hit.position, first.position, filter, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                }
            }
            spawnPosition = hit.position; found = true; break;
        }
        if (!found) { Debug.LogWarning("No reachable NavMesh at this spawn point.", this); return null; }
        // An inactive staging parent prevents Awake from running before profile/route injection.
        GameObject staging = new GameObject("Enemy Spawn Staging");
        staging.SetActive(false);
        SceneManager.MoveGameObjectToScene(staging, gameObject.scene);
        EnemyController instance = Instantiate(enemyPrefab, spawnPosition, transform.rotation, staging.transform);
        instance.gameObject.SetActive(false);
        instance.ConfigureSpawn(resolvedProfile, patrolRoute, target);
        instance.transform.SetParent(null, true);
        instance.gameObject.SetActive(true);
        Destroy(staging);
        living.Add(instance);
        spawnedCount++;
        return instance;
    }
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.65f, 0f);
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
        Gizmos.DrawCube(transform.position + Vector3.up * 0.25f, Vector3.one * 0.35f);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up, name + " | Enemy Spawn");
#endif
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.Events;

public class EnemySpawnPoint : MonoBehaviour
{
    public enum SpawnMode { ExistingSettings, Once, Continuous, FixedCount }
    [System.Serializable] public class EnemySpawnedEvent : UnityEvent<EnemyController> { }
    public EnemyController enemyPrefab;
    public PatrolRoute patrolRoute;
    [UnityEngine.Serialization.FormerlySerializedAs("player")]
    [Tooltip("Optional target. Leave empty to use the prefab target or automatically find the object tagged Player.")]
    public Transform target;
    public EnemyDataSO EffectiveProfile => enemyPrefab != null ? enemyPrefab.enemyData : null;
    [Header("Schedule")]
    [Tooltip("Existing Settings preserves old scene/prefab behavior. Choose one of the other modes for a new encounter.")]
    public SpawnMode spawnMode = SpawnMode.ExistingSettings;
    public bool spawnOnStart = true;
    [Min(1)] public int initialCount = 1;
    [Min(1)] public int maxAlive = 3;
    [HideInInspector] public bool respawn;
    [HideInInspector, Min(0.1f)] public float spawnInterval = 5f;
    [HideInInspector, Min(0)] public int totalSpawnLimit;
    [Tooltip("Total successful spawns, including the initial batch. Fixed Count mode only.")]
    [Min(1)] public int enemyCount = 10;
    public bool randomizeInterval = true;
    [Min(0.1f)] public float minimumInterval = 3f;
    [Min(0.1f)] public float maximumInterval = 6f;
    [Header("Placement")]
    [Min(0f)] public float spawnRadius = 2f;
    [Min(0.1f)] public float navMeshSnapDistance = 2f;
    private readonly List<EnemyController> living = new List<EnemyController>();
    private int spawnedCount;
    private float nextSpawnTime;
    private bool initialBatchIssued;
    public bool IsSpawning { get; private set; }
    public bool HasFinishedSpawning { get; private set; }
    public int SpawnedCount => spawnedCount;
    [Header("Events")]
    public EnemySpawnedEvent onEnemySpawned = new EnemySpawnedEvent();
    [Tooltip("No more scheduled enemies will spawn. This does NOT mean all enemies have died.")]
    public UnityEvent onSpawningFinished = new UnityEvent();
    public int AliveCount { get { living.RemoveAll(e => e == null || e.IsDead); return living.Count; } }

    private void Start()
    {
        // Old respawn points used to run their timer even without an initial batch.
        if (spawnMode == SpawnMode.ExistingSettings)
        {
            if (spawnOnStart) IssueInitialBatch();
            IsSpawning = respawn && HasBudget;
            ScheduleNext();
        }
        else if (spawnOnStart) StartSpawning();
    }
    private void Update()
    {
        if (!IsSpawning || Time.time < nextSpawnTime) return;
        if (!HasBudget) { FinishSpawning(); return; }
        // At most one attempt per interval; full capacity and failed NavMesh samples do not consume the budget.
        ScheduleNext();
        SpawnOne();
    }
    private int Budget => spawnMode == SpawnMode.Once ? Mathf.Max(1, initialCount) :
        spawnMode == SpawnMode.FixedCount ? Mathf.Max(1, enemyCount) :
        spawnMode == SpawnMode.ExistingSettings && totalSpawnLimit > 0 ? totalSpawnLimit : int.MaxValue;
    private bool HasBudget => spawnedCount < Budget;
    private bool Repeats => spawnMode == SpawnMode.Continuous || spawnMode == SpawnMode.FixedCount ||
        spawnMode == SpawnMode.ExistingSettings && respawn;
    public float SampleInterval()
    {
        if (spawnMode == SpawnMode.ExistingSettings) return Mathf.Max(.1f, spawnInterval);
        float min = Mathf.Max(.1f, minimumInterval);
        float max = Mathf.Max(min, maximumInterval);
        return randomizeInterval ? Random.Range(min, max) : min;
    }
    private void ScheduleNext() => nextSpawnTime = Time.time + SampleInterval();
    private void IssueInitialBatch(bool respectRunning = false)
    {
        initialBatchIssued = true;
        for (int i = 0; i < Mathf.Min(Mathf.Max(1, initialCount), Mathf.Max(1, maxAlive)); i++)
        {
            SpawnOne();
            if (HasFinishedSpawning || respectRunning && !IsSpawning) break; // Events may stop or pause the encounter.
        }
    }
    public void StartSpawning()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || IsSpawning || !HasBudget ||
            spawnMode == SpawnMode.Once && initialBatchIssued) return;
        HasFinishedSpawning = false;
        IsSpawning = true;
        if (!initialBatchIssued) IssueInitialBatch(true);
        if (!IsSpawning) return;
        if (!Repeats || !HasBudget) FinishSpawning();
        else ScheduleNext();
    }
    public void PauseSpawning() => IsSpawning = false;
    public void StopSpawning() { if (IsSpawning) FinishSpawning(); }
    private void FinishSpawning()
    {
        IsSpawning = false;
        if (HasFinishedSpawning) return;
        HasFinishedSpawning = true;
        onSpawningFinished.Invoke();
    }
    public EnemyController SpawnOne()
    {
        if (!Application.isPlaying) return null;
        if (enemyPrefab == null || AliveCount >= Mathf.Max(1, maxAlive) || !HasBudget) return null;
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
        onEnemySpawned.Invoke(instance);
        if (IsSpawning && !HasBudget) FinishSpawning();
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

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.Events;

[DefaultExecutionOrder(350)]
public class EnemySpawnPoint : MonoBehaviour
{
    public enum SpawnMode { ExistingSettings, Once, Continuous, FixedCount }
    [System.Serializable] public class EnemySpawnedEvent : UnityEvent<EnemyController> { }
    public EnemyController enemyPrefab;
    public enum PrefabSelection { Single, Random }
    [Tooltip("Single uses Enemy Prefab. Random selects a valid entry each time an enemy spawns; repeated types are allowed across spawns.")]
    public PrefabSelection prefabSelection;
    [Tooltip("Random selection pool. Each entry has equal chance. Every prefab uses its own Enemy Profile and Starting Weapon. Empty entries or prefabs without profiles are skipped.")]
    public EnemyController[] enemyPrefabs = new EnemyController[0];
    public IEnumerable<EnemyController> ConfiguredPrefabs => prefabSelection == PrefabSelection.Single
        ? new[] { enemyPrefab } : enemyPrefabs ?? new EnemyController[0];
    public EnemyController PreviewPrefab
    {
        get { foreach (var candidate in ConfiguredPrefabs) if (candidate != null && candidate.enemyData != null) return candidate; return null; }
    }
    public EnemyController SelectPrefab()
    {
        if (prefabSelection == PrefabSelection.Single) return PreviewPrefab;
        // Reservoir sampling: equal chance per valid entry, with no temporary list.
        EnemyController selected = null;
        int count = 0;
        foreach (var candidate in ConfiguredPrefabs)
            if (candidate != null && candidate.enemyData != null && Random.Range(0, ++count) == 0) selected = candidate;
        return selected;
    }
    public PatrolRoute patrolRoute;
    public enum AggressionMode { UseProfile, Aggressive, Normal }
    [Header("Encounter Behaviour")]
    [Tooltip("Override the profile for new enemies. Enemy Group takes priority.")]
    public AggressionMode aggressionMode;
    [Tooltip("Optional group. New enemies inherit its current Aggressive state.")]
    public EnemyGroup enemyGroup;
    [UnityEngine.Serialization.FormerlySerializedAs("player")]
    [Tooltip("Optional target. Leave empty to use the prefab target or automatically find the object tagged Player.")]
    public Transform target;
    public EnemyDataSO EffectiveProfile => PreviewPrefab != null ? PreviewPrefab.enemyData : null;
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
    [Header("Spawn Visibility")]
    [Tooltip("Spawn only when the entire enemy placement bounds are outside the gameplay camera frustum. Visible positions are skipped without consuming the spawn budget. Continuous points retry on their normal interval.")]
    public bool spawnOutOfView;
    [Tooltip("Optional gameplay render camera. Empty uses Camera.main. If no active camera is available, hidden spawning waits rather than spawning unchecked.")]
    public Camera visibilityCamera;
    public string LastPlacementResult { get; private set; } = "Not attempted";

    public bool IsPositionOutOfView(Vector3 position, NavMeshAgent templateAgent = null)
    {
        Camera camera = visibilityCamera != null ? visibilityCamera : Camera.main;
        if (camera == null || !camera.isActiveAndEnabled) return false;
        // Test a body-sized volume, not just its feet: a partly visible enemy
        // must not pop into the edge of the screen. Padding covers animation.
        float height = Mathf.Max(2f, templateAgent != null ? templateAgent.height : 2f);
        float radius = Mathf.Max(.5f, templateAgent != null ? templateAgent.radius : .5f);
        float offset = templateAgent != null ? templateAgent.baseOffset : 0;
        Bounds bounds = new Bounds(position + Vector3.up * (offset + height * .5f),
            new Vector3(radius * 2 + 1, height + 1, radius * 2 + 1));
        return !GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(camera), bounds);
    }
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
    public void RegisterLivingEnemies(EnemyGroup group)
    {
        foreach (var enemy in living) group.RegisterEnemy(enemy);
    }

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
    // Cinemachine applies the visible camera pose in LateUpdate. Test that pose,
    // not the previous frame's view while the player is turning the camera.
    private void LateUpdate()
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
        if (AliveCount >= Mathf.Max(1, maxAlive) || !HasBudget) return null;
        EnemyController selectedPrefab = SelectPrefab();
        if (selectedPrefab == null)
        {
            Debug.LogWarning("No valid enemy prefab. Assign a prefab with an Enemy Profile in the selected Single/Random mode.", this);
            return null;
        }
        EnemyDataSO resolvedProfile = selectedPrefab.enemyData;
        NavMeshAgent templateAgent = selectedPrefab.GetComponent<NavMeshAgent>();
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
            if (spawnOutOfView && !IsPositionOutOfView(hit.position, templateAgent)) continue;
            spawnPosition = hit.position; found = true; break;
        }
        if (!found)
        {
            LastPlacementResult = spawnOutOfView ? "Waiting: no hidden, reachable position (or no active gameplay camera)" : "No reachable NavMesh";
            if (!spawnOutOfView) Debug.LogWarning("No reachable NavMesh at this spawn point.", this);
            return null;
        }
        LastPlacementResult = "Spawned at " + spawnPosition.ToString("F1");
        // An inactive staging parent prevents Awake from running before profile/route injection.
        GameObject staging = new GameObject("Enemy Spawn Staging");
        staging.SetActive(false);
        SceneManager.MoveGameObjectToScene(staging, gameObject.scene);
        EnemyController instance = Instantiate(selectedPrefab, spawnPosition, transform.rotation, staging.transform);
        instance.gameObject.SetActive(false);
        instance.ConfigureSpawn(resolvedProfile, patrolRoute, target);
        instance.transform.SetParent(null, true);
        instance.gameObject.SetActive(true);
        instance.RecordSpawnSource(this);
        if (aggressionMode != AggressionMode.UseProfile)
            instance.SetAggressive(aggressionMode == AggressionMode.Aggressive);
        if (enemyGroup != null) enemyGroup.RegisterEnemy(instance);
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

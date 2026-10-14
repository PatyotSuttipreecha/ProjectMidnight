using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Enemy/Enemy Group")]
public class EnemyGroup : MonoBehaviour
{
    [Tooltip("Initial group mode. Runtime changes never modify Enemy Profiles.")]
    public bool aggressiveOnStart;
    [Tooltip("Scene members. Paused actors remain paused until StartPatrol is called.")]
    public EnemyController[] sceneEnemies = new EnemyController[0];
    [Tooltip("These points and their existing/future enemies belong to this group.")]
    public EnemySpawnPoint[] spawnPoints = new EnemySpawnPoint[0];
    private readonly HashSet<EnemyController> members = new HashSet<EnemyController>();
    private bool initialized;
    public bool IsAggressive { get; private set; }
    private void Initialize()
    {
        if (initialized) return;
        initialized = true;
        IsAggressive = aggressiveOnStart;
    }
    private void Awake()
    {
        Initialize();
        foreach (var enemy in sceneEnemies) RegisterEnemy(enemy);
        foreach (var point in spawnPoints)
        {
            if (point == null) continue;
            point.enemyGroup = this;
            point.RegisterLivingEnemies(this);
        }
    }
    public void RegisterEnemy(EnemyController enemy)
    {
        if (enemy == null || enemy.IsDead) return;
        Initialize();
        members.Add(enemy);
        enemy.SetAggressive(IsAggressive);
    }
    public void SetAggressive(bool enabled)
    {
        Initialize();
        IsAggressive = enabled;
        members.RemoveWhere(enemy => enemy == null || enemy.IsDead);
        foreach (var enemy in members) enemy.SetAggressive(enabled);
    }
    public void EnableAggressive() => SetAggressive(true);
    public void DisableAggressive() => SetAggressive(false);
}

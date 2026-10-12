using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[AddComponentMenu("Gameplay/Enemy Defeated Trigger")]
public class EnemyDefeatedTrigger : MonoBehaviour
{
    public enum CompletionMode { SpecificEnemy, AllTrackedEnemies }

    [Header("Enemies")]
    public CompletionMode mode = CompletionMode.SpecificEnemy;
    public EnemyController specificEnemy;
    public List<EnemyController> enemies = new List<EnemyController>();
    [Tooltip("Optional: include enemies under this parent at startup, including inactive children. This is a snapshot, not a scene-wide search.")]
    public Transform enemyGroupRoot;
    [Tooltip("For waves: disable this until every enemy has been registered, then call SealGroup.")]
    public bool groupSealedOnStart = true;

    [Header("Activation")]
    public bool armed = true;
    [Min(0)] public float delay;
    [Tooltip("Optional: uses the existing controller's player lock and fade. Leave empty to use only On Triggered.")]
    public CutsceneController cutscene;
    public UnityEvent onTriggered = new UnityEvent();

    public bool HasTriggered { get; private set; }
    public int TrackedCount => tracked.Count;
    public int DefeatedCount => defeated.Count;
    private readonly HashSet<EnemyController> tracked = new HashSet<EnemyController>();
    private readonly HashSet<EnemyController> defeated = new HashSet<EnemyController>();
    private bool initialized, groupSealed;
    private Coroutine pending;

    private void OnEnable()
    {
        if (!initialized)
        {
            initialized = true;
            groupSealed = groupSealedOnStart;
            if (mode == CompletionMode.SpecificEnemy) AddEnemy(specificEnemy);
            else
            {
                if (enemies != null) foreach (var enemy in enemies) AddEnemy(enemy);
                if (enemyGroupRoot != null)
                    foreach (var enemy in enemyGroupRoot.GetComponentsInChildren<EnemyController>(true)) AddEnemy(enemy);
            }
        }
        else foreach (var enemy in tracked) Subscribe(enemy);
        Evaluate();
    }
    private void AddEnemy(EnemyController enemy)
    {
        if (enemy == null || !tracked.Add(enemy)) return;
        if (isActiveAndEnabled) Subscribe(enemy);
    }
    private void Subscribe(EnemyController enemy)
    {
        if (enemy == null) return;
        enemy.Died -= OnEnemyDied;
        enemy.Died += OnEnemyDied;
        if (enemy.IsDead) defeated.Add(enemy);
    }
    // Use from spawn code immediately after SpawnOne returns. Only open groups accept new members.
    public void RegisterEnemy(EnemyController enemy)
    {
        if (mode != CompletionMode.AllTrackedEnemies || groupSealed || HasTriggered) return;
        AddEnemy(enemy);
        Evaluate();
    }
    public void SealGroup() { groupSealed = true; Evaluate(); }
    public void Arm() { armed = true; Evaluate(); }
    public void Disarm()
    {
        armed = false;
        CancelPending();
    }
    private void OnEnemyDied(EnemyController enemy)
    {
        if (!tracked.Contains(enemy)) return;
        defeated.Add(enemy);
        Evaluate();
    }
    private bool Complete => tracked.Count > 0 && defeated.Count == tracked.Count &&
        (mode == CompletionMode.SpecificEnemy || groupSealed);
    private void Evaluate()
    {
        if (!isActiveAndEnabled || !armed || HasTriggered || pending != null || !Complete) return;
        if (delay <= 0) Fire();
        else pending = StartCoroutine(FireAfterDelay());
    }
    private IEnumerator FireAfterDelay()
    {
        yield return new WaitForSeconds(delay);
        pending = null;
        if (isActiveAndEnabled && armed && !HasTriggered && Complete) Fire();
    }
    private void Fire()
    {
        HasTriggered = true; // Set before callbacks to prevent reentrant activation.
        if (cutscene != null) cutscene.Play();
        onTriggered.Invoke();
    }
    private void CancelPending()
    {
        if (pending != null) StopCoroutine(pending);
        pending = null;
    }
    private void OnDisable()
    {
        CancelPending();
        // Keep death subscriptions while disabled so short-lived corpses still count.
    }
    private void OnDestroy()
    {
        foreach (var enemy in tracked) if (enemy != null) enemy.Died -= OnEnemyDied;
    }
}

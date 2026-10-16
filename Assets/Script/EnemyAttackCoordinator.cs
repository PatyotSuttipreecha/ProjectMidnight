using System.Collections.Generic;
using UnityEngine;

// Reservations are scoped to the actual target, so separate encounters do not share a turn limit.
public static class EnemyAttackCoordinator
{
    private sealed class Turn { public Transform Target; public float Expires; public int Limit; }
    private static readonly Dictionary<EnemyController, Turn> turns = new Dictionary<EnemyController, Turn>();
    private static readonly List<EnemyController> expired = new List<EnemyController>();
    private static readonly Dictionary<Transform, float> nextStarts = new Dictionary<Transform, float>();
    private static readonly List<Transform> expiredTargets = new List<Transform>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { turns.Clear(); expired.Clear(); nextStarts.Clear(); expiredTargets.Clear(); }

    public static float StartDelay(Transform target) => target != null && nextStarts.TryGetValue(target, out var next)
        ? Mathf.Max(0, next - Time.time) : 0;

    public static bool TryStartAttack(EnemyController owner, Transform target)
    {
        if (owner == null || target == null || !owner.isActiveAndEnabled || owner.IsDead || !owner.IsPatrolRunning) return false;
        if (StartDelay(target) > 0) return false;
        // Reserve atomically before Preparation. Cancellation keeps the short gap;
        // releasing a turn must not let another enemy start in the same frame.
        nextStarts[target] = Time.time + EnemyCombatSettings.StartInterval;
        return true;
    }

    private static void Clean()
    {
        expiredTargets.Clear();
        foreach (var entry in nextStarts)
            if (entry.Key == null || entry.Value <= Time.time) expiredTargets.Add(entry.Key);
        foreach (var target in expiredTargets) nextStarts.Remove(target);
        expired.Clear();
        foreach (var entry in turns)
            if (entry.Key == null || !entry.Key.isActiveAndEnabled || entry.Key.IsDead || !entry.Key.IsPatrolRunning ||
                entry.Value.Target == null || entry.Value.Expires <= Time.time) expired.Add(entry.Key);
        foreach (var owner in expired) turns.Remove(owner);
    }
    public static bool HasTurn(EnemyController owner, Transform target)
    {
        Clean();
        return owner != null && turns.TryGetValue(owner, out var turn) && turn.Target == target;
    }
    public static bool Request(EnemyController owner, Transform target, int limit)
    {
        Clean();
        if (owner == null || target == null || !owner.isActiveAndEnabled || owner.IsDead || !owner.IsPatrolRunning) return false;
        if (turns.TryGetValue(owner, out var current))
        {
            if (current.Target == target) { current.Expires = Time.time + 1f; return true; }
            turns.Remove(owner);
        }
        int count = 0;
        limit = Mathf.Max(1, limit);
        foreach (var turn in turns.Values)
            if (turn.Target == target) { count++; limit = Mathf.Min(limit, turn.Limit); }
        if (count >= limit) return false;
        turns[owner] = new Turn { Target = target, Expires = Time.time + 1f, Limit = limit };
        return true;
    }
    public static void Release(EnemyController owner) { if (!ReferenceEquals(owner, null)) turns.Remove(owner); }
}

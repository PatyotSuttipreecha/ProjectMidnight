using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Pressure ownership survives melee/recovery. Running is a separate decision.
public static class EnemyRushCoordinator
{
    private sealed class Slot
    {
        public Transform Target;
        public float ProgressTime;
        public Vector3 Position;
    }
    private sealed class Encounter
    {
        public bool Formed, EscapePaused;
        public float ResumeAt, NextReplacement;
        public int Frame = -1;
        public readonly List<EnemyController> Candidates = new List<EnemyController>();
    }
    private static readonly Dictionary<EnemyController, Slot> slots = new Dictionary<EnemyController, Slot>();
    private static readonly Dictionary<EnemyController, float> cooldowns = new Dictionary<EnemyController, float>();
    private static readonly Dictionary<Transform, Encounter> encounters = new Dictionary<Transform, Encounter>();
    private static readonly List<EnemyController> removed = new List<EnemyController>();
    private static readonly List<Transform> oldTargets = new List<Transform>();
    private static readonly NavMeshPath path = new NavMeshPath();
    private static int cleanupFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        slots.Clear(); cooldowns.Clear(); encounters.Clear(); removed.Clear(); oldTargets.Clear(); cleanupFrame = -1;
    }

    private static void Clean()
    {
        if (cleanupFrame == Time.frameCount) return;
        cleanupFrame = Time.frameCount;
        removed.Clear();
        foreach (var entry in cooldowns)
            if (entry.Key == null || entry.Value <= Time.time) removed.Add(entry.Key);
        foreach (var owner in removed) cooldowns.Remove(owner);
        removed.Clear();
        foreach (var entry in slots)
            if (entry.Key == null || !entry.Key.CanHoldPressure || entry.Value.Target == null ||
                entry.Key.RushTarget != entry.Value.Target) removed.Add(entry.Key);
        foreach (var owner in removed) Release(owner);
        oldTargets.Clear();
        foreach (var entry in encounters)
        {
            bool referenced = false;
            if (entry.Key != null)
                foreach (var enemy in EnemyController.ActiveEnemies)
                    if (enemy != null && enemy.RushEncounterActive && enemy.RushTarget == entry.Key) { referenced = true; break; }
            if (!referenced) oldTargets.Add(entry.Key);
        }
        foreach (var target in oldTargets) encounters.Remove(target);
    }

    private static int Count(Transform target)
    {
        int count = 0;
        foreach (var slot in slots.Values) if (slot.Target == target) count++;
        return count;
    }
    private static bool CoolingDown(EnemyController owner) => cooldowns.TryGetValue(owner, out float until) && until > Time.time;
    private static bool Owns(EnemyController owner, Transform target) => owner != null &&
        slots.TryGetValue(owner, out var slot) && slot.Target == target && owner.CanHoldPressure && owner.RushTarget == target;

    private static Encounter Evaluate(Transform target)
    {
        Clean();
        if (target == null || !EnemyCombatSettings.RushEnabled) return null;
        if (!encounters.TryGetValue(target, out var group)) encounters[target] = group = new Encounter();
        if (group.Frame == Time.frameCount) return group;
        group.Frame = Time.frameCount;
        group.Candidates.Clear();
        bool movingAwayFromThreat = false;
        target.TryGetComponent(out PlayerController player);
        bool sprinting = player != null && player.isActiveAndEnabled && !player.IsGameplayInputBlocked &&
            player.currentHealth > 0 && player.IsSprinting && player.PlanarVelocity.sqrMagnitude > .04f;
        foreach (var enemy in EnemyController.ActiveEnemies)
        {
            if (enemy == null || !enemy.RushEncounterActive || enemy.RushTarget != target) continue;
            // An enemy in front must not mask another pursuer behind the player.
            Vector3 away = Vector3.ProjectOnPlane(target.position - enemy.transform.position, Vector3.up).normalized;
            if (sprinting && Vector3.Dot(player.PlanarVelocity, away) > .1f) movingAwayFromThreat = true;
            if (enemy.CanHoldPressure && !CoolingDown(enemy)) group.Candidates.Add(enemy);
        }

        // Once escape starts, turning or weaving while still sprinting must not
        // restart pursuit. The settle timer starts only when actual sprinting stops.
        bool escaping = EnemyCombatSettings.PauseWhileEscaping && sprinting &&
            (movingAwayFromThreat || group.EscapePaused);
        if (escaping)
        {
            group.EscapePaused = true;
            group.ResumeAt = Time.time + EnemyCombatSettings.EscapeResumeDelay;
        }
        else if (group.EscapePaused && (!EnemyCombatSettings.PauseWhileEscaping || Time.time >= group.ResumeAt))
        {
            group.EscapePaused = false;
            // A settled escape starts a fresh initial approach for this target.
            // This is not an interruption: do not give old owners a reentry penalty.
            removed.Clear();
            foreach (var entry in slots)
                if (entry.Value.Target == target) removed.Add(entry.Key);
            foreach (var owner in removed) slots.Remove(owner);
            group.Formed = false;
            group.NextReplacement = 0;
        }

        removed.Clear();
        foreach (var entry in slots)
        {
            if (entry.Value.Target != target) continue;
            var owner = entry.Key;
            var slot = entry.Value;
            if (!owner.CanHoldPressure || owner.RushTarget != target) { removed.Add(owner); continue; }
            if (group.EscapePaused || !owner.CanRush || owner.CurrentState == EnemyController.EnemyState.Pressure)
            {
                slot.ProgressTime = Time.time;
                slot.Position = owner.transform.position;
                continue;
            }
            if (Vector3.ProjectOnPlane(owner.transform.position - slot.Position, Vector3.up).sqrMagnitude >= .01f)
            { slot.Position = owner.transform.position; slot.ProgressTime = Time.time; }
            else if (Time.time - slot.ProgressTime >= EnemyCombatSettings.RushStallTimeout) removed.Add(owner);
        }
        foreach (var owner in removed) Release(owner);

        int count = Count(target);
        if (count >= EnemyCombatSettings.RushLimit) group.Formed = true;
        if (group.EscapePaused || count >= EnemyCombatSettings.RushLimit || Time.time < group.NextReplacement) return group;
        group.Candidates.Sort((a, b) =>
        {
            int distance = (a.transform.position - target.position).sqrMagnitude.CompareTo((b.transform.position - target.position).sqrMagnitude);
            return distance != 0 ? distance : a.GetInstanceID().CompareTo(b.GetInstanceID());
        });
        foreach (var candidate in group.Candidates)
        {
            if (Owns(candidate, target) || !candidate.CanHoldPressure || CoolingDown(candidate)) continue;
            if (!group.Formed && Vector3.Distance(candidate.transform.position, target.position) > EnemyCombatSettings.PressureDistance) continue;
            if (!candidate.CanReachRushTarget(path)) continue;
            slots[candidate] = new Slot { Target = target, ProgressTime = Time.time, Position = candidate.transform.position };
            count++;
            if (group.Formed)
            {
                group.NextReplacement = Time.time + EnemyCombatSettings.ReplacementInterval;
                break; // Stagger replacements even if several owners die together.
            }
            if (count >= EnemyCombatSettings.RushLimit) { group.Formed = true; break; }
        }
        return group;
    }

    public static void Tick(Transform target) => Evaluate(target);
    public static bool HasSlot(EnemyController owner, Transform target)
    {
        Evaluate(target);
        return EnemyCombatSettings.RushEnabled && Owns(owner, target);
    }
    public static bool ShouldRun(EnemyController owner, Transform target)
    {
        var group = Evaluate(target);
        return group != null && owner != null && owner.CanRush && !group.EscapePaused && !CoolingDown(owner) &&
            (Owns(owner, target) || !group.Formed);
    }
    public static bool IsEscapePaused(Transform target) => Evaluate(target)?.EscapePaused ?? false;
    public static bool IsWaiting(EnemyController owner, Transform target)
    {
        var group = Evaluate(target);
        return group != null && owner != null && !Owns(owner, target) &&
            (group.Formed || group.EscapePaused || CoolingDown(owner));
    }
    public static string Describe(EnemyController owner, Transform target)
    {
        var group = Evaluate(target);
        if (group == null) return "Legacy chase";
        string status = Owns(owner, target) ? "Pressure owner" : CoolingDown(owner) ? "Interrupted / resting" :
            group.Formed ? "Waiting for pressure slot" : "Initial approach";
        return status + " | " + Count(target) + "/" + EnemyCombatSettings.RushLimit +
            (group.EscapePaused ? " | Escape pause" : "");
    }
    public static void Release(EnemyController owner)
    {
        if (ReferenceEquals(owner, null) || !slots.TryGetValue(owner, out var slot)) return;
        slots.Remove(owner);
        if (owner != null) cooldowns[owner] = Time.time + EnemyCombatSettings.RushReentry;
        if (slot.Target != null && encounters.TryGetValue(slot.Target, out var group))
        {
            group.NextReplacement = Mathf.Max(group.NextReplacement, Time.time + EnemyCombatSettings.ReplacementInterval);
            group.Frame = -1;
        }
    }
}


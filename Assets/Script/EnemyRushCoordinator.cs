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
        public float GapSince = -1, GapDistance;
    }
    private sealed class Encounter
    {
        public bool Formed;
        public float NextReplacement, NextHandoff;
        public float NextRunnerStart = -1;
        public int Frame = -1;
        public readonly List<EnemyController> Candidates = new List<EnemyController>();
        public readonly Dictionary<EnemyController, Slot> Runners = new Dictionary<EnemyController, Slot>();
        public readonly Dictionary<EnemyController, bool> RunChoices = new Dictionary<EnemyController, bool>();
        public readonly Dictionary<EnemyController, float> Arrivals = new Dictionary<EnemyController, float>();
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
        target.TryGetComponent(out PlayerController player);
        foreach (var enemy in EnemyController.ActiveEnemies)
            if (enemy != null && enemy.RushEncounterActive && enemy.RushTarget == target && enemy.CanHoldPressure && !CoolingDown(enemy))
                group.Candidates.Add(enemy);

        // Intercept the movement route before the player reaches melee range.
        // Admit one reachable front enemy per shared interval, without pausing the group.
        if (Time.time >= Mathf.Max(group.NextHandoff, group.NextReplacement))
        {
            EnemyController interceptor = null;
            float nearest = float.PositiveInfinity;
            foreach (var candidate in group.Candidates)
            {
                float distance = Vector3.Distance(candidate.transform.position, target.position);
                if (Owns(candidate, target) || !candidate.IsFrontOpportunity || distance >= nearest || !candidate.CanReachRushTarget(path)) continue;
                interceptor = candidate; nearest = distance;
            }
            if (interceptor != null)
            {
                EnemyController displaced = null;
                float farthest = -1;
                if (Count(target) >= EnemyCombatSettings.RushLimit)
                    foreach (var entry in slots)
                    {
                        var owner = entry.Key;
                        if (entry.Value.Target != target || owner.isAttacking || owner.IsFrontOpportunity || owner.IsCloseDefenseCandidate) continue;
                        float distance = Vector3.Distance(owner.transform.position, target.position);
                        if (distance > farthest) { displaced = owner; farthest = distance; }
                    }
                if (displaced != null) Release(displaced);
                if (Count(target) < EnemyCombatSettings.RushLimit)
                {
                    slots[interceptor] = new Slot { Target = target, Position = interceptor.transform.position, ProgressTime = Time.time };
                    group.Runners.Remove(interceptor);
                    group.NextHandoff = group.NextReplacement = Time.time + EnemyCombatSettings.ReplacementInterval;
                    // Running nominations cannot exceed the same combined cap.
                    while (Count(target) + group.Runners.Count > EnemyCombatSettings.RushLimit)
                    {
                        EnemyController demoted = null;
                        float farthestRunner = -1;
                        foreach (var runner in group.Runners.Keys)
                        {
                            float distance = (runner.transform.position - target.position).sqrMagnitude;
                            if (distance > farthestRunner) { demoted = runner; farthestRunner = distance; }
                        }
                        if (demoted == null) break;
                        group.Runners.Remove(demoted);
                    }
                }
            }
        }

        // A nearby reachable defender can replace a distant uncommitted owner.
        // Per-owner handoff does not reroll or restart the whole group.
        if (Time.time >= Mathf.Max(group.NextHandoff, group.NextReplacement) && Count(target) >= EnemyCombatSettings.RushLimit)
        {
            EnemyController defender = null;
            float nearest = float.PositiveInfinity;
            foreach (var candidate in group.Candidates)
            {
                float distance = Vector3.Distance(candidate.transform.position, target.position);
                if (Owns(candidate, target) || !candidate.IsCloseDefenseCandidate || distance >= nearest || !candidate.CanReachRushTarget(path)) continue;
                defender = candidate; nearest = distance;
            }
            EnemyController victim = null;
            float farthest = EnemyCombatSettings.PressureDistance + .5f;
            if (defender != null)
                foreach (var entry in slots)
                {
                    var owner = entry.Key;
                    if (entry.Value.Target != target || owner.isAttacking) continue;
                    float distance = Vector3.Distance(owner.transform.position, target.position);
                    if (distance > farthest && distance > nearest + 1f)
                    { victim = owner; farthest = distance; }
                }
            if (victim != null)
            {
                Release(victim);
                group.NextHandoff = Time.time + EnemyCombatSettings.ReplacementInterval;
            }
        }

        removed.Clear();
        foreach (var entry in slots)
        {
            if (entry.Value.Target != target) continue;
            var owner = entry.Key;
            var slot = entry.Value;
            if (!owner.CanHoldPressure || owner.RushTarget != target) { removed.Add(owner); continue; }
            float distanceToTarget = Vector3.ProjectOnPlane(target.position - owner.transform.position, Vector3.up).magnitude;
            bool walkingAway = player != null && player.isActiveAndEnabled && !player.IsGameplayInputBlocked && player.currentHealth > 0 &&
                player.PlanarVelocity.sqrMagnitude > .04f && Vector3.Dot(player.PlanarVelocity,
                    Vector3.ProjectOnPlane(target.position - owner.transform.position, Vector3.up).normalized) > .1f;
            if (!owner.isAttacking && walkingAway &&
                distanceToTarget > Mathf.Max(EnemyCombatSettings.PressureDistance, owner.AttackReach + .75f))
            {
                if (slot.GapSince < 0 || distanceToTarget < slot.GapDistance - .15f)
                { slot.GapSince = Time.time; slot.GapDistance = distanceToTarget; }
                if (Time.time - slot.GapSince >= EnemyCombatSettings.HandoffDelay)
                {
                    if (distanceToTarget >= slot.GapDistance + EnemyCombatSettings.HandoffGap && Time.time >= group.NextHandoff)
                    {
                        removed.Add(owner);
                        group.NextHandoff = Time.time + EnemyCombatSettings.ReplacementInterval;
                        continue;
                    }
                    slot.GapSince = Time.time; slot.GapDistance = distanceToTarget;
                }
            }
            else slot.GapSince = -1;
            if (!owner.CanRush || owner.CurrentState == EnemyController.EnemyState.Pressure)
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

        // Record the first sampled arrival, regardless of whether the enemy ran.
        // Walking into range must not lose to an incoming runner farther away.
        removed.Clear();
        foreach (var entry in group.Arrivals)
            if (entry.Key == null || !entry.Key.CanHoldPressure || entry.Key.RushTarget != target || CoolingDown(entry.Key) ||
                Vector3.Distance(entry.Key.transform.position, target.position) > EnemyCombatSettings.PressureDistance) removed.Add(entry.Key);
        foreach (var owner in removed) group.Arrivals.Remove(owner);
        foreach (var candidate in group.Candidates)
            if (Vector3.Distance(candidate.transform.position, target.position) <= EnemyCombatSettings.PressureDistance &&
                !group.Arrivals.ContainsKey(candidate)) group.Arrivals[candidate] = Time.time;

        int count = Count(target);
        if (count >= EnemyCombatSettings.RushLimit) group.Formed = true;
        group.Candidates.Sort((a, b) =>
        {
            int defense = b.IsCloseDefenseCandidate.CompareTo(a.IsCloseDefenseCandidate);
            if (defense != 0) return defense;
            float aTime = group.Arrivals.TryGetValue(a, out var first) ? first : float.PositiveInfinity;
            float bTime = group.Arrivals.TryGetValue(b, out var second) ? second : float.PositiveInfinity;
            int arrival = aTime.CompareTo(bTime);
            if (arrival != 0) return arrival;
            int distance = (a.transform.position - target.position).sqrMagnitude.CompareTo((b.transform.position - target.position).sqrMagnitude);
            return distance != 0 ? distance : a.GetInstanceID().CompareTo(b.GetInstanceID());
        });
        foreach (var candidate in group.Candidates)
        {
            if (count >= EnemyCombatSettings.RushLimit || Time.time < group.NextReplacement) break;
            if (Owns(candidate, target) || !candidate.CanHoldPressure || CoolingDown(candidate) || !group.Arrivals.ContainsKey(candidate)) continue;
            if (!candidate.CanReachRushTarget(path)) continue;
            slots[candidate] = new Slot { Target = target, ProgressTime = Time.time, Position = candidate.transform.position };
            group.Runners.Remove(candidate);
            count++;
            if (group.Formed)
            {
                group.NextReplacement = Time.time + EnemyCombatSettings.ReplacementInterval;
                break; // Stagger replacements even if several owners die together.
            }
            if (count >= EnemyCombatSettings.RushLimit) { group.Formed = true; break; }
        }
        UpdateRunners(group, target, count);
        return group;
    }

    private static void UpdateRunners(Encounter group, Transform target, int owners)
    {
        removed.Clear();
        foreach (var entry in group.Runners)
        {
            var enemy = entry.Key;
            var runner = entry.Value;
            if (enemy == null || !enemy.CanHoldPressure || enemy.RushTarget != target || CoolingDown(enemy) || Owns(enemy, target))
            { removed.Add(enemy); continue; }
            if (!enemy.CanRush)
            { runner.ProgressTime = Time.time; runner.Position = enemy.transform.position; continue; }
            if (Vector3.ProjectOnPlane(enemy.transform.position - runner.Position, Vector3.up).sqrMagnitude >= .01f)
            { runner.ProgressTime = Time.time; runner.Position = enemy.transform.position; }
            else if (Time.time - runner.ProgressTime >= EnemyCombatSettings.RushStallTimeout) removed.Add(enemy);
        }
        foreach (var enemy in removed) Release(enemy);
        // A walker arriving first can take the place of an incoming runner.
        while (owners + group.Runners.Count > EnemyCombatSettings.RushLimit)
        {
            EnemyController farthest = null;
            float farthestDistance = -1;
            foreach (var enemy in group.Runners.Keys)
            {
                float distance = (enemy.transform.position - target.position).sqrMagnitude;
                if (distance > farthestDistance) { farthest = enemy; farthestDistance = distance; }
            }
            if (farthest == null) break;
            group.Runners.Remove(farthest); // Demotion is not a hit or a cooldown penalty.
        }
        if (owners + group.Runners.Count >= EnemyCombatSettings.RushLimit) return;
        float chance = EnemyCombatSettings.ApproachRunChance;
        foreach (var candidate in group.Candidates)
            if (!group.RunChoices.ContainsKey(candidate)) group.RunChoices[candidate] = chance > 0 && (chance >= 1 || Random.value < chance);
        if (group.NextRunnerStart < 0) group.NextRunnerStart = Time.time + EnemyCombatSettings.SampleRushStartDelay();
        if (Time.time < group.NextRunnerStart) return;
        EnemyController chosen = null;
        int eligible = 0;
        foreach (var candidate in group.Candidates)
        {
            if (!candidate.CanRush || Owns(candidate, target) || group.Runners.ContainsKey(candidate) || CoolingDown(candidate) ||
                !group.RunChoices.TryGetValue(candidate, out bool willing) || !willing ||
                Vector3.Distance(candidate.transform.position, target.position) <= EnemyCombatSettings.PressureDistance ||
                !candidate.CanReachRushTarget(path)) continue;
            if (Random.Range(0, ++eligible) == 0) chosen = candidate;
        }
        if (chosen == null) return;
        group.Runners[chosen] = new Slot { Target = target, Position = chosen.transform.position, ProgressTime = Time.time };
        group.NextRunnerStart = Time.time + EnemyCombatSettings.SampleRushStartDelay();
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
        return group != null && owner != null && owner.CanRush && !CoolingDown(owner) &&
            (Owns(owner, target) || group.Runners.ContainsKey(owner));
    }
    public static bool IsEscapePaused(Transform target) => false;
    public static bool IsWaiting(EnemyController owner, Transform target)
    {
        var group = Evaluate(target);
        return group != null && owner != null && !Owns(owner, target) &&
            (Count(target) >= EnemyCombatSettings.RushLimit || CoolingDown(owner));
    }
    public static string Describe(EnemyController owner, Transform target)
    {
        var group = Evaluate(target);
        if (group == null) return "Legacy chase";
        string status = Owns(owner, target) ? "Pressure owner" : CoolingDown(owner) ? "Interrupted / resting" :
            group.Runners.ContainsKey(owner) ? "Random runner / no pressure slot" :
            Count(target) >= EnemyCombatSettings.RushLimit ? "Waiting for pressure slot" : "Walking / first arrivals claim slots";
        return status + " | " + Count(target) + "/" + EnemyCombatSettings.RushLimit;
    }
    public static void Release(EnemyController owner)
    {
        if (ReferenceEquals(owner, null)) return;
        bool releasedPressure = slots.TryGetValue(owner, out var slot);
        if (releasedPressure) slots.Remove(owner);
        bool released = releasedPressure;
        foreach (var entry in encounters)
        {
            var group = entry.Value;
            bool runner = group.Runners.Remove(owner);
            if (!runner && (!releasedPressure || entry.Key != slot.Target)) continue;
            released = true;
            if (releasedPressure && entry.Key == slot.Target)
                group.NextReplacement = Mathf.Max(group.NextReplacement, Time.time + EnemyCombatSettings.ReplacementInterval);
            group.NextRunnerStart = Mathf.Max(group.NextRunnerStart, Time.time + EnemyCombatSettings.SampleRushStartDelay());
            group.Frame = -1;
        }
        if (released && owner != null) cooldowns[owner] = Time.time + EnemyCombatSettings.RushReentry;
    }
}


using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EnemyStaggerSettings
{
    [Tooltip("Enable Action Break independently of physical hit feedback.")] public bool enabled = true;
    [Min(1), Tooltip("Accumulated impact required for a guaranteed break.")] public float threshold = 60;
    [Min(0), Tooltip("Impact per shot per enemy. Shotgun volleys contribute once per enemy.")] public float impactPerShot = 20;
    [Min(1), Tooltip("Head impact multiplier, independent of HP damage.")] public float headMultiplier = 1.5f;
    [Range(0, 1), Tooltip("Early-break chance per shot per enemy. Does not increase HP damage.")] public float criticalChance = .05f;
    [Min(0), Tooltip("Impact recovered per second after Recovery Delay.")] public float recoveryPerSecond = 10;
    [Min(0), Tooltip("Delay after the last impact before recovery begins.")] public float recoveryDelay = 1;
    [Min(0), Tooltip("Extra break resistance after the interruption ends. Damage and physical feedback still apply.")] public float resistanceAfterBreak = 1.2f;
}

// State belongs to an actor, not its shared ScriptableObject.
public sealed class EnemyStaggerMeter
{
    public float Impact { get; private set; }
    public string LastResult { get; private set; } = "Ready";
    private float lastHit = -100, lastTick, resistantUntil;
    private readonly Dictionary<int, float> shots = new Dictionary<int, float>();
    private readonly List<int> expired = new List<int>();
    public void Tick(EnemyStaggerSettings settings, float now)
    {
        if (settings == null) return;
        float elapsed = Mathf.Max(0, now - Mathf.Max(lastTick, lastHit + Mathf.Max(0, settings.recoveryDelay)));
        Impact = Mathf.Max(0, Impact - elapsed * Mathf.Max(0, settings.recoveryPerSecond));
        lastTick = now;
    }
    public bool Hit(EnemyStaggerSettings settings, float now, bool head, int shotId, float overrideImpact, float breakDuration)
    {
        if (settings == null || !settings.enabled) { LastResult = "Disabled"; return false; }
        Tick(settings, now);
        if (shotId != 0)
        {
            expired.Clear();
            foreach (var shot in shots) if (shot.Value <= now) expired.Add(shot.Key);
            foreach (int id in expired) shots.Remove(id);
            if (shots.ContainsKey(shotId)) return false; // Keep the volley's original result visible in debug.
            shots[shotId] = now + 6;
        }
        if (now < resistantUntil) { LastResult = "Break resistance"; return false; }
        float power = overrideImpact > 0 && !float.IsInfinity(overrideImpact) && !float.IsNaN(overrideImpact) ? overrideImpact : Mathf.Max(0, settings.impactPerShot);
        Impact += power * (head ? Mathf.Max(1, settings.headMultiplier) : 1);
        lastHit = lastTick = now;
        bool critical = settings.criticalChance > 0 && Random.value < Mathf.Clamp01(settings.criticalChance);
        bool breaks = critical || Impact >= Mathf.Max(1, settings.threshold);
        LastResult = breaks ? critical ? "Critical Action Break" : "Impact Action Break" : "Physical hit / impact accumulated";
        if (!breaks) return false;
        Impact = 0; resistantUntil = now + Mathf.Max(0, breakDuration) + Mathf.Max(0, settings.resistanceAfterBreak);
        return true;
    }
}

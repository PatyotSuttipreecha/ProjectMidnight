using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "EnemyDataSO", menuName = "Scriptable Objects/EnemyDataSO")]
public class EnemyDataSO : ScriptableObject
{
   [Header("Detection Settings")]
    [Range(1f, 50f)] public float detectionRadius = 12f;
    [Range(0f, 180f)] public float fieldOfView = 90f;
    public LayerMask obstacleMask;
    [Header("Hearing")]
    public bool canHear = true;
    [Min(0)] public float hearingMultiplier = 1f;
    [Min(0)] public float walkHearingRadius = 3f;
    [Min(0)] public float runHearingRadius = 8f;
    [Min(0)] public float gunshotHearingRadius = 25f;
    public float HearingRadius(GameplayNoise.Kind kind)
    {
        float radius = kind == GameplayNoise.Kind.Walk ? walkHearingRadius : kind == GameplayNoise.Kind.Run ? runHearingRadius : gunshotHearingRadius;
        return canHear ? Mathf.Max(0, radius) * Mathf.Max(0, hearingMultiplier) : 0;
    }
    [Min(.1f)] public float noiseSearchDuration = 3f;
    [Min(1)] public float noiseTravelTimeout = 15f;

    [Header("Patrol Settings")]
    [Range(0.5f, 10f)] public float waitTimeAtPoint = 2f;
    [Range(1f, 10f)] public float lostSightCooldown = 5f;

    [Header("Attack Settings")]
    [Range(1f, 100f)] public float minAttack = 1f;
    [Range(1f, 100f)] public float maxAttack = 10f;
    [Range(1f, 5f)] public float attackRange = 2f;
    [Range(0.5f, 5f)] public float attackCooldown = 2f;
    [Header("Shared Enemy Profile")]
    public bool useSharedSettings;
    [Header("Movement and Health")]
    [Min(1f)] public float startingHealth = 100f;
    [Min(0f)] public float walkSpeed = 0.5f;
    [Min(0f)] public float acceleration = 8f;
    [Min(0f)] public float turnSpeed = 120f;
    [Min(0f)] public float stoppingDistance = 1f;
    [Header("Vision and Attack Timing")]
    [Min(0f)] public float eyeHeight = 1f;
    [Min(0f)] public float playerTargetHeight = 1f;
    [Min(0f)] public float attackWindup = 0.5f;
    [Header("Death and Ragdoll")]
     public bool useRagdoll;
     public string deathAnimation = "zombie_dead2";
    [Min(0f)] public float deathAnimationLeadIn = 0.6f;
    [Min(0f)] public float deathBlendDuration = 1.2f;
    [Min(0f)] public float deathAnimationDrive = 25f;
    [Range(0f, 1f)] public float hybridDeathPushScale = 0.35f;
    [Min(0f)] public float corpseLifetime = 8f;
    [System.Serializable]
    public class LootEntry
    {
        public GameObject prefab;
        [Range(0f, 100f)] public float percent;
    }
    [Header("Loot")]
    public List<LootEntry> lootTable = new List<LootEntry>();
     public GameObject lootPrefab;
    [Tooltip("Overall probability of dropping loot. If successful, the Loot Table selects which item drops. Enemy Lab displays this as 0–100%.")]
    [Range(0f, 1f)] public float lootDropChance = 1f;
    public Vector3 lootOffset = new Vector3(0f, 0.2f, 0f);
    [Header("Ragdoll Physics")]
    [Range(0.1f, 1f)] public float ragdollGravityScale = 0.45f;
    [Min(0f)] public float ragdollLinearDamping = 0.8f;
    [Min(0f)] public float ragdollAngularDamping = 1.5f;
    [Min(0f)] public float deathPushImpulse = 3f;
    [Header("Bullet Hit Reaction")]
     public bool enableHitReaction = true;
    [Min(0f)] public float hitPushImpulse = 0.8f;
    [Min(0.02f)] public float hitPhysicsDuration = 0.12f;
    [Min(0.01f)] public float hitRecoveryDuration = 0.2f;
    [Range(0f, 1f)] public float hitReactionBlend = 0.75f;
    [Min(0f)] public float hitAnimationSupport = 30f;
    [Range(0f, 60f)] public float hitMaxAngle = 25f;
    [Min(0f)] public float corpseSettleDelay = 0.75f;
    [Header("Physics Mass")]
    [Min(0.01f)] public float physicsMassMultiplier = 1f;
     public List<EnemyController.BoneMassOverride> boneMassOverrides = new List<EnemyController.BoneMassOverride>();

    public GameObject RollLoot(float roll)
    {
        float total = 0f;
        foreach (LootEntry entry in lootTable)
        {
            if (entry == null || float.IsNaN(entry.percent) || float.IsInfinity(entry.percent) || entry.percent < 0f || (entry.percent > 0f && entry.prefab == null))
            { Debug.LogWarning("Invalid loot entry in " + name, this); return null; }
            total += entry.percent;
        }
        if (Mathf.Abs(total - 100f) > 0.01f)
        { Debug.LogWarning("Loot percentages must total 100% in " + name, this); return null; }
        float threshold = Mathf.Clamp(roll, 0f, 0.999999f) * total;
        float cumulative = 0f;
        foreach (LootEntry entry in lootTable)
        {
            cumulative += entry.percent;
            if (threshold < cumulative) return entry.prefab;
        }
        return null;
    }
}

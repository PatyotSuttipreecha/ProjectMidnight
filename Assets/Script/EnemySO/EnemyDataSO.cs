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

    [Header("Shared Profile and Health")]
    [Tooltip("Use this profile for health, navigation, combat and physics settings instead of legacy per-enemy values.")]
    public bool useSharedSettings;
    [Tooltip("Health assigned when the enemy is initialized.")]
    [Min(1f)] public float startingHealth = 100f;

    [Header("Patrol Movement")]
    [Tooltip("Normal patrol and investigation speed in world units per second.")]
    [Min(0f)] public float walkSpeed = 0.5f;
    [Tooltip("Default pause at patrol points. Route-specific wait settings can override this.")]
    [Range(0.5f, 10f)] public float waitTimeAtPoint = 2f;

    [Header("Chase Movement")]
    [Tooltip("Maximum pursuit speed while the player is visible and outside the slowdown zone.")]
    [Min(0f)] public float chaseSpeed = 2.5f;
    [Tooltip("Target pursuit speed near attack range. The enemy stops during attack preparation.")]
    [Min(0f)] public float closeApproachSpeed = .5f;
    [Tooltip("Distance in world units where pursuit starts slowing down. Set above Attack Range.")]
    [Min(0f)] public float chaseSlowdownDistance = 4f;
    [Tooltip("Rate of change of the requested movement speed, in units per second squared. Agent acceleration also limits actual velocity.")]
    [Min(.1f)] public float speedChangeRate = 5f;
    [Tooltip("Minimum seconds between chase path updates. Lower values react faster but cost more path calculations.")]
    [Min(.05f)] public float chaseRepathInterval = .25f;
    [Tooltip("Seconds without seeing the player before abandoning chase and returning to patrol.")]
    [Range(1f, 10f)] public float lostSightCooldown = 5f;
    [Tooltip("Optional Animator state name on layer 0 for faster visible pursuit. Empty or missing state uses the walk animation.")]
    public string runAnimation = "";

    [Header("Approach Decisions")]
    [Tooltip("Choose direct pressure, a side approach, or a short faster approach. Does not change attack animation or damage.")]
    public bool useApproachDecisions = true;
    [Tooltip("Seconds to commit to an approach before considering a new one. Decisions are not changed every frame.")]
    [Min(.5f)] public float approachDecisionInterval = 2.5f;
    [Tooltip("Relative weights for Direct, Flank and Burst. All zero uses Direct.")]
    public Vector3 approachWeights = new Vector3(1f, 1f, .6f);
    [Tooltip("Side approach angle relative to the enemy's direction when the decision is made.")]
    [Range(0f, 75f)] public float flankAngle = 40f;
    [Tooltip("Movement speed multiplier for a direct pressure approach, outside the slowdown zone.")]
    [Range(.1f, 1f)] public float pressureSpeedMultiplier = .7f;
    [Tooltip("Movement speed multiplier for a burst approach, outside the slowdown zone.")]
    [Range(1f, 2f)] public float burstSpeedMultiplier = 1.25f;

    [Header("Group Engagement")]
    [Tooltip("Reserve attack turns per player. Enemies without a turn hold pressure positions instead of all rushing into melee.")]
    public bool coordinateAttacks = true;
    [Tooltip("Maximum simultaneous reserved attackers for the same target. Use consistent limits for profiles in the same encounter.")]
    [Min(1)] public int maxConcurrentAttackers = 2;
    [Tooltip("Distance from the player at which an enemy can request an attack turn.")]
    [Min(.5f)] public float engagementDistance = 6f;
    [Tooltip("Pressure ring radius for enemies waiting for a turn. Automatically kept outside attack range.")]
    [Min(.5f)] public float pressureRingRadius = 3.5f;
    [Tooltip("Maximum approach time for a reserved attacker before releasing its turn if unable to attack.")]
    [Min(.5f)] public float attackTurnTimeout = 4f;
    [Tooltip("Delay before requesting another turn after attacking, being interrupted, or timing out. Lets other enemies take a turn.")]
    [Min(.1f)] public float attackTurnRest = 1f;

    [Header("Navigation Steering")]
    [Tooltip("NavMeshAgent acceleration limit in units per second squared.")]
    [Min(0f)] public float acceleration = 8f;
    [Tooltip("Maximum NavMeshAgent turning speed in degrees per second.")]
    [Min(0f)] public float turnSpeed = 120f;
    [Tooltip("Distance where the agent stops before its destination. Keep below Attack Range.")]
    [Min(0f)] public float stoppingDistance = 1f;

    [Header("Crowd Movement - Approach")]
    [Tooltip("Give distant pursuing enemies different approach targets. Close pursuit returns to the player directly.")]
    public bool spreadChaseTargets = true;
    [Tooltip("Maximum approach offset in world units. Clamped to allow stopping within attack range; unreachable offsets fall back to direct pursuit.")]
    [Min(0f)] public float chaseApproachRadius = 1.5f;
    [Tooltip("Assign different NavMesh avoidance priorities at initialization. Lower priority numbers take precedence.")]
    public bool varyAvoidancePriority = true;

    [Header("Crowd Movement - Separation")]
    [Tooltip("Moving enemies steer away from nearby living enemies. Does not move this enemy while attacking or paused.")]
    public bool separateNearbyEnemies = true;
    [Tooltip("Preferred horizontal distance between enemy centers in world units. Separation ignores enemies on another floor.")]
    [Min(.1f)] public float separationRadius = 1f;
    [Tooltip("Maximum separation speed in units per second, capped by agent speed and clipped at NavMesh boundaries.")]
    [Min(0f)] public float separationSpeed = .75f;

    [Header("Attack - Range and Damage")]
    [Tooltip("Minimum damage applied by a successful attack after its hit delay.")]
    [Range(1f, 100f)] public float minAttack = 1f;
    [Tooltip("Maximum damage applied by a successful attack after its hit delay.")]
    [Range(1f, 100f)] public float maxAttack = 10f;
    [Tooltip("Maximum distance between enemy and player transforms for attack. Clear sight is also required.")]
    [Range(1f, 5f)] public float attackRange = 2f;

    [Header("Attack - Timing")]
    [Tooltip("Stopped Idle time before starting the attack animation. Moving out of range cancels preparation.")]
    [Min(0f)] public float attackPreparationTime = .25f;
    [Tooltip("Seconds from starting the attack animation to checking range and applying damage.")]
    [Min(0f)] public float attackWindup = 0.5f;
    [Tooltip("Minimum time between attack attempts, measured from preparation start. Cannot be shorter than preparation plus hit delay.")]
    [Range(0.5f, 5f)] public float attackCooldown = 2f;

    [Header("Vision Target Heights")]
    [Tooltip("Sight ray origin height above the enemy transform in world units.")]
    [Min(0f)] public float eyeHeight = 1f;
    [Tooltip("Sight ray target height above the player controller transform in world units.")]
    [Min(0f)] public float playerTargetHeight = 1f;
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
    [Tooltip("Maximum root displacement away from a bullet hit. NavMesh walls and edges limit movement.")]
    [Min(0f)] public float hitKnockbackDistance = .15f;
    [Tooltip("Duration of root knockback in seconds. Repeated hits refresh rather than accumulate displacement.")]
    [Min(.02f)] public float hitKnockbackDuration = .12f;
    [Tooltip("Time movement and attacks are interrupted after a bullet hit. Zero disables stagger.")]
    [Min(0f)] public float hitStaggerDuration = .3f;
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

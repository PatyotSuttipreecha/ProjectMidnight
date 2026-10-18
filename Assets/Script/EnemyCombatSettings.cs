using UnityEngine;

// One shared asset, independent of enemy profiles and spawn groups.
[CreateAssetMenu(menuName = "System Modification/Enemy Combat Settings", fileName = "EnemyCombatSettings")]
public sealed class EnemyCombatSettings : ScriptableObject
{
    [Header("Attack Timing")]
    [Tooltip("Minimum scaled seconds between entering Preparation for enemies targeting the same player. Does not guarantee separated damage frames when weapon windups differ. Zero disables spacing.")]
    [Min(0)] public float attackStartInterval = .8f;
    [Header("Pressure Group")]
    [Tooltip("Coordinate an initial rush, persistent pressure owners per player. Disabling restores each profile's saved Walk/Boost rhythm.")]
    public bool useRushSlots = true;
    [Tooltip("Shared cap for pressure owners plus incoming random runners per player. Walking enemies may arrive first and take pressure ownership; a running nomination does not reserve it. Independent of attack turns.")]
    [Min(1)] public int maxRushers = 3;
    [Tooltip("Probability each eligible enemy volunteers to run this approach round. Rolled once per round, not every frame. Others walk and can still claim pressure ownership by arriving first. Zero makes everyone walk.")]
    [Range(0, 1)] public float approachRunChance = .55f;
    [Tooltip("Random seconds before each incoming runner starts. Shared per player so the whole group never starts together. Applies to the first encounter and replacements.")]
    public Vector2 rushStartDelay = new Vector2(.3f, .7f);
    [Tooltip("Distance in metres at which approaching enemies can join the initial pressure group. Once the group fills, other enemies walk and wait outside this distance.")]
    [Min(1)] public float pressureDistance = 4.5f;
    [Tooltip("Delay before admitting a replacement and between replacements. The first eligible enemy to enter Pressure Distance wins; a distant runner has no reserved pressure ownership.")]
    [Min(0)] public float replacementInterval = .6f;
    [Tooltip("Minimum time before an enemy that released its rush slot can request another. Prevents boundary flicker and immediate reacquisition.")]
    [Min(0)] public float rushReentryDelay = 2f;
    [Tooltip("Release an owner making no progress while actively pursuing from far away. Circling, attacking and recovery do not count as stalls.")]
    [Min(.5f)] public float rushStallTimeout = 2f;
    [Header("Front interception")]
    [Tooltip("Moving toward a visible enemy within this range lets it request pressure ownership before melee range. Applies to walking and sprinting.")]
    [Min(1)] public float frontInterceptDistance = 6f;
    [Tooltip("Full cone angle around the player's actual movement direction for nearby interceptors.")]
    [Range(10, 160)] public float frontInterceptAngle = 100f;
    [Header("Pressure Handoff")]
    [Tooltip("Seconds of walking away with a growing gap before an uncommitted distant owner yields its pressure slot. No full encounter reset.")]
    [Min(.1f)] public float pursuitHandoffDelay = 2.5f;
    [Tooltip("Minimum net increase in distance over the handoff window. Equal-speed pursuit does not lose ownership.")]
    [Min(.1f)] public float pursuitHandoffGap = .5f;
    [Tooltip("Maximum deliberate outward travel during one Pressure episode. Body clearance still applies; close defenders may request an attack turn.")]
    [Min(0)] public float pressureRetreatBudget = .6f;
    [Header("Player Escape")]
    [Tooltip("Sprinting away pauses new running decisions and attacks, including attack-turn owners, while enemies ease down to their profile Walk Speed. This stays active while sprinting, even when weaving. Nearby enemies and enemies you sprint toward may defend with normal shared attack limits. Existing attacks can finish and hit.")]
    [HideInInspector] public bool pauseRushWhileEscaping = true;
    [Tooltip("Seconds after actual sprinting stops before clearing this target's old pressure owners and starting a fresh initial rush. Enemies walk during the delay; brief sprint pauses restart the timer.")]
    [Min(0)] [HideInInspector] public float escapeResumeDelay = .8f;
    [Header("Charge")]
    [Tooltip("Master switch for profile-specific melee charges. Mid-range enemies may request attack turns without pressure ownership. Stone users are excluded.")]
    public bool enableCharge = true;
    [Tooltip("Confirmed charge hold duration. Player input and camera look pause during the hold; group attacks remain blocked through recovery grace. Zero disables.")]
    [Min(0)] public float chargeHoldDuration = .8f;
    [Tooltip("Freeze movement during charge restraint. The confirmed charge presentation also pauses gameplay input and camera look.")]
    public bool chargeLockMovement = true;
    [Tooltip("Field of view during a confirmed charge hold. Restored when the hold ends.")]
    [Range(15, 90)] public float chargeCameraFov = 45f;
    [Range(.25f, 1)] public float chargeHoldSpeedMultiplier = .25f;
    [Tooltip("Seconds to ease from running to walking when the player escapes.")]
    [Min(.05f)] [HideInInspector] public float escapeSlowdownTime = .8f;
    [Tooltip("Grace period after a charge restraint/event finishes before any enemy may start another attack.")]
    [Min(0)] public float chargePostHoldGrace = 1f;
    private static EnemyCombatSettings cached;
    public static EnemyCombatSettings Shared => cached != null ? cached : (cached = Resources.Load<EnemyCombatSettings>("EnemyCombatSettings"));
    public static float StartInterval => Shared != null ? Mathf.Max(0, Shared.attackStartInterval) : .8f;
    public static bool RushEnabled => Shared == null || Shared.useRushSlots;
    public static int RushLimit => Shared != null ? Mathf.Max(1, Shared.maxRushers) : 3;
    public static float ApproachRunChance => Shared != null ? Mathf.Clamp01(Shared.approachRunChance) : .55f;
    public static float SampleRushStartDelay()
    {
        Vector2 range = Shared != null ? Shared.rushStartDelay : new Vector2(.3f, .7f);
        float minimum = Mathf.Max(.05f, range.x);
        return Random.Range(minimum, Mathf.Max(minimum, range.y));
    }
    public static float PressureDistance => Shared != null ? Mathf.Max(1, Shared.pressureDistance) : 4.5f;
    public static float ReplacementInterval => Shared != null ? Mathf.Max(0, Shared.replacementInterval) : .6f;
    public static float RushReentry => Shared != null ? Mathf.Max(0, Shared.rushReentryDelay) : 2f;
    public static float RushStallTimeout => Shared != null ? Mathf.Max(.5f, Shared.rushStallTimeout) : 2f;
    public static float InterceptDistance => Shared != null ? Mathf.Max(1, Shared.frontInterceptDistance) : 6f;
    public static float InterceptAngle => Shared != null ? Mathf.Clamp(Shared.frontInterceptAngle, 10, 160) : 100f;
    public static float HandoffDelay => Shared != null ? Mathf.Max(.1f, Shared.pursuitHandoffDelay) : 2.5f;
    public static float HandoffGap => Shared != null ? Mathf.Max(.1f, Shared.pursuitHandoffGap) : .5f;
    public static float RetreatBudget => Shared != null ? Mathf.Max(0, Shared.pressureRetreatBudget) : .6f;
    public static bool PauseWhileEscaping => false;
    public static float EscapeResumeDelay => Shared != null ? Mathf.Max(0, Shared.escapeResumeDelay) : .8f;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() => cached = null;
}

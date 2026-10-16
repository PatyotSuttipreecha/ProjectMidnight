using UnityEngine;

// One shared asset, independent of enemy profiles and spawn groups.
[CreateAssetMenu(menuName = "System Modification/Enemy Combat Settings", fileName = "EnemyCombatSettings")]
public sealed class EnemyCombatSettings : ScriptableObject
{
    [Header("Attack Timing")]
    [Tooltip("Minimum scaled seconds between entering Preparation for enemies targeting the same player. Does not guarantee separated damage frames when weapon windups differ. Zero disables spacing.")]
    [Min(0)] public float attackStartInterval = .8f;
    [Header("Pressure Group")]
    [Tooltip("Coordinate an initial rush, persistent pressure owners and escape pauses per player. Disabling restores each profile's saved Walk/Boost rhythm.")]
    public bool useRushSlots = true;
    [Tooltip("Maximum pressure owners per player, including circling, preparing, attacking and recovering. Initially all distant enemies may run until this group fills. Independent of attack turns.")]
    [Min(1)] public int maxRushers = 3;
    [Tooltip("Distance in metres at which approaching enemies can join the initial pressure group. Once the group fills, other enemies walk and wait outside this distance.")]
    [Min(1)] public float pressureDistance = 4.5f;
    [Tooltip("Delay before replacing a released owner, and between replacements. Choose the nearest eligible enemy with a complete NavMesh path; never reopen the initial mass rush for a single vacancy.")]
    [Min(0)] public float replacementInterval = .6f;
    [Tooltip("Minimum time before an enemy that released its rush slot can request another. Prevents boundary flicker and immediate reacquisition.")]
    [Min(0)] public float rushReentryDelay = 2f;
    [Tooltip("Release an owner making no progress while actively pursuing from far away. Circling, attacking, recovery and escape pauses do not count as stalls.")]
    [Min(.5f)] public float rushStallTimeout = 2f;
    [Header("Player Escape")]
    [Tooltip("Sprinting away pauses running and new attacks, including attack-turn owners, while enemies continue following at their profile Walk Speed. This stays active while sprinting, even when weaving. Existing attacks can finish and hit.")]
    public bool pauseRushWhileEscaping = true;
    [Tooltip("Seconds after actual sprinting stops before clearing this target's old pressure owners and starting a fresh initial rush. Enemies walk during the delay; brief sprint pauses restart the timer.")]
    [Min(0)] public float escapeResumeDelay = .8f;
    private static EnemyCombatSettings cached;
    public static EnemyCombatSettings Shared => cached != null ? cached : (cached = Resources.Load<EnemyCombatSettings>("EnemyCombatSettings"));
    public static float StartInterval => Shared != null ? Mathf.Max(0, Shared.attackStartInterval) : .8f;
    public static bool RushEnabled => Shared == null || Shared.useRushSlots;
    public static int RushLimit => Shared != null ? Mathf.Max(1, Shared.maxRushers) : 3;
    public static float PressureDistance => Shared != null ? Mathf.Max(1, Shared.pressureDistance) : 4.5f;
    public static float ReplacementInterval => Shared != null ? Mathf.Max(0, Shared.replacementInterval) : .6f;
    public static float RushReentry => Shared != null ? Mathf.Max(0, Shared.rushReentryDelay) : 2f;
    public static float RushStallTimeout => Shared != null ? Mathf.Max(.5f, Shared.rushStallTimeout) : 2f;
    public static bool PauseWhileEscaping => Shared == null || Shared.pauseRushWhileEscaping;
    public static float EscapeResumeDelay => Shared != null ? Mathf.Max(0, Shared.escapeResumeDelay) : .8f;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() => cached = null;
}

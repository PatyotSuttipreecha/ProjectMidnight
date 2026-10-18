using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour
{
    public enum PatrolMode { Random, Loop, PingPong }
    public enum EnemyState { Idle, Patrol, Waiting, Chase, Search, Attack, Dead, HitReaction, Pressure, Detect, Preparation, Recovery, Approach, Retreat, ChaseBurst, Rush, EscapeWalk, ChargePreparation, Charge }
    private float nextChargeTime;
    private float nextChargeDecision;
    private bool choseCharge;
    private bool chargeAttack;
    private readonly EnemyStaggerMeter staggerMeter = new EnemyStaggerMeter();
    public float AccumulatedImpact => staggerMeter.Impact;
    public string LastImpactResult => staggerMeter.LastResult;
    private float createdAt;
    public float SpawnAge => Mathf.Max(0, Time.time - createdAt);
    public float LastRootJumpDistance { get; private set; }
    private float lastRootJumpTime = -100;
    public bool HasRecentRootJump => Time.time - lastRootJumpTime < 3;
    public string SpawnSourceName { get; private set; } = "Scene / manual";
    public void RecordSpawnSource(EnemySpawnPoint point) => SpawnSourceName = point != null ? point.name : "Scene / manual";

    public static void PrepareAnimatedVisibility(GameObject root)
    {
        // The NavMesh root and procedural hit bones keep moving off camera.
        // Do not let skinning/animation retain an old pose/bounds until re-entry.
        foreach (var animation in root.GetComponentsInChildren<Animator>(true))
            animation.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            skin.updateWhenOffscreen = true;
    }
    private float retreatDeceleration;
    private bool easingRetreat;
    [Tooltip("Confirmed charge contact. Hook bite or camera events here. Player input/camera hold and group protection are configured in Shared Combat.")]
    public UnityEngine.Events.UnityEvent onChargeHit = new UnityEngine.Events.UnityEvent();
    private float chasePhaseDeadline;
    private bool chaseRhythmActive;
    public bool IsChaseBoosting { get; private set; }
    private float pressureWaitTime, retreatUntil;
    private bool pressureOrbitActive;
    private float pressureHoldRadius, pressureRetreatRemaining;
    private Vector3 lastPressurePosition;
    public bool IsCloseDefenseCandidate => playerDetected && player != null && !isAttacking &&
        Vector3.ProjectOnPlane(player.position - transform.position, Vector3.up).magnitude <= AttackReach + .25f && HasClearSight();
    private float nextPressureDirectionTime;
    public int PressureOrbitDirection { get; private set; } = 1;
    private bool aggressionOverrideSet, aggressionOverride;
    public bool IsAggressive => aggressionOverrideSet ? aggressionOverride : enemyData != null && enemyData.aggressiveOnStart;
    public void SetAggressive(bool enabled)
    {
        aggressionOverrideSet = true;
        ResetChaseRhythm();
        aggressionOverride = enabled;
        pressureWaitTime = retreatUntil = sightConfirmation = lostSightTimer = 0;
        if (enabled)
        {
            if (player == null)
            {
                var target = GameObject.FindWithTag("Player");
                if (target != null) player = ResolveTargetTransform(target.transform);
            }
            if (player != null && IsPatrolRunning && !IsDead) AlertTo(player);
        }
        else if (!isAttacking)
        {
            ReleaseAttackTurn();
            isAlert = playerDetected = investigatingNoise = false;
            returningToPatrol = true;
        }
    }
    public void EnableAggressive() => SetAggressive(true);
    public void DisableAggressive() => SetAggressive(false);
    private EnemyWeaponRuntime weaponRuntime;
    private EnemyStoneProjectile activeStone;
    private float sightConfirmation, noiseAnticipation;
    public EnemyWeaponSO CurrentWeapon => Application.isPlaying ? (weaponRuntime != null ? weaponRuntime.Current : null) :
        enemyData != null && enemyData.useLostCombat ? enemyData.startingWeapon : null;
    public float AttackReach => CurrentWeapon != null ? Mathf.Max(.1f, CurrentWeapon.range) : enemyData != null ? enemyData.attackRange : 0;
    public enum ApproachStyle { Direct, Flank, Burst }
    public ApproachStyle CurrentApproach { get; private set; }
    public bool HasAttackTurn => enemyData != null && IsPatrolRunning && !IsDead && player != null &&
        (!enemyData.coordinateAttacks || EnemyAttackCoordinator.HasTurn(this, player));
    public float AttackStartDelay => EnemyAttackCoordinator.StartDelay(ResolveTargetTransform(player));
    private float RushStopDistance => Mathf.Max(AttackReach, enemyData != null ? enemyData.chaseSlowdownDistance : 0);
    public Transform RushTarget => player;
    public bool RushEncounterActive => enemyData != null && isActiveAndEnabled && !IsDead && IsPatrolRunning &&
        AgentReady && isAlert && player != null;
    public bool CanHoldPressure => EnemyCombatSettings.RushEnabled && RushEncounterActive &&
        (playerDetected || isAttacking) && staggerRemaining <= 0 && knockbackRemaining <= 0 && Time.time >= retreatUntil;
    public bool CanRush => CanHoldPressure && !isAttacking &&
        Vector3.Distance(transform.position, player.position) > RushStopDistance;
    public bool HasRushSlot => EnemyRushCoordinator.HasSlot(this, ResolveTargetTransform(player));
    public bool IsRushRunning => EnemyRushCoordinator.ShouldRun(this, RushTarget);
    public bool WaitingForPressureSlot => EnemyRushCoordinator.IsWaiting(this, RushTarget);
    public string RushStatus => EnemyRushCoordinator.Describe(this, RushTarget);
    public bool CanReachRushTarget(NavMeshPath path)
    {
        if (!CanHoldPressure) return false;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        return NavMesh.SamplePosition(player.position, out var hit, 2f, filter) &&
            agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete;
    }
    public Vector3 GameplayDebugHeadPosition
    {
        get
        {
            Transform head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            return head != null ? head.position : transform.position + Vector3.up * 1.7f;
        }
    }
    private float nextApproachDecision, turnDeadline, nextTurnRequest;
    private Vector3 committedApproachDirection;
    public EnemyState CurrentState { get; private set; }
    private void OnGUI()
    {
        if (!EnemyStateDisplay.Available || (!EnemyStateDisplay.Enabled && !EnemyStateDisplay.ShowAttackRanges)) return;
        EnemyStateDisplay.DrawAll(activeEnemies);
    }
    public string GameplayDebugState
    {
        get
        {
        string status = CurrentState.ToString();
        if (!IsDead)
            status = !IsPatrolRunning ? "Paused - waiting for StartPatrol" : enemyData == null ? "Missing Enemy Profile" : !AgentReady ? "Agent not on NavMesh" : status;
        return status;
        }
    }
    public bool IsDead { get; private set; }
    // Raised once after death setup, before the corpse is removed.
    public event System.Action<EnemyController> Died;
    [Header("References")]
    public EnemyDataSO enemyData;
    [SerializeField] private Transform player;
    private NavMeshAgent agent;
    private Animator animator;
    private EnemyLocomotionAnimator locomotionAnimator;
    public bool UsesLocomotionBlendTree => locomotionAnimator != null && locomotionAnimator.Compatible;
    public float AnimationMovementSpeed => locomotionAnimator != null ? locomotionAnimator.MeasuredSpeed : 0;
    public float AnimationTargetBlend => locomotionAnimator != null ? locomotionAnimator.TargetBlend : 0;
    public float AnimationCurrentBlend => locomotionAnimator != null ? locomotionAnimator.CurrentBlend : 0;
    public string AnimationSpeedParameter => UsesLocomotionBlendTree ? locomotionAnimator.ResolvedSpeedParameter : "Not connected";
    public string LastAttackAnimation => locomotionAnimator != null ? locomotionAnimator.LastAttackStateName : "";
    public Vector2 AnimationMovementDirection => locomotionAnimator != null ? locomotionAnimator.Direction : Vector2.zero;
    private Vector3 lastAnimationPosition;

    [Header("Patrol Settings")]
    [Tooltip("Disable for cutscene actors. The enemy waits without patrol, sight or hearing reactions until StartPatrol is called.")]
    public bool startPatrolOnStart = true;
    [field: SerializeField, HideInInspector]
    public bool IsPatrolRunning { get; private set; }
    [SerializeField, HideInInspector] private Transform[] patrolPoints;
    [SerializeField] private PatrolRoute patrolRoute;
    public PatrolRoute Route => patrolRoute;
    [SerializeField, HideInInspector] private PatrolMode patrolMode;
    private int patrolDirection = 1;
    private int lastPatrolIndex = -1;
    private float waitTimer;
    private static int nextApproachSlot;
    private Vector3 approachDirection;
    private float nextChaseRepath;
    private NavMeshPath approachPath;
    private static readonly HashSet<EnemyController> activeEnemies = new HashSet<EnemyController>();
    public static IEnumerable<EnemyController> ActiveEnemies => activeEnemies;
    private bool returningToPatrol;
    private readonly List<int> validPatrolIndices = new List<int>();

    [Header("Vision and Attack")]
    [SerializeField, HideInInspector, Min(0f)] private float eyeHeight = 1f;
    [SerializeField, HideInInspector, Min(0f)] private float playerTargetHeight = 1f;
    [SerializeField, HideInInspector, Min(0f)] private float attackWindup = 0.5f;
    [Header("Debug Gizmos")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField, Min(0.1f)] private float patrolMarkerSize = 0.4f;
    [SerializeField] private Color patrolPointColor = new Color(1f, 0.1f, 0.8f);
    [SerializeField] private Color patrolRouteColor = new Color(0.1f, 0.85f, 1f);

    [Header("Death and Loot")]
    [SerializeField, HideInInspector] private bool useRagdoll;
    [SerializeField, HideInInspector] private string deathAnimation = "zombie_dead2";
    [SerializeField, HideInInspector, Min(0f)] private float deathAnimationLeadIn = 0.6f;
    [SerializeField, HideInInspector, Min(0f)] private float deathBlendDuration = 1.2f;
    [SerializeField, HideInInspector, Min(0f)] private float deathAnimationDrive = 25f;
    private EnemyDeathBlend deathBlend;
    [SerializeField, HideInInspector, Range(0f, 1f)] private float hybridDeathPushScale = 0.35f;
    private Rigidbody pendingDeathBody;
    private Vector3 pendingDeathPoint;
    private Vector3 pendingDeathImpulse;
    [SerializeField, HideInInspector, Min(0f)] private float corpseLifetime = 8f;
    [SerializeField, HideInInspector] private GameObject lootPrefab;
    [SerializeField, HideInInspector, Range(0f, 1f)] private float lootDropChance = 1f;
    [SerializeField, HideInInspector] private Vector3 lootOffset = new Vector3(0f, 0.2f, 0f);
    [SerializeField, HideInInspector, Range(0.1f, 1f)] private float ragdollGravityScale = 0.45f;
    [SerializeField, HideInInspector, Min(0f)] private float ragdollLinearDamping = 0.8f;
    [SerializeField, HideInInspector, Min(0f)] private float ragdollAngularDamping = 1.5f;
    [SerializeField, HideInInspector, Min(0f)] private float deathPushImpulse = 3f;
    [Header("Bullet Hit Reaction")]
    [SerializeField, HideInInspector] private bool enableHitReaction = true;
    [SerializeField, HideInInspector, Min(0f)] private float hitPushImpulse = 0.8f;
    [SerializeField, HideInInspector, Min(0.02f)] private float hitPhysicsDuration = 0.12f;
    [SerializeField, HideInInspector, Min(0.01f)] private float hitRecoveryDuration = 0.2f;
    [SerializeField, HideInInspector, Range(0f, 1f)] private float hitReactionBlend = 0.75f;
    [SerializeField, HideInInspector, Min(0f)] private float hitAnimationSupport = 30f;
    [SerializeField, HideInInspector, Range(0f, 60f)] private float hitMaxAngle = 25f;
    private EnemyHitReaction hitReaction;
    private Coroutine attackRoutine;
    private float staggerRemaining, knockbackRemaining;
    private Vector3 knockbackVelocity;
    private Rigidbody[] deathBodies;
    [SerializeField, HideInInspector, Min(0f)] private float corpseSettleDelay = 0.75f;
    private float corpseStillTime;
    private bool corpseSettled;
    [System.Serializable]
    public class BoneMassOverride
    {
        public HumanBodyBones bone = HumanBodyBones.Hips;
        [Min(0.01f)] public float mass = 5f;
    }
    [Header("Physics Mass")]
    [SerializeField, HideInInspector, Min(0.01f)] private float physicsMassMultiplier = 1f;
    [SerializeField, HideInInspector] private List<BoneMassOverride> boneMassOverrides = new List<BoneMassOverride>();
    private readonly Dictionary<Rigidbody, float> originalBodyMasses = new Dictionary<Rigidbody, float>();
    private bool massRefreshRequested;

    public bool isAttacking;
    public bool isAlert;
    private bool playerDetected;
    private float lostSightTimer;
    private bool investigatingNoise;
    private float noiseSearchTimer, noiseTravelTimer;
    private Vector3 noisePosition;
    [Header("Hearing Gizmos")]
    public bool showHearingGizmos = true;
    [Tooltip("Player whose movement radii are shown. Empty finds the scene player.")]
    public PlayerController hearingPreviewPlayer;
    public Guns hearingPreviewGun;
    private void OnDrawGizmosSelected()
    {
        if (!showHearingGizmos || enemyData == null || !enemyData.canHear) return;
        var target = hearingPreviewPlayer != null ? hearingPreviewPlayer : FindFirstObjectByType<PlayerController>();
        if (target == null) return;
        GameplayNoise.DrawRadius(EyePosition, EffectiveHearingRadius(target.walkNoiseRadius, GameplayNoise.Kind.Walk), GameplayNoise.Kind.Walk, "Hears ");
        GameplayNoise.DrawRadius(EyePosition, EffectiveHearingRadius(target.runNoiseRadius, GameplayNoise.Kind.Run), GameplayNoise.Kind.Run, "Hears ");
        var gun = hearingPreviewGun != null ? hearingPreviewGun : target.EquippedGun;
        if (gun != null) GameplayNoise.DrawRadius(EyePosition, EffectiveHearingRadius(gun.gunshotNoiseRadius, GameplayNoise.Kind.Gunshot), GameplayNoise.Kind.Gunshot, "Hears ");
    }
    private float EffectiveHearingRadius(float sourceRadius, GameplayNoise.Kind kind)
    {
        if (sourceRadius <= 0 || enemyData == null) return 0;
        return Mathf.Min(sourceRadius * Mathf.Max(0, enemyData.hearingMultiplier), enemyData.HearingRadius(kind));
    }
    private void OnEnable()
    {
        activeEnemies.Add(this);
        GameplayNoise.Emitted += HearNoise;
    }
    private void LateUpdate()
    {
        ApplyCrowdSeparation(Time.deltaTime);
        float deltaTime = Time.deltaTime;
        if (!IsDead && SpawnAge > .25f && deltaTime > 0)
        {
            float distance = Vector3.Distance(transform.position, lastAnimationPosition);
            float speed = Mathf.Max(agent != null ? agent.speed : 0, ChargeSettings != null ? ChargeSettings.speed : 0);
            if (distance > Mathf.Max(1, speed * deltaTime * 3))
            {
                LastRootJumpDistance = distance; lastRootJumpTime = Time.time;
                if (EnemyStateDisplay.Enabled) Debug.LogWarning("Enemy root jump: " + name + " moved " + distance.ToString("0.00") + "m in one frame; source=" + SpawnSourceName, this);
            }
        }
        if (deltaTime > 0 && !IsDead)
        {
            // Include NavMesh avoidance and separation; exclude forced knockback.
            Vector3 velocity = IsPatrolRunning && (!isAttacking || CurrentState == EnemyState.Charge || CurrentState == EnemyState.Preparation && !agent.isStopped) && staggerRemaining <= 0 && knockbackRemaining <= 0 && AgentReady && !agent.isStopped
                ? (transform.position - lastAnimationPosition) / deltaTime : Vector3.zero;
            if (velocity.sqrMagnitude > 10000) velocity = Vector3.zero; // Warp/teleport is not locomotion.
            if (!HasChargeRunAnimation) locomotionAnimator?.Update(velocity, isAttacking && CurrentState == EnemyState.Attack, deltaTime, CurrentWeapon);
        }
        lastAnimationPosition = transform.position;
    }
    private void ApplyCrowdSeparation(float deltaTime)
    {
        if (IsDead || !IsPatrolRunning || isAttacking || !AgentReady || agent.isStopped ||
            enemyData == null || !enemyData.separateNearbyEnemies || deltaTime <= 0f) return;
        float radius = Mathf.Max(.1f, enemyData.separationRadius);
        Vector3 push = Vector3.zero;
        foreach (var other in activeEnemies)
        {
            if (other == null || other == this || other.IsDead || !other.AgentReady ||
                other.gameObject.scene != gameObject.scene || Mathf.Abs(other.transform.position.y - transform.position.y) > .75f) continue;
            Vector3 away = Vector3.ProjectOnPlane(transform.position - other.transform.position, Vector3.up);
            float distance = away.magnitude;
            if (distance >= radius) continue;
            if (distance < .001f)
            {
                // Give overlapping pairs opposite directions, without fresh random jitter.
                int mine = GetInstanceID(), theirs = other.GetInstanceID();
                float angle = (Mathf.Min(mine, theirs) % 360) * Mathf.Deg2Rad;
                away = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (mine < theirs ? 1 : -1);
            }
            else away /= distance;
            push += away * (1f - distance / radius);
        }
        Vector3 step = Vector3.ClampMagnitude(push, 1f) * Mathf.Min(Mathf.Max(0f, enemyData.separationSpeed), agent.speed) * deltaTime;
        if (step.sqrMagnitude < .000001f) return;
        Vector3 destination = agent.nextPosition + step;
        // Clip against NavMesh boundaries; never push through a wall or off an edge.
        if (agent.Raycast(destination, out var edge))
        {
            Vector3 safe = Vector3.ProjectOnPlane(edge.position - agent.nextPosition, Vector3.up);
            step = Vector3.ClampMagnitude(safe, Mathf.Max(0f, safe.magnitude - .02f));
        }
        agent.Move(step);
    }
    private void HearNoise(Vector3 position, float radius, Transform source, GameplayNoise.Kind kind)
    {
        if (IsDead || !IsPatrolRunning || enemyData == null || !enemyData.canHear || !AgentReady || source == null || source.GetComponent<PlayerController>() == null) return;
        float hearingRadius = EffectiveHearingRadius(radius, kind);
        if (hearingRadius <= 0 || Vector3.Distance(EyePosition, position) > hearingRadius) return;
        // Sight/chase takes priority; hearing never reveals a moving player's live position.
        if (isAlert || isAttacking || Time.time < retreatUntil) return;
        if (!NavMesh.SamplePosition(position, out var hit, 2f, agent.areaMask)) return;
        var path = new NavMeshPath();
        if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) return;
        bool newInvestigation = !investigatingNoise;
        investigatingNoise = true; noisePosition = hit.position; noiseSearchTimer = noiseTravelTimer = 0;
        if (newInvestigation) noiseAnticipation = enemyData.useLostCombat ? Mathf.Max(0, enemyData.detectAnticipation) : 0;
        agent.isStopped = false; agent.SetPath(path); CurrentState = EnemyState.Search;
    }
    private float attackTimer;
    private Vector3 lastKnownPlayerPosition;
    private int animationState;
    private static readonly int Idle = Animator.StringToHash("zombie_idle1");
    private static readonly int Walk = Animator.StringToHash("zombie_walk2");
    private static readonly int Attack = Animator.StringToHash("zombie_attack2_1");
    private bool AgentReady => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;
    private Vector3 EyePosition => transform.position + Vector3.up * eyeHeight;

    private void ApplySharedProfile()
    {
        if (enemyData == null || !enemyData.useSharedSettings) return;
        eyeHeight = enemyData.eyeHeight;
        playerTargetHeight = enemyData.playerTargetHeight;
        attackWindup = enemyData.attackWindup;
        useRagdoll = enemyData.useRagdoll;
        deathAnimation = enemyData.deathAnimation;
        deathAnimationLeadIn = enemyData.deathAnimationLeadIn;
        deathBlendDuration = enemyData.deathBlendDuration;
        deathAnimationDrive = enemyData.deathAnimationDrive;
        hybridDeathPushScale = enemyData.hybridDeathPushScale;
        corpseLifetime = enemyData.corpseLifetime;
        lootPrefab = enemyData.lootPrefab;
        lootDropChance = enemyData.lootDropChance;
        lootOffset = enemyData.lootOffset;
        ragdollGravityScale = enemyData.ragdollGravityScale;
        ragdollLinearDamping = enemyData.ragdollLinearDamping;
        ragdollAngularDamping = enemyData.ragdollAngularDamping;
        deathPushImpulse = enemyData.deathPushImpulse;
        enableHitReaction = enemyData.enableHitReaction;
        hitPushImpulse = enemyData.hitPushImpulse;
        hitPhysicsDuration = enemyData.hitPhysicsDuration;
        hitRecoveryDuration = enemyData.hitRecoveryDuration;
        hitReactionBlend = enemyData.hitReactionBlend;
        hitAnimationSupport = enemyData.hitAnimationSupport;
        hitMaxAngle = enemyData.hitMaxAngle;
        corpseSettleDelay = enemyData.corpseSettleDelay;
        physicsMassMultiplier = enemyData.physicsMassMultiplier;
        boneMassOverrides = new List<BoneMassOverride>();
        if (enemyData.boneMassOverrides != null)
            foreach (BoneMassOverride entry in enemyData.boneMassOverrides)
                if (entry != null) boneMassOverrides.Add(new BoneMassOverride { bone = entry.bone, mass = entry.mass });
        if (agent != null)
        {
            agent.speed = enemyData.walkSpeed;
            agent.acceleration = enemyData.acceleration;
            agent.angularSpeed = enemyData.turnSpeed;
            agent.stoppingDistance = enemyData.stoppingDistance;
        }
        if (TryGetComponent(out HitBoxManager hp)) hp.health = enemyData.startingHealth;
    }

    public void ConfigureSpawn(EnemyDataSO profile, PatrolRoute route, Transform target = null)
    {
        if (profile != null) enemyData = profile;
        patrolRoute = route;
        patrolPoints = new Transform[0];
        if (target != null) player = target;
    }
    private void Awake()
    {
        createdAt = Time.time;
        approachPath = new NavMeshPath();
        IsPatrolRunning = startPatrolOnStart;
        agent = GetComponent<NavMeshAgent>();
        float angle = (++nextApproachSlot * 137.50776f) * Mathf.Deg2Rad;
        approachDirection = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        if (enemyData != null && enemyData.varyAvoidancePriority)
            agent.avoidancePriority = 30 + nextApproachSlot % 40;
        animator = GetComponent<Animator>();
        locomotionAnimator = new EnemyLocomotionAnimator(animator, enemyData);
        lastAnimationPosition = transform.position;
        ApplySharedProfile();
        if (enemyData != null && enemyData.useLostCombat)
        {
            weaponRuntime = GetComponent<EnemyWeaponRuntime>();
            if (weaponRuntime == null) weaponRuntime = gameObject.AddComponent<EnemyWeaponRuntime>();
            weaponRuntime.Equip(enemyData.startingWeapon);
        }
        if (patrolRoute != null) { patrolPoints = patrolRoute.points != null ? (Transform[])patrolRoute.points.Clone() : new Transform[0]; patrolMode = patrolRoute.mode; }
        // NavMeshAgent owns movement; animation must not also move the root.
        if (animator != null) animator.applyRootMotion = false;
        PrepareAnimatedVisibility(gameObject);
        if ((useRagdoll || enableHitReaction) && GetComponentsInChildren<Joint>().Length == 0)
        {
            if (!EnemyRagdollBuilder.Build(animator))
                Debug.LogWarning("Cannot auto-build ragdoll: this enemy needs a Humanoid Animator or a configured physics rig.", this);
        }
        if (useRagdoll || enableHitReaction)
            foreach (Rigidbody body in GetComponentsInChildren<Rigidbody>())
                if (body.transform != transform) body.isKinematic = true;
        hitReaction = GetComponent<EnemyHitReaction>();
        if (hitReaction == null) hitReaction = gameObject.AddComponent<EnemyHitReaction>();
        ApplyPhysicsMass();
    }

    private void Start()
    {
        if (player == null)
        {
            GameObject target = GameObject.FindWithTag("Player");
            if (target != null) player = target.transform;
        }
        player = ResolveTargetTransform(player);
        if (IsPatrolRunning) PickPatrolPoint();
        else PausePatrol();
    }

    public void StartPatrol()
    {
        if (IsDead || IsPatrolRunning) return;
        IsPatrolRunning = true;
        returningToPatrol = true;
        waitTimer = 0f;
        if (AgentReady) agent.isStopped = false;
    }

    public void PausePatrol()
    {
        if (IsDead) return;
        EnemyRushCoordinator.Release(this);
        pressureOrbitActive = false;
        ResetChaseRhythm();
        ReleaseAttackTurn();
        IsPatrolRunning = false;
        choseCharge = chargeAttack = easingRetreat = false;
        StopAllCoroutines(); // Only attack routines run while this enemy is alive.
        attackRoutine = null;
        staggerRemaining = knockbackRemaining = 0f;
        if (activeStone != null) activeStone.Cancel();
        activeStone = null;
        sightConfirmation = noiseAnticipation = 0;
        pressureWaitTime = retreatUntil = 0;
        if (agent != null) agent.updateRotation = true;
        isAttacking = isAlert = playerDetected = investigatingNoise = false;
        returningToPatrol = true;
        waitTimer = 0f;
        if (AgentReady) { agent.isStopped = true; agent.ResetPath(); }
        CurrentState = EnemyState.Idle;
        PlayAnimation(Idle);
    }

    private void Update()
    {
        if (massRefreshRequested) ApplyPhysicsMass();
        if (IsDead) return;
        if (!IsPatrolRunning)
        {
            if (AgentReady) agent.isStopped = true;
            return;
        }
        attackTimer = Mathf.Max(0f, attackTimer - Time.deltaTime);
        if (enemyData != null) staggerMeter.Tick(enemyData.actionBreak, Time.time);
        if (enemyData == null || !AgentReady) { EnemyRushCoordinator.Release(this); return; }
        agent.updateRotation = true;
        agent.stoppingDistance = isAttacking && CurrentState == EnemyState.Attack ? 0 : Mathf.Max(0, enemyData.stoppingDistance);
        if (staggerRemaining > 0f || knockbackRemaining > 0f)
        {
            EnemyRushCoordinator.Release(this);
            pressureOrbitActive = false;
            ResetChaseRhythm();
            staggerRemaining = Mathf.Max(0f, staggerRemaining - Time.deltaTime);
            agent.isStopped = true;
            CurrentState = EnemyState.HitReaction;
            if (knockbackRemaining > 0f)
            {
                float stepTime = Mathf.Min(Time.deltaTime, knockbackRemaining);
                knockbackRemaining = Mathf.Max(0f, knockbackRemaining - stepTime);
                Vector3 step = knockbackVelocity * stepTime;
                if (agent.Raycast(agent.nextPosition + step, out var edge))
                {
                    Vector3 safe = Vector3.ProjectOnPlane(edge.position - agent.nextPosition, Vector3.up);
                    step = Vector3.ClampMagnitude(safe, Mathf.Max(0f, safe.magnitude - .02f));
                }
                agent.Move(step);
            }
            return;
        }
        if (Time.time < retreatUntil && !isAttacking)
        {
            pressureOrbitActive = false;
            ResetChaseRhythm();
            agent.isStopped = false;
            if (!easingRetreat)
            {
                easingRetreat = true;
                float duration = .8f;
                retreatDeceleration = Mathf.Max(.1f, (agent.speed - Mathf.Max(0, enemyData.walkSpeed)) / Mathf.Max(.05f, duration));
            }
            agent.speed = Mathf.MoveTowards(agent.speed, Mathf.Max(0, enemyData.walkSpeed), retreatDeceleration * Time.deltaTime);
            Patrol();
            if (CurrentState == EnemyState.Patrol) CurrentState = EnemyState.Retreat;
            return;
        }
        // Orbit movement must not lose an already visible target just because steering
        // faces tangentially. Walls and detection distance still end visual contact.
        playerDetected = IsAggressive && player != null || CanSeePlayer() || (CurrentState == EnemyState.Pressure && player != null &&
            Vector3.Distance(transform.position, player.position) <= enemyData.detectionRadius && HasClearSight());
        if (!IsAggressive && enemyData.useLostCombat && !isAlert && playerDetected)
        {
            sightConfirmation += Time.deltaTime;
            if (sightConfirmation < Mathf.Max(0, enemyData.detectAnticipation))
            {
                ResetChaseRhythm();
                CurrentState = EnemyState.Detect;
                agent.isStopped = true;
                PlayAnimation(Idle);
                FaceTarget(player);
                return;
            }
        }
        else sightConfirmation = 0;
        if (playerDetected)
        {
            investigatingNoise = false;
            isAlert = true;
            lostSightTimer = 0f;
            lastKnownPlayerPosition = player.position;
        }
        else if (isAlert)
        {
            lostSightTimer += Time.deltaTime;
            if (lostSightTimer >= enemyData.lostSightCooldown)
            {
                isAlert = false;
                returningToPatrol = true;
                waitTimer = 0f;
            }
        }

        if (!CanHoldPressure) EnemyRushCoordinator.Release(this);
        if (!isAttacking && !InChargeBand) choseCharge = false;
        EnemyRushCoordinator.Tick(RushTarget);
        easingRetreat = false;
        UpdateApproachDecision();
        UpdateCombatTurn();
        if (isAttacking) { pressureOrbitActive = false; ResetChaseRhythm(); return; }
        agent.isStopped = false;
        if (isAlert)
        {
            CurrentState = playerDetected ? EnemyState.Chase : EnemyState.Search;
            float targetDistance = player != null ? Vector3.Distance(transform.position, player.position) : float.PositiveInfinity;
            if (playerDetected && enemyData.coordinateAttacks)
                CurrentState = HasAttackTurn ? targetDistance <= Mathf.Max(AttackReach, enemyData.chaseSlowdownDistance) ? EnemyState.Approach : EnemyState.Chase :
                    targetDistance <= Mathf.Max(enemyData.engagementDistance, PressureRadius + 1f) ? EnemyState.Pressure : EnemyState.Chase;
            if (EnemyCombatSettings.RushEnabled && playerDetected && !HasRushSlot && !HasAttackTurn)
                CurrentState = WaitingForPressureSlot && targetDistance <= Mathf.Max(enemyData.engagementDistance, PressureRadius + 1f)
                    ? EnemyState.Pressure : EnemyState.Chase;
            if (IsRushRunning) CurrentState = EnemyState.Rush;
            UpdateMovementSpeed();
            if (CurrentState == EnemyState.Pressure)
            {
                if (!pressureOrbitActive)
                {
                    // Losing a turn changes permission, not the enemy's position.
                    // Keep an inner arrival/recovery instead of backing out to
                    // the outer waiting ring. Hold this radius for this orbit.
                    float distance = Vector3.ProjectOnPlane(transform.position - player.position, Vector3.up).magnitude;
                    pressureHoldRadius = Mathf.Max(agent.radius + .5f,
                        Mathf.Min(RequestedPressureRadius, distance));
                    pressureRetreatRemaining = EnemyCombatSettings.RetreatBudget;
                    lastPressurePosition = transform.position;
                    nextChaseRepath = 0;
                }
                else
                {
                    Vector3 outward = Vector3.ProjectOnPlane(transform.position - player.position, Vector3.up).normalized;
                    pressureRetreatRemaining = Mathf.Max(0, pressureRetreatRemaining - Mathf.Max(0,
                        Vector3.Dot(transform.position - lastPressurePosition, outward)));
                    lastPressurePosition = transform.position;
                }
                UpdatePressureDirection();
                pressureWaitTime += Time.deltaTime;
                if (!HasRushSlot && !IsAggressive && enemyData.pressureWaitTimeout > 0 && pressureWaitTime >= enemyData.pressureWaitTimeout)
                {
                    EnemyRushCoordinator.Release(this);
                    ReleaseAttackTurn();
                    pressureWaitTime = 0;
                    retreatUntil = Time.time + Mathf.Max(.5f, enemyData.pressureRetreatCooldown);
                    isAlert = playerDetected = investigatingNoise = false;
                    sightConfirmation = lostSightTimer = waitTimer = 0;
                    returningToPatrol = true;
                    agent.ResetPath();
                    Patrol();
                    CurrentState = EnemyState.Retreat;
                    return;
                }
                agent.updateRotation = false;
                agent.stoppingDistance = .1f;
                agent.speed = Mathf.Max(0, enemyData.pressureOrbitSpeed);
                Vector3 facing = Vector3.ProjectOnPlane(player.position - transform.position, Vector3.up);
                if (facing.sqrMagnitude > .001f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(facing), Mathf.Max(1, enemyData.turnSpeed) * Time.deltaTime);
            }
            else { pressureWaitTime = 0; pressureOrbitActive = false; }
            if (CanStartCharge(targetDistance))
            {
                attackRoutine = StartCoroutine(PerformCharge());
                return;
            }
            // Attack needs reach and clear sight, not the patrol vision cone.
            // A nearby player can be beside the enemy after an avoidance manoeuvre.
            if (attackTimer <= 0f && InAttackRange() && HasAttackTurn && TargetAvailableForLostHit())
            {
                attackRoutine = StartCoroutine(PerformAttack());
                return;
            }
            if (!agent.pathPending && Time.time >= nextChaseRepath)
            {
                nextChaseRepath = Time.time + Mathf.Max(.05f, enemyData.chaseRepathInterval);
                SetChaseDestination();
            }
            PlayLocomotion(agent.pathPending || agent.remainingDistance > agent.stoppingDistance + 0.1f);
        }
        else
        {
            pressureOrbitActive = false;
            ResetChaseRhythm();
            UpdateMovementSpeed();
            pressureWaitTime = 0;
            if (investigatingNoise)
            {
                if (noiseAnticipation > 0)
                {
                    noiseAnticipation = Mathf.Max(0, noiseAnticipation - Time.deltaTime);
                    CurrentState = EnemyState.Detect;
                    agent.isStopped = true;
                    PlayAnimation(Idle);
                    return;
                }
                CurrentState = EnemyState.Search;
                noiseTravelTimer += Time.deltaTime;
                bool arrived = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + .1f;
                if (arrived) noiseSearchTimer += Time.deltaTime;
                PlayAnimation(arrived ? Idle : Walk);
                if (noiseSearchTimer >= enemyData.noiseSearchDuration || noiseTravelTimer >= enemyData.noiseTravelTimeout)
                { investigatingNoise = false; returningToPatrol = true; waitTimer = 0; Patrol(); }
            }
            else Patrol();
        }
    }

    private void UpdateMovementSpeed()
    {
        if (!isAlert || !playerDetected || player == null) ResetChaseRhythm();
        float speed = Mathf.Max(0f, enemyData.walkSpeed);
        bool grantedApproach = false;
        if (isAlert && playerDetected && player != null)
        {
            float distance = Vector3.Distance(transform.position, player.position);
            float range = Mathf.Max(0f, AttackReach);
            float outer = Mathf.Max(range + .01f, enemyData.chaseSlowdownDistance);
            float blend = Mathf.InverseLerp(range, outer, distance);
            bool rhythm = enemyData.useChaseRhythm && CurrentState == EnemyState.Chase && distance > outer;
            bool rushMode = EnemyCombatSettings.RushEnabled;
            if (rushMode)
            {
                ResetChaseRhythm();
                grantedApproach = HasRushSlot || HasAttackTurn;
                // Granted attackers keep one approach speed throughout the close
                // zone; do not dip to Patrol Walk Speed at its outer boundary.
                speed = IsRushRunning ? Mathf.Max(0, enemyData.chaseSpeed) * Mathf.Max(1, enemyData.burstSpeedMultiplier) :
                    grantedApproach && distance <= outer ? Mathf.Max(0, enemyData.closeApproachSpeed) :
                    Mathf.Max(0, enemyData.walkSpeed);
            }
            else if (rhythm)
            {
                if (!chaseRhythmActive)
                {
                    chaseRhythmActive = true;
                    IsChaseBoosting = false;
                    chasePhaseDeadline = Time.time + SampleChaseDuration(enemyData.chaseWalkDuration);
                }
                else if (Time.time >= chasePhaseDeadline)
                {
                    IsChaseBoosting = !IsChaseBoosting;
                    chasePhaseDeadline = Time.time + SampleChaseDuration(IsChaseBoosting ? enemyData.chaseBoostDuration : enemyData.chaseWalkDuration);
                }
                speed = IsChaseBoosting ? Mathf.Max(0, enemyData.chaseSpeed) * Mathf.Max(1, enemyData.burstSpeedMultiplier) : Mathf.Max(0, enemyData.walkSpeed);
                if (IsChaseBoosting) CurrentState = EnemyState.ChaseBurst;
            }
            else
            {
                ResetChaseRhythm();
                speed = Mathf.Lerp(Mathf.Max(0f, enemyData.closeApproachSpeed), Mathf.Max(0f, enemyData.chaseSpeed), blend);
            }
            if (!rushMode && !rhythm && enemyData.useApproachDecisions)
            {
                float multiplier = CurrentApproach == ApproachStyle.Burst ? enemyData.burstSpeedMultiplier :
                    CurrentApproach == ApproachStyle.Direct ? enemyData.pressureSpeedMultiplier : 1f;
                speed *= Mathf.Lerp(1f, Mathf.Max(.1f, multiplier), blend);
            }
            if (!rushMode && enemyData.coordinateAttacks && !HasAttackTurn && distance <= Mathf.Max(enemyData.engagementDistance, AttackReach))
                speed = Mathf.Min(speed, Mathf.Max(enemyData.walkSpeed, enemyData.closeApproachSpeed));
        }
        // A full pressure group or an escaping player must slow waiters immediately.
        agent.speed = EnemyCombatSettings.RushEnabled && !grantedApproach && !IsRushRunning && speed < agent.speed ? speed :
            Mathf.MoveTowards(agent.speed, speed, Mathf.Max(.1f, enemyData.speedChangeRate) * Time.deltaTime);
    }
    private static float SampleChaseDuration(Vector2 duration)
    {
        float min = Mathf.Max(.1f, duration.x);
        return Random.Range(min, Mathf.Max(min, duration.y));
    }
    private void ResetChaseRhythm()
    {
        chaseRhythmActive = IsChaseBoosting = false;
        chasePhaseDeadline = 0;
    }

    public float EffectiveAttackTurnRequestDistance => enemyData == null ? 0 : Mathf.Max(
        enemyData.engagementDistance, AttackReach,
        EnemyCombatSettings.RushEnabled
            ? Mathf.Max(EnemyCombatSettings.PressureDistance, enemyData.chaseSlowdownDistance, enemyData.pressureRingRadius) + .5f : 0,
        ChargeSettings != null && ChargeSettings.enabled && EnemyCombatSettings.Shared != null && EnemyCombatSettings.Shared.enableCharge
            ? EffectiveChargeDistance : 0);

    private void UpdateApproachDecision()
    {
        if (isAlert && playerDetected && player != null && !isAttacking && Time.time >= nextApproachDecision)
        {
            nextApproachDecision = Time.time + Mathf.Max(.5f, enemyData.approachDecisionInterval);
            Vector3 weights = enemyData.approachWeights;
            float direct = Mathf.Max(0f, weights.x), flank = Mathf.Max(0f, weights.y), burst = Mathf.Max(0f, weights.z);
            float roll = Random.value * (direct + flank + burst);
            CurrentApproach = !enemyData.useApproachDecisions || direct + flank + burst <= 0f ? ApproachStyle.Direct :
                roll < direct ? ApproachStyle.Direct : roll < direct + flank ? ApproachStyle.Flank : ApproachStyle.Burst;
            Vector3 side = Vector3.ProjectOnPlane(transform.position - player.position, Vector3.up).normalized;
            if (side.sqrMagnitude < .01f) side = approachDirection;
            committedApproachDirection = Quaternion.AngleAxis((approachDirection.x < 0f ? -1f : 1f) * enemyData.flankAngle, Vector3.up) * side;
        }
    }

    // This is the only combat permission request. It deliberately does not
    // consult runner ownership or the random choice of attack animation/action.
    private void UpdateCombatTurn()
    {
        if (!enemyData.coordinateAttacks) { EnemyAttackCoordinator.Release(this); return; }
        bool reserved = EnemyAttackCoordinator.HasTurn(this, player);
        if (isAttacking)
        {
            // Hold the turn through preparation, damage and recovery.
            EnemyAttackCoordinator.Request(this, player, enemyData.maxConcurrentAttackers);
            return;
        }
        if (!isAlert || !playerDetected || player == null || attackTimer > 0f || !TargetAvailableForLostHit() ||
            reserved && Time.time >= turnDeadline)
        {
            if (reserved) ReleaseAttackTurn();
            return;
        }
        if (Time.time < nextTurnRequest || Vector3.Distance(transform.position, player.position) > EffectiveAttackTurnRequestDistance) return;
        if (EnemyAttackCoordinator.Request(this, player, enemyData.maxConcurrentAttackers) && !reserved)
            turnDeadline = Time.time + Mathf.Max(.5f, enemyData.attackTurnTimeout);
    }

    private void ReleaseAttackTurn()
    {
        EnemyAttackCoordinator.Release(this);
        nextTurnRequest = Time.time + (enemyData != null ? Mathf.Max(.1f, enemyData.attackTurnRest) : 1f);
    }

    private float RequestedPressureRadius => Mathf.Max(enemyData.pressureRingRadius, agent.radius + .5f,
        WaitingForPressureSlot ? EnemyCombatSettings.PressureDistance + .5f : 0);
    private float PressureRadius => pressureOrbitActive
        ? Mathf.Min(RequestedPressureRadius, pressureHoldRadius) : RequestedPressureRadius;
    private void UpdatePressureDirection()
    {
        if (enemyData.pressureOrbitMode != EnemyDataSO.PressureOrbitMode.Random)
        {
            PressureOrbitDirection = enemyData.pressureOrbitMode == EnemyDataSO.PressureOrbitMode.Clockwise ? 1 : -1;
            pressureOrbitActive = true;
            return;
        }
        if (pressureOrbitActive && Time.time < nextPressureDirectionTime) return;
        pressureOrbitActive = true;
        PressureOrbitDirection = Random.value < .5f ? -1 : 1;
        float minimum = Mathf.Max(.5f, enemyData.pressureDirectionDuration.x);
        nextPressureDirectionTime = Time.time + Random.Range(minimum, Mathf.Max(minimum, enemyData.pressureDirectionDuration.y));
        nextChaseRepath = 0; // Repath once for the newly selected direction.
    }
    private bool SetPressureDestination()
    {
        if ((!enemyData.coordinateAttacks && !EnemyCombatSettings.RushEnabled) || HasAttackTurn || !playerDetected || player == null) return false;
        if (CurrentState != EnemyState.Pressure) return false;
        float distance = Vector3.ProjectOnPlane(transform.position - lastKnownPlayerPosition, Vector3.up).magnitude;
        float clearance = agent.radius + .5f;
        if (pressureRetreatRemaining <= .01f && distance <= clearance + .1f)
        {
            agent.isStopped = true;
            return true;
        }
        float radius = Mathf.Max(clearance, Mathf.Min(PressureRadius, distance + pressureRetreatRemaining));
        if (distance > Mathf.Max(radius + 1f, enemyData.engagementDistance)) return false;
        Vector3 side = Vector3.ProjectOnPlane(transform.position - lastKnownPlayerPosition, Vector3.up).normalized;
        if (side.sqrMagnitude < .01f) side = approachDirection;
        // Short same-direction arcs avoid cutting across the player's body.
        if (distance >= radius - .25f && distance <= radius + .5f)
            side = Quaternion.AngleAxis(enemyData.pressureOrbitStep * PressureOrbitDirection, Vector3.up) * side;
        Vector3 desired = lastKnownPlayerPosition + side * radius;
        if (NavMesh.SamplePosition(desired, out var hit, .6f, agent.areaMask) &&
            Mathf.Abs(hit.position.y - lastKnownPlayerPosition.y) < .75f &&
            Vector3.Distance(hit.position, lastKnownPlayerPosition) >= radius - .35f &&
            agent.CalculatePath(hit.position, approachPath) && approachPath.status == NavMeshPathStatus.PathComplete)
        {
            // Reject detours/chords that pass through the player's clearance circle.
            var corners = approachPath.corners;
            for (int i = 1; i < corners.Length; i++)
            {
                Vector3 start = Vector3.ProjectOnPlane(corners[i - 1] - lastKnownPlayerPosition, Vector3.up);
                Vector3 segment = Vector3.ProjectOnPlane(corners[i] - corners[i - 1], Vector3.up);
                float t = segment.sqrMagnitude > .0001f ? Mathf.Clamp01(-Vector3.Dot(start, segment) / segment.sqrMagnitude) : 0;
                if ((start + segment * t).magnitude < Mathf.Min(distance, radius) - .35f)
                { agent.isStopped = true; return true; }
            }
            if (!agent.hasPath || (agent.destination - hit.position).sqrMagnitude > .09f) agent.SetDestination(hit.position);
        }
        else agent.isStopped = true; // Wait where reachable rather than rush into a blocked slot.
        return true;
    }

    private void PlayLocomotion(bool moving)
    {
        if (!moving) { PlayAnimation(Idle); return; }
        if (playerDetected && isAlert && agent.velocity.magnitude > enemyData.walkSpeed + .2f &&
            animator != null && !string.IsNullOrWhiteSpace(enemyData.runAnimation))
        {
            int run = Animator.StringToHash(enemyData.runAnimation);
            if (animator.HasState(0, run)) { PlayAnimation(run); return; }
        }
        PlayAnimation(Walk);
    }

    public void AlertTo(Transform source)
    {
        if (IsDead || !IsPatrolRunning) return;
        if (source != null) player = ResolveTargetTransform(source);
        if (player == null) return;
        isAlert = true;
        lostSightTimer = 0f;
        waitTimer = 0f;
        lastKnownPlayerPosition = player.position;
        nextChaseRepath = 0f;
    }

    private static Transform ResolveTargetTransform(Transform target)
    {
        if (target == null) return null;
        var controller = target.GetComponentInParent<PlayerController>();
        if (controller == null) controller = target.GetComponentInChildren<PlayerController>();
        return controller != null ? controller.transform : target;
    }

    private void SetChaseDestination()
    {
        if (approachPath == null) approachPath = new NavMeshPath();
        if (SetPressureDestination()) return;
        // Search uses the last seen position; offset only a currently visible target.
        float targetDistance = Vector3.ProjectOnPlane(lastKnownPlayerPosition - transform.position, Vector3.up).magnitude;
        if (playerDetected && enemyData.spreadChaseTargets &&
            targetDistance > AttackReach + agent.stoppingDistance + .5f)
        {
            // Allow for the agent's stopping distance so the enemy can still reach attack range.
            float radius = Mathf.Min(Mathf.Max(0f, enemyData.chaseApproachRadius),
                Mathf.Max(0f, AttackReach - agent.stoppingDistance - .25f));
            bool flank = enemyData.useApproachDecisions && CurrentApproach == ApproachStyle.Flank;
            if (flank) radius = Mathf.Min(Mathf.Max(0f, enemyData.chaseApproachRadius), targetDistance * .5f);
            // Approach from this enemy's side; a fixed world-space slot could
            // send it around/behind the player and out of its own vision cone.
            Vector3 towardEnemy = Vector3.ProjectOnPlane(transform.position - lastKnownPlayerPosition, Vector3.up).normalized;
            Vector3 direction = enemyData.useApproachDecisions && CurrentApproach == ApproachStyle.Flank && committedApproachDirection.sqrMagnitude > .01f
                ? committedApproachDirection : Quaternion.AngleAxis(approachDirection.x * 35f, Vector3.up) * towardEnemy;
            Vector3 desired = lastKnownPlayerPosition + direction * radius;
            if (radius > 0f && NavMesh.SamplePosition(desired, out var hit, .5f, agent.areaMask) &&
                Mathf.Abs(hit.position.y - lastKnownPlayerPosition.y) < .75f &&
                (flank || Vector3.Distance(hit.position, lastKnownPlayerPosition) + agent.stoppingDistance < AttackReach) &&
                agent.CalculatePath(hit.position, approachPath) && approachPath.status == NavMeshPathStatus.PathComplete)
            {
                // Do not replace the active path every interval: it can repeatedly
                // restart acceleration while many agents are avoiding each other.
                if (!agent.hasPath || (agent.destination - hit.position).sqrMagnitude > .04f)
                    agent.SetDestination(hit.position);
                return;
            }
        }
        // Narrow paths or blocked approach positions retain the normal reachable chase behavior.
        // Close pursuit uses the player directly so offset slots cannot leave
        // the agent idle just outside attack range.
        agent.SetDestination(lastKnownPlayerPosition);
    }

    private void PickPatrolPoint()
    {
        validPatrolIndices.Clear();
        if (patrolPoints != null)
            for (int i = 0; i < patrolPoints.Length; i++)
                if (patrolPoints[i] != null) validPatrolIndices.Add(i);
        if (validPatrolIndices.Count == 0 || !AgentReady)
        {
            CurrentState = EnemyState.Idle;
            PlayAnimation(Idle);
            return;
        }
        if (patrolMode == PatrolMode.Random)
        {
            if (validPatrolIndices.Count > 1) validPatrolIndices.Remove(lastPatrolIndex);
            lastPatrolIndex = validPatrolIndices[Random.Range(0, validPatrolIndices.Count)];
        }
        else
        {
            int current = validPatrolIndices.IndexOf(lastPatrolIndex);
            int next = current < 0 ? 0 : current + (patrolMode == PatrolMode.Loop ? 1 : patrolDirection);
            if (patrolMode == PatrolMode.Loop) next %= validPatrolIndices.Count;
            else if (next >= validPatrolIndices.Count) { patrolDirection = -1; next = Mathf.Max(0, current - 1); }
            else if (next < 0) { patrolDirection = 1; next = Mathf.Min(validPatrolIndices.Count - 1, current + 1); }
            lastPatrolIndex = validPatrolIndices[next];
        }
        agent.SetDestination(patrolPoints[lastPatrolIndex].position);
        waitTimer = 0f;
        CurrentState = EnemyState.Patrol;
        PlayAnimation(Walk);
    }

    private void Patrol()
    {
        if (returningToPatrol || patrolPoints == null || lastPatrolIndex < 0 ||
            lastPatrolIndex >= patrolPoints.Length || patrolPoints[lastPatrolIndex] == null)
        {
            returningToPatrol = false;
            PickPatrolPoint();
            return;
        }
        if (agent.pathPending) return;
        if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            CurrentState = EnemyState.Idle;
            PlayAnimation(Idle);
            return;
        }
        if (agent.remainingDistance <= Mathf.Max(0.1f, agent.stoppingDistance) + 0.1f)
        {
            CurrentState = EnemyState.Waiting;
            PlayAnimation(Idle);
            waitTimer += Time.deltaTime;
            PatrolPoint point = patrolPoints[lastPatrolIndex].GetComponent<PatrolPoint>();
            float wait = patrolRoute != null ? patrolRoute.GetWaitTime(patrolPoints[lastPatrolIndex], enemyData.waitTimeAtPoint)
                : point != null && point.overrideWaitTime ? point.waitTime : enemyData.waitTimeAtPoint;
            if (waitTimer >= Mathf.Max(0f, wait)) PickPatrolPoint();
        }
        else { CurrentState = EnemyState.Patrol; PlayAnimation(Walk); }
    }

    private bool HasClearSight()
    {
        if (player == null || enemyData == null) return false;
        Vector3 offset = player.position + Vector3.up * playerTargetHeight - EyePosition;
        return !Physics.Raycast(EyePosition, offset.normalized, offset.magnitude,
            enemyData.obstacleMask, QueryTriggerInteraction.Ignore);
    }

    private bool CanSeePlayer()
    {
        if (player == null || enemyData == null) return false;
        Vector3 offset = player.position - transform.position;
        if (offset.magnitude > enemyData.detectionRadius) return false;
        Vector3 horizontal = Vector3.ProjectOnPlane(offset, Vector3.up);
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        return Vector3.Angle(forward, horizontal) <= enemyData.fieldOfView * 0.5f && HasClearSight();
    }

    private bool InAttackRange()
    {
        return player != null && Vector3.Distance(transform.position, player.position) <= AttackReach
            && HasClearSight();
    }

    public EnemyChargeSettings ChargeSettings => enemyData != null ? enemyData.charge : null;
    private bool InChargeBand => EnemyCombatSettings.Shared != null && EnemyCombatSettings.Shared.enableCharge &&
        ChargeSettings != null && ChargeSettings.enabled && player != null && playerDetected &&
        (CurrentWeapon == null || CurrentWeapon.loadout != EnemyWeaponSO.Loadout.Stone) &&
        Vector3.Distance(transform.position, player.position) >= Mathf.Max(AttackReach + .3f, ChargeSettings.minDistance) &&
        Vector3.Distance(transform.position, player.position) <= EffectiveChargeDistance;
    public float EffectiveChargeDistance
    {
        get
        {
            if (ChargeSettings == null) return 0;
            float speed = Mathf.Max(.1f, ChargeSettings.speed);
            float duration = Mathf.Max(.1f, ChargeSettings.duration);
            float acceleration = agent != null ? Mathf.Max(.1f, agent.acceleration) : 8f;
            float accelerating = Mathf.Min(duration, speed / acceleration);
            float travel = .5f * acceleration * accelerating * accelerating + speed * (duration - accelerating);
            return Mathf.Min(ChargeSettings.maxDistance, Mathf.Max(0, travel + AttackReach - .35f));
        }
    }
    private bool ChooseCharge()
    {
        // Keep an accepted decision while waiting for the shared start clock.
        // Arrival inside the close-role boundary must not cancel that decision.
        if (choseCharge) return true;
        if (Time.time >= nextChargeDecision)
        {
            nextChargeDecision = Time.time + Mathf.Max(.1f, ChargeSettings.decisionInterval) * Random.Range(.8f, 1.2f);
            float chance = Mathf.Clamp01(ChargeSettings.selectionChance);
            choseCharge = Random.value < chance;
        }
        return choseCharge;
    }
    public bool IsFrontOpportunity
    {
        get
        {
            if (!playerDetected || player == null || !player.TryGetComponent(out PlayerController controller) ||
                !controller.isActiveAndEnabled || controller.IsGameplayInputBlocked || controller.currentHealth <= 0 ||
                controller.PlanarVelocity.sqrMagnitude <= .04f) return false;
            Vector3 toward = Vector3.ProjectOnPlane(transform.position - player.position, Vector3.up);
            return toward.magnitude <= EnemyCombatSettings.InterceptDistance &&
                Vector3.Angle(controller.PlanarVelocity, toward) <= EnemyCombatSettings.InterceptAngle * .5f && HasClearSight();
        }
    }
    public bool CanDefendDuringEscape => IsCloseDefenseCandidate || IsFrontOpportunity;
    private bool HasChargeRunAnimation => CurrentState == EnemyState.Charge && ChargeSettings != null &&
        !string.IsNullOrEmpty(ChargeSettings.runAnimation) && animator != null && animator.HasState(0, Animator.StringToHash(ChargeSettings.runAnimation));
    private void PlayChargeAnimation(string name, int fallback)
    {
        int state = !string.IsNullOrEmpty(name) && animator != null && animator.HasState(0, Animator.StringToHash(name)) ? Animator.StringToHash(name) : fallback;
        PlayAnimation(state);
    }
    public string ChargeStatus => ChargeSettings == null || !ChargeSettings.enabled ? "Charge: profile disabled" :
        EnemyCombatSettings.Shared == null || !EnemyCombatSettings.Shared.enableCharge ? "Charge: globally disabled" :
        CurrentWeapon != null && CurrentWeapon.loadout == EnemyWeaponSO.Loadout.Stone ? "Charge: ranged loadout" :
        CurrentState == EnemyState.Charge || CurrentState == EnemyState.ChargePreparation ? "Charge: committed" :
        Time.time < nextChargeTime ? "Charge cooldown: " + (nextChargeTime - Time.time).ToString("0.0") + "s" :
        !playerDetected || player == null ? "Charge: no detected target" :
        EffectiveChargeDistance < Mathf.Max(AttackReach + .3f, ChargeSettings.minDistance) ? "Charge: INVALID range; increase duration or reduce Min Distance" :
        !InChargeBand ? "Charge: outside reachable band (max " + EffectiveChargeDistance.ToString("0.0") + "m)" :
        !HasAttackTurn ? "Charge: waiting for engagement turn" :
        !choseCharge ? "Charge: walk / next decision in " + Mathf.Max(0, nextChargeDecision - Time.time).ToString("0.0") + "s" :
        !HasClearSight() ? "Charge: sight blocked" :
        AttackStartDelay > 0 ? "Charge: chosen; start in " + AttackStartDelay.ToString("0.0") + "s" : "Charge: eligible";
    private bool CanStartCharge(float distance) => HasAttackTurn && InChargeBand && Time.time >= nextChargeTime && attackTimer <= 0 &&
        TargetAvailableForLostHit() && HasClearSight() && ChooseCharge();

    private IEnumerator PerformCharge()
    {
        var settings = ChargeSettings;
        if (settings == null) yield break;
        var target = ResolveTargetTransform(player);
        var module = CurrentWeapon;
        // Charge uses the same attack start clock and turn as melee. At most one
        // charge per target is preparing/running, even with multiple melee turns.
        foreach (var other in ActiveEnemies)
            if (other != this && other != null && other.RushTarget == RushTarget &&
                (other.CurrentState == EnemyState.ChargePreparation || other.CurrentState == EnemyState.Charge)) yield break;
        if (!EnemyAttackCoordinator.TryStartAttack(this, target)) yield break;
        nextChargeTime = Time.time + Mathf.Max(.1f, settings.cooldown);
        choseCharge = false;
        nextChargeDecision = nextChargeTime;
        isAttacking = true;
        CurrentState = EnemyState.ChargePreparation;
        agent.isStopped = true;
        PlayChargeAnimation(settings.preparationAnimation, Idle);
        float until = Time.time + Mathf.Max(.1f, settings.preparation);
        while (Time.time < until)
        {
            if (target == null || CurrentWeapon != module || !TargetAvailableForLostHit()) { FinishLostAttack(); yield break; }
            FaceTarget(target);
            yield return null;
        }
        // Snapshot a fixed destination: no homing after the telegraph.
        var path = new NavMeshPath();
        NavMeshHit hit = default;
        bool valid = target != null && Vector3.Distance(transform.position, target.position) <= EffectiveChargeDistance && HasClearSight() &&
            NavMesh.SamplePosition(target.position, out hit, .75f, agent.areaMask);
        Vector3 destination = valid ? hit.position : transform.position;
        valid = valid && agent.CalculatePath(destination, path) && path.status == NavMeshPathStatus.PathComplete && path.corners.Length <= 2;
        if (valid)
        {
            CurrentState = EnemyState.Charge;
            PlayChargeAnimation(settings.runAnimation, Walk);
            agent.isStopped = false;
            agent.updateRotation = true;
            agent.stoppingDistance = Mathf.Max(.1f, AttackReach * .65f);
            agent.speed = Mathf.Max(.1f, settings.speed);
            agent.SetPath(path);
            until = Time.time + Mathf.Max(.1f, settings.duration);
            while (Time.time < until && target != null && CurrentWeapon == module && TargetAvailableForLostHit())
            {
                if (InAttackRange())
                {
                    agent.isStopped = true;
                    chargeAttack = true;
                    yield return PerformAttack();
                    chargeAttack = false;
                    yield break;
                }
                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + .05f) break;
                yield return null;
            }
        }
        agent.isStopped = true;
        CurrentState = EnemyState.Recovery;
        PlayAnimation(Idle);
        yield return new WaitForSeconds(Mathf.Max(.05f, settings.recovery));
        FinishLostAttack();
    }

    private IEnumerator PerformAttack()
    {
        // The turn permits approaching; the shared target clock permits starting.
        if (!chargeAttack && !EnemyAttackCoordinator.TryStartAttack(this, ResolveTargetTransform(player))) yield break;
        if (enemyData.useLostCombat)
        {
            yield return PerformLostAttack();
            yield break;
        }
        isAttacking = true;
        CurrentState = EnemyState.Preparation;
        agent.isStopped = true;
        float preparation = chargeAttack ? 0f : Mathf.Max(0f, enemyData.attackPreparationTime);
        attackTimer = Mathf.Max(preparation + attackWindup, enemyData.attackCooldown);
        if (preparation > 0f)
        {
            PlayAnimation(Idle);
            yield return new WaitForSeconds(preparation);
            if (!InAttackRange())
            {
                ReleaseAttackTurn();
                isAttacking = false;
                if (AgentReady) agent.isStopped = false;
                animationState = 0;
                yield break;
            }
        }
        Vector3 facing = Vector3.ProjectOnPlane(player.position - transform.position, Vector3.up);
        if (facing.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(facing);
        CurrentState = EnemyState.Attack;
        if (chargeAttack) PlayChargeAnimation(ChargeSettings.attackAnimation, Attack);
        else PlayAnimation(Attack);
        if (!chargeAttack) yield return new WaitForSeconds(attackWindup);
        if (InAttackRange())
        {
            var target = ResolveTargetTransform(player);
            if (target != null && target.TryGetComponent(out PlayerController controller))
            {
                if (chargeAttack)
                {
                    var combat = EnemyCombatSettings.Shared;
                    var effects = EnemyPlayerEffects.For(controller);
                    if (effects.TryHit(Random.Range(enemyData.minAttack, enemyData.maxAttack), enemyData.playerHitProtection,
                        combat.chargeHoldDuration, combat.chargeHoldSpeedMultiplier))
                    { effects.BeginChargeLock(combat.chargeHoldDuration); onChargeHit?.Invoke(); }
                }
                else if (!EnemyPlayerEffects.For(controller).BlocksGroupAttack) controller.TakeDamage(Random.Range(enemyData.minAttack, enemyData.maxAttack));
            }
        }
        // Recover once; cooldown is measured from attack start, not counted twice.
        CurrentState = EnemyState.Recovery;
        while (attackTimer > 0f) yield return null;
        chargeAttack = false;
        isAttacking = false;
        if (AgentReady) agent.isStopped = false;
        animationState = 0;
        ReleaseAttackTurn();
    }

    private bool TargetAvailableForLostHit()
    {
        if (!enemyData.useLostCombat) return true;
        var target = ResolveTargetTransform(player);
        return target != null && target.TryGetComponent(out PlayerController controller) && EnemyPlayerEffects.For(controller).CanReceiveAttack;
    }
    private void FaceTarget(Transform target)
    {
        if (target == null) return;
        Vector3 facing = Vector3.ProjectOnPlane(target.position - transform.position, Vector3.up);
        if (facing.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(facing);
    }
    private IEnumerator PerformLostAttack()
    {
        var module = CurrentWeapon;
        var target = ResolveTargetTransform(player);
        float prep = module != null ? module.preparation : enemyData.attackPreparationTime;
        float windup = module != null ? module.windup : attackWindup;
        // Charge already telegraphed before the run. Contact confirms the grab;
        // replaying ordinary melee preparation here lets a reached player escape it.
        if (chargeAttack) { prep = 0; windup = 0; }
        float recovery = module != null ? module.recovery : enemyData.baseRecovery;
        isAttacking = true;
        CurrentState = EnemyState.Preparation;
        agent.isStopped = true;
        attackTimer = Mathf.Max(0, prep) + Mathf.Max(0, windup) + Mathf.Max(.05f, recovery);
        FaceTarget(target);
        bool melee = module == null || module.loadout != EnemyWeaponSO.Loadout.Stone;
        if (melee && enemyData.advanceDuringPreparation && prep > 0)
        {
            // Preparation is a closing phase, not an immediate full stop at the
            // very edge of reach. Damage direction still commits at windup.
            agent.isStopped = false;
            agent.speed = Mathf.Max(0, enemyData.closeApproachSpeed);
            float preparedAt = Time.time + prep;
            while (Time.time < preparedAt)
            {
                if (target == null || CurrentWeapon != module || !TargetAvailableForLostHit())
                { FinishLostAttack(); yield break; }
                FaceTarget(target);
                if (!agent.pathPending && (!agent.hasPath || (agent.destination - target.position).sqrMagnitude > .04f))
                    agent.SetDestination(target.position);
                PlayLocomotion(agent.velocity.sqrMagnitude > .01f);
                yield return null;
            }
            agent.isStopped = true;
        }
        else
        {
            PlayAnimation(Idle);
            if (prep > 0) yield return new WaitForSeconds(prep);
        }
        if (!InAttackRange() || !TargetAvailableForLostHit() || CurrentWeapon != module)
        { FinishLostAttack(); yield break; }
        // Last adjustment before windup. No tracking rotation during the hit window.
        FaceTarget(target);
        Vector3 facing = transform.forward;
        Vector3 committedAim = target != null ? target.position + Vector3.up * playerTargetHeight : lastKnownPlayerPosition;
        CurrentState = EnemyState.Attack;
        string stateName = module != null ? module.attackAnimation : enemyData.baseAttackAnimation;
        int state = locomotionAnimator != null ? locomotionAnimator.ResolveAttackState(stateName, module != null) : Attack;
        if (chargeAttack) PlayChargeAnimation(ChargeSettings.attackAnimation, state);
        else PlayAnimation(state);
        if (melee) yield return StepMeleeWindup(windup, facing, target);
        else if (windup > 0) yield return new WaitForSeconds(windup);
        if (CurrentWeapon == module && target != null && TargetAvailableForLostHit())
        {
            if (module != null && module.loadout == EnemyWeaponSO.Loadout.Stone)
            {
                // One stone. Snapshot aim can miss if the player moves or hides behind cover.
                Vector3 origin = transform.position + Vector3.up * eyeHeight + facing * .35f;
                activeStone = EnemyStoneProjectile.Launch(transform, origin, committedAim,
                    module, enemyData.playerHitProtection);
                weaponRuntime.Consume();
            }
            else
            {
                Vector3 offset = Vector3.ProjectOnPlane(target.position - transform.position, Vector3.up);
                float arc = module != null ? module.hitArc : enemyData.baseHitArc;
                if (InAttackRange() && Vector3.Angle(facing, offset) <= arc * .5f && target.TryGetComponent(out PlayerController controller))
                {
                    float low = module != null ? module.minDamage : enemyData.minAttack;
                    float high = module != null ? module.maxDamage : enemyData.maxAttack;
                    var effects = EnemyPlayerEffects.For(controller);
                    var combat = EnemyCombatSettings.Shared;
                    bool hold = chargeAttack && combat != null;
                    if (effects.TryHit(Random.Range(Mathf.Min(low, high), Mathf.Max(low, high)), enemyData.playerHitProtection,
                        hold ? combat.chargeHoldDuration : module == null ? enemyData.restraintDuration : 0,
                        hold ? combat.chargeHoldSpeedMultiplier : enemyData.restraintSpeedMultiplier))
                    {
                        if (chargeAttack)
                        {
                            effects.BeginChargeLock(combat != null ? combat.chargeHoldDuration : 0);
                            onChargeHit?.Invoke();
                        }
                        if (module != null && module.loadout == EnemyWeaponSO.Loadout.Torch)
                            effects.Ignite(module.burnDuration, module.burnDamagePerSecond, module.burnEffect);
                    }
                }
            }
        }
        CurrentState = EnemyState.Recovery;
        if (recovery > 0) yield return new WaitForSeconds(recovery);
        // Keep the turn while a stone is in flight, even if recovery has ended.
        while (activeStone != null && !activeStone.Finished) yield return null;
        activeStone = null;
        FinishLostAttack();
    }
    private IEnumerator StepMeleeWindup(float duration, Vector3 facing, Transform target)
    {
        float distance = target != null ? Vector3.ProjectOnPlane(target.position - transform.position, Vector3.up).magnitude : 0;
        float step = Mathf.Min(Mathf.Clamp(enemyData.meleeAttackStep, 0, 1), Mathf.Max(0, distance - agent.radius - .5f));
        if (step > .01f && duration > .01f)
        {
            Vector3 desired = transform.position + facing * step;
            if (agent.Raycast(desired, out var edge)) desired = Vector3.MoveTowards(edge.position, transform.position, .02f);
            var path = new NavMeshPath();
            if (agent.CalculatePath(desired, path) && path.status == NavMeshPathStatus.PathComplete && path.corners.Length <= 2)
            {
                agent.stoppingDistance = 0;
                agent.speed = step / duration;
                agent.SetPath(path);
                agent.isStopped = false;
            }
        }
        if (duration > 0) yield return new WaitForSeconds(duration);
        if (AgentReady) { agent.isStopped = true; agent.ResetPath(); }
    }

    private void FinishLostAttack()
    {
        chargeAttack = false;
        isAttacking = false;
        attackRoutine = null;
        animationState = 0;
        if (AgentReady) agent.isStopped = false;
        ReleaseAttackTurn();
    }
    private void InterruptLostAttack()
    {
        chargeAttack = false;
        // Disarm cancels a weapon action without automatically abandoning pursuit.
        if (activeStone != null) activeStone.Cancel();
        activeStone = null;
        if (attackRoutine != null) StopCoroutine(attackRoutine);
        attackRoutine = null;
        isAttacking = false;
        attackTimer = Mathf.Max(attackTimer, enemyData != null ? enemyData.attackTurnRest : 1f);
        animationState = 0;
        ReleaseAttackTurn();
        PlayAnimation(Idle);
        if (AgentReady) agent.isStopped = !IsPatrolRunning;
    }

    private void PlayAnimation(int state)
    {
        if (animator == null) return;
        state = locomotionAnimator != null ? locomotionAnimator.ResolveState(state, Idle, Walk, Attack) : state;
        if (animationState == state || !animator.HasState(0, state)) return;
        if (locomotionAnimator != null && locomotionAnimator.IsLocomotion(state))
            animator.CrossFadeInFixedTime(state, Mathf.Max(0, enemyData.locomotionTransition), 0);
        else animator.Play(state, 0, 0f);
        animationState = state;
    }

    private void OnDisable()
    {
        chargeAttack = false;
        easingRetreat = false;
        EnemyRushCoordinator.Release(this);
        if (activeStone != null) activeStone.Cancel();
        activeStone = null;
        ReleaseAttackTurn();
        activeEnemies.Remove(this);
        GameplayNoise.Emitted -= HearNoise;
        investigatingNoise = false;
        if (!IsDead && hitReaction != null) hitReaction.Cancel();
        StopAllCoroutines();
        isAttacking = false;
        attackRoutine = null;
        staggerRemaining = knockbackRemaining = 0f;
        animationState = 0;
        if (!IsDead && AgentReady) agent.isStopped = false;
    }

    public void BeginDeath()
    {
        if (IsDead) return;
        EnemyRushCoordinator.Release(this);
        if (activeStone != null) activeStone.Cancel();
        activeStone = null;
        if (weaponRuntime != null) weaponRuntime.Drop();
        ReleaseAttackTurn();
        if (hitReaction != null) hitReaction.Cancel();
        IsDead = true;
        CurrentState = EnemyState.Dead;
        StopAllCoroutines();
        isAttacking = false;
        isAlert = false;
        playerDetected = false;
        if (AgentReady) { agent.isStopped = true; agent.ResetPath(); }
        if (agent != null) agent.enabled = false;
        // Also supports enabling the option after Awake while testing in Play Mode.
        if (useRagdoll && GetComponentsInChildren<Joint>().Length == 0)
            EnemyRagdollBuilder.Build(animator);
        ApplyPhysicsMass();
        Rigidbody[] bodies = GetComponentsInChildren<Rigidbody>();
        bool ragdollReady = false;
        if (useRagdoll)
            foreach (Rigidbody body in bodies)
                if (body.transform != transform && body.GetComponent<Joint>() != null) ragdollReady = true;
        foreach (Collider bodyCollider in GetComponentsInChildren<Collider>())
            bodyCollider.enabled = ragdollReady && bodyCollider.attachedRigidbody != null && bodyCollider.attachedRigidbody.transform != transform;
        if (ragdollReady)
        {
            bool playAnimation = animator != null && animator.enabled && !string.IsNullOrEmpty(deathAnimation)
                && animator.HasState(0, Animator.StringToHash(deathAnimation)) && deathAnimationLeadIn > 0f;
            if (playAnimation)
            {
                animator.Play(Animator.StringToHash(deathAnimation), 0, 0f);
                StartCoroutine(ReleaseDeathRagdoll(bodies));
            }
            else ActivateDeathRagdoll(bodies, false);
        }
        else if (animator != null && !string.IsNullOrEmpty(deathAnimation))
        {
            int deathState = Animator.StringToHash(deathAnimation);
            if (animator.HasState(0, deathState)) animator.Play(deathState, 0, 0f);
        }
        bool hasLootTable = enemyData != null && enemyData.lootTable != null && enemyData.lootTable.Count > 0;
        GameObject droppedPrefab = hasLootTable ? (enemyData.lootDropChance > 0f && (enemyData.lootDropChance >= 1f || Random.value < enemyData.lootDropChance) ? enemyData.RollLoot(Random.value) : null)
            : lootPrefab != null && lootDropChance > 0f && (lootDropChance >= 1f || Random.value < lootDropChance) ? lootPrefab : null;
        if (droppedPrefab != null)
        {
            GameObject loot = Instantiate(droppedPrefab, transform.position + lootOffset, Quaternion.identity);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(loot, gameObject.scene);
        }
        // Zero keeps the corpse indefinitely.
        if (corpseLifetime > 0f) Destroy(gameObject, corpseLifetime);
        Died?.Invoke(this);
    }

    private IEnumerator ReleaseDeathRagdoll(Rigidbody[] bodies)
    {
        yield return new WaitForSeconds(deathAnimationLeadIn);
        // Wait until this frame's animation pose is evaluated before handing it to physics.
        yield return new WaitForEndOfFrame();
        ActivateDeathRagdoll(bodies, true);
    }

    private void ActivateDeathRagdoll(Rigidbody[] bodies, bool hybrid)
    {
        if (hybrid && animator != null && deathBlendDuration > 0f && deathAnimationDrive > 0f)
            deathBlend = new EnemyDeathBlend(animator, bodies, deathBlendDuration, deathAnimationDrive);
        if (animator != null) animator.enabled = false;
        deathBodies = bodies;
        Physics.SyncTransforms();
        foreach (Rigidbody body in bodies)
        {
            if (body == null || body.transform == transform) continue;
            body.isKinematic = false;
            body.useGravity = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.linearDamping = ragdollLinearDamping;
            body.angularDamping = ragdollAngularDamping;
            // Animated poses may overlap the floor slightly. Limit the correction impulse.
            body.maxDepenetrationVelocity = 1f;
            body.solverIterations = 12;
            body.solverVelocityIterations = 8;
            body.sleepThreshold = 0.02f;
        }
        if (pendingDeathBody != null && !pendingDeathBody.isKinematic)
            pendingDeathBody.AddForceAtPosition(pendingDeathImpulse * (hybrid ? hybridDeathPushScale : 1f),
                pendingDeathBody.transform.TransformPoint(pendingDeathPoint), ForceMode.Impulse);
        pendingDeathBody = null;
    }

    private void FixedUpdate()
    {
        if (!IsDead || deathBodies == null) return;
        if (deathBlend != null && !deathBlend.Step(Time.fixedDeltaTime))
        {
            deathBlend.Dispose();
            deathBlend = null;
        }
        if (corpseSettled)
        {
            bool awake = false;
            foreach (Rigidbody body in deathBodies)
                if (body != null && body.transform != transform && !body.isKinematic && !body.IsSleeping()) awake = true;
            if (!awake) return;
            corpseSettled = false;
            corpseStillTime = 0f;
        }
        bool still = deathBlend == null;
        foreach (Rigidbody body in deathBodies)
            if (body != null && body.transform != transform && !body.isKinematic)
            {
                if (body.linearVelocity.sqrMagnitude > 0.0225f || body.angularVelocity.sqrMagnitude > 0.36f) still = false;
                body.AddForce(Physics.gravity * ragdollGravityScale, ForceMode.Acceleration);
            }
        corpseStillTime = still ? corpseStillTime + Time.fixedDeltaTime : 0f;
        if (still && corpseStillTime >= Mathf.Max(0.1f, corpseSettleDelay) && CorpseTouchesGround())
        {
            foreach (Rigidbody body in deathBodies)
                if (body != null && body.transform != transform && !body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                    body.Sleep();
                }
            corpseSettled = true;
        }
    }

    private bool CorpseTouchesGround()
    {
        foreach (Collider collider in GetComponentsInChildren<Collider>())
        {
            if (!collider.enabled || collider.isTrigger || collider.attachedRigidbody == null) continue;
            Bounds bounds = collider.bounds;
            RaycastHit[] hits = Physics.RaycastAll(bounds.center, Vector3.down, bounds.extents.y + 0.08f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
                if (!hit.collider.transform.IsChildOf(transform)) return true;
        }
        return false;
    }

    private void OnDestroy()
    {
        if (deathBlend != null) deathBlend.Dispose();
    }

    private void OnValidate() => massRefreshRequested = true;

    public void ApplyPhysicsMass()
    {
        massRefreshRequested = false;
        if (animator == null) return;
        Dictionary<Transform, float> overrides = new Dictionary<Transform, float>();
        if (animator.isHuman && boneMassOverrides != null)
            foreach (BoneMassOverride entry in boneMassOverrides)
            {
                if (entry == null || entry.bone == HumanBodyBones.LastBone) continue;
                Transform bone = animator.GetBoneTransform(entry.bone);
                if (bone != null) overrides[bone] = Mathf.Max(0.01f, entry.mass);
            }
        foreach (Rigidbody body in animator.GetComponentsInChildren<Rigidbody>())
        {
            if (body.transform == transform) continue;
            if (!originalBodyMasses.TryGetValue(body, out float originalMass))
            {
                originalMass = body.mass;
                originalBodyMasses[body] = originalMass;
            }
            float baseMass = overrides.TryGetValue(body.transform, out float customMass) ? customMass : originalMass;
            body.mass = Mathf.Max(0.01f, baseMass * Mathf.Max(0.01f, physicsMassMultiplier));
        }
        if (hitReaction != null) hitReaction.SyncMasses();
    }

    public void ReceiveBulletImpact(Collider collider, Vector3 point, Vector3 direction, int shotId = 0, float impactPower = 0)
    {
        if (collider == null || direction.sqrMagnitude < 0.001f) return;
        if (!IsDead && weaponRuntime != null && weaponRuntime.TryDisarm(collider))
            InterruptLostAttack();
        var hitBox = collider.GetComponent<HitBox>();
        if (!IsDead && enemyData != null && IsPatrolRunning &&
            staggerMeter.Hit(enemyData.actionBreak, Time.time, hitBox != null && hitBox.hitBox == HitBoxPart.Head,
                shotId, impactPower, Mathf.Max(enemyData.hitStaggerDuration, enemyData.hitKnockbackDistance > 0 ? enemyData.hitKnockbackDuration : 0)))
        {
            float stagger = Mathf.Max(0f, enemyData.hitStaggerDuration);
            float push = Mathf.Max(0f, enemyData.hitKnockbackDistance);
            if (stagger > 0f || push > 0f)
            {
                EnemyRushCoordinator.Release(this);
                if (activeStone != null) activeStone.Cancel();
                activeStone = null;
                ReleaseAttackTurn();
                if (attackRoutine != null) { StopCoroutine(attackRoutine); attackRoutine = null; }
                chargeAttack = false;
                isAttacking = false;
                staggerRemaining = Mathf.Max(staggerRemaining, stagger);
                knockbackRemaining = push > 0f ? Mathf.Max(.02f, enemyData.hitKnockbackDuration) : 0f;
                knockbackVelocity = knockbackRemaining > 0f ? Vector3.ProjectOnPlane(direction, Vector3.up).normalized * push / knockbackRemaining : Vector3.zero;
                CurrentState = EnemyState.HitReaction;
                if (AgentReady) agent.isStopped = true;
                PlayAnimation(Idle);
            }
        }
        Rigidbody body = collider.attachedRigidbody;
        if (body == null || body.transform == transform) return;
        if (!IsDead && body.GetComponent<Joint>() == null)
        {
            // Hips anchor the living rig. React through the nearest jointed child instead.
            Rigidbody nearest = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (Rigidbody candidate in body.GetComponentsInChildren<Rigidbody>())
            {
                if (candidate.GetComponent<Joint>() == null) continue;
                float distance = (candidate.position - point).sqrMagnitude;
                if (distance < nearestDistance) { nearest = candidate; nearestDistance = distance; }
            }
            if (nearest != null) body = nearest;
        }
        if (IsDead)
        {
            if (!body.isKinematic) body.AddForceAtPosition(direction.normalized * deathPushImpulse, point, ForceMode.Impulse);
            else if (useRagdoll)
            {
                pendingDeathBody = body;
                pendingDeathPoint = body.transform.InverseTransformPoint(point);
                pendingDeathImpulse = direction.normalized * deathPushImpulse;
            }
        }
        else if (enableHitReaction && hitPushImpulse > 0f && body.GetComponent<Joint>() != null)
        {
            hitReaction.Play(animator, body, point, direction.normalized * hitPushImpulse,
                hitPhysicsDuration, hitRecoveryDuration, hitReactionBlend, hitAnimationSupport, hitMaxAngle);
        }
    }

    public Color StateColor
    {
        get
        {
            switch (CurrentState)
            {
                case EnemyState.Patrol: return new Color(0.15f, 1f, 0.25f);
                case EnemyState.Waiting: return Color.yellow;
                case EnemyState.Chase: return new Color(1f, 0.4f, 0f);
                case EnemyState.Rush:
                case EnemyState.ChaseBurst: return new Color(1f, .15f, .05f);
                case EnemyState.Pressure: return new Color(1f, .75f, .2f);
                case EnemyState.ChargePreparation: return new Color(1f, .5f, .1f);
                case EnemyState.Charge: return Color.red;
                case EnemyState.EscapeWalk: return new Color(.2f, .8f, 1f);
                case EnemyState.Approach: return new Color(1f, .5f, .1f);
                case EnemyState.Retreat: return new Color(.4f, .65f, .9f);
                case EnemyState.Search: return new Color(0.75f, 0.3f, 1f);
                case EnemyState.Attack: return Color.red;
                case EnemyState.Dead: return new Color(0.6f, 0.6f, 0.6f);
                case EnemyState.HitReaction: return new Color(0.1f, 0.6f, 1f);
                case EnemyState.Detect: return new Color(1f, .85f, .25f);
                case EnemyState.Preparation: return new Color(1f, .55f, .1f);
                case EnemyState.Recovery: return new Color(.35f, .8f, .6f);
                default: return Color.white;
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        Gizmos.color = Application.isPlaying ? StateColor : Color.white;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 2.2f, 0.3f);
#if UNITY_EDITOR
        UnityEditor.Handles.color = Gizmos.color;
        string stateLabel = CurrentState.ToString();
        if (Application.isPlaying && !IsDead)
            stateLabel = !IsPatrolRunning ? "Waiting for StartPatrol" : enemyData == null ? "Missing Profile" : !AgentReady ? "Agent not on NavMesh" : stateLabel;
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2.6f,
            name + " | " + (Application.isPlaying ? stateLabel : patrolMode.ToString()), LabelStyle(Gizmos.color));
#endif
        List<Transform> points = new List<Transform>();
        if (patrolPoints != null)
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                Transform point = patrolPoints[i];
                if (point == null) continue;
                points.Add(point);
                Vector3 marker = point.position + Vector3.up * patrolMarkerSize;
                bool active = Application.isPlaying && !IsDead && !isAlert && i == lastPatrolIndex;
                Gizmos.color = active ? Color.yellow : patrolPointColor;
                Gizmos.DrawSphere(marker, patrolMarkerSize);
                Gizmos.color = Color.black;
                Gizmos.DrawWireSphere(marker, patrolMarkerSize * 1.05f);
                Gizmos.color = active ? Color.yellow : patrolPointColor;
                Gizmos.DrawLine(point.position, marker + Vector3.up * 0.8f);
#if UNITY_EDITOR
                PatrolPoint settings = point.GetComponent<PatrolPoint>();
                string wait = settings != null && settings.overrideWaitTime ? " | " + settings.waitTime.ToString("0.#") + "s" : "";
                UnityEditor.Handles.Label(marker + Vector3.up * (patrolMarkerSize + 0.2f),
                    "P" + (i + 1) + wait + (active ? " TARGET" : ""), LabelStyle(active ? Color.yellow : patrolPointColor));
#endif
            }
        if (patrolMode == PatrolMode.Random)
        {
            Gizmos.color = patrolRouteColor;
            for (int i = 0; i < points.Count; i++)
                for (int j = i + 1; j < points.Count; j++) DrawRoute(points[i].position, points[j].position, false);
        }
        else
        {
            Gizmos.color = patrolRouteColor;
            for (int i = 1; i < points.Count; i++)
            {
                DrawRoute(points[i - 1].position, points[i].position, true);
                if (patrolMode == PatrolMode.PingPong) DrawRoute(points[i].position, points[i - 1].position, true);
            }
            if (patrolMode == PatrolMode.Loop && points.Count > 1) DrawRoute(points[points.Count - 1].position, points[0].position, true);
        }
        if (Application.isPlaying && AgentReady && agent.hasPath)
        {
            Gizmos.color = StateColor;
            Vector3[] corners = agent.path.corners;
            for (int i = 1; i < corners.Length; i++) DrawRoute(corners[i - 1], corners[i], true);
        }
        if (Application.isPlaying && investigatingNoise && !IsDead)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(noisePosition + Vector3.up * .3f, .45f);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(noisePosition + Vector3.up, "Heard noise", LabelStyle(Gizmos.color));
#endif
        }
        if (Application.isPlaying && isAlert && !IsDead)
        {
            Gizmos.color = new Color(0.75f, 0.3f, 1f);
            Gizmos.DrawWireSphere(lastKnownPlayerPosition + Vector3.up * 0.3f, 0.45f);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(lastKnownPlayerPosition + Vector3.up, "Last seen", LabelStyle(Gizmos.color));
#endif
        }
        if (enemyData == null) return;
        Gizmos.color = new Color(1f, 0.85f, 0.1f);
        Gizmos.DrawWireSphere(transform.position, enemyData.detectionRadius);
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Gizmos.color = Application.isPlaying ? StateColor : Color.cyan;
        Vector3 previous = EyePosition;
        const int segments = 32;
        for (int i = 0; i <= segments; i++)
        {
            float angle = -enemyData.fieldOfView * 0.5f + enemyData.fieldOfView * i / segments;
            Vector3 point = EyePosition + Quaternion.Euler(0f, angle, 0f) * forward * enemyData.detectionRadius;
            if (i == 0 || i == segments) Gizmos.DrawLine(EyePosition, point);
            if (i > 0) Gizmos.DrawLine(previous, point);
            previous = point;
        }
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, AttackReach);
        if (Application.isPlaying && player != null && !IsDead)
        {
            Gizmos.color = playerDetected ? Color.green : Color.red;
            Gizmos.DrawLine(EyePosition, player.position + Vector3.up * playerTargetHeight);
        }
    }

    private void DrawRoute(Vector3 from, Vector3 to, bool arrow)
    {
        from += Vector3.up * 0.15f;
        to += Vector3.up * 0.15f;
#if UNITY_EDITOR
        UnityEditor.Handles.color = Gizmos.color;
        UnityEditor.Handles.DrawAAPolyLine(4f, from, to);
#else
        Gizmos.DrawLine(from, to);
#endif
        if (!arrow || (to - from).sqrMagnitude < 0.01f) return;
        Vector3 direction = (to - from).normalized;
        Vector3 tip = Vector3.Lerp(from, to, 0.65f);
        Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
        Gizmos.DrawLine(tip, tip - direction * 0.5f + side * 0.25f);
        Gizmos.DrawLine(tip, tip - direction * 0.5f - side * 0.25f);
    }

#if UNITY_EDITOR
    private static GUIStyle LabelStyle(Color color)
    {
        GUIStyle style = new GUIStyle(UnityEditor.EditorStyles.helpBox);
        style.fontSize = 13;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = color;
        return style;
    }
#endif
}

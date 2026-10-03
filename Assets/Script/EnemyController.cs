using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour
{
    public enum PatrolMode { Random, Loop, PingPong }
    public enum EnemyState { Idle, Patrol, Waiting, Chase, Search, Attack, Dead, HitReaction }
    public EnemyState CurrentState { get; private set; }
    public bool IsDead { get; private set; }
    [Header("References")]
    public EnemyDataSO enemyData;
    [SerializeField] private Transform player;
    private NavMeshAgent agent;
    private Animator animator;

    [Header("Patrol Settings")]
    [SerializeField, HideInInspector] private Transform[] patrolPoints;
    [SerializeField] private PatrolRoute patrolRoute;
    public PatrolRoute Route => patrolRoute;
    [SerializeField, HideInInspector] private PatrolMode patrolMode;
    private int patrolDirection = 1;
    private int lastPatrolIndex = -1;
    private float waitTimer;
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
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        ApplySharedProfile();
        if (patrolRoute != null) { patrolPoints = patrolRoute.points != null ? (Transform[])patrolRoute.points.Clone() : new Transform[0]; patrolMode = patrolRoute.mode; }
        // NavMeshAgent owns movement; animation must not also move the root.
        if (animator != null) animator.applyRootMotion = false;
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
        PickPatrolPoint();
    }

    private void Update()
    {
        if (massRefreshRequested) ApplyPhysicsMass();
        if (IsDead) return;
        attackTimer = Mathf.Max(0f, attackTimer - Time.deltaTime);
        if (enemyData == null || !AgentReady) return;
        playerDetected = CanSeePlayer();
        if (playerDetected)
        {
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

        if (isAttacking) return;
        agent.isStopped = false;
        if (isAlert)
        {
            CurrentState = playerDetected ? EnemyState.Chase : EnemyState.Search;
            agent.SetDestination(lastKnownPlayerPosition);
            PlayAnimation(agent.pathPending || agent.remainingDistance > agent.stoppingDistance + 0.1f ? Walk : Idle);
            if (playerDetected && attackTimer <= 0f && InAttackRange())
                StartCoroutine(PerformAttack());
        }
        else
        {
            Patrol();
        }
    }

    public void AlertTo(Transform source)
    {
        if (IsDead) return;
        if (source != null) player = source;
        if (player == null) return;
        isAlert = true;
        lostSightTimer = 0f;
        waitTimer = 0f;
        lastKnownPlayerPosition = player.position;
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
        return player != null && Vector3.Distance(transform.position, player.position) <= enemyData.attackRange
            && HasClearSight();
    }

    private IEnumerator PerformAttack()
    {
        isAttacking = true;
        CurrentState = EnemyState.Attack;
        agent.isStopped = true;
        attackTimer = Mathf.Max(attackWindup, enemyData.attackCooldown);
        Vector3 facing = Vector3.ProjectOnPlane(player.position - transform.position, Vector3.up);
        if (facing.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(facing);
        PlayAnimation(Attack);
        yield return new WaitForSeconds(attackWindup);
        if (InAttackRange() && player.TryGetComponent(out PlayerController controller))
            controller.TakeDamage(Random.Range(enemyData.minAttack, enemyData.maxAttack));
        // Recover once; cooldown is measured from attack start, not counted twice.
        while (attackTimer > 0f) yield return null;
        isAttacking = false;
        if (AgentReady) agent.isStopped = false;
        animationState = 0;
    }

    private void PlayAnimation(int state)
    {
        if (animator == null || animationState == state) return;
        animator.Play(state, 0, 0f);
        animationState = state;
    }

    private void OnDisable()
    {
        if (!IsDead && hitReaction != null) hitReaction.Cancel();
        StopAllCoroutines();
        isAttacking = false;
        animationState = 0;
        if (!IsDead && AgentReady) agent.isStopped = false;
    }

    public void BeginDeath()
    {
        if (IsDead) return;
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

    public void ReceiveBulletImpact(Collider collider, Vector3 point, Vector3 direction)
    {
        if (collider == null || direction.sqrMagnitude < 0.001f) return;
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
            if (!IsDead && hitReaction != null && hitReaction.IsReacting) return new Color(0.1f, 0.6f, 1f);
            switch (CurrentState)
            {
                case EnemyState.Patrol: return new Color(0.15f, 1f, 0.25f);
                case EnemyState.Waiting: return Color.yellow;
                case EnemyState.Chase: return new Color(1f, 0.4f, 0f);
                case EnemyState.Search: return new Color(0.75f, 0.3f, 1f);
                case EnemyState.Attack: return Color.red;
                case EnemyState.Dead: return new Color(0.6f, 0.6f, 0.6f);
                case EnemyState.HitReaction: return new Color(0.1f, 0.6f, 1f);
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
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2.6f,
            name + " | " + (Application.isPlaying ? CurrentState.ToString() : patrolMode.ToString()), LabelStyle(Gizmos.color));
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
        Gizmos.DrawWireSphere(transform.position, enemyData.attackRange);
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

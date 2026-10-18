using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

[InitializeOnLoad]
public static class LostModuleChecks
{
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    private static int checks;
    static LostModuleChecks() { if (SessionState.GetBool("LostModuleChecks", false)) EditorApplication.update += AwaitPlay; }
    public static void Begin() { SessionState.SetBool("LostModuleChecks", true); EditorApplication.EnterPlaymode(); }
    private static void AwaitPlay()
    {
        if (!EditorApplication.isPlaying) return;
        EditorApplication.update -= AwaitPlay;
        SessionState.SetBool("LostModuleChecks", false);
        steps.Push(Run());
        EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        try
        {
            if (!steps.Peek().MoveNext()) steps.Pop();
            else if (steps.Peek().Current is IEnumerator child) steps.Push(child);
            if (steps.Count == 0)
            {
                System.IO.File.WriteAllText("lost-module-success.txt", checks + " checks passed in real Play Mode with production AI, module, projectile and effects; PlayerController damage stub.");
                EditorApplication.update -= Tick;
                EditorApplication.Exit(0);
            }
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    private static IEnumerator Wait(float seconds)
    { float end = Time.time + seconds; while (Time.time < end) yield return null; }
    private static void Check(bool value, string label)
    { if (!value) throw new Exception(label); checks++; Debug.Log("PASS " + label); }
    private static void Call(EnemyController enemy, string method) => typeof(EnemyController).GetMethod(method, Hidden).Invoke(enemy, null);
    private static void ResetAttack(EnemyController enemy)
    {
        enemy.StartPatrol();
        typeof(EnemyController).GetField("attackTimer", Hidden).SetValue(enemy, 0f);
        typeof(EnemyController).GetField("nextTurnRequest", Hidden).SetValue(enemy, 0f);
        enemy.AlertTo(GameObject.FindWithTag("Player").transform);
        Call(enemy, "Update");
    }
    private static int PressureOwners(EnemyController[] enemies) => Array.FindAll(enemies, candidate => candidate.HasRushSlot).Length;
    private static void PlacePressureScenario(EnemyController[] enemies, Vector3[] positions)
    {
        foreach (var candidate in enemies) candidate.PausePatrol();
        typeof(EnemyRushCoordinator).GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        for (int index = 0; index < enemies.Length; index++)
        {
            var candidate = enemies[index];
            candidate.GetComponent<NavMeshAgent>().Warp(positions[index]);
            candidate.StartPatrol(); candidate.SetAggressive(true);
            // Publish all candidates before the next frame's shared scan; attack scheduling
            // is tested separately so these movement scenarios can keep stable ownership.
            typeof(EnemyController).GetField("playerDetected", Hidden).SetValue(candidate, true);
            typeof(EnemyController).GetField("attackTimer", Hidden).SetValue(candidate, 100f);
            typeof(EnemyController).GetField("nextTurnRequest", Hidden).SetValue(candidate, Time.time + 100f);
        }
    }
    private static IEnumerator PressureChecks(EnemyCombatSettings settings, PlayerController player, EnemyDataSO originalProfile, EnemyController[] enemies)
    {
        var profile = UnityEngine.Object.Instantiate(originalProfile);
        profile.separateNearbyEnemies = false;
        profile.pressureOrbitSpeed = 0; profile.pressureWaitTimeout = 0;
        profile.chaseSlowdownDistance = 3.5f;
        profile.maxConcurrentAttackers = 1;
        profile.hitStaggerDuration = .35f; profile.hitKnockbackDistance = 0;
        foreach (var candidate in enemies) { candidate.PausePatrol(); candidate.enemyData = profile; }
        settings.useRushSlots = true; settings.maxRushers = 3;
        settings.approachRunChance = 1;
        settings.rushStartDelay = Vector2.one * .15f;
        settings.pressureDistance = 4.5f; settings.replacementInterval = .2f;
        settings.rushReentryDelay = .8f; settings.rushStallTimeout = .5f;
        settings.pauseRushWhileEscaping = true; settings.escapeResumeDelay = .3f;
        player.transform.position = Vector3.zero;
        player.currentHealth = 1000;
        player.IsSprinting = false; player.PlanarVelocity = Vector3.zero;
        var distant = new[] { new Vector3(-3, 0, -9), new Vector3(-1, 0, -9), new Vector3(1, 0, -9), new Vector3(3, 0, -9) };
        // Deliberately register the farthest candidate first: nearest admission must not
        // depend on HashSet iteration, Update order, prefab or spawning order.
        var close = new[] { Vector3.left * 3.8f, Vector3.left * 1.8f, Vector3.back * 2.8f, Vector3.back * 1.2f };
        PlacePressureScenario(enemies, distant);
        yield return Wait(.08f);
        Check(PressureOwners(enemies) == 0 && Array.TrueForAll(enemies, candidate => !candidate.IsRushRunning),
            "Initial approach waits for the first staggered random runner instead of rushing together");
        yield return Wait(.1f);
        Check(Array.FindAll(enemies, candidate => candidate.IsRushRunning).Length == 1,
            "Only one nominated runner starts after the first shared delay");
        yield return Wait(.33f);
        Check(PressureOwners(enemies) == 0 && Array.FindAll(enemies, candidate => candidate.IsRushRunning).Length == 3 &&
            Array.TrueForAll(enemies, candidate => !candidate.IsChaseBoosting),
            "Random incoming runners respect the shared cap without reserving pressure ownership");
        settings.approachRunChance = 0;
        PlacePressureScenario(enemies, distant);
        yield return Wait(.35f);
        Check(Array.TrueForAll(enemies, candidate => !candidate.IsRushRunning && candidate.GetComponent<NavMeshAgent>().speed <= profile.walkSpeed + .01f),
            "Zero run chance keeps all distant enemies walking across multiple start intervals");
        settings.approachRunChance = 1;
        settings.maxRushers = 1;
        PlacePressureScenario(enemies, distant);
        yield return Wait(.2f);
        var nominated = Array.Find(enemies, candidate => candidate.IsRushRunning);
        var arrivingWalker = Array.Find(enemies, candidate => candidate != nominated);
        Check(nominated != null && !nominated.HasRushSlot, "Running nomination does not preassign pressure ownership");
        arrivingWalker.GetComponent<NavMeshAgent>().Warp(Vector3.right * 4.2f);
        yield return Wait(.08f);
        Check(arrivingWalker.HasRushSlot && !nominated.HasRushSlot && !nominated.IsRushRunning,
            "An arriving walker takes the available place before an incoming runner, who immediately slows");
        settings.maxRushers = 3;

        PlacePressureScenario(enemies, close);
        yield return Wait(.08f);
        Check(!enemies[0].HasRushSlot && enemies[1].HasRushSlot && enemies[2].HasRushSlot && enemies[3].HasRushSlot,
            "The three nearest reachable enemies claim initial pressure ownership regardless of registration order");
        Check(!enemies[0].IsRushRunning && !enemies[0].HasAttackTurn && enemies[0].GetComponent<NavMeshAgent>().speed <= profile.walkSpeed + .01f,
            "A full pressure group immediately slows its waiting enemy and prevents another attack admission");
        yield return Wait(.65f);
        Check(PressureOwners(enemies) == 3 && enemies[1].HasRushSlot && enemies[2].HasRushSlot && enemies[3].HasRushSlot,
            "Stationary close pressure retains ownership beyond the distant-pursuit stall timeout");

        var attackModule = Weapon(EnemyWeaponSO.Loadout.Machete);
        attackModule.otherDropChance = attackModule.armDropChance = 0;
        profile.actionBreak.criticalChance = 0;
        attackModule.preparation = .12f; attackModule.windup = .12f; attackModule.recovery = 1f;
        enemies[3].GetComponent<EnemyWeaponRuntime>().Equip(attackModule);
        typeof(EnemyController).GetField("attackTimer", Hidden).SetValue(enemies[3], 0f);
        typeof(EnemyController).GetField("nextTurnRequest", Hidden).SetValue(enemies[3], 0f);
        yield return Wait(.05f);
        Check(enemies[3].CurrentState == EnemyController.EnemyState.Preparation && enemies[3].HasRushSlot && PressureOwners(enemies) == 3,
            "Entering real attack preparation retains pressure ownership instead of admitting another runner");
        enemies[3].ReceiveBulletImpact(enemies[3].GetComponent<BoxCollider>(), enemies[3].transform.position, Vector3.right, 90001, 20);
        Check(enemies[3].isAttacking && enemies[3].CurrentState == EnemyController.EnemyState.Preparation && enemies[3].HasRushSlot && enemies[3].HasAttackTurn,
            "An ordinary bullet accumulates impact without cancelling a real attack or releasing combat/movement rights");
        yield return Wait(.3f);
        Check(enemies[3].CurrentState == EnemyController.EnemyState.Recovery && enemies[3].HasRushSlot,
            "The real attack coroutine retains its pressure ownership through damage and recovery");
        yield return Wait(.55f);
        Check(enemies[3].HasRushSlot && !enemies[0].HasRushSlot && !enemies[0].IsRushRunning,
            "A recovery longer than the stall timeout does not refill the pressure group");

        enemies[1].PausePatrol();
        Check(PressureOwners(enemies) == 2 && !enemies[0].IsRushRunning,
            "Pausing one owner releases exactly one place without restarting the mass approach");
        yield return Wait(.1f);
        Check(!enemies[0].HasRushSlot, "Replacement respects the configured delay after an owner pauses");
        yield return Wait(.15f);
        Check(enemies[0].HasRushSlot && PressureOwners(enemies) == 3 && !enemies[1].HasRushSlot,
            "One eligible waiting enemy replaces the paused owner after the delay");

        PlacePressureScenario(enemies, close);
        yield return Wait(.08f);
        typeof(EnemyController).GetField("enableHitReaction", Hidden).SetValue(enemies[2], true);
        var impactCollider = enemies[2].GetComponent<BoxCollider>();
        float savedBreakChance = profile.actionBreak.criticalChance; profile.actionBreak.criticalChance = 1;
        enemies[2].ReceiveBulletImpact(impactCollider, enemies[2].transform.position, Vector3.right);
        Check(!enemies[2].HasRushSlot && PressureOwners(enemies) == 2 && !enemies[0].IsRushRunning,
            "A production bullet stagger releases only its victim's pressure ownership");
        yield return Wait(.25f);
        Check(enemies[0].HasRushSlot && !enemies[2].HasRushSlot && PressureOwners(enemies) == 3,
            "Bullet interruption fills one vacancy while the hit owner rests on reentry cooldown");
        yield return Wait(.14f);
        var recoveringAgent = enemies[2].GetComponent<NavMeshAgent>();
        float heldRadius = (float)typeof(EnemyController).GetProperty("PressureRadius", Hidden).GetValue(enemies[2]);
        Check(enemies[2].CurrentState == EnemyController.EnemyState.Pressure && !enemies[2].HasRushSlot,
            "Shot victim resumes Pressure while its replacement keeps ownership");
        Check(heldRadius <= 2.85f && heldRadius < EnemyCombatSettings.PressureDistance,
            "Post-hit Pressure holds the inner recovery radius instead of expanding to the waiting ring");
        Check(recoveringAgent.hasPath && Vector3.ProjectOnPlane(recoveringAgent.destination - player.transform.position, Vector3.up).magnitude <= 3.15f,
            "Post-hit navigation does not command a retreat to the outer pressure ring");
        profile.actionBreak.criticalChance = savedBreakChance;
        typeof(EnemyController).GetField("enableHitReaction", Hidden).SetValue(enemies[2], false);

        PlacePressureScenario(enemies, close);
        yield return Wait(.08f);
        player.IsSprinting = true; player.PlanarVelocity = Vector3.forward * 4;
        yield return Wait(.05f);
        Check(!EnemyRushCoordinator.IsEscapePaused(player.transform) && PressureOwners(enemies) == 3,
            "Sprinting no longer pauses the encounter or clears its pressure owners");
        Check(Array.TrueForAll(enemies, candidate => candidate.CurrentState != EnemyController.EnemyState.EscapeWalk),
            "No enemy enters the retired EscapeWalk state");
        PlacePressureScenario(enemies, distant);
        yield return Wait(.22f);
        Check(Array.Exists(enemies, candidate => candidate.IsRushRunning),
            "Staggered runners continue pursuing while the player sprints away");
        player.IsSprinting = false; player.PlanarVelocity = Vector3.zero;
        yield return Wait(.05f);
        Check(Array.Exists(enemies, candidate => candidate.IsRushRunning),
            "Stopping sprint does not reset existing runners or their movement choices");

        PlacePressureScenario(enemies, close);
        yield return Wait(.08f);
        var stalledAgent = enemies[3].GetComponent<NavMeshAgent>();
        stalledAgent.Warp(Vector3.forward * 10);
        stalledAgent.updatePosition = false;
        yield return Wait(.6f);
        Check(!enemies[3].HasRushSlot && !enemies[3].IsRushRunning,
            "A genuinely stalled distant pursuer releases ownership instead of holding it forever");
        stalledAgent.updatePosition = true;
        stalledAgent.Warp(Vector3.forward * 10);

        // Multiple waiters: select by actual proximity, then stagger a second vacancy.
        PlacePressureScenario(enemies, close);
        yield return Wait(.08f);
        var replacementObject = UnityEngine.Object.Instantiate(enemies[0].gameObject);
        var nearestReplacement = replacementObject.GetComponent<EnemyController>();
        nearestReplacement.PausePatrol();
        nearestReplacement.GetComponent<NavMeshAgent>().Warp(Vector3.right * 5.5f);
        nearestReplacement.StartPatrol(); nearestReplacement.SetAggressive(true);
        typeof(EnemyController).GetField("attackTimer", Hidden).SetValue(nearestReplacement, 100f);
        typeof(EnemyController).GetField("nextTurnRequest", Hidden).SetValue(nearestReplacement, Time.time + 100f);
        enemies[0].GetComponent<NavMeshAgent>().Warp(Vector3.back * 9);
        yield return Wait(.05f);
        enemies[1].PausePatrol(); enemies[2].PausePatrol();
        yield return Wait(.24f);
        Check(!nearestReplacement.HasRushSlot && !enemies[0].HasRushSlot && PressureOwners(enemies) == 1,
            "No distant replacement is granted pressure ownership before it actually arrives");
        nearestReplacement.GetComponent<NavMeshAgent>().Warp(Vector3.right * 4.2f);
        yield return Wait(.05f);
        Check(nearestReplacement.HasRushSlot && !enemies[0].HasRushSlot,
            "The first replacement arriving within the radius claims the first vacancy");
        enemies[0].GetComponent<NavMeshAgent>().Warp(Vector3.back);
        yield return Wait(.08f);
        Check(!enemies[0].HasRushSlot, "A later closer arrival still respects the interval after the first arrival");
        yield return Wait(.15f);
        Check(enemies[0].HasRushSlot && nearestReplacement.HasRushSlot && enemies[3].HasRushSlot,
            "Two simultaneous vacancies refill one at a time rather than admitting both on the first interval");

        // The same static coordinator must not let another player's group consume this budget.
        var secondPlayerObject = new GameObject("Independent pressure player");
        secondPlayerObject.transform.position = Vector3.right * 10;
        secondPlayerObject.AddComponent<PlayerController>();
        var independentObject = UnityEngine.Object.Instantiate(enemies[0].gameObject);
        var independentEnemy = independentObject.GetComponent<EnemyController>();
        independentEnemy.PausePatrol();
        independentEnemy.GetComponent<NavMeshAgent>().Warp(Vector3.right * 10 + Vector3.back * 2);
        independentEnemy.StartPatrol(); independentEnemy.SetAggressive(true); independentEnemy.AlertTo(secondPlayerObject.transform);
        typeof(EnemyController).GetField("attackTimer", Hidden).SetValue(independentEnemy, 100f);
        yield return Wait(.06f);
        Check(independentEnemy.HasRushSlot && enemies[0].HasRushSlot && nearestReplacement.HasRushSlot && enemies[3].HasRushSlot,
            "Pressure ownership is independent per actual player even when another target's group is full");
        independentEnemy.PausePatrol(); nearestReplacement.PausePatrol();
        UnityEngine.Object.Destroy(independentObject); UnityEngine.Object.Destroy(replacementObject);
        UnityEngine.Object.Destroy(secondPlayerObject);

        PlacePressureScenario(enemies, distant);
        settings.useRushSlots = false;
        yield return Wait(.08f);
        Check(Array.TrueForAll(enemies, candidate => !candidate.HasRushSlot && !candidate.IsRushRunning &&
            (candidate.CurrentState == EnemyController.EnemyState.Chase || candidate.CurrentState == EnemyController.EnemyState.ChaseBurst)),
            "Disabling shared pressure restores the existing legacy chase and boost state flow");

        settings.useRushSlots = true;
        PlacePressureScenario(enemies, close);
        yield return Wait(.08f);
        enemies[2].BeginDeath();
        Check(!enemies[2].HasRushSlot && PressureOwners(enemies) == 2 && !enemies[0].IsRushRunning,
            "Death releases one pressure place without allowing all waiting enemies to rush");
        yield return Wait(.25f);
        Check(enemies[0].HasRushSlot && PressureOwners(enemies) == 3,
            "The nearest surviving eligible candidate replaces a dead owner after the delay");
        foreach (var candidate in enemies) { candidate.PausePatrol(); candidate.enemyData = originalProfile; }
        player.IsSprinting = false; player.PlanarVelocity = Vector3.zero;
        settings.useRushSlots = false;
        UnityEngine.Object.Destroy(profile);
    }
    private static IEnumerator HandoffChecks(EnemyCombatSettings settings, PlayerController player, EnemyDataSO original, EnemyController[] enemies)
    {
        var profile = UnityEngine.Object.Instantiate(original);
        profile.walkSpeed = profile.chaseSpeed = profile.closeApproachSpeed = 0;
        profile.pressureOrbitSpeed = 0;
        profile.separateNearbyEnemies = false;
        profile.pressureWaitTimeout = 0;
        profile.attackRange = 1.5f;
        profile.coordinateAttacks = true;
        foreach (var candidate in enemies) { candidate.PausePatrol(); candidate.enemyData = profile; }
        settings.useRushSlots = true; settings.maxRushers = 1; settings.approachRunChance = 0;
        settings.pursuitHandoffDelay = .15f; settings.pursuitHandoffGap = .4f;
        settings.replacementInterval = .05f; settings.rushReentryDelay = .8f; settings.rushStallTimeout = 100;
        settings.enableCharge = false;
        player.IsSprinting = false; player.PlanarVelocity = Vector3.zero;
        player.transform.position = Vector3.zero;
        PlacePressureScenario(enemies, new[] { Vector3.back * 3, Vector3.left * 6, Vector3.right * 8 });
        yield return Wait(.08f);
        Check(enemies[0].HasRushSlot && PressureOwners(enemies) == 1, "Handoff begins with one pressure owner");
        enemies[0].GetComponent<NavMeshAgent>().Warp(Vector3.back * 7);
        player.PlanarVelocity = Vector3.forward * 2;
        yield return Wait(.22f);
        Check(enemies[0].HasRushSlot, "A moving player with no growing gap does not evict a distant owner");
        // Grow the gap continuously while walking (no sprint/escape reset).
        for (int frame = 0; frame < 8; frame++)
        {
            player.transform.position += Vector3.forward * .14f;
            yield return Wait(.04f);
        }
        Check(!enemies[0].HasRushSlot && !EnemyRushCoordinator.IsEscapePaused(player.transform),
            "Growing walking gap releases a stale owner without triggering group Escape");
        enemies[1].GetComponent<NavMeshAgent>().Warp(player.transform.position + Vector3.left * 2.5f);
        yield return Wait(.1f);
        Check(enemies[1].HasRushSlot && PressureOwners(enemies) == 1 && !enemies[0].HasRushSlot,
            "Nearby arrival replaces the stale owner while the old owner remains on reentry cooldown");
        player.PlanarVelocity = Vector3.zero;
        enemies[1].GetComponent<NavMeshAgent>().Warp(player.transform.position + Vector3.back * 8);
        enemies[2].GetComponent<NavMeshAgent>().Warp(player.transform.position + Vector3.right * 1.4f);
        yield return Wait(.15f);
        Check(enemies[2].HasRushSlot && PressureOwners(enemies) == 1 && !enemies[1].HasRushSlot,
            "A close defender takes a distant uncommitted pressure place without increasing the cap");
        // Attack-turn preemption must also work when pressure scheduling is separate.
        foreach (var candidate in enemies) EnemyAttackCoordinator.Release(candidate);
        Check(EnemyAttackCoordinator.Request(enemies[1], player.transform, 1), "Distant approach can reserve a turn");
        Check(EnemyAttackCoordinator.Request(enemies[2], player.transform, 1) && !EnemyAttackCoordinator.HasTurn(enemies[1], player.transform),
            "Close defender replaces a distant uncommitted attack reservation");
        EnemyAttackCoordinator.Release(enemies[2]);
        EnemyAttackCoordinator.Request(enemies[1], player.transform, 1);
        enemies[1].isAttacking = true;
        Check(!EnemyAttackCoordinator.Request(enemies[2], player.transform, 1), "A committed attack cannot be preempted by a close defender");
        enemies[1].isAttacking = false;
        foreach (var candidate in enemies) EnemyAttackCoordinator.Release(candidate);
        // Exhaust the retreat budget then inspect a real pressure destination.
        enemies[2].PausePatrol();
        settings.useRushSlots = false;
        enemies[2].StartPatrol(); enemies[2].SetAggressive(true);
        typeof(EnemyController).GetField("playerDetected", Hidden).SetValue(enemies[2], true);
        typeof(EnemyController).GetProperty("CurrentState").SetValue(enemies[2], EnemyController.EnemyState.Pressure);
        typeof(EnemyController).GetField("pressureOrbitActive", Hidden).SetValue(enemies[2], true);
        typeof(EnemyController).GetField("pressureHoldRadius", Hidden).SetValue(enemies[2], 4f);
        typeof(EnemyController).GetField("pressureRetreatRemaining", Hidden).SetValue(enemies[2], 0f);
        typeof(EnemyController).GetField("lastKnownPlayerPosition", Hidden).SetValue(enemies[2], player.transform.position);
        Call(enemies[2], "SetPressureDestination");
        yield return Wait(.03f);
        Check(Vector3.ProjectOnPlane(enemies[2].GetComponent<NavMeshAgent>().destination - player.transform.position, Vector3.up).magnitude < 1.7f,
            "Exhausted pressure budget prevents retreat toward the old four-metre ring");
        foreach (var candidate in enemies) { candidate.PausePatrol(); candidate.enemyData = original; }
        player.PlanarVelocity = Vector3.zero;
        UnityEngine.Object.Destroy(profile);
    }

    private static IEnumerator FrontInterceptChecks(EnemyCombatSettings settings, PlayerController player, EnemyDataSO original, EnemyController rear, EnemyController front)
    {
        var profile = UnityEngine.Object.Instantiate(original);
        profile.walkSpeed = profile.chaseSpeed = profile.closeApproachSpeed = 0;
        profile.pressureOrbitSpeed = 0; profile.separateNearbyEnemies = false; profile.pressureWaitTimeout = 0;
        profile.attackRange = 1.5f; profile.chaseSlowdownDistance = 1.8f;
        profile.coordinateAttacks = true; profile.maxConcurrentAttackers = 1;
        profile.engagementDistance = 6;
        rear.PausePatrol(); front.PausePatrol(); rear.enemyData = front.enemyData = profile;
        settings.useRushSlots = true; settings.maxRushers = 1; settings.approachRunChance = 0;
        settings.enableCharge = false; settings.replacementInterval = .05f;
        settings.frontInterceptDistance = 6; settings.frontInterceptAngle = 100;
        player.transform.position = Vector3.zero; player.IsSprinting = false; player.PlanarVelocity = Vector3.zero;
        var pair = new[] { rear, front };
        PlacePressureScenario(pair, new[] { Vector3.back * 4, Vector3.forward * 5 });
        yield return Wait(.08f);
        Check(rear.HasRushSlot && !front.HasRushSlot, "Rear owner holds pressure before player starts escaping");
        typeof(EnemyController).GetField("attackTimer", Hidden).SetValue(front, 0f);
        typeof(EnemyController).GetField("nextTurnRequest", Hidden).SetValue(front, 0f);
        player.IsSprinting = true; player.PlanarVelocity = Vector3.forward * 2;
        yield return Wait(.1f);
        Check(!EnemyRushCoordinator.IsEscapePaused(player.transform) && front.HasRushSlot && !rear.HasRushSlot && PressureOwners(pair) == 1,
            "Escape hands one rear slot to a reachable front enemy before melee range without exceeding the cap");
        Check(front.HasAttackTurn && front.IsRushRunning && front.CurrentState != EnemyController.EnemyState.Pressure && front.CurrentState != EnemyController.EnemyState.EscapeWalk,
            "Front slot owner approaches immediately instead of waiting in Pressure");
        player.transform.position = Vector3.forward * 10;
        yield return Wait(.08f);
        Check(front.CurrentState != EnemyController.EnemyState.EscapeWalk,
            "Passing an interceptor removes its running exception; it cannot chase forever from behind");
        rear.PausePatrol(); front.PausePatrol();
        player.transform.position = Vector3.zero; player.IsSprinting = false; player.PlanarVelocity = Vector3.zero;
        PlacePressureScenario(pair, new[] { Vector3.back * 4, Vector3.forward * 5 });
        yield return Wait(.08f);
        player.PlanarVelocity = Vector3.forward;
        yield return Wait(.1f);
        Check(front.HasRushSlot && !rear.HasRushSlot && !EnemyRushCoordinator.IsEscapePaused(player.transform),
            "Walking toward a front enemy also creates an opportunity before touching melee range");
        rear.PausePatrol(); front.PausePatrol(); player.PlanarVelocity = Vector3.zero;
        PlacePressureScenario(pair, new[] { Vector3.back * 4, Vector3.forward * 5 });
        yield return Wait(.08f);
        rear.isAttacking = true;
        player.PlanarVelocity = Vector3.forward * 2;
        yield return Wait(.1f);
        Check(rear.HasRushSlot && !front.HasRushSlot, "Front interception cannot steal a committed attack's pressure slot");
        rear.isAttacking = false;
        foreach (var candidate in pair) { candidate.PausePatrol(); candidate.enemyData = original; }
        player.IsSprinting = false; player.PlanarVelocity = Vector3.zero;
        settings.useRushSlots = false;
        UnityEngine.Object.Destroy(profile);
    }

    private static IEnumerator CloseSpeedChecks(EnemyCombatSettings settings, PlayerController player, EnemyDataSO original, EnemyController owner, EnemyController waiter)
    {
        var profile = UnityEngine.Object.Instantiate(original);
        profile.coordinateAttacks = true; profile.maxConcurrentAttackers = 1; profile.separateNearbyEnemies = false;
        profile.walkSpeed = 1; profile.closeApproachSpeed = 2.3f;
        profile.chaseSpeed = 2.5f; profile.burstSpeedMultiplier = 1.2f;
        profile.chaseSlowdownDistance = 4; profile.speedChangeRate = 100000;
        profile.pressureOrbitSpeed = .6f; profile.pressureWaitTimeout = 0;
        owner.PausePatrol(); waiter.PausePatrol(); owner.enemyData = waiter.enemyData = profile;
        settings.useRushSlots = true; settings.maxRushers = 1; settings.approachRunChance = 0; settings.enableCharge = false;
        player.transform.position = Vector3.zero; player.PlanarVelocity = Vector3.zero; player.IsSprinting = false;
        PlacePressureScenario(new[] { owner, waiter }, new[] { Vector3.back * 3, Vector3.right * 8 });
        yield return Wait(.06f);
        Check(owner.HasRushSlot && !waiter.HasRushSlot, "Close approach speed test has one granted owner and one waiting enemy");
        Check(!owner.HasAttackTurn, "A movement slot alone does not grant combat permission");
        var nav = owner.GetComponent<NavMeshAgent>();
        nav.Warp(Vector3.back * 4.5f); nav.speed = 0;
        Call(owner, "UpdateMovementSpeed");
        Check(Mathf.Abs(nav.speed - 3f) < .01f, "Granted runner keeps boosted speed outside the four-metre boundary");
        foreach (float distance in new[] { 4f, 3f, Mathf.Max(1.55f, owner.AttackReach + .05f) })
        {
            nav.Warp(Vector3.back * distance); nav.speed = 0;
            Call(owner, "UpdateMovementSpeed");
            Check(Mathf.Abs(nav.speed - 2.3f) < .01f, "Granted close-zone speed stays 2.3 instead of dipping toward Walk Speed at " + distance + " m");
        }
        var waitingNav = waiter.GetComponent<NavMeshAgent>(); waitingNav.Warp(Vector3.right * 3.7f);
        waitingNav.speed = 0; Call(waiter, "UpdateMovementSpeed");
        Check(!waiter.HasRushSlot && !waiter.HasAttackTurn && Mathf.Abs(waitingNav.speed - 1f) < .01f,
            "Ungranted waiters do not inherit the faster close-approach speed");
        Check(EnemyAttackCoordinator.Request(waiter, player.transform, 1) && waiter.HasAttackTurn && !waiter.HasRushSlot,
            "A combat turn can be granted independently of the movement-slot owner");
        owner.PausePatrol(); waiter.PausePatrol(); owner.enemyData = waiter.enemyData = original;
        settings.useRushSlots = false;
        UnityEngine.Object.Destroy(profile);
    }

    private static IEnumerator SavedChargeChecks(EnemyCombatSettings settings, PlayerController player, EnemyController template)
    {
        var parked = new Dictionary<EnemyController, Vector3>();
        int parkedIndex = 0;
        foreach (var old in EnemyController.ActiveEnemies)
            if (!old.IsPatrolRunning && old.GetComponent<NavMeshAgent>().isOnNavMesh)
            {
                parked[old] = old.transform.position;
                old.GetComponent<NavMeshAgent>().Warp(new Vector3(10 + parkedIndex++ % 3, 0, -10));
            }
        settings.useRushSlots = true; settings.enableCharge = true; settings.attackStartInterval = 0;
        player.IsSprinting = false; player.PlanarVelocity = Vector3.zero; player.transform.position = Vector3.zero;
        var randomState = UnityEngine.Random.state;
        foreach (string name in new[] { "Machete", "Torch" })
        {
            var profile = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<EnemyDataSO>("Assets/TheLostPresets/Data_Enemies_TheLost_" + name + ".asset"));
            var obj = UnityEngine.Object.Instantiate(template.gameObject);
            var actor = obj.GetComponent<EnemyController>(); actor.PausePatrol(); actor.enemyData = profile;
            Call(actor, "ApplySharedProfile");
            actor.GetComponent<EnemyWeaponRuntime>().Equip(profile.startingWeapon);
            Check(actor.EffectiveChargeDistance >= profile.charge.minDistance, name + " actual saved charge has a non-empty reachable band");
            float distance = (Mathf.Max(profile.charge.minDistance, actor.AttackReach + .3f) + actor.EffectiveChargeDistance) * .5f;
            float configuredChance = profile.charge.selectionChance;
            profile.charge.selectionChance = 0;
            actor.GetComponent<NavMeshAgent>().Warp(Vector3.back * distance);
            actor.StartPatrol(); actor.SetAggressive(true);
            foreach (string field in new[] { "nextChargeTime", "nextChargeDecision", "attackTimer", "nextTurnRequest" })
                typeof(EnemyController).GetField(field, Hidden).SetValue(actor, 0f);
            Call(actor, "Update");
            Check(actor.HasAttackTurn && !actor.isAttacking, name + " receives its combat turn even when Charge is rejected");
            if (distance > EnemyCombatSettings.PressureDistance)
                Check(!actor.HasRushSlot && actor.HasAttackTurn, "Combat permission is independent of movement-slot ownership");
            var beforeRead = UnityEngine.Random.state;
            float expectedNextRandom = UnityEngine.Random.value;
            UnityEngine.Random.state = beforeRead;
            bool turnRead = actor.HasAttackTurn;
            float rangeRead = actor.EffectiveAttackTurnRequestDistance;
            string statusRead = actor.ChargeStatus;
            Check(UnityEngine.Random.value == expectedNextRandom, name + " permission/range/debug reads do not roll attack decisions");
            profile.charge.selectionChance = configuredChance;
            bool started = false;
            for (int attempt = 0; attempt < 32 && !started; attempt++)
            {
                actor.PausePatrol();
                typeof(EnemyRushCoordinator).GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                actor.GetComponent<NavMeshAgent>().Warp(Vector3.back * distance);
                actor.StartPatrol(); actor.SetAggressive(true);
                foreach (string field in new[] { "nextChargeTime", "nextChargeDecision", "attackTimer", "nextTurnRequest" })
                    typeof(EnemyController).GetField(field, Hidden).SetValue(actor, 0f);
                UnityEngine.Random.InitState(700 + attempt);
                Call(actor, "Update");
                started = actor.CurrentState == EnemyController.EnemyState.ChargePreparation;
            }
            Check(started, name + " actual saved chance/range/timing starts Charge through Update: " + actor.ChargeStatus);
            Vector3 origin = actor.transform.position;
            float deadline = Time.time + profile.charge.preparation + .65f;
            while (Time.time < deadline && Vector3.Distance(origin, actor.transform.position) < .15f) yield return null;
            Check(Vector3.Distance(origin, actor.transform.position) >= .15f, name + " actual saved charge physically moves after its preparation: " + actor.CurrentState + " at " + actor.transform.position + " speed=" + actor.GetComponent<NavMeshAgent>().speed);
            actor.PausePatrol(); UnityEngine.Object.Destroy(obj); UnityEngine.Object.Destroy(profile);
            yield return null;
        }
        UnityEngine.Random.state = randomState;
        foreach (var old in parked) if (old.Key != null) old.Key.GetComponent<NavMeshAgent>().Warp(old.Value);
    }
    private static IEnumerator PressureApproachChecks(EnemyCombatSettings settings, PlayerController player, EnemyDataSO original, EnemyController template)
    {
        var profile = UnityEngine.Object.Instantiate(original);
        profile.useLostCombat = true; profile.coordinateAttacks = true; profile.maxConcurrentAttackers = 1;
        profile.engagementDistance = 1.6f; profile.pressureRingRadius = 1.8f;
        profile.walkSpeed = 1; profile.closeApproachSpeed = 2.3f; profile.chaseSlowdownDistance = 4;
        profile.stoppingDistance = .8f; // Current Machete/Torch prefab tuning.
        profile.chaseSpeed = 2.5f; profile.burstSpeedMultiplier = 1.2f;
        profile.separateNearbyEnemies = false; profile.pressureWaitTimeout = 0;
        profile.spreadChaseTargets = false; profile.useApproachDecisions = false;
        profile.obstacleMask = 0;
        profile.pressureOrbitSpeed = .6f; profile.speedChangeRate = 20;
        var module = Weapon(EnemyWeaponSO.Loadout.Machete);
        module.range = 1.5f; module.preparation = .25f; module.windup = .5f; module.recovery = .15f;
        profile.meleeAttackStep = .25f; profile.advanceDuringPreparation = true;
        profile.startingWeapon = module;
        // Isolate this physical pursuit from paused actors left by earlier tests.
        // Their stopped NavMeshAgents remain solid avoidance obstacles.
        var parked = new Dictionary<EnemyController, Vector3>();
        int parkedIndex = 0;
        foreach (var oldActor in EnemyController.ActiveEnemies)
        {
            var oldNav = oldActor.GetComponent<NavMeshAgent>();
            if (!oldActor.IsPatrolRunning && oldNav != null && oldNav.enabled && oldNav.isOnNavMesh)
            {
                parked[oldActor] = oldActor.transform.position;
                oldNav.Warp(new Vector3(10 + (parkedIndex++ % 3), 0, -10));
            }
        }
        var obj = UnityEngine.Object.Instantiate(template.gameObject);
        var actor = obj.GetComponent<EnemyController>();
        actor.PausePatrol(); actor.enemyData = profile;
        actor.GetComponent<NavMeshAgent>().radius = .3f; // Current production enemy radius.
        actor.GetComponent<EnemyWeaponRuntime>().Equip(module);
        settings.useRushSlots = true; settings.maxRushers = 1; settings.approachRunChance = 0;
        settings.enableCharge = false; settings.attackStartInterval = 0;
        player.transform.position = Vector3.zero; player.IsSprinting = false; player.PlanarVelocity = Vector3.zero; player.currentHealth = 1000;
        var effects = EnemyPlayerEffects.For(player); effects.enabled = false; effects.enabled = true;
        PlacePressureScenario(new[] { actor }, new[] { Vector3.back * 3.7f });
        typeof(EnemyController).GetField("attackTimer", Hidden).SetValue(actor, 0f);
        typeof(EnemyController).GetField("nextTurnRequest", Hidden).SetValue(actor, 0f);
        yield return Wait(.12f);
        Check(actor.HasRushSlot && actor.HasAttackTurn && actor.EffectiveAttackTurnRequestDistance >= 4.5f,
            "A short 1.6m engagement setting cannot strand an owner in the 4m Pressure ring");
        Check(actor.CurrentState == EnemyController.EnemyState.Approach && actor.GetComponent<NavMeshAgent>().speed > 1,
            "Granted attacker leaves Pressure and physically approaches from the ring");
        float until = Time.time + 3;
        while (Time.time < until && player.currentHealth >= 1000) yield return null;
        Check(player.currentHealth < 1000, "Production NavMesh pursuit reaches melee and damages a stationary player with Charge disabled: " + actor.CurrentState +
            " distance=" + Vector3.Distance(actor.transform.position, player.transform.position) + " speed=" + actor.GetComponent<NavMeshAgent>().speed +
            " velocity=" + actor.GetComponent<NavMeshAgent>().velocity + " remaining=" + actor.GetComponent<NavMeshAgent>().remainingDistance +
            " stopped=" + actor.GetComponent<NavMeshAgent>().isStopped + " slot=" + actor.HasRushSlot + " turn=" + actor.HasAttackTurn +
            " cooldown=" + typeof(EnemyController).GetField("attackTimer", Hidden).GetValue(actor) + " effect=" + effects.BlockReason + " last=" + effects.LastHitResult);
        actor.PausePatrol(); effects.enabled = false; effects.enabled = true;
        player.currentHealth = 1000; player.transform.position = Vector3.zero; player.PlanarVelocity = Vector3.forward;
        PlacePressureScenario(new[] { actor }, new[] { Vector3.back * 3.7f });
        typeof(EnemyController).GetField("attackTimer", Hidden).SetValue(actor, 0f);
        typeof(EnemyController).GetField("nextTurnRequest", Hidden).SetValue(actor, 0f);
        until = Time.time + 4;
        bool startedAttack = false;
        float walkingStartedAt = Time.time;
        while (Time.time < until && player.currentHealth >= 1000)
        {
            // Editor update callbacks need not match runtime frames; use elapsed
            // simulation time so the fixture truly walks at 1 metre per second.
            player.transform.position = Vector3.forward * (Time.time - walkingStartedAt);
            startedAttack |= actor.CurrentState == EnemyController.EnemyState.Preparation || actor.CurrentState == EnemyController.EnemyState.Attack;
            yield return null;
        }
        Check(startedAttack, "Attacker physically catches a player walking at 1m/s and starts a melee attempt instead of orbiting forever");
        Check(player.currentHealth < 1000, "Closing preparation and a committed 0.25m step can hit a player walking straight away at 1m/s without Charge: " + actor.CurrentState +
            " distance=" + Vector3.Distance(actor.transform.position, player.transform.position) + " stopped=" + actor.GetComponent<NavMeshAgent>().isStopped +
            " speed=" + actor.GetComponent<NavMeshAgent>().speed + " effects=" + effects.BlockReason + " last=" + effects.LastHitResult);
        actor.PausePatrol(); effects.enabled = false; effects.enabled = true;
        player.currentHealth = 1000; player.transform.position = Vector3.zero; player.PlanarVelocity = Vector3.zero;
        PlacePressureScenario(new[] { actor }, new[] { Vector3.back * 1.4f });
        typeof(EnemyController).GetField("attackTimer", Hidden).SetValue(actor, 0f);
        typeof(EnemyController).GetField("nextTurnRequest", Hidden).SetValue(actor, 0f);
        until = Time.time + 1;
        while (Time.time < until && actor.CurrentState != EnemyController.EnemyState.Attack) yield return null;
        Check(actor.CurrentState == EnemyController.EnemyState.Attack, "Committed-step dodge test reaches the windup");
        player.transform.position = Vector3.right * 3;
        yield return Wait(.6f);
        Check(player.currentHealth == 1000 && Mathf.Abs(actor.transform.position.x) < .2f,
            "A dodge after commitment avoids melee damage and the small step does not home sideways");
        actor.PausePatrol(); player.PlanarVelocity = Vector3.zero;
        settings.useRushSlots = false;
        UnityEngine.Object.Destroy(obj); UnityEngine.Object.Destroy(profile); UnityEngine.Object.Destroy(module);
        foreach (var entry in parked)
            if (entry.Key != null) entry.Key.GetComponent<NavMeshAgent>().Warp(entry.Value);
        effects.enabled = false; effects.enabled = true;
        yield return null;
    }

    private static EnemyWeaponSO Weapon(EnemyWeaponSO.Loadout type)
    {
        var weapon = ScriptableObject.CreateInstance<EnemyWeaponSO>();
        weapon.loadout = type; weapon.minDamage = weapon.maxDamage = 10;
        weapon.preparation = .08f; weapon.windup = .15f; weapon.recovery = .3f;
        weapon.range = type == EnemyWeaponSO.Loadout.Stone ? 8 : 2.8f;
        weapon.armDropChance = 1; weapon.otherDropChance = 0;
        return weapon;
    }
    private static IEnumerator Run()
    {
        var breakSettings = new EnemyStaggerSettings { criticalChance = 0 };
        var meter = new EnemyStaggerMeter();
        Check(!meter.Hit(breakSettings, 0, false, 1, 0, .3f) && meter.Impact == 20, "Ordinary shot contributes impact without guaranteed interruption");
        for (int pellet = 0; pellet < 16; pellet++) meter.Hit(breakSettings, .01f, false, 1, 0, .3f);
        Check(meter.Impact == 20, "A shotgun volley contributes once per enemy instead of once per pellet");
        Check(!meter.Hit(breakSettings, .1f, false, 2, 0, .3f) && meter.Impact == 40, "A second distinct shot accumulates impact");
        Check(meter.Hit(breakSettings, .2f, true, 3, 0, .3f) && meter.Impact == 0, "Head impact can cross the guaranteed break threshold");
        Check(!meter.Hit(breakSettings, .3f, false, 4, 60, .3f) && meter.Impact == 0, "Post-break resistance prevents immediate chain stun");
        Check(meter.Hit(breakSettings, 2f, false, 5, 60, .3f), "A later shot can break after resistance expires");
        var recoveryMeter = new EnemyStaggerMeter(); recoveryMeter.Hit(breakSettings, 0, false, 7, 0, .3f);
        recoveryMeter.Tick(breakSettings, 2f);
        Check(Mathf.Abs(recoveryMeter.Impact - 10) < .001f && breakSettings.threshold == 60, "Impact recovers after delay without modifying shared tuning");
        var criticalMeter = new EnemyStaggerMeter(); breakSettings.criticalChance = 1; breakSettings.threshold = 1000;
        Check(criticalMeter.Hit(breakSettings, 0, false, 10, 1, .3f), "Critical can break before the accumulation threshold");
        Check(!criticalMeter.Hit(breakSettings, 2f, false, 10, 1, .3f), "The same volley cannot roll another critical even after resistance expires");
        var timingSettings = ScriptableObject.CreateInstance<EnemyCombatSettings>();
        timingSettings.enableCharge = false; // Dedicated charge checks enable this explicitly.
        timingSettings.escapeSlowdownTime = .05f;
        timingSettings.useRushSlots = false;
        timingSettings.attackStartInterval = 0; // Existing module tests isolate damage timing from shared cadence.
        typeof(EnemyCombatSettings).GetField("cached", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, timingSettings);

        var schemaFields = new List<string>();
        foreach (var groupFields in EnemyProfileGUI.Fields) schemaFields.AddRange(groupFields);
        Check(new HashSet<string>(schemaFields).Count == schemaFields.Count, "Canonical Enemy Profile editor has no duplicate setting paths");
        var rulesProfile = ScriptableObject.CreateInstance<EnemyDataSO>();
        var rulesData = new SerializedObject(rulesProfile);
        Check(schemaFields.TrueForAll(field => rulesData.FindProperty(field) != null), "Every Enemy editor setting resolves to a serialized property");
        var missingFields = Array.FindAll(typeof(EnemyDataSO).GetFields(BindingFlags.Public | BindingFlags.Instance), field => !schemaFields.Contains(field.Name));
        Check(missingFields.Length == 0,
            "Canonical editor covers all current profile fields without dropping saved settings: " + string.Join(", ", Array.ConvertAll(missingFields, field => field.Name)));
        rulesProfile.useLostCombat = false;
        Check(!EnemyProfileGUI.IsVisible("startingWeapon", rulesProfile, true), "Weapon module is hidden when Lost Combat is disabled");
        rulesProfile.useLostCombat = true;
        Check(EnemyProfileGUI.IsVisible("startingWeapon", rulesProfile, true), "Weapon module returns when Lost Combat is enabled");
        rulesProfile.coordinateAttacks = false;
        Check(!EnemyProfileGUI.IsVisible("pressureRingRadius", rulesProfile, true), "Pressure settings are hidden without turn coordination");
        rulesProfile.coordinateAttacks = true;
        Check(EnemyProfileGUI.IsVisible("pressureRingRadius", rulesProfile, true), "Pressure settings return with coordination enabled");
        Check(!EnemyProfileGUI.IsVisible("animationWalkSpeed", rulesProfile, true), "Automatic references hide duplicate custom speed controls");
        rulesProfile.autoAnimationReferenceSpeeds = false;
        Check(EnemyProfileGUI.IsVisible("animationWalkSpeed", rulesProfile, true), "Custom references remain available on request");
        Check(!EnemyProfileGUI.IsVisible("locomotionState", rulesProfile, false) && EnemyProfileGUI.IsVisible("locomotionState", rulesProfile, true),
            "Advanced Animator binding fields remain accessible");
        rulesProfile.canHear = false;
        Check(!EnemyProfileGUI.IsVisible("gunshotHearingRadius", rulesProfile, true), "Hearing ranges are hidden when hearing is off");
        rulesProfile.animationWalkSpeed = 2;
        Check(EnemyProfileGUI.Warnings(rulesProfile).Exists(message => message.Contains("Custom Walk Reference")), "Editor warns about the screenshot's reference-speed mismatch");
        var newController = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/TheLostAnimator/Enemy.controller");
        Check(newController != null, "New Enemy Blend Tree controller imports");
        var states = newController.layers[0].stateMachine.states;
        var locomotionState = Array.Find(states, s => s.state.name == "Controlling").state;
        var familyTree = locomotionState.motion as UnityEditor.Animations.BlendTree;
        Check(familyTree != null && familyTree.blendParameter == "EnemyType", "Family selector is independent of movement Speed");
        var weaponTree = familyTree.children[0].motion as UnityEditor.Animations.BlendTree;
        Check(weaponTree != null && weaponTree.blendParameter == "WeaponType" && weaponTree.children.Length == 6, "TheLost has six discrete loadout branches");
        foreach (var branch in weaponTree.children)
        {
            var movement = branch.motion as UnityEditor.Animations.BlendTree;
            Check(movement != null && movement.children.Length == 3 && movement.children[0].motion != null && movement.children[2].motion != null, "Every loadout has a usable movement branch");
        }
        var speedTree = weaponTree.children[1].motion as UnityEditor.Animations.BlendTree;
        Check(speedTree != null && speedTree.children.Length == 3 && speedTree.children[1].threshold == .5f && speedTree.children[2].threshold == 1f,
            "Actual controller has Idle/Walk/Run thresholds matching the velocity adapter");
        var directionTree = speedTree.children[1].motion as UnityEditor.Animations.BlendTree;
        Check(directionTree != null && directionTree.blendParameter == "Horizontal" && directionTree.blendParameterY == "Verticle",
            "Walk directional parameters match current controller spelling");
        Check(!Array.Exists(newController.layers[0].stateMachine.anyStateTransitions, transition =>
            transition.conditions.Length == 0 || Array.Exists(transition.conditions, condition => condition.parameter == "Attacking")),
            "No unconditional/Attacking Any State entry can override a script-selected attack");
        foreach (var clipPath in new[] { "Idle", "Walking", "Run", "LTurn", "RTurn" })
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/TheLostAnimator/Machete/Art_Animation_Thelost_Machete_" + clipPath + ".anim");
            Check(clip != null && AnimationUtility.GetAnimationClipSettings(clip).loopTime, clipPath + " loops in locomotion");
        }
        var testController = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath("Assets/LocomotionFixture.controller");
        foreach (var parameter in new[] { "Blend", "Horizontal", "Verticle", "WeaponType", "EnemyType" }) testController.AddParameter(parameter, AnimatorControllerParameterType.Float);
        testController.AddParameter("Attacking", AnimatorControllerParameterType.Bool);
        testController.layers[0].stateMachine.AddState("Controlling");
        testController.layers[0].stateMachine.AddState("Art_Animation_Thelost_Machete_Attack");
        var animationObject = new GameObject("Locomotion parameter test");
        var animation = animationObject.AddComponent<Animator>(); animation.runtimeAnimatorController = testController; animation.Update(0);
        var animationProfile = ScriptableObject.CreateInstance<EnemyDataSO>(); animationProfile.locomotionDamping = 0;
        var driver = new EnemyLocomotionAnimator(animation, animationProfile);
        var selectionModule = ScriptableObject.CreateInstance<EnemyWeaponSO>();
        var loadouts = new[] { EnemyWeaponSO.Loadout.Machete, EnemyWeaponSO.Loadout.Log, EnemyWeaponSO.Loadout.Torch, EnemyWeaponSO.Loadout.Stone, EnemyWeaponSO.Loadout.HeavyAxe };
        for (int index = 0; index < loadouts.Length; index++)
        {
            selectionModule.loadout = loadouts[index];
            driver.Update(Vector3.forward, false, .1f, selectionModule);
            Check(Mathf.Abs(animation.GetFloat("WeaponType") - (index + 1) * .2f) < .001f && animation.GetFloat("EnemyType") == 0, "Held loadout selects its exact branch: " + loadouts[index]);
        }
        driver.Update(Vector3.zero, false, .1f);
        Check(animation.GetFloat("WeaponType") == 0, "Disarmed enemy selects Unarm without blending loadouts");
        UnityEngine.Object.Destroy(selectionModule);
        animationProfile.autoAnimationReferenceSpeeds = false;
        animationProfile.keepWalkBlendWhileMoving = false;
        animationProfile.animationWalkSpeed = 2;
        Check(Mathf.Abs(EnemyLocomotionAnimator.BlendForMovement(.6f, animationProfile) - .15f) < .001f,
            "Reproduces low Blend from 0.6 m/s movement and 2 m/s custom walk reference");
        animationProfile.autoAnimationReferenceSpeeds = true;
        animationProfile.keepWalkBlendWhileMoving = true;
        Check(EnemyLocomotionAnimator.BlendForMovement(.6f, animationProfile) >= .5f,
            "Automatic movement references prevent old custom reference leaving Pressure mostly Idle");
        Check(Mathf.Abs(EnemyLocomotionAnimator.BlendForMovement(.1f, animationProfile) - .5f) < .001f,
            "Crowd-slowed movement retains full Walk branch");
        Check(EnemyLocomotionAnimator.BlendForMovement(.01f, animationProfile) == 0,
            "Tiny avoidance jitter stays Idle under stop threshold");
        animationProfile.walkSpeed = 1; animationProfile.chaseSpeed = 4; animationProfile.burstSpeedMultiplier = 1.5f;
        Check(EnemyLocomotionAnimator.ReferenceSpeeds(animationProfile) == new Vector2(1, 6),
            "Automatic animation references follow current AI speed settings");
        animationProfile.walkSpeed = .5f; animationProfile.chaseSpeed = 2.5f; animationProfile.burstSpeedMultiplier = 1.25f;
        animationProfile.animationWalkSpeed = .5f;
        Check(driver.Compatible, "Velocity adapter recognizes configured locomotion state and parameters");
        Check(driver.ResolvedSpeedParameter == "Blend", "New Speed default remains compatible with an old Blend controller");
        driver.Update(Vector3.zero, false, .1f);
        Check(animation.GetFloat("Blend") == 0, "Stationary actor drives Idle blend");
        driver.Update(Vector3.forward * animationProfile.animationWalkSpeed, false, .1f);
        Check(Mathf.Abs(animation.GetFloat("Blend") - .5f) < .001f && animation.GetFloat("Verticle") > .99f, "Real walk speed selects Walk and forward direction");
        driver.Update(Vector3.right * animationProfile.animationWalkSpeed, false, .1f);
        Check(animation.GetFloat("Horizontal") > .99f && Mathf.Abs(animation.GetFloat("Verticle")) < .001f, "Pressure side movement drives horizontal walk direction");
        animationObject.transform.rotation = Quaternion.Euler(0, 90, 0);
        driver.Update(Vector3.forward * animationProfile.animationWalkSpeed, false, .1f);
        Check(animation.GetFloat("Horizontal") < -.99f, "Direction is relative to enemy facing rather than world axes");
        driver.Update(Vector3.forward * animationProfile.animationRunSpeed, true, .1f);
        Check(Mathf.Abs(animation.GetFloat("Blend") - 1f) < .001f && animation.GetBool("Attacking"), "Run speed and attack bool reach configured parameters");
        Check(driver.ResolveState(Animator.StringToHash("zombie_walk2"), Animator.StringToHash("zombie_idle1"), Animator.StringToHash("zombie_walk2"), Animator.StringToHash("zombie_attack2_1")) == Animator.StringToHash("Controlling"),
            "Legacy walk request resolves to the new locomotion state");
        animationProfile.useLocomotionBlendTree = false;
        animationProfile.useLocomotionBlendTree = true;
        Check(driver.ResolveState(Animator.StringToHash("zombie_attack2_1"), Animator.StringToHash("zombie_idle1"), Animator.StringToHash("zombie_walk2"), Animator.StringToHash("zombie_attack2_1")) == Animator.StringToHash("Art_Animation_Thelost_Machete_Attack"),
            "Legacy attack fallback resolves to the new attack state");
        animationProfile.useLocomotionBlendTree = false;
        Check(!driver.Compatible, "Disabling Blend Tree preserves legacy animation path");
        var migratedParameters = testController.parameters;
        foreach (var parameter in migratedParameters) if (parameter.name == "Blend") parameter.name = "Speed";
        testController.parameters = migratedParameters;
        animation.Rebind(); animation.Update(0);
        animationProfile.useLocomotionBlendTree = true;
        animationProfile.locomotionBlendParameter = "Blend";
        var migratedDriver = new EnemyLocomotionAnimator(animation, animationProfile);
        Check(migratedDriver.Compatible && migratedDriver.ResolvedSpeedParameter == "Speed", "Saved Blend profile connects to renamed Speed controller");
        migratedDriver.Update(Vector3.forward * .5f, false, .1f);
        Check(Mathf.Abs(animation.GetFloat("Speed") - .5f) < .001f, "Migrated controller receives movement on Speed instead of the missing Blend parameter");
        testController.layers[0].stateMachine.AddState("zombie_attack2_1");
        testController.layers[0].stateMachine.AddState("Unarmed_Attack");
        animation.Rebind(); animation.Update(0);
        var attackDriver = new EnemyLocomotionAnimator(animation, animationProfile);
        int legacyAttack = Animator.StringToHash("zombie_attack2_1");
        Check(attackDriver.ResolveState(legacyAttack, Animator.StringToHash("zombie_idle1"), Animator.StringToHash("zombie_walk2"), legacyAttack) == legacyAttack,
            "Existing unarmed legacy state is never rewritten to armed fallback");
        Check(attackDriver.ResolveAttackState("Missing_Sweep", true) == Animator.StringToHash("Art_Animation_Thelost_Machete_Attack"),
            "Armed module without a specific state uses the armed fallback");
        Check(attackDriver.ResolveAttackState("zombie_attack2_1", false) == legacyAttack && attackDriver.LastAttackStateName == "zombie_attack2_1",
            "Dropped weapon selects the configured base animation instead of Machete");
        Check(attackDriver.ResolveAttackState("Unarmed_Attack", false) == Animator.StringToHash("Unarmed_Attack"),
            "Custom Base Attack Animation takes priority after disarm");
        UnityEngine.Object.Destroy(animationObject);
        var expectedModuleRefs = new Dictionary<string, string>();
        foreach (string record in System.IO.File.ReadAllLines("expected-module-refs.txt"))
        {
            var parts = record.Split('|'); expectedModuleRefs[parts[0]] = parts[1];
        }
        foreach (string name in new[] { "Unarmed", "Machete", "Log", "HeavyAxe", "Stone", "Torch" })
        {
            var imported = AssetDatabase.LoadAssetAtPath<EnemyDataSO>("Assets/TheLostPresets/Data_Enemies_TheLost_" + name + ".asset");
            Check(imported != null && imported.useLostCombat && imported.maxConcurrentAttackers == 2, "Imported " + name + " prototype profile");
            string expectedGuid = expectedModuleRefs["Data_Enemies_TheLost_" + name];
            Check(string.IsNullOrEmpty(expectedGuid) ? imported.startingWeapon == null : imported.startingWeapon != null &&
                AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(imported.startingWeapon)) == expectedGuid,
                "Imported " + name + " preserves its configured module reference");
            Check(imported.locomotionBlendParameter == speedTree.blendParameter, name + " profile matches actual controller speed parameter");
            Check(imported.charge.selectionChance > 0 && imported.charge.decisionInterval > 0, name + " saved profile imports a usable randomized Charge decision");
            if (name == "Machete") Check(imported.autoAnimationReferenceSpeeds && EnemyLocomotionAnimator.BlendForMovement(.6f, imported) >= .5f,
                "Existing Machete profile uses automatic references even with saved custom walk speed 2");
        }
        var visibilityRig = new GameObject("Offscreen animated visibility regression");
        visibilityRig.transform.position = new Vector3(1000, 0, 0);
        var visibilityAnimator = visibilityRig.AddComponent<Animator>(); visibilityAnimator.cullingMode = AnimatorCullingMode.CullCompletely;
        var movingBone = new GameObject("Moving bone").transform; movingBone.SetParent(visibilityRig.transform, false);
        var visibilitySkin = visibilityRig.AddComponent<SkinnedMeshRenderer>(); visibilitySkin.updateWhenOffscreen = false;
        var visibilityMesh = new Mesh(); visibilityMesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up * 2 };
        visibilityMesh.triangles = new[] { 0, 1, 2 }; visibilityMesh.bindposes = new[] { Matrix4x4.identity };
        visibilityMesh.boneWeights = new[] { new BoneWeight { boneIndex0=0, weight0=1 }, new BoneWeight { boneIndex0=0, weight0=1 }, new BoneWeight { boneIndex0=0, weight0=1 } };
        visibilityMesh.RecalculateBounds(); visibilitySkin.sharedMesh = visibilityMesh; visibilitySkin.bones = new[] { movingBone }; visibilitySkin.rootBone = movingBone;
        EnemyController.PrepareAnimatedVisibility(visibilityRig);
        Check(visibilityAnimator.cullingMode == AnimatorCullingMode.AlwaysAnimate && visibilitySkin.updateWhenOffscreen,
            "AI-owned moving skeletons keep animation and skin bounds updating outside camera view");
        Vector3 unchangedRoot = visibilityRig.transform.position;
        movingBone.localPosition = Vector3.up * 3;
        var bakedVisibilityMesh = new Mesh(); visibilitySkin.BakeMesh(bakedVisibilityMesh);
        Check(bakedVisibilityMesh.bounds.center.y > 3 && visibilityRig.transform.position == unchangedRoot,
            "An offscreen skinned pose can update without teleporting its actor root");
        UnityEngine.Object.Destroy(visibilityRig); UnityEngine.Object.Destroy(visibilityMesh); UnityEngine.Object.Destroy(bakedVisibilityMesh);
        var floor = new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, size = new Vector3(30, .2f, 30), transform = Matrix4x4.TRS(new Vector3(0, -.1f, 0), Quaternion.identity, Vector3.one) };
        var mesh = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), new List<NavMeshBuildSource> { floor }, new Bounds(Vector3.zero, new Vector3(30, 4, 30)), Vector3.zero, Quaternion.identity);
        NavMesh.AddNavMeshData(mesh);
        var root = new GameObject("Player"); root.tag = "Player";
        var moving = new GameObject("Moving player"); moving.transform.SetParent(root.transform); moving.transform.position = Vector3.forward;
        var player = moving.AddComponent<PlayerController>();
        var capsule = moving.AddComponent<CapsuleCollider>(); capsule.center = Vector3.up * .9f; capsule.height = 1.8f; capsule.radius = .25f;
        var profile = ScriptableObject.CreateInstance<EnemyDataSO>();
        profile.useSharedSettings = profile.useLostCombat = true;
        profile.useRagdoll = profile.enableHitReaction = false;
        profile.coordinateAttacks = true; profile.maxConcurrentAttackers = 1;
        profile.detectAnticipation = .12f; profile.attackPreparationTime = .1f; profile.attackWindup = .12f;
        profile.meleeAttackStep = 0; // Existing timing checks isolate the optional forward step.
        profile.baseRecovery = .3f; profile.restraintDuration = .5f; profile.playerHitProtection = .65f;
        profile.attackTurnRest = 3f; profile.minAttack = profile.maxAttack = 10;
        var go = new GameObject("Lost"); go.SetActive(false);
        go.AddComponent<NavMeshAgent>(); var enemy = go.AddComponent<EnemyController>(); enemy.enemyData = profile;
        go.SetActive(true); go.GetComponent<NavMeshAgent>().Warp(Vector3.zero); Call(enemy, "Start");
        Call(enemy, "Update");
        Check(enemy.CurrentState == EnemyController.EnemyState.Detect && !enemy.isAlert, "Detect anticipation precedes confirmed chase");
        yield return Wait(.44f);
        var effects = EnemyPlayerEffects.For(player);
        Check(player.currentHealth == 90, "Unarmed timed hit deals configured damage");
        Check(effects.MovementMultiplier < 1 && effects.MovementMultiplier >= .25f, "Unarmed restraint slows without freezing");
        Check(!effects.TryHit(50, .65f), "Shared protection blocks follow-up hit");
        enemy.PausePatrol();
        yield return Wait(.7f);
        Check(effects.MovementMultiplier == 1 && effects.CanReceiveAttack, "Restraint and protection expire");

        var log = Weapon(EnemyWeaponSO.Loadout.Log);
        profile.startingWeapon = log;
        var runtime = enemy.GetComponent<EnemyWeaponRuntime>(); runtime.Equip(log);
        Check(enemy.AttackReach == log.range, "Module overrides range");
        var hand = new GameObject("Hand hitbox"); hand.transform.SetParent(go.transform);
        var handCollider = hand.AddComponent<BoxCollider>(); hand.AddComponent<HitBox>().hitBox = HitBoxPart.Hand;
        var bodyCollider = go.AddComponent<BoxCollider>();
        Check(!runtime.TryDisarm(bodyCollider) && runtime.Current == log, "Other-part zero chance retains weapon");
        yield return Wait(.25f);
        ResetAttack(enemy);
        Check(enemy.CurrentState == EnemyController.EnemyState.Preparation && enemy.isAttacking, "Weapon preparation uses common attack lifecycle");
        enemy.ReceiveBulletImpact(handCollider, Vector3.zero, Vector3.back);
        Check(runtime.Current == null && !enemy.isAttacking && !enemy.HasAttackTurn, "Arm disarm cancels attack and releases reservation");
        Check(profile.startingWeapon == log && enemy.AttackReach == profile.attackRange, "Disarm returns to base without mutating shared profile");
        yield return Wait(.5f);
        Check(player.currentHealth == 90, "Canceled weapon hit never deals delayed damage");
        enemy.PausePatrol(); handCollider.enabled = bodyCollider.enabled = false;

        var axe = Weapon(EnemyWeaponSO.Loadout.HeavyAxe);
        axe.preparation = .3f; axe.windup = .2f; axe.recovery = .4f; axe.minDamage = axe.maxDamage = 30;
        runtime.Equip(axe); moving.transform.position = Vector3.forward; go.transform.rotation = Quaternion.identity;
        ResetAttack(enemy); yield return Wait(.18f);
        Check(enemy.CurrentState == EnemyController.EnemyState.Preparation && player.currentHealth == 90, "Heavy axe waits through longer anticipation");
        Check(go.GetComponent<NavMeshAgent>().isStopped, "Heavy axe anticipation stops movement");
        yield return Wait(.43f);
        Check(player.currentHealth == 60 && enemy.CurrentState == EnemyController.EnemyState.Recovery, "Heavy axe applies its damage then enters independent recovery");
        enemy.PausePatrol(); runtime.Consume(); yield return Wait(.7f); player.currentHealth = 90;

        // Turn away after the attack facing is committed, while staying within distance.
        moving.transform.position = Vector3.forward; go.transform.rotation = Quaternion.identity;
        ResetAttack(enemy); yield return Wait(.14f);
        moving.transform.position = Vector3.right;
        yield return Wait(.4f);
        Check(player.currentHealth == 90, "Facing-limited unarmed attack misses a side dodge");
        enemy.PausePatrol();

        var stone = Weapon(EnemyWeaponSO.Loadout.Stone);
        stone.preparation = stone.windup = .05f; stone.recovery = .1f;
        stone.projectileSpeed = 6; stone.projectileGravity = 0; stone.projectileLifetime = 2;
        runtime.Equip(stone); moving.transform.position = Vector3.forward * 4; go.transform.rotation = Quaternion.identity;
        ResetAttack(enemy); yield return Wait(.22f);
        Check(runtime.Current == null && enemy.AttackReach == profile.attackRange, "Throw consumes one stone and returns loadout to base");
        Check(enemy.HasAttackTurn && player.currentHealth == 90, "Thrown stone holds the shared turn while in flight");
        yield return Wait(.8f);
        Check(player.currentHealth == 80, "Swept stone hits moving-controller collider");
        Check(!enemy.HasAttackTurn, "Stone impact releases reserved turn after recovery");
        enemy.PausePatrol(); yield return Wait(.7f);

        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position = new Vector3(0, 1, 2); wall.transform.localScale = new Vector3(2, 3, .3f);
        EnemyStoneProjectile.Launch(go.transform, new Vector3(0, 1, .4f), new Vector3(0, 1, 4), stone, .65f);
        yield return Wait(.8f);
        Check(player.currentHealth == 80, "Stone obstruction blocks damage through wall");
        UnityEngine.Object.Destroy(wall); yield return null;
        moving.transform.position = Vector3.forward * 4;
        EnemyStoneProjectile.Launch(go.transform, new Vector3(0, 1, .4f), new Vector3(0, 1, 4), stone, .65f);
        moving.transform.position += Vector3.right * 3;
        yield return Wait(.8f);
        Check(player.currentHealth == 80, "Stone snapshot trajectory does not home toward player");

        stone.windup = .2f; runtime.Equip(stone); moving.transform.position = Vector3.forward * 4; go.transform.rotation = Quaternion.identity;
        ResetAttack(enemy); yield return Wait(.1f);
        moving.transform.position += Vector3.right * 3;
        yield return Wait(.95f);
        Check(player.currentHealth == 80 && runtime.Current == null, "Throw commits aim before windup and misses a preparation dodge");
        enemy.PausePatrol();

        var torch = Weapon(EnemyWeaponSO.Loadout.Torch);
        torch.preparation = torch.windup = .05f; torch.burnDuration = .6f; torch.burnDamagePerSecond = 2;
        runtime.Equip(torch); moving.transform.position = Vector3.forward; go.transform.rotation = Quaternion.identity;
        ResetAttack(enemy); yield return Wait(.25f);
        Check(effects.IsBurning && player.currentHealth < 70 && player.currentHealth > 69, "Torch hit starts one burn effect");
        enemy.PausePatrol();
        effects.Ignite(.6f, 2, null); float before = player.currentHealth;
        yield return Wait(.25f);
        Check(before - player.currentHealth > .3f && before - player.currentHealth < .8f && moving.GetComponents<EnemyPlayerEffects>().Length == 1,
            "Repeated ignition refreshes expiry without multiplying burn DPS");
        yield return Wait(.55f);
        Check(!effects.IsBurning && effects.MovementMultiplier == 1, "Burn ends without locking movement");
        player.IsCutsceneInputLocked = true;
        Check(!effects.TryHit(50, .65f), "Cutscene-locked target rejects Lost hits");
        player.IsCutsceneInputLocked = false;
        float healthBeforeBlocked = player.currentHealth;
        player.CheatInfiniteHealth = true;
        Check(!effects.TryHit(10, .65f, 1, .5f) && effects.ProtectionRemaining == 0 && effects.MovementMultiplier == 1 && player.currentHealth == healthBeforeBlocked, "Blocked damage creates no hit protection or restraint");
        player.CheatInfiniteHealth = false;
        Check(!effects.TryHit(-10, .65f) && !effects.TryHit(float.NaN, .65f) && player.currentHealth == healthBeforeBlocked, "Invalid damage never heals or counts as a hit");
        Check(effects.TryHit(1, .05f) && player.currentHealth == healthBeforeBlocked - 1, "Damage succeeds immediately after invincibility ends");
        yield return Wait(.06f);
        moving.transform.position = Vector3.back * 3; go.transform.rotation = Quaternion.identity;
        enemy.StartPatrol();
        GameplayNoise.Emit(moving.transform.position, 25, moving.transform, GameplayNoise.Kind.Gunshot);
        yield return Wait(.08f);
        GameplayNoise.Emit(moving.transform.position, 25, moving.transform, GameplayNoise.Kind.Gunshot);
        yield return Wait(.08f);
        GameplayNoise.Emit(moving.transform.position, 25, moving.transform, GameplayNoise.Kind.Gunshot);
        yield return Wait(.16f);
        Check(enemy.CurrentState == EnemyController.EnemyState.Search && !enemy.isAlert, "Repeated hearing events do not restart Detect forever");
        moving.transform.position = Vector3.back * 5;
        yield return Wait(.1f);
        Check(Vector3.Distance(go.GetComponent<NavMeshAgent>().destination, Vector3.back * 3) < .2f, "Hearing follows the emitted point, not hidden live player position");
        enemy.PausePatrol();
        moving.transform.position = Vector3.zero;
        profile.fieldOfView = 30;
        profile.pressureRingRadius = 1.8f;
        profile.pressureWaitTimeout = 1.2f;
        profile.pressureRetreatCooldown = 2;
        profile.pressureOrbitSpeed = .6f;
        go.GetComponent<NavMeshAgent>().Warp(Vector3.back * 1.8f);
        go.transform.rotation = Quaternion.identity;
        enemy.StartPatrol();
        enemy.AlertTo(moving.transform);
        typeof(EnemyController).GetField("nextTurnRequest", Hidden).SetValue(enemy, Time.time + 10);
        yield return Wait(.35f);
        Check(enemy.CurrentState == EnemyController.EnemyState.Pressure && !enemy.HasAttackTurn, "Waiting without a turn stays in Pressure");
        Check(!go.GetComponent<NavMeshAgent>().updateRotation && Vector3.Dot(go.transform.forward, (moving.transform.position - go.transform.position).normalized) > .85f,
            "Pressure faces player independently of orbit movement");
        profile.pressureOrbitMode = EnemyDataSO.PressureOrbitMode.Clockwise;
        Call(enemy, "UpdatePressureDirection");
        var radial = Vector3.ProjectOnPlane(go.transform.position - moving.transform.position, Vector3.up);
        Call(enemy, "SetPressureDestination");
        yield return Wait(.05f); // NavMesh commits requested destinations asynchronously.
        Check(enemy.PressureOrbitDirection == 1 && Vector3.SignedAngle(radial, go.GetComponent<NavMeshAgent>().destination - moving.transform.position, Vector3.up) > 5,
            "Clockwise Pressure generates a positive orbit path");
        profile.pressureOrbitMode = EnemyDataSO.PressureOrbitMode.CounterClockwise;
        Call(enemy, "UpdatePressureDirection"); Call(enemy, "SetPressureDestination");
        yield return Wait(.05f);
        Check(enemy.PressureOrbitDirection == -1 && Vector3.SignedAngle(radial, go.GetComponent<NavMeshAgent>().destination - moving.transform.position, Vector3.up) < -5,
            "Counter-clockwise Pressure generates the opposite orbit path");
        profile.pressureOrbitMode = EnemyDataSO.PressureOrbitMode.Random;
        var savedRandom = UnityEngine.Random.state; UnityEngine.Random.InitState(42);
        bool clockwiseSeen = false, counterSeen = false;
        for (int sample = 0; sample < 20; sample++)
        {
            typeof(EnemyController).GetField("pressureOrbitActive", Hidden).SetValue(enemy, false);
            Call(enemy, "UpdatePressureDirection");
            clockwiseSeen |= enemy.PressureOrbitDirection > 0; counterSeen |= enemy.PressureOrbitDirection < 0;
        }
        Check(clockwiseSeen && counterSeen, "Random Pressure entry can select either orbit direction");
        int heldDirection = enemy.PressureOrbitDirection;
        float heldDeadline = (float)typeof(EnemyController).GetField("nextPressureDirectionTime", Hidden).GetValue(enemy);
        Call(enemy, "UpdatePressureDirection");
        Check(enemy.PressureOrbitDirection == heldDirection && (float)typeof(EnemyController).GetField("nextPressureDirectionTime", Hidden).GetValue(enemy) == heldDeadline,
            "Random direction remains committed instead of rerolling each frame");
        UnityEngine.Random.state = savedRandom;
        Check(Vector3.Distance(go.GetComponent<NavMeshAgent>().destination, moving.transform.position) > 1.4f &&
            Vector3.Distance(go.GetComponent<NavMeshAgent>().destination, moving.transform.position) < 2.2f, "Pressure destination uses configured close ring rather than weapon range");
        go.transform.rotation = Quaternion.LookRotation(Vector3.right);
        Call(enemy, "Update");
        Check(enemy.CurrentState == EnemyController.EnemyState.Pressure, "Tangential facing does not incorrectly change visible Pressure to Search");
        yield return Wait(1.05f);
        Check(!enemy.isAlert && !enemy.HasAttackTurn && enemy.CurrentState != EnemyController.EnemyState.Pressure,
            "Pressure timeout withdraws and prevents immediate sight reacquisition");
        var groupObject = new GameObject("Chase group"); groupObject.SetActive(false);
        var group = groupObject.AddComponent<EnemyGroup>();
        group.sceneEnemies = new[] { enemy };
        groupObject.SetActive(true);
        group.EnableAggressive();
        profile.detectionRadius = .2f;
        go.transform.rotation = Quaternion.LookRotation(Vector3.back);
        Call(enemy, "Update");
        Check(enemy.IsAggressive && enemy.isAlert && enemy.CurrentState == EnemyController.EnemyState.Pressure,
            "Group Aggressive skips Detect and tracks player outside vision range");
        yield return Wait(1.35f);
        Check(enemy.isAlert && enemy.CurrentState == EnemyController.EnemyState.Pressure,
            "Aggressive pressure never withdraws on wait timeout");
        enemy.PausePatrol();
        group.EnableAggressive();
        Call(enemy, "Update");
        Check(!enemy.IsPatrolRunning && enemy.CurrentState == EnemyController.EnemyState.Idle,
            "Group Aggressive does not start paused cutscene actors");
        enemy.StartPatrol();
        var aggressiveWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        aggressiveWall.layer = 7; aggressiveWall.transform.position = new Vector3(0, 1, -.7f);
        aggressiveWall.transform.localScale = new Vector3(5, 3, .2f);
        profile.obstacleMask = 1 << 7;
        go.GetComponent<NavMeshAgent>().Warp(Vector3.back * 1.4f);
        Physics.SyncTransforms();
        Call(enemy, "Update");
        Check(enemy.isAlert && !(bool)typeof(EnemyController).GetMethod("InAttackRange", Hidden).Invoke(enemy, null),
            "Aggressive tracks through occlusion but melee cannot hit through walls");
        UnityEngine.Object.Destroy(aggressiveWall);
        var pointObject = new GameObject("Aggressive spawn test");
        pointObject.transform.position = Vector3.left * 5;
        var point = pointObject.AddComponent<EnemySpawnPoint>();
        point.spawnOnStart = false; point.spawnRadius = 0; point.enemyPrefab = enemy;
        Check(point.SelectPrefab() == enemy, "Existing spawn points keep single-prefab selection");
        var alternateObject = new GameObject("Alternate random prefab"); alternateObject.SetActive(false);
        var alternate = alternateObject.AddComponent<EnemyController>();
        var alternateProfile = ScriptableObject.CreateInstance<EnemyDataSO>(); alternate.enemyData = alternateProfile;
        point.prefabSelection = EnemySpawnPoint.PrefabSelection.Random;
        point.enemyPrefabs = new[] { enemy, alternate, null };
        var selectionRandomState = UnityEngine.Random.state; UnityEngine.Random.InitState(719);
        bool selectedOriginal = false, selectedAlternate = false;
        for (int index = 0; index < 64; index++)
        {
            var chosen = point.SelectPrefab();
            Check(chosen == enemy || chosen == alternate, "Random selection returns a valid configured prefab");
            selectedOriginal |= chosen == enemy; selectedAlternate |= chosen == alternate;
        }
        UnityEngine.Random.state = selectionRandomState;
        Check(selectedOriginal && selectedAlternate && alternate.enemyData == alternateProfile && enemy.enemyData == profile,
            "Random pool selects both variants without changing their profiles");
        alternate.enemyData = null;
        point.enemyPrefabs = new[] { null, alternate };
        Check(point.SelectPrefab() == null, "Invalid random pool does not silently use the hidden Single prefab");
        point.enemyPrefabs = new[] { null, alternate, enemy };
        Check(point.SelectPrefab() == enemy, "Random pool skips entries missing profiles");
        UnityEngine.Object.Destroy(alternateObject); UnityEngine.Object.Destroy(alternateProfile);
        var spawnCamera = new GameObject("Spawn visibility camera").AddComponent<Camera>();
        spawnCamera.transform.position = point.transform.position + Vector3.back * 8 + Vector3.up;
        spawnCamera.transform.rotation = Quaternion.identity; spawnCamera.fieldOfView = 60;
        point.visibilityCamera = spawnCamera; point.spawnOutOfView = true;
        Check(!point.IsPositionOutOfView(point.transform.position), "Hidden spawn rejects a body visible in front of the gameplay camera");
        Vector3 edgePosition = spawnCamera.ViewportToWorldPoint(new Vector3(1.01f, .5f, 8)); edgePosition.y = point.transform.position.y;
        Check(!point.IsPositionOutOfView(edgePosition), "Hidden spawn checks body bounds instead of permitting feet just outside the viewport");
        int spawnBudgetBefore = point.SpawnedCount;
        Check(point.SpawnOne() == null && point.SpawnedCount == spawnBudgetBefore && point.AliveCount == 0,
            "A visible spawn attempt creates no enemy and consumes no spawn budget");
        spawnCamera.enabled = false;
        Check(!point.IsPositionOutOfView(point.transform.position), "Missing/inactive visibility camera fails closed");
        spawnCamera.enabled = true; spawnCamera.transform.rotation = Quaternion.Euler(0, 180, 0);
        Check(point.IsPositionOutOfView(point.transform.position), "A point behind the gameplay camera is eligible");
        var hiddenSpawn = point.SpawnOne();
        Check(hiddenSpawn != null && point.SpawnedCount == spawnBudgetBefore + 1, "Looking away allows a real NavMesh hidden spawn");
        UnityEngine.Object.Destroy(hiddenSpawn.gameObject); UnityEngine.Object.Destroy(spawnCamera.gameObject);
        point.spawnOutOfView = false; point.visibilityCamera = null;
        yield return null;
        point.enemyGroup = group; point.aggressionMode = EnemySpawnPoint.AggressionMode.Normal;
        var spawned = point.SpawnOne();
        Check(spawned != null && spawned.SpawnSourceName == point.name && spawned.SpawnAge >= 0 && spawned.SpawnAge < 1,
            "Spawn diagnostics record source point and actor age to distinguish new spawns from rendering re-entry");
        Check(spawned != null && spawned.IsAggressive && !profile.aggressiveOnStart,
            "Future spawn inherits current group mode above spawn override without mutating profile");
        group.DisableAggressive();
        Check(!enemy.IsAggressive && !spawned.IsAggressive, "Disabling group updates existing and spawned members");
        spawned.PausePatrol();
        point.enemyGroup = null; point.aggressionMode = EnemySpawnPoint.AggressionMode.Aggressive;
        Vector3 rootBeforeDiagnostic = spawned.transform.position;
        typeof(EnemyController).GetField("createdAt", Hidden).SetValue(spawned, Time.time - 1);
        typeof(EnemyController).GetField("lastAnimationPosition", Hidden).SetValue(spawned, rootBeforeDiagnostic - Vector3.right * 3);
        Call(spawned, "LateUpdate");
        Check(spawned.HasRecentRootJump && spawned.LastRootJumpDistance >= 2.9f && spawned.transform.position == rootBeforeDiagnostic,
            "Root discontinuity diagnostics distinguish a jump without moving or correcting the actor");
        var standaloneSpawn = point.SpawnOne();
        Check(standaloneSpawn != null && standaloneSpawn.IsAggressive, "Spawn Aggressive override works without a group");
        standaloneSpawn.PausePatrol();
        profile.detectionRadius = 10;
        profile.aggressiveOnStart = true;
        point.aggressionMode = EnemySpawnPoint.AggressionMode.UseProfile;
        var profileSpawn = point.SpawnOne();
        Check(profileSpawn != null && profileSpawn.IsAggressive, "Spawn UseProfile inherits Aggressive default");
        profileSpawn.PausePatrol();
        profile.aggressiveOnStart = false;
        enemy.SetAggressive(true);
        enemy.StartPatrol();
        go.GetComponent<NavMeshAgent>().Warp(Vector3.zero);
        moving.transform.position = Vector3.forward * 10;
        profile.useChaseRhythm = true;
        profile.useApproachDecisions = false;
        profile.chaseWalkDuration = profile.chaseBoostDuration = Vector2.one * .3f;
        profile.walkSpeed = .5f; profile.chaseSpeed = 2f; profile.burstSpeedMultiplier = 1.3f;
        profile.speedChangeRate = 100;
        typeof(EnemyController).GetField("nextTurnRequest", Hidden).SetValue(enemy, Time.time + 10);
        yield return Wait(.15f);
        Check(!enemy.IsChaseBoosting && enemy.CurrentState == EnemyController.EnemyState.Chase && go.GetComponent<NavMeshAgent>().speed < .6f,
            "Chase rhythm begins with ordinary walk speed");
        yield return Wait(.22f);
        Check(enemy.IsChaseBoosting && enemy.CurrentState == EnemyController.EnemyState.ChaseBurst && go.GetComponent<NavMeshAgent>().speed > 2.4f,
            "Chase enters a timed speed-boosted burst");
        yield return Wait(.3f);
        Check(!enemy.IsChaseBoosting && go.GetComponent<NavMeshAgent>().speed < .6f,
            "Chase burst returns to walking after its duration");
        profile.useChaseRhythm = false;
        yield return Wait(.1f);
        Check(!enemy.IsChaseBoosting && Mathf.Abs(go.GetComponent<NavMeshAgent>().speed - 2f) < .05f,
            "Disabling rhythm restores continuous chase speed");
        profile.useChaseRhythm = true;
        moving.transform.position = go.transform.position + Vector3.forward * 2;
        yield return Wait(.1f);
        Check(!enemy.IsChaseBoosting && enemy.CurrentState == EnemyController.EnemyState.Pressure &&
            Mathf.Abs(go.GetComponent<NavMeshAgent>().speed - profile.pressureOrbitSpeed) < .05f,
            "Close Pressure keeps its orbit speed and clears chase boost");
        enemy.PausePatrol();
        var rushOwners = new[] { enemy, spawned, standaloneSpawn, profileSpawn };
        yield return PressureChecks(timingSettings, player, profile, rushOwners);
        yield return HandoffChecks(timingSettings, player, profile, new[] { enemy, spawned, profileSpawn });
        yield return FrontInterceptChecks(timingSettings, player, profile, enemy, spawned);
        yield return CloseSpeedChecks(timingSettings, player, profile, enemy, spawned);
        yield return PressureApproachChecks(timingSettings, player, profile, enemy);
        foreach (var candidate in rushOwners) candidate.PausePatrol();
        player.currentHealth = 100;
        player.IsCutsceneInputLocked = false;
        effects.enabled = false; effects.enabled = true;
        timingSettings.chargePostHoldGrace = .12f;
        rushOwners[0].StartPatrol();
        effects.BeginChargeLock(.15f);
        Check(effects.IsChargeLocked && effects.BlocksGroupAttack, "Charge lock blocks the target encounter");
        Check(!EnemyAttackCoordinator.TryStartAttack(rushOwners[0], player.transform), "Charge lock rejects attack starts");
        Check(!effects.TryHit(10, 0), "Already winding attacks cannot damage a charge-locked player");
        effects.EndChargeLock();
        Check(!effects.IsChargeLocked && effects.BlocksGroupAttack, "End event retains a grace period");
        yield return Wait(.16f);
        Check(!effects.BlocksGroupAttack, "Event completion restores attack eligibility after grace");
        effects.BeginChargeLock(.08f);
        yield return Wait(.24f);
        Check(!effects.IsChargeLocked && !effects.BlocksGroupAttack, "Missing event completion has a finite lock fallback");

        typeof(EnemyRushCoordinator).GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        // Drive the production coroutine on a real NavMesh: finite miss, recovery,
        // fixed destination and locomotion state are independent of animation assets.
        timingSettings.useRushSlots = true;
        timingSettings.enableCharge = true;
        timingSettings.attackStartInterval = 0;
        enemy.enemyData.charge.preparation = .1f;
        enemy.enemyData.charge.duration = .2f;
        enemy.enemyData.charge.recovery = .1f;
        enemy.enemyData.charge.speed = 1;
        Check(enemy.EffectiveChargeDistance < 4f, "Charge range excludes targets beyond acceleration-aware travel budget");
        enemy.enemyData.charge.selectionChance = 0;
        enemy.enemyData.charge.reserveForMidRange = false;
        enemy.enemyData.charge.duration = .6f;
        enemy.enemyData.charge.speed = 8;
        enemy.GetComponent<NavMeshAgent>().acceleration = 40;
        typeof(EnemyController).GetField("nextChargeDecision", Hidden).SetValue(enemy, 0f);
        Check(!(bool)typeof(EnemyController).GetMethod("ChooseCharge", Hidden).Invoke(enemy, null), "Zero chance chooses ordinary approach");
        enemy.enemyData.charge.selectionChance = 1;
        Check(!(bool)typeof(EnemyController).GetMethod("ChooseCharge", Hidden).Invoke(enemy, null), "A rejected decision is cached instead of rerolled each frame");
        typeof(EnemyController).GetField("nextChargeDecision", Hidden).SetValue(enemy, 0f);
        bool savedLost = enemy.enemyData.useLostCombat;
        bool savedCoordination = enemy.enemyData.coordinateAttacks;
        enemy.enemyData.useLostCombat = false;
        enemy.enemyData.coordinateAttacks = true;
        enemy.enemyData.maxConcurrentAttackers = 2;
        player.transform.position = Vector3.zero;
        player.IsSprinting = true; player.PlanarVelocity = Vector3.back * 4;
        spawned.GetComponent<NavMeshAgent>().Warp(Vector3.forward * 8);
        spawned.StartPatrol(); spawned.SetAggressive(true);
        typeof(EnemyController).GetField("playerDetected", Hidden).SetValue(spawned, true);
        enemy.GetComponent<NavMeshAgent>().Warp(Vector3.back * 4);
        enemy.StartPatrol(); enemy.SetAggressive(true);
        typeof(EnemyController).GetField("playerDetected", Hidden).SetValue(enemy, true);
        typeof(EnemyController).GetField("attackTimer", Hidden).SetValue(enemy, 0f);
        typeof(EnemyController).GetField("nextTurnRequest", Hidden).SetValue(enemy, 0f);
        typeof(EnemyController).GetField("turnDeadline", Hidden).SetValue(enemy, Time.time + 10);
        Check(!EnemyRushCoordinator.IsEscapePaused(player.transform) && enemy.CanDefendDuringEscape, "Incoming sprint permits local defense while another threat pauses group pursuit");
        EnemyAttackCoordinator.Request(enemy, player.transform, 2);
        Check(enemy.HasRushSlot, "Incoming sprint grants a front approach slot before melee range");
        enemy.enemyData.charge.reserveForMidRange = true;
        Check((bool)typeof(EnemyController).GetMethod("ChooseCharge", Hidden).Invoke(enemy, null), "Close-role preference never prohibits a forced charge inside the valid band");
        enemy.GetComponent<NavMeshAgent>().Warp(Vector3.back * 4.25f);
        Check((bool)typeof(EnemyController).GetMethod("ChooseCharge", Hidden).Invoke(enemy, null), "Pressure ownership does not prohibit Charge outside the close-approach band");
        enemy.GetComponent<NavMeshAgent>().Warp(Vector3.back * 4f);
        Check((bool)typeof(EnemyController).GetMethod("ChooseCharge", Hidden).Invoke(enemy, null), "Accepted Charge remains selected after entering the close-role band while waiting for a turn");
        enemy.enemyData.charge.reserveForMidRange = false;
        Check((bool)typeof(EnemyController).GetMethod("CanStartCharge", Hidden).Invoke(enemy, new object[] { 4f }), "Front interception can acquire a charge attack turn before melee range");
        Call(enemy, "Update");
        IEnumerator charge;

        Check(enemy.CurrentState == EnemyController.EnemyState.ChargePreparation && enemy.isAttacking,
            "Charge begins with a stationary telegraph");
        spawned.StartCoroutine((IEnumerator)typeof(EnemyController).GetMethod("PerformCharge", Hidden).Invoke(spawned, null));
        Check(spawned.CurrentState != EnemyController.EnemyState.ChargePreparation && spawned.CurrentState != EnemyController.EnemyState.Charge,
            "A second enemy cannot prepare a charge at the same target simultaneously");
        yield return Wait(.14f);
        Check(enemy.CurrentState == EnemyController.EnemyState.Charge, "Telegraph enters finite charge movement");
        Vector3 committed = enemy.GetComponent<NavMeshAgent>().destination;
        player.transform.position = Vector3.right * 3;
        yield return Wait(.05f);
        Check(Vector3.Distance(committed, enemy.GetComponent<NavMeshAgent>().destination) < .05f,
            "Charge does not home after player dodges");
        yield return Wait(.8f);
        Check(!enemy.isAttacking, "Missed charge completes recovery and releases attack action");
        enemy.PausePatrol(); spawned.PausePatrol();
        player.IsSprinting = false; player.PlanarVelocity = Vector3.zero;
        timingSettings.useRushSlots = false;
        enemy.enemyData.coordinateAttacks = false;
        effects.enabled = false; effects.enabled = true;
        player.currentHealth = 100;
        player.transform.position = Vector3.zero;
        enemy.GetComponent<NavMeshAgent>().Warp(Vector3.back * .7f);
        enemy.StartPatrol(); enemy.SetAggressive(true);
        int chargeHits = 0;
        UnityEngine.Events.UnityAction chargeListener = () => chargeHits++;
        enemy.onChargeHit.AddListener(chargeListener);
        charge = (IEnumerator)typeof(EnemyController).GetMethod("PerformCharge", Hidden).Invoke(enemy, null);
        enemy.StartCoroutine(charge);
        yield return Wait(.8f);
        Check(chargeHits == 1 && player.currentHealth < 100, "Confirmed charge hit invokes the event once");
        Check(effects.IsChargeLocked && effects.MovementMultiplier == 0, "Confirmed charge locks movement while preserving camera input");
        enemy.onChargeHit.RemoveListener(chargeListener);
        enemy.PausePatrol();
        effects.EndChargeLock();
        yield return Wait(.16f);
        timingSettings.useRushSlots = true;
        enemy.enemyData.coordinateAttacks = true;
        enemy.enemyData.charge.reserveForMidRange = true;
        enemy.enemyData.charge.selectionChance = 1;
        enemy.enemyData.charge.duration = 1.2f;
        enemy.enemyData.charge.speed = 4;
        player.PlanarVelocity = Vector3.zero; player.IsSprinting = false;
        player.transform.position = Vector3.zero;
        yield return Wait(1f);
        enemy.GetComponent<NavMeshAgent>().Warp(Vector3.back * 4.5f);
        enemy.StartPatrol(); enemy.SetAggressive(true);
        foreach (string field in new[] { "nextChargeTime", "nextChargeDecision", "attackTimer", "nextTurnRequest" })
            typeof(EnemyController).GetField(field, Hidden).SetValue(enemy, 0f);
        Call(enemy, "Update");
        Check(enemy.CurrentState == EnemyController.EnemyState.ChargePreparation,
            "Normal production Update selects and starts mid-range Charge with reserve enabled, without manually invoking the coroutine: " + enemy.ChargeStatus);
        enemy.PausePatrol();
        enemy.enemyData.useLostCombat = savedLost;
        enemy.enemyData.coordinateAttacks = savedCoordination;

        yield return SavedChargeChecks(timingSettings, player, enemy);
        timingSettings.enableCharge = false;
        timingSettings.useRushSlots = false;
        // Two eligible owners may approach, but cannot start Preparation together.
        moving.transform.position = Vector3.forward * 20;
        enemy.StartPatrol(); spawned.StartPatrol();
        timingSettings.attackStartInterval = .8f;
        var timingTarget = new GameObject("Shared cadence target");
        var independentTarget = new GameObject("Independent cadence target");
        Check(EnemyAttackCoordinator.TryStartAttack(enemy, timingTarget.transform), "First attacker reserves the shared start timestamp");
        Check(!EnemyAttackCoordinator.TryStartAttack(spawned, timingTarget.transform) && EnemyAttackCoordinator.StartDelay(timingTarget.transform) > .7f,
            "Second attacker cannot start in the same frame even with another turn slot");
        EnemyAttackCoordinator.Release(enemy);
        Check(!EnemyAttackCoordinator.TryStartAttack(spawned, timingTarget.transform), "Turn release or cancellation cannot bypass the start gap");
        Check(EnemyAttackCoordinator.TryStartAttack(spawned, independentTarget.transform), "Separate targets do not block one another's attack starts");
        yield return Wait(.82f);
        Check(EnemyAttackCoordinator.TryStartAttack(spawned, timingTarget.transform), "Another attacker may start after the shared interval expires");
        timingSettings.attackStartInterval = 0;
        Check(EnemyAttackCoordinator.TryStartAttack(enemy, independentTarget.transform) && EnemyAttackCoordinator.TryStartAttack(spawned, independentTarget.transform),
            "Zero interval allows back-to-back starts once the prior deadline has expired");
        enemy.PausePatrol(); spawned.PausePatrol();
        UnityEngine.Object.Destroy(timingTarget); UnityEngine.Object.Destroy(independentTarget);
        enemy.BeginDeath();
        Check(enemy.IsDead && !enemy.HasAttackTurn && runtime.Current == null, "Death clears module and group reservation");
    }
}

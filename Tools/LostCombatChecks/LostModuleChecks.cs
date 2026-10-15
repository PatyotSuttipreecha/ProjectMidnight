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
        foreach (string name in new[] { "Unarmed", "Machete", "Log", "HeavyAxe", "Stone", "Torch" })
        {
            var imported = AssetDatabase.LoadAssetAtPath<EnemyDataSO>("Assets/TheLostPresets/Data_Enemies_TheLost_" + name + ".asset");
            Check(imported != null && imported.useLostCombat && imported.maxConcurrentAttackers == 1, "Imported " + name + " prototype profile");
            Check(name == "Unarmed" ? imported.startingWeapon == null : imported.startingWeapon != null && imported.startingWeapon.loadout.ToString() == name,
                "Imported " + name + " module reference");
            Check(imported.locomotionBlendParameter == speedTree.blendParameter, name + " profile matches actual controller speed parameter");
            if (name == "Machete") Check(imported.autoAnimationReferenceSpeeds && EnemyLocomotionAnimator.BlendForMovement(.6f, imported) >= .5f,
                "Existing Machete profile uses automatic references even with saved custom walk speed 2");
        }
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
        point.enemyGroup = group; point.aggressionMode = EnemySpawnPoint.AggressionMode.Normal;
        var spawned = point.SpawnOne();
        Check(spawned != null && spawned.IsAggressive && !profile.aggressiveOnStart,
            "Future spawn inherits current group mode above spawn override without mutating profile");
        group.DisableAggressive();
        Check(!enemy.IsAggressive && !spawned.IsAggressive, "Disabling group updates existing and spawned members");
        spawned.PausePatrol();
        point.enemyGroup = null; point.aggressionMode = EnemySpawnPoint.AggressionMode.Aggressive;
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
        enemy.BeginDeath();
        Check(enemy.IsDead && !enemy.HasAttackTurn && runtime.Current == null, "Death clears module and group reservation");
    }
}

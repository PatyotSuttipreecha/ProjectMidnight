using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// One editor layout shared by the asset Inspector and every Enemy Lab profile view.
public sealed class EnemyProfileGUI
{
    public static readonly string[] Categories = { "Overview", "Movement", "Animation", "Senses", "Group", "Combat", "Physics", "Loot", "All" };
    public static readonly string[][] Fields = {
        new[] { "useSharedSettings", "startingHealth", "useLostCombat", "aggressiveOnStart" },
        new[] { "walkSpeed", "waitTimeAtPoint", "chaseSpeed", "closeApproachSpeed", "chaseSlowdownDistance", "burstSpeedMultiplier", "useChaseRhythm", "chaseWalkDuration", "chaseBoostDuration", "speedChangeRate", "acceleration", "turnSpeed", "stoppingDistance", "chaseRepathInterval" },
        new[] { "useLocomotionBlendTree", "autoAnimationReferenceSpeeds", "keepWalkBlendWhileMoving", "animationStopSpeed", "animationWalkSpeed", "animationRunSpeed", "locomotionDamping", "locomotionTransition", "locomotionState", "locomotionBlendParameter", "locomotionHorizontalParameter", "locomotionVerticalParameter", "locomotionAttackParameter", "locomotionAttackState", "runAnimation" },
        new[] { "detectionRadius", "fieldOfView", "obstacleMask", "eyeHeight", "playerTargetHeight", "detectAnticipation", "lostSightCooldown", "canHear", "hearingMultiplier", "walkHearingRadius", "runHearingRadius", "gunshotHearingRadius", "noiseSearchDuration", "noiseTravelTimeout" },
        new[] { "coordinateAttacks", "maxConcurrentAttackers", "engagementDistance", "pressureRingRadius", "pressureOrbitSpeed", "pressureOrbitStep", "pressureOrbitMode", "pressureDirectionDuration", "pressureWaitTimeout", "pressureRetreatCooldown", "attackTurnTimeout", "attackTurnRest", "useApproachDecisions", "approachDecisionInterval", "approachWeights", "flankAngle", "pressureSpeedMultiplier", "spreadChaseTargets", "chaseApproachRadius", "varyAvoidancePriority", "separateNearbyEnemies", "separationRadius", "separationSpeed" },
        new[] { "startingWeapon", "minAttack", "maxAttack", "attackRange", "attackPreparationTime", "advanceDuringPreparation", "attackWindup", "meleeAttackStep", "attackCooldown", "charge", "baseHitArc", "baseRecovery", "baseAttackAnimation", "restraintDuration", "restraintSpeedMultiplier", "playerHitProtection" },
        new[] { "actionBreak", "hitKnockbackDistance", "hitKnockbackDuration", "hitStaggerDuration", "enableHitReaction", "hitPushImpulse", "hitPhysicsDuration", "hitRecoveryDuration", "hitReactionBlend", "hitAnimationSupport", "hitMaxAngle", "useRagdoll", "deathAnimation", "deathAnimationLeadIn", "deathBlendDuration", "deathAnimationDrive", "hybridDeathPushScale", "corpseLifetime", "ragdollGravityScale", "ragdollLinearDamping", "ragdollAngularDamping", "deathPushImpulse", "corpseSettleDelay", "physicsMassMultiplier", "boneMassOverrides" },
        new[] { "lootDropChance", "lootTable", "lootPrefab", "lootOffset" }
    };
    private static readonly HashSet<string> advancedFields = new HashSet<string> {
        "chaseRepathInterval", "locomotionState", "locomotionBlendParameter", "locomotionHorizontalParameter", "locomotionVerticalParameter", "locomotionAttackParameter", "locomotionAttackState",
        "noiseTravelTimeout", "flankAngle", "pressureSpeedMultiplier", "chaseApproachRadius", "varyAvoidancePriority", "hitAnimationSupport", "hitMaxAngle", "deathAnimationDrive", "boneMassOverrides"
    };
    private int category;
    private string search = "";
    private bool advanced, weaponExpanded = true, hiddenExpanded;
    public static bool IsVisible(string field, EnemyDataSO p, bool showAdvanced)
    {
        if (!showAdvanced && advancedFields.Contains(field)) return false;
        if (field == "runAnimation") return !p.useLocomotionBlendTree;
        if (field.StartsWith("locomotion", StringComparison.Ordinal) || field.StartsWith("animation", StringComparison.Ordinal) || field == "autoAnimationReferenceSpeeds" || field == "keepWalkBlendWhileMoving")
        {
            if (!p.useLocomotionBlendTree) return false;
            if ((field == "animationWalkSpeed" || field == "animationRunSpeed") && p.autoAnimationReferenceSpeeds) return false;
        }
        if ((field == "chaseWalkDuration" || field == "chaseBoostDuration") && !p.useChaseRhythm) return false;
        if (field == "pressureSpeedMultiplier" && p.useChaseRhythm) return false;
        if ((field == "approachDecisionInterval" || field == "approachWeights" || field == "flankAngle" || field == "pressureSpeedMultiplier") && !p.useApproachDecisions) return false;
        if (field == "chaseApproachRadius" && !p.spreadChaseTargets) return false;
        if (field == "pressureDirectionDuration" && p.pressureOrbitMode != EnemyDataSO.PressureOrbitMode.Random) return false;
        if ((field == "separationRadius" || field == "separationSpeed") && !p.separateNearbyEnemies) return false;
        if ((field.StartsWith("pressure", StringComparison.Ordinal) && field != "pressureSpeedMultiplier" || field == "maxConcurrentAttackers" || field == "engagementDistance" || field.StartsWith("attackTurn", StringComparison.Ordinal)) && !p.coordinateAttacks) return false;
        if ((field == "hearingMultiplier" || field.EndsWith("HearingRadius", StringComparison.Ordinal) || field.StartsWith("noise", StringComparison.Ordinal)) && !p.canHear) return false;
        if ((field == "startingWeapon" || field == "detectAnticipation" || field.StartsWith("base", StringComparison.Ordinal) || field.StartsWith("restraint", StringComparison.Ordinal) || field == "playerHitProtection") && !p.useLostCombat) return false;
        if ((field == "meleeAttackStep" || field == "advanceDuringPreparation") && !p.useLostCombat) return false;
        if (field == "attackCooldown" && p.useLostCombat) return false;
        if (field == "restraintSpeedMultiplier" && p.restraintDuration <= 0) return false;
        if (new[] { "hitPushImpulse", "hitPhysicsDuration", "hitRecoveryDuration", "hitReactionBlend", "hitAnimationSupport", "hitMaxAngle" }.Contains(field) && !p.enableHitReaction) return false;
        if (new[] { "deathBlendDuration", "deathAnimationDrive", "hybridDeathPushScale", "ragdollGravityScale", "ragdollLinearDamping", "ragdollAngularDamping", "deathPushImpulse", "corpseSettleDelay" }.Contains(field) && !p.useRagdoll) return false;
        if ((field == "physicsMassMultiplier" || field == "boneMassOverrides") && !p.useRagdoll && !p.enableHitReaction) return false;
        return true;
    }
    public static List<string> Warnings(EnemyDataSO p)
    {
        var result = new List<string>();
        if (!p.useSharedSettings) result.Add("Legacy component tuning is active for health/navigation/physics. Promote the enemy to shared settings to tune those values here.");
        if (p.obstacleMask.value == 0) result.Add("Obstacle Mask is empty: sight and melee checks cannot block walls.");
        if (p.stoppingDistance >= p.attackRange) result.Add("Stopping Distance is at least Unarmed Attack Range; the agent may stop before it can hit after disarm.");
        if (p.useLocomotionBlendTree && !p.autoAnimationReferenceSpeeds && p.animationWalkSpeed > Mathf.Max(.01f, p.walkSpeed) * 1.5f)
            result.Add("Custom Walk Reference Speed is much higher than AI Walk Speed. Enable automatic references or align the two values.");
        if (p.useLocomotionBlendTree && !p.keepWalkBlendWhileMoving) result.Add("Full Walk while moving is disabled: slow movement will blend toward Idle.");
        if (p.animationStopSpeed >= p.walkSpeed && p.useLocomotionBlendTree) result.Add("Animation Stop Speed is at least Walk Speed; ordinary walking may look idle.");
        if (p.charge != null && p.charge.enabled)
        {
            float chargeSpeed = Mathf.Max(.1f, p.charge.speed);
            float chargeDuration = Mathf.Max(.1f, p.charge.duration);
            float acceleration = Mathf.Max(.1f, p.acceleration);
            float accelerating = Mathf.Min(chargeDuration, chargeSpeed / acceleration);
            float reach = p.useLostCombat && p.startingWeapon != null ? p.startingWeapon.range : p.attackRange;
            float reachable = Mathf.Min(p.charge.maxDistance, .5f * acceleration * accelerating * accelerating + chargeSpeed * (chargeDuration - accelerating) + reach - .35f);
            if (reachable < Mathf.Max(reach + .3f, p.charge.minDistance)) result.Add("Charge cannot start: reachable maximum " + reachable.ToString("0.00") + " m is below its minimum. Increase Charge Duration or reduce Min Distance.");
            if (p.charge.selectionChance <= 0) result.Add("Charge Selection Chance is zero: this profile always chooses ordinary approach.");
            if (p.charge.maxDistance > p.charge.speed * p.charge.duration + p.attackRange - .35f) result.Add("Charge Max Distance exceeds its travel budget. Runtime limits the start range using speed, duration, acceleration and actual weapon reach.");
            if (p.charge.maxDistance <= p.charge.minDistance) result.Add("Charge Max Distance should exceed Min Distance and melee reach.");
            if (string.IsNullOrEmpty(p.charge.runAnimation)) result.Add("Charge Run Animation is empty: the charge uses the existing locomotion animation. Assign a dedicated layer-0 Animator state to make it visually distinct.");
        }
        if (p.coordinateAttacks && EnemyCombatSettings.RushEnabled &&
            p.engagementDistance < Mathf.Max(EnemyCombatSettings.PressureDistance, p.chaseSlowdownDistance, p.pressureRingRadius) + .5f)
            result.Add("Engagement Distance is inside the waiting ring. Detected enemies request the same combat turn from the ring boundary + 0.5 m; movement ownership does not gate that request.");
        if (p.minAttack > p.maxAttack) result.Add("Minimum Damage exceeds Maximum Damage.");
        if (!p.useLostCombat && p.startingWeapon != null) result.Add("Starting Weapon is assigned but Lost Combat is disabled; the module is not used.");
        if (p.chaseWalkDuration.x <= 0 || p.chaseWalkDuration.y < p.chaseWalkDuration.x || p.chaseBoostDuration.x <= 0 || p.chaseBoostDuration.y < p.chaseBoostDuration.x)
            result.Add("Chase duration ranges should have a positive minimum and maximum >= minimum.");
        return result;
    }
    public static List<string> AnimatorWarnings(Animator animator, EnemyDataSO profile)
    {
        var warnings = new List<string>();
        if (animator == null) { warnings.Add("Animator missing on the enemy root; locomotion cannot be driven."); return warnings; }
        var runtime = animator.runtimeAnimatorController;
        if (runtime is AnimatorOverrideController overrides) runtime = overrides.runtimeAnimatorController;
        var controller = runtime as UnityEditor.Animations.AnimatorController;
        if (controller == null || controller.layers.Length == 0) return warnings;
        var machine = controller.layers[0].stateMachine;
        foreach (var transition in machine.anyStateTransitions)
        {
            if (transition.conditions.Length == 0 && !transition.hasExitTime)
                warnings.Add("Unconditional Any State transition to " + (transition.destinationState != null ? transition.destinationState.name : "a state machine") + " can interrupt locomotion.");
            if (transition.canTransitionToSelf && transition.conditions.Any(condition => condition.parameter == profile.locomotionAttackParameter))
                warnings.Add("Any State attack transition can restart itself. Disable Can Transition To Self.");
            if (transition.conditions.Any(condition => condition.parameter == profile.locomotionAttackParameter))
                warnings.Add("A shared Any State attack entry can override Base/Weapon Module animation selection. This enemy uses script-selected attack states.");
        }
        if (profile.useLocomotionBlendTree)
        {
            if (!controller.parameters.Any(parameter => parameter.name == profile.locomotionBlendParameter && parameter.type == AnimatorControllerParameterType.Float))
                warnings.Add("Configured speed parameter '" + profile.locomotionBlendParameter + "' is missing. The adapter can fall back to Speed/Blend; update Advanced Animation bindings to match the controller.");
            var state = machine.states.FirstOrDefault(child => child.state.name == profile.locomotionState).state;
            if (state == null) warnings.Add("Configured Locomotion State was not found on the base layer. Check Advanced Animation bindings.");
            else if (state.motion is UnityEditor.Animations.BlendTree tree && tree.children.Length == 3 &&
                (Mathf.Abs(tree.children[1].threshold - .5f) > .001f || Mathf.Abs(tree.children[2].threshold - 1f) > .001f))
                warnings.Add("Locomotion thresholds differ from adapter mapping: Idle 0, Walk 0.5, Run 1.");
        }
        return warnings;
    }
    private static void DrawCharge(SerializedProperty charge)
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField(new GUIContent("Charge", charge.tooltip), EditorStyles.boldLabel);
        // Draw direct children without a parent PropertyField/foldout. Expansion
        // remains independent of Unity's saved serialized foldout state.
        var child = charge.Copy();
        var end = child.GetEndProperty();
        if (child.NextVisible(true))
            do
            {
                if (SerializedProperty.EqualContents(child, end)) break;
                EditorGUILayout.PropertyField(child, true);
            } while (child.NextVisible(false));
        EditorGUILayout.Space(8);
    }

    public void Draw(EnemyDataSO p)
    {
        if (p == null) return;
        category = EditorGUILayout.Popup("Settings category", category, Categories);
        if (category == 1 || category == 4 || category == 8)
        {
            EditorGUILayout.HelpBox("Group settings here define this profile’s orbit, separation and requested attack-turn cap. Shared Combat Timing controls random staggered runners, first-arrival pressure ownership, per-owner handoff and attack spacing across profiles. Walkers can arrive first and earn a slot. Owners keep their slots through melee. This mode overrides the saved Walk/Boost rhythm while enabled.", MessageType.Info);
            if (GUILayout.Button("Open Shared Combat Timing"))
                EnemyCombatSettingsWindow.Open();
        }
        search = EditorGUILayout.TextField("Find setting", search);
        advanced = EditorGUILayout.ToggleLeft("Show advanced settings", advanced);
        EditorGUILayout.HelpBox("Hidden fields keep their saved values. Enable the relevant feature or Show advanced settings to reveal them. See Docs/EnemyLabGuide.md for the visibility rules.", MessageType.None);
        foreach (var warning in Warnings(p)) EditorGUILayout.HelpBox(warning, MessageType.Warning);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            var data = new SerializedObject(p); data.Update();
            var drawn = new HashSet<string>();
            bool searching = !string.IsNullOrWhiteSpace(search);
            for (int group = 0; group < Fields.Length; group++)
            {
                if (!searching && category != 8 && category != group) continue;
                if (searching && !Fields[group].Any(field => {
                    var property = data.FindProperty(field);
                    return property != null && (property.displayName + " " + field + " " + property.tooltip).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
                })) continue;
                using (new ModificationGUI.Card(Categories[group]))
                {
                if (group == 5) EditorGUILayout.HelpBox("These are the unarmed/base attack values. Equipped weapon range, damage and timing belong to Weapon Module below.", MessageType.Info);
                if (group == 2 && p.useLocomotionBlendTree)
                {
                    Vector2 refs = EnemyLocomotionAnimator.ReferenceSpeeds(p);
                    EditorGUILayout.LabelField("Effective Walk / Run references", refs.x.ToString("0.###") + " / " + refs.y.ToString("0.###") + " m/s");
                }
                foreach (string field in Fields[group])
                {
                    if (group == 7) continue; // Dedicated percentage editor is drawn once below.
                    var property = data.FindProperty(field);
                    if (property == null || !drawn.Add(field)) continue;
                    bool match = !searching || (property.displayName + " " + field + " " + property.tooltip).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!match) continue;
                    bool visible = IsVisible(field, p, advanced);
                    if (!visible && !searching) continue;
                    using (new EditorGUI.DisabledScope(!visible))
                    {
                        if (field == "actionBreak") EditorGUILayout.HelpBox("Ordinary shots keep physical feedback without losing pursuit or combat rights. Action Break uses accumulated impact or a per-shot critical, then releases only the interrupted enemy.", MessageType.Info);
                        if (field == "hitKnockbackDistance") EditorGUILayout.LabelField("Action Break movement", EditorStyles.boldLabel);
                        if (field == "enableHitReaction") EditorGUILayout.LabelField("Physical feedback (ordinary hits)", EditorStyles.boldLabel);
                        if (field == "charge") DrawCharge(property);
                        else EditorGUILayout.PropertyField(property, new GUIContent(property.displayName, property.tooltip), true);
                    }
                    if (!visible && searching) EditorGUILayout.LabelField("Inactive / advanced setting; enable its feature to edit.", EditorStyles.miniLabel);
                }
                if (group == 7 && (!searching || Fields[7].Any(field => ObjectNames.NicifyVariableName(field).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)))
                {
                    EnemyLootEditor.Draw(data);
                    EditorGUILayout.PropertyField(data.FindProperty("lootOffset"));
                }
                }
            }
            data.ApplyModifiedProperties();
            var hidden = new List<SerializedProperty>();
            for (int group = 0; group < Fields.Length; group++)
                if (category == 8 || category == group)
                    foreach (string field in Fields[group])
                        if (!IsVisible(field, p, advanced) && data.FindProperty(field) != null) hidden.Add(data.FindProperty(field));
            hiddenExpanded = EditorGUILayout.Foldout(hiddenExpanded, "Hidden settings in this category (" + hidden.Count + ")", true);
            if (hiddenExpanded)
                foreach (var property in hidden)
                {
                    string reason = advancedFields.Contains(property.name) && !advanced ? "Advanced tuning: enable Show advanced settings." :
                        property.name == "animationWalkSpeed" || property.name == "animationRunSpeed" ? "Automatic references use AI movement speeds instead." :
                        property.name == "runAnimation" ? "Legacy clip switching; the Blend Tree handles Walk/Run now." :
                        property.name == "attackCooldown" ? "Lost attacks use Preparation + Windup + Recovery instead." :
                        property.name == "pressureSpeedMultiplier" ? "Only used by legacy approach speed, with Chase Rhythm disabled." :
                        "Its feature is disabled or this control does not apply to the current mode. Saved value is retained.";
                    EditorGUILayout.LabelField(property.displayName + " — " + reason, EditorStyles.wordWrappedLabel);
                    if (!string.IsNullOrEmpty(property.tooltip)) EditorGUILayout.LabelField(property.tooltip, EditorStyles.wordWrappedMiniLabel);
                }
            if ((category == 0 || category == 2 || category == 8) && p.useLocomotionBlendTree && GUILayout.Button("Align custom animation references with AI speeds"))
            {
                Undo.RecordObject(p, "Align animation references");
                p.animationWalkSpeed = Mathf.Max(.01f, p.walkSpeed);
                p.animationRunSpeed = Mathf.Max(p.animationWalkSpeed + .01f, p.chaseSpeed * Mathf.Max(1, p.burstSpeedMultiplier));
                EditorUtility.SetDirty(p);
            }
        }
        if ((category == 5 || category == 8) && p.useLostCombat)
            EnemyWeaponLabWindow.DrawInlineSettings(p, ref weaponExpanded);
    }
}

public static class EnemyDiagnosticsGUI
{
    public static void Draw(EnemyController enemy)
    {
        if (!Application.isPlaying || enemy == null) return;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Live AI / Animation", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("State", enemy.GameplayDebugState);
        if (enemy.CurrentState == EnemyController.EnemyState.Pressure)
            EditorGUILayout.LabelField("Orbit direction", enemy.PressureOrbitDirection > 0 ? "Clockwise" : "Counter-clockwise");
        EditorGUILayout.LabelField("Mode / attack permission", (enemy.IsAggressive ? "Aggressive" : "Normal") + " / " + (enemy.HasAttackTurn ? "Granted" : "Waiting"));
        EditorGUILayout.LabelField("Blend Tree adapter", enemy.UsesLocomotionBlendTree ? "Connected" : "Legacy / incompatible controller");
        EditorGUILayout.LabelField("Actual speed parameter", enemy.AnimationSpeedParameter);
        EditorGUILayout.LabelField("Current loadout", enemy.CurrentWeapon != null ? enemy.CurrentWeapon.loadout.ToString() : "Unarmed / Base");
        if (!string.IsNullOrEmpty(enemy.LastAttackAnimation)) EditorGUILayout.LabelField("Last attack state", enemy.LastAttackAnimation);
        EditorGUILayout.LabelField("Measured movement", enemy.AnimationMovementSpeed.ToString("0.000") + " m/s");
        EditorGUILayout.LabelField("Blend target / current", enemy.AnimationTargetBlend.ToString("0.000") + " / " + enemy.AnimationCurrentBlend.ToString("0.000"));
        EditorGUILayout.LabelField("Local movement X / Z", enemy.AnimationMovementDirection.ToString("F3"));
        var agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (enemy.enemyData != null)
            foreach (var warning in EnemyProfileGUI.AnimatorWarnings(enemy.GetComponent<Animator>(), enemy.enemyData)) EditorGUILayout.HelpBox(warning, MessageType.Warning);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("Animator", enemy.GetComponent<Animator>(), typeof(Animator), true);
            EditorGUILayout.ObjectField("Runtime profile", enemy.enemyData, typeof(EnemyDataSO), false);
        }
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            EditorGUILayout.LabelField("Agent speed / velocity", agent.speed.ToString("0.000") + " / " + agent.velocity.magnitude.ToString("0.000") + " m/s");
            EditorGUILayout.LabelField("Path", agent.isStopped ? "Stopped" : agent.pathPending ? "Calculating" : agent.hasPath ? agent.pathStatus.ToString() : "No path");
            EditorGUILayout.LabelField("Pressure group", enemy.RushStatus);
            EditorGUILayout.LabelField("Effective turn request range", enemy.EffectiveAttackTurnRequestDistance.ToString("0.##") + " m");
            EditorGUILayout.LabelField("Attack target gate", enemy.RushTarget != null && enemy.RushTarget.TryGetComponent(out PlayerController targetPlayer)
                ? EnemyPlayerEffects.For(targetPlayer).BlockReason : "Missing player");
        }
        if (GUILayout.Button(enemy.IsPatrolRunning ? "Pause AI / Patrol" : "Start AI / Patrol"))
        { if (enemy.IsPatrolRunning) enemy.PausePatrol(); else enemy.StartPatrol(); }
        if (GUILayout.Button(enemy.IsAggressive ? "Switch to Normal" : "Enable Aggressive")) enemy.SetAggressive(!enemy.IsAggressive);
        EnemyStateDisplay.Enabled = EditorGUILayout.Toggle("Gameplay state labels (F8)", EnemyStateDisplay.Enabled);
        EditorGUILayout.EndVertical();
    }
}

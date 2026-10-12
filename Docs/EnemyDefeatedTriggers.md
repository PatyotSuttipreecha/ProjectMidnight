# Enemy Defeated Trigger

Add `EnemyDefeatedTrigger` to an empty scene object that remains active (not to a corpse that will be destroyed).

## Specific enemy
Set Mode = Specific Enemy, drag the scene's EnemyController into Specific Enemy. Do not assign the prefab asset: assign the live scene instance.

## Whole group
Set Mode = All Tracked Enemies. Populate Enemies with scene instances, or set Enemy Group Root to the parent of enemies. Both sources are combined without duplicates; inactive child enemies are included. Root collection happens once when the trigger first enables. It does not include enemies outside that group. An empty group never fires. Disabling or destroying a living enemy is not counted as a kill.

## Cutscene or other events
- Assign an existing CutsceneController to Cutscene. It handles its existing fade/input lock behavior.
- On Triggered can call PlayableDirector.Play directly, WorldTrigger.Arm, GameObject.SetActive, door actions, or any compatible Inspector event. Choose one playback route; do not wire the same cutscene twice.
- Delay is in scaled seconds (0 = immediate). Armed can be controlled through Arm/Disarm. Completion fires once per trigger instance.
- CutsceneController.Play retains its existing requirements: a valid Timeline and a living player whose gameplay input is not blocked. If it refuses playback, this trigger's completion event still fires and is not automatically retried. Use the event route for custom cutscene queuing.

## Spawned waves
Disable Group Sealed On Start. For each successful spawn call `trigger.RegisterEnemy(instance)` with the returned EnemyController, then call `trigger.SealGroup()` when no additional enemies will join. Until sealed, killing the currently spawned members does not finish the group. Do not seal between waves. This is an explicit finite encounter; it does not automatically track unlimited respawns.

The trigger listens to EnemyController.Died, emitted once at the end of BeginDeath after death/ragdoll/loot setup. Disabled triggers retain recorded death notifications and evaluate when enabled again. A delay is canceled on disable/disarm and restarts on reactivation. Changing Mode or the initial list during play does not rebuild the tracked group; configure before play.

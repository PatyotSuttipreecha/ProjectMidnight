# Aggressive encounter setup

Aggressive follows the assigned player's live position immediately, bypassing Detect anticipation and Search even outside the vision range or behind cover. It does not increase speed or damage. NavMesh paths, attack range, wall checks, preparation/recovery, group turn limits, and cutscene pause remain active. Pressure does not time out into Retreat while Aggressive is enabled.

## Configuration priority

1. **Enemy Group** controls all registered members and future spawns using its current runtime mode.
2. **Enemy Spawn Point / Aggression Mode** chooses UseProfile, Aggressive, or Normal for new spawns without a group.
3. **Enemy Profile / Aggressive On Start** is the default when no runtime override is assigned.

Existing profiles default to Normal. Runtime changes never modify shared SO assets. Spawn settings affect future spawns; use a group to change an encounter already in progress.

## Creating a chase encounter

- Use **System Modification > Enemy Lab > Create > Enemy Group** to create a group GameObject.
- Add placed enemies to Scene Enemies and spawn points to Spawn Points, or assign Enemy Group directly on each spawn point. Assign each actor/point to only one group.
- Leave Aggressive On Start disabled if a Trigger/Timeline should start the chase later.
- In a Trigger UnityEvent or Timeline Signal Receiver, reference the group and call **EnemyGroup.EnableAggressive()**. Call **DisableAggressive()** to return the entire group to normal detection.
- Alternatively bind **SetAggressive(bool)** with a bool argument. Individual EnemyController exposes the same functions.
- Actors with Start Patrol On Start disabled remain paused. Call EnemyController.StartPatrol separately when gameplay resumes.

After group mode changes, enemies created afterward inherit the new mode. Enabling interrupts the Pressure retreat cooldown. Disabling clears the forced alert for non-attacking members; an attack already in progress completes normally before ordinary detection resumes. Disabling Aggressive can still lead to normal detection if the player remains visible.

The group is an encounter controller, not a replacement for EnemyAttackCoordinator: attack reservations remain scoped to the target. Disabling the group component does not switch modes; use DisableAggressive explicitly. The current target still comes from the enemy/spawn target or the Player tag.

Testing uses an isolated Unity Play Mode harness with production enemy/group/spawn/module code and a PlayerController damage stub. Main-scene chase timing and Timeline event placement need scene testing.

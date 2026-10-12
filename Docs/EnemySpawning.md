# Enemy spawn schedules

On EnemySpawnPoint (also visible in Enemy Lab), select Spawn Mode:

- Once: one immediate batch of Initial Count, capped by Max Alive. No automatic retries or refills. Start Spawning cannot repeat that batch.
- Continuous: initial batch, then one enemy per interval indefinitely. Max Alive limits living enemies, not total enemies. Old Total Spawn Limit is ignored in this mode.
- Fixed Count: initial batch plus timed arrivals until Enemy Count successful spawns in total. Initial enemies count toward this total. Deaths free capacity, but do not reset the total.
- Existing Settings: compatibility mode for existing scenes/prefabs. Uses the saved Respawn, Spawn Interval and Total Spawn Limit without changing their behavior.

Randomize Interval samples between Minimum Interval and Maximum Interval for each scheduled attempt. When off, Minimum Interval is the fixed interval. Invalid values are clamped to at least 0.1 seconds. Time uses scaled game time, so pausing Time.timeScale pauses arrivals. Positions are independently randomized within Spawn Radius and checked against the compatible NavMesh.

Spawn On Start = false means new modes wait for StartSpawning, which can be called from WorldTrigger.OnTriggered or a Timeline event. Initial Count creates the first batch immediately when started. Full capacity or failed spawn positions do not consume Enemy Count; the next attempt waits for the next sampled interval. Disabled objects do not update; enabling resumes their existing schedule with at most one attempt, without a catch-up burst.

StartSpawning starts/resumes without resetting the total or creating a second initial batch. PauseSpawning pauses without a completion event. StopSpawning stops and emits On Spawning Finished. Restarting Continuous or an unfinished Fixed Count is allowed; restart keeps the current total. Neither Stop nor Pause removes enemies that already spawned.

On Enemy Spawned supplies the successful live EnemyController. For a finite group EnemyDefeatedTrigger, disable Group Sealed On Start and wire this event's dynamic EnemyController argument to RegisterEnemy. Wire On Spawning Finished to SealGroup. On Spawning Finished means spawning has stopped, not that the enemies have died. Do not seal an unlimited encounter before the escape exit stops it.

## Opening escape example

Set Mode = Continuous, Initial Count = 2, Max Alive = 5, Randomize Interval = true, Minimum = 2, Maximum = 4, Spawn Radius = 3. Disable Spawn On Start and call StartSpawning when the player enters the encounter. At the rear escape exit, call StopSpawning on every participating spawn point. These are starting tuning values; test against player speed, available ammunition and path length.

Provide a reachable escape path and bake NavMesh for each spawn area. Continuous arrival does not itself guarantee ammunition depletion or cause pursuit: assign the appropriate Enemy Prefab/profile/target and test its detection or alert behavior. This change does not alter ammunition, enemy AI, scene placement, or existing saved encounters automatically.

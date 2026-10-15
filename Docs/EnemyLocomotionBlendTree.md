# Enemy locomotion Blend Tree

Disarm attack selection: a valid Base Attack Animation such as zombie_attack2_1 is now preserved instead of remapped to the Machete fallback. Equipped modules prefer their own state and then the configured armed fallback; unarmed attacks prefer the base state. Script-controlled attack/death playback now owns state entry on this controller: the shared Attacking Any State entry and unconditional legacy entries were disconnected while all states/clips were retained. The Attacking bool still controls the existing return transition. Live diagnostics show Current Loadout and Last Attack State. This does not author a new unarmed clip; the base state must already exist in the Animator.

15 October: the controller's speed float was renamed from Blend to Speed. All six The Lost profiles and NormalEnemy now bind to Speed, and new profiles default to Speed. The runtime adapter falls back between the two known names only when the configured float is missing; Live AI / Animation shows the actual resolved speed parameter. Custom controller parameter names still use the configured binding. This restores shared-controller locomotion for variants whose saved/default binding was still Blend.

Update: automatic reference speeds now follow AI Walk Speed and Chase Speed × Boost Multiplier. While actual motion exceeds the 0.03 m/s stop threshold, Keep Walk Blend While Moving defaults to a minimum Blend of 0.25; damping smooths the start/stop. Custom references and the old proportional Idle/Walk blend remain available by disabling the corresponding options. This fixes the Machete profile's saved 2 m/s reference producing roughly 0.075 Blend at 0.6 m/s Pressure speed. See EnemyLabGuide.md for the reorganized settings and exact visibility rules. The controller now also contains legacy attack/death states; their unconditional Any State entry transitions were removed so they cannot steal locomotion.

The current `Assets/Art/Animation/TheLost/Enemy.controller` uses `Controlling` with `Blend`, `Horizontal`, `Verticle` (current spelling), and `Attacking`. EnemyController now recognizes a compatible controller and routes legacy idle/walk requests to this state. Older controllers without the configured state/float parameter retain their clip playback.

Parameters come from actual frame movement after NavMesh avoidance and crowd separation. Forced knockback, paused actors, attack preparation/recovery and stopped agents feed zero locomotion speed. Direction is relative to the enemy's facing; Pressure circling therefore drives the side branches. NavMesh remains responsible for root movement, with Animator root motion disabled.

| Actual movement | Blend |
|---|---|
| Stationary | 0 / Idle |
| Walk reference speed (default 0.5 m/s) | 0.25 / Walk |
| Run reference speed (default 3.125 m/s) | 0.5 / Run |

Intermediate speeds blend between these thresholds. Reference speeds describe the animation's footwork; AI Walk Speed, Chase Speed and Boost Multiplier still determine movement. Tune references to match the clips if feet slide. Set parameter damping (default 0.12 s) and locomotion transition (0.12 s) in Enemy Profile / Standalone Profile or Enemy Lab → Locomotion Blend Tree. The tree's current thresholds are 0 / 0.25 / 0.5.

Idle, Walking, Run, LTurn and RTurn now loop. Attack remains a one-shot. Any State → Attack cannot transition to itself; Attack → Controlling follows the cleared Attacking bool without an additional exit-time gate. Attack timing/damage remains owned by the gameplay coroutine. Legacy attack also exposes Preparation/Attack/Recovery so the bool does not start the swing during anticipation. A missing module-specific state falls back to `Art_Animation_Thelost_Machete_Attack` on the new controller.

The Walk branch currently uses the same forward walking clip for forward and backward. LTurn/RTurn occupy the left/right branches. The adapter supplies proper directional values, but it does not author genuine backward or strafe clips; substitute suitable animations for natural circling. Death continues to use the profile's configured death state and ragdoll settings; this locomotion adapter does not author new death poses.

Validation uses the actual imported controller/clip settings plus a generic Animator fixture for parameter/state mapping, and production enemy combat code with a PlayerController damage stub. It does not verify Humanoid deformation, foot sliding or visual timing in the main scene.

## Revised hierarchy — 15 October 2026

Controlling → EnemyType (TheLost = 0) → WeaponType (Unarm 0, Machete 0.2, Log 0.4, Torch 0.6, Stone 0.8, Axe 1) → Speed (Idle 0 / Walk 0.5 / Run 1) → Horizontal/Verticle. This supersedes the old 0/0.25/0.5 mapping above. WeaponType changes immediately on disarm. Empty Log/Stone/Axe movement branches temporarily reuse Unarm motion; replace those clips when authored. Other enemy-family branches remain unfinished and are not selected by TheLost adapter.

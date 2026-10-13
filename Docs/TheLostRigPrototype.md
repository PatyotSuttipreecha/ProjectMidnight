# The Lost rig and animation experiment

Six rigged visual prefabs: `Assets/Prefab/Character/TheLostRigPrototype`. This is a Generic rig (not Humanoid). Original low-poly visual prefabs and the working gameplay Enemy prefab remain unchanged.

Each body is a SkinnedMeshRenderer bound to 19 bones. Each original mesh part has full weight to one appropriate bone. The intentionally disconnected joints can separate when bent; this is the agreed prototype approach, not production-quality smooth skinning. Weapons remain separate objects attached beneath RightHand/Weapon_Removable, so they follow the animated hand and can later be detached by gameplay code.

Seven original prototype animation clips: Idle, Walk, Run, Attack, Reach, Hit, Death. Walking/running are in-place. Reach is a visual reaching pose only, not an implemented grab. Death is a pose experiment without ragdoll or ground alignment. Weapon-specific heavy axe, throwing, torch attacks and disarm animations have not been authored; all variants currently share the same base motions. No AI or combat script is attached.

## Quick test

1. Open `Assets/Prefab/Character/TheLostRigPrototype/TheLost_RigPreview.unity` (separate preview scene).
2. Enter Play Mode, select the character, then open Animator → Parameters.
3. Change PreviewState: 1 Idle, 2 Walk, 3 Run, 4 Attack, 5 Reach, 6 Hit, 7 Death.
4. Select a different value then return to 4/5/6/7 to replay a one-shot clip.

The test scene uses `TheLost_AnimationTest.controller` with that selector. Rigged prefabs use `TheLost_Prototype.controller` with no automatic transitions, so external gameplay can call Animator.Play without the preview selector forcing Idle again. Default state is zombie_idle1; Walk/Attack/Death use zombie_walk2, zombie_attack2_1 and zombie_dead2. These names match existing EnemyController expectations, but replacing the existing enemy is not automatically supported: its ragdoll builder and other systems may require Humanoid. Run, Reach and Hit are additional named states.

To test any other variant independently, drag its prefab into a test scene and assign TheLost_AnimationTest.controller to its Animator. All six share matching bone paths. CapsuleCollider is supplied for the visible body; it is not a CharacterController or NavMeshAgent. Do not layer this solid collider over existing hitboxes without checking collision settings.

Generic clips animate explicit bone paths. They do not retarget Humanoid/Mixamo animations directly. A Humanoid Avatar, T-pose and deformation-weight pass are a separate next step if this basic movement test is approved.

Validation in isolated Unity 6000.2.4f1: all six Generic Avatars valid; seven clips created; sampling Walk changes the foot and baked skinned mesh; sampling Attack changes the hand. Actual player combat and animation quality have not been tested in the gameplay scene. Generator remains in Tools/TheLostRigPrototype; no generator scripts are required by delivered prefabs.

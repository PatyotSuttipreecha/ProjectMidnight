# Key and padlock base props

Prefabs: `Assets/Prefab/Props/KeyAndPadlock/Key_Base.prefab` and `Padlock_Base.prefab`.

Simple brass key with an open bow, shaft and teeth; metal padlock with a U-shaped shackle and an applied keyhole detail. The keyhole is a visual surface detail, without internal lock mechanics. Both face +Z, with +Y up, and include an `InteractionPoint` transform.

The padlock's `Visual/Shackle` group is separate for later opening animations. No interaction logic, animation clips, colliders or runtime scripts are attached. Materials use URP/Lit with flat colors.

The generated deliverables are native mesh assets, materials, two prefabs and a studio preview scene. Generator scripts remain under `Tools/KeyAndPadlock`, outside the main project's Assets folder.

Run `generate.cjs` to regenerate the recipe and prepare the isolated Unity validation project. Run Unity with `-executeMethod KeyAndPadlockBuilder.Run`, then `export.cjs` to copy validated results back. Regeneration in the disposable test project recreates native asset GUIDs; preserve manual edits and references before repeating this initial-generation workflow.

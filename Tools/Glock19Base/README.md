# Glock 19 base model

Prefab: `Assets/Prefab/Weapon/Glock19Base/Glock19_Base.prefab`.

A basic untextured Glock 19 visual blockout based on the user's side-view reference. 24 mesh parts, 2,148 triangles, five plain URP materials. Approximately 21 cm overall; artistic proportions rather than exact CAD.

Native Unity mesh assets include a beveled slide, polymer frame, angled grip with basic finger grooves and side panels, open trigger guard, trigger and safety insert, muzzle, front/rear sights, rear slide serrations, simple controls, and magazine floorplate.

`Visual/Slide` and `Visual/Magazine` are separate groups. The root includes `FirePoint` and `GripPoint`. +Z is forward and +Y is up. No firing script, animations, colliders, textures, logos, or engraving are included.

`Glock19_Preview.unity` is a separate studio preview scene. `Glock19-preview.png` is rendered through Unity URP.

The source geometry is in `Glock19_Source.json`. Use **Tools > Project Midnight > Rebuild Glock 19 Base** to rebuild the generated native meshes/materials/prefab while retaining their asset GUIDs. This overwrites edits to those generated assets.

`node Tools/Glock19Base/generate.cjs` regenerates the source and prepares the isolated `Library/Glock19BaseValidation` project using the existing cached validation packages. `Glock19BaseBuilder.ValidateBatch` builds, checks and renders it. `export.cjs` copies the successful result back into the workspace.

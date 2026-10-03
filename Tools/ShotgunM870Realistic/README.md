# M870 detailed FPS model

**Prefab:** `Assets/Prefab/Weapon/M870Realistic/Shotgun_M870_Realistic.prefab`

Wood-stock, short-barrel M870 visual interpretation. Original art geometry, not manufacturer CAD. Visual reference: [Remington Model 870](https://remington-arms.com/870-series/).

The receiver was revised against the user's supplied Marui photograph: thinner body, small end bevels, and explicit flat side normals. The stock wrist is shallower and the forend ribs are more restrained. This revision updates the existing prefab and retains its asset GUIDs.

A subsequent receiver revision replaces the box roof with a curved canopy and a rounded longitudinal shoulder flowing into the stock. The side panels stay planar; the visible change is in the actual silhouette and cross section, not only the shading.

## Asset contents

- 35 mesh parts, 23,136 triangles; approximately 1.086 m long.
- +Z forward, +Y up; root pivot at the receiver.
- Contoured wooden stock with shaped wrist and textured checkering, ribbed wooden forend, recoil pad with modeled grooves, sling stud and loop.
- Open barrel bore, actual opening in the right receiver wall and underside loading opening. Interior bolt, extractor, loading gate and receiver pins are separate meshes. Interior geometry is representative, not a mechanically complete assembly.
- Seven URP/Lit materials. Stock and pump each use 2048px textures; blued steel uses 1024px; rubber uses 512px.
- Each texture set includes BaseColor (sRGB), tangent-space Normal, and MetallicSmoothness (linear; metallic in R, smoothness in A). Grain, pores, grip checkering, roughness and mild scratches are procedural. Normal maps derive from texture height; these are not high-poly sculpt bakes.
- Directional UVs follow the wooden stock and cylindrical forend. Basic UVs on small hardware share tiling steel textures.

## Using the prefab

Drag it into the scene or parent it to the existing weapon mount. Adjust the mount transform for the player's hand/view; this model has not been placed into the gameplay scene.

`M870_Preview.unity` is a separate studio scene with the prefab, camera and three lights. Open it to inspect the imported model, or use its Game view to see the studio framing.

`Visual/Pump` contains the forend and action bars. Animate its local Z for a pump stroke. `Visual/Bolt` groups the visible bolt pieces for independent movement. `FirePoint`, `RightHandGrip`, and `Visual/Pump/LeftHandGrip` are attachment transforms.

No gameplay scripts, firing logic, animation clips, LODs, colliders, or audio are attached. The 23k-triangle mesh targets a held/FPS weapon; use a simpler representation for large numbers of world pickups.

Unity's editor helper resolves imported mesh references and checks material maps, UVs, tangents and attachment transforms. An explicit check is available at **Tools > Project Midnight > Validate Realistic M870**.

## Regeneration and verification

`node Tools/ShotgunM870Realistic/generate.cjs` regenerates this asset folder and offline previews, retaining existing GUIDs. It overwrites generated M870Realistic content; preserve manual edits before regenerating.

`prepare-validation.cjs` prepares the isolated test project at `Library/M870AssetValidation` using cached Unity packages. `UnityAssetValidation.Run` performs a Unity 6000.2.4f1 import check and renders the prefab through URP. `validation-report.json` records the result. `M870-unity-preview.png` is the actual Unity render; the other PNG previews are offline source renders.

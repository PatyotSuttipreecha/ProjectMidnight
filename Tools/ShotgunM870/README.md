# Stylized M870 shotgun

Prefab: `Assets/Prefab/Weapon/M870/Shotgun_M870.prefab`.

Approximate full length: 1.13 m. Unity forward is +Z; up is +Y. The root pivot sits at the receiver. 23 separate mesh parts, 4,392 triangles, five URP materials. This is an M870-inspired wood-stock game model, not an exact manufacturer CAD replica.

The barrel has an actual bore and a muzzle ring. The receiver has an applied dark ejection-port recess and bolt face; the port is not a through-cut. Geometry includes a contoured stock, recoil pad, ribbed forend, tubular magazine, curved trigger guard, trigger, loading gate, receiver pins and front bead. Materials are plain colors, with generated basic UVs for later texturing.

`Visual/Pump` groups the forend and action bars. Animate it along local Z to move the pump. `LeftHandGrip` travels with this group; `RightHandGrip` and `FirePoint` are attachment transforms. No firing scripts, colliders, audio, rig or animation clips are included.

Unity resolves OBJ mesh references after import using `M870PrefabImportValidator`. Use **Tools > Project Midnight > Validate M870 Prefab** for an explicit import check.

To regenerate source meshes and the prefab, run `node Tools/ShotgunM870/generate.cjs` from the project root. This overwrites the generated M870 assets, retaining their GUIDs. Back up manual modifications before regenerating. It does not alter the earlier simple shotgun or any scene.

The PNG preview is an offline rendering of the source mesh geometry. It does not confirm Unity import or in-engine shading.

# Gameplay Terrain

`Scenes_Gameplay.unity` now contains `Terrain_Gameplay`, fitted to the world-space
surface of the original ProBuilder ground under `TerrainMock`. Its TerrainData
uses the project's Unity 6000.2.4f1 native asset format and a 1025 × 1025 heightmap.

`Terrain_ProBuilderReference` retains the original mesh and ProBuilder components;
its MeshRenderer and MeshCollider are disabled to avoid overlapping surfaces.
The other ProBuilder objects and scene props are unchanged.

The ground has an irregular footprint. On the first Unity Editor import,
`GameplayTerrainFootprintImport` applies the packed footprint through
`TerrainData.SetHoles`, saves only this TerrainData and marks the import complete.
Later terrain sculpting and hole painting are preserved across editor restarts.
Open the project in Unity before inspecting the scene or building it. Wait for
script compilation and asset import to finish. No scene save is needed for this
initial footprint operation. The terrain starts with Unity's default unpainted
surface so a terrain layer can be chosen independently of the height fitting.

To rebake from the reference mesh, install `numpy`, `scipy`, and `UnityPy`, then
run `python Tools/GameplayTerrain/bake.py` from the project root. This replaces
terrain heights, resets the one-time footprint import marker, and updates the
terrain origin in the scene. Rebaking discards terrain sculpting; preserve a copy
of the TerrainData first if those edits should be kept. Rotated/scaled reference
transforms are rejected rather than baked incorrectly.

See `bake-report.txt` for numeric verification. Quantization error describes
heightmap samples, not interpolation error everywhere between samples. The
footprint keeps only cells whose four corners lie on the original ground, so its
edge is conservative at the heightmap's approximately 0.30 × 0.47 metre spacing.
Unity compilation, interactive inspection and NavMesh rebaking were not run in
the cloud environment, which has no Unity Editor installation.

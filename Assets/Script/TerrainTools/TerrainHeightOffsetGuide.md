# Terrain Height Offset

Open **System Modification > TerrainTools > Terrain Height Offset**, or **Open Height Offset** in the TerrainTools menu. This is an Editor tool, not a runtime component.

## Raise one zone
1. Assign the scene Terrain. Leave **Whole Terrain** off.
2. Enable **Edit boundary in Scene**. Hold **Shift** and left-click the Terrain to add vertices around the zone in perimeter order. The tool closes the polygon automatically. A TerrainCollider using the same TerrainData is required for clicking; coordinates can also be entered directly in the window.
3. Drag handles to adjust the boundary; use **X** beside a point to remove it. Coordinates are Terrain-local X/Z distances, not height values.
4. Enter **Height offset**, for example `20` to raise or `-10` to lower.
5. Set **Inward blend**. Points farther than this distance from the boundary receive the full offset and retain their relative heights. The band inside the boundary blends to the unchanged exterior. `0` keeps the full offset up to the boundary, creating a steep transition over the heightmap sample spacing. Cyan marks the boundary; yellow preview dots mark the full-offset interior. Dots are a coarse weight preview, not the future surface.
6. Keep **Save to configured path** enabled to isolate this Terrain from other users of the source asset. Set **Save Path (folder)** using **Browse** and enter a **File name** once. The tool remembers them per project and creates or overwrites the same asset there on each Apply, preserving its GUID, without a save dialog. The folder must already exist inside Assets. Disabling this option edits the shared original TerrainData.
7. Click **Apply height offset**. Inspect the result, use **Ctrl+Z** to undo if needed, then save when satisfied. Applying again adds the offset again.

For the whole sheet, enable **Whole Terrain**; polygon and blend are ignored.

## Props and vegetation
- Terrain trees move by the actual ground-height change, retaining their original vertical clearance. Terrain grass/detail placement follows the surface.
- For separate GameObjects, select objects and use **Add selected roots**. A root and all its descendants move together based on the ground change at the root pivot.
- For a folder containing separate props, select the folder and use **Add selected children**. Remove the folder itself from the roots list if it was previously added.
- Nested or duplicate roots move once. Pivots outside the Terrain do not move. In the transition band, each root follows the sampled ground-height change at its pivot; wide objects may require manual adjustment.

## Behavior
- The tool rejects offsets beyond the Terrain height range instead of flattening peaks by clamping.
- The region is sampled on the existing heightmap grid. Very small regions can affect no samples; transitions interpolate between samples.
- Terrain textures and holes are preserved. Baked navigation and game-specific collision data must be rebuilt separately when needed.
- Terrain world rotation/scale must be identity. Play Mode and Prefab Mode editing are disabled.
- Undo groups Terrain heights, tree positions, prop movement, and copy assignment together. A copied asset remains in Project after undo so it can be recovered; the previous Terrain/collider assignment is restored.
- Editing an original TerrainData affects every scene/Terrain referencing it. Prop movement only affects the explicit roots list.

## Verification
The initial copy implementation used Instantiate/CreateAsset without explicitly restoring surface datasets. It has been replaced with an explicit transfer of paint weights, Terrain Layers, detail maps/prototypes, tree prototypes/instances and holes from the live source. The configured output asset is saved automatically, including overwrites; unrelated assets are not saved.

The updated implementation compiled and passed 104 automated checks in an isolated Unity 6000.3.11f1 project, plus 10 persistence checks after starting a fresh Unity process. Checks include five painted layers (multiple alphamap textures), vegetation and holes, source preservation, recovery without repeating the height offset, and Undo/Redo. The user's scene was not altered by these tests. Interactive Scene handles and final visual placement still need checking in the working editor.

## Recover a copy made by the earlier version
1. If Undo is still available, undo the faulty Apply to return to the original TerrainData, then apply again with the updated tool.
2. Otherwise select the affected Terrain in the window. Under **Recover paint and vegetation**, assign the intact original TerrainData to **Original TerrainData**. Do not select the damaged copy.
3. Click **Recover surface; keep current heights** to create or overwrite the _Recovered asset in the configured Save Path automatically. Recovery copies the original surface and vegetation, retains the currently adjusted heights, and offsets the original trees to those heights. It does not move props or apply the height offset again.
4. Inspect the result and save the scene. Recovery supports Undo. The source and the damaged copy remain available.

Recovery requires the original TerrainData with matching Terrain dimensions and heightmap resolution. It restores that original's paint, details and holes; it cannot reconstruct edits missing from both assets.

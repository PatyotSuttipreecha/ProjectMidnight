# Terrain Slope: A to B

Open **System Modification > TerrainTools > Terrain Slope**, or **Open Terrain Slope (A > B)** in the toolbox.

1. Assign a scene Terrain with a matching TerrainCollider.
2. Enable **Draw A > B in Scene**, press the left mouse button at A, drag to B, and release. Alt remains available for Scene navigation. A/B handles can be moved afterward, or edited numerically in Terrain-local XYZ units.
3. Set **Slope angle**. A anchors the start elevation. Positive angles climb toward B; negative angles descend. In this mode B supplies direction and horizontal distance; its entered Y does not constrain the resulting height. The panel displays the resulting B world height.
4. Alternatively enable **Match A/B heights** to connect their sampled/entered elevations. The angle is then calculated from the endpoints.
5. Set **Core width**, **Side blend**, and **End blend** in world units. The flat-across core follows the chosen longitudinal angle from A to B. Blend bands extend outside the core and ends, fading into the captured ground. Those transition bands do not maintain the specified angle. Zero blend gives an abrupt boundary at heightmap resolution.
6. Select an existing **Save Path** inside Assets and a **File name**. These preferences are remembered independently from Height Offset. Bake creates or overwrites that TerrainData asset, keeping its GUID. Existing outputs with incompatible Terrain layouts are rejected rather than resized. All scenes sharing the output asset see its changes.
7. Click **Bake Slope**. Save the scene separately when satisfied. **Ctrl+Z** undoes the bake.

## Editing and preview
- Yellow is the ramp centerline; cyan lines show a coarse sampled surface preview. The actual surface is limited by the Terrain heightmap resolution.
- The tool captures a height-only snapshot on the first stroke. Repeated bakes use that snapshot, so blend weights do not accumulate.
- Moving the current ramp restores its previous footprint from that snapshot. Terrain heights outside both the current and previous footprint remain untouched, including exterior sculpt edits.
- To keep the current ramp and build another, click **Capture current heights / Start next ramp**, then draw the next A to B. Also capture again to retain external sculpt changes made inside the ramp footprint. Closing the window discards the editing snapshot, but leaves baked Terrain assets intact.
- Changing settings and drawing previews do not modify the scene Terrain until Bake. A matching TerrainCollider is required for mouse picking; numeric endpoint entry is also supported.

## Props and vegetation
Terrain paint, layers, holes, grass/detail maps and tree prototypes are preserved by the shared Terrain Height Offset writer. Trees follow the actual ground-height change. Add explicit scene objects with **Add selected roots**, or use **Add selected children** for a folder of independent props. Each root moves vertically based on its pivot, preserving its ground clearance; nested selections move once. Wide objects can require manual adjustment in blend bands. Rebuild any baked navigation or game-specific collision data separately.

## Validation
The tool compiles in Unity 6000.3.11f1. Automated checks cover signed and diagonal grade, endpoint fitting, width/end blending, exterior preservation, height-range rejection, repeated-bake stability, native tree/prop movement, paint retention, output GUID preservation, and Undo/Redo. Saved output is also checked in a fresh Unity process. Interactive drag/handle behavior and final appearance in the working scene have not been manually verified.

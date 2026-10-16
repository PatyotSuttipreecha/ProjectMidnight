# Procedural Decoration Placer

Open **System Modification > TerrainTools > Procedural Decoration Placer**, or inspect a scene object containing the component.

## Preview workflow

1. **Assets / Area**: assign Terrain, choose **Area Shape**, and add prefab assets or a Placement Data SO. Bounding Box keeps the existing box including its height. Polygon uses a closed boundary of pivots on Terrain without a box Y limit. Complete geometry must fit inside the selected boundary.
2. **Distribution**: set Total Decorations, spacing, rotation/scale, groups and seed.
3. **Terrain / Avoid**: configure slope/height, excluded paint layers, path-edge bands and scene obstacles.
4. **Build Preview**: calculates a plan without instantiating GameObjects. Colored markers show accepted pivots and their up axes; optional wire bounds show the planned geometry. Yellow ticks show the selected layer contour. Rejected-sample markers are optional. The report gives accepted counts, per-prefab totals and rejection reasons.
5. **Generate This Preview**: replaces the previous generated container contents using exactly those prefab choices and transforms. Changes to settings, Terrain or scene objects invalidate the preview; rebuild before generating. New Seed Each Preview chooses a seed during planning, never again when applying the preview.

Preview limits drawing to 5,000 accepted markers, 1,000 bounds, 500 rejected trials and about 2,000 contour samples. Planning still considers the complete requested amount. Cancellation and plans with no valid positions preserve previous output. Generation and clearing support Undo in Edit Mode.

## Polygon boundary

Choose **Area Shape > Polygon**, then **Edit Boundary in Scene**. Shift + left click on the assigned Terrain to add pivots in boundary order; at least three are required. Drag a pivot's X/Z handles to reshape the area. The boundary closes automatically and its cyan outline follows the Terrain. Press Esc to finish. The Terrain needs its TerrainCollider for click placement.

**Start from Box** replaces the polygon with the existing box's four X/Z corners. **Remove Last Pivot**, **Clear Pivots**, numeric **Boundary Pivots** and **Frame Boundary** provide further editing controls. All edits support Undo and invalidate an existing Preview. Switching modes preserves both sets of settings and does not regenerate scene objects.

Concave polygons are supported. Crossed edges, duplicate pivots and zero-area shapes block generation with an explanation. The whole renderer/collider bounds footprint must fit, so large decorations can be rejected near an edge or across a concave cutout. Polygon points are local to the Placer and move/rotate/scale with it; use Y rotation only (X/Z tilt is rejected). Height/slope filters still apply. Very narrow regions may need smaller prefabs or more generation attempts.

Terrain tools now open through **System Modification > TerrainTools > …**: Terrain Height Offset, Terrain Slope, Terrain Snapper, Decoration Auto Follow Terrain and Create Terrain Object Snapper. Use these menu entries directly in this project.

## Renderer grounding

**Distribution > Appearance > Renderer Grounding** is enabled by default. The planner measures MeshRenderer/SkinnedMeshRenderer bounds after the candidate rotation/scale, including nested visuals and LODs. It samples both local Y faces (corners, edge midpoints and centers) against Terrain, then adjusts the prefab root vertically. Collider geometry is used as a fallback for collider-only prefabs. No child local transform is changed.

**Ground Offset** adds vertical clearance; negative values intentionally sink objects. Disable Renderer Grounding to use the legacy pivot-on-Terrain placement. Footprints extending beyond the assigned Terrain are rejected with a separate diagnostic. Preview bounds and obstacle/area checks use the final grounded position. Bounds and discrete samples are conservative approximations: wide objects on uneven ground can still leave gaps, and small bumps between samples are not an exact mesh/terrain contact simulation. Water meshes do not replace the assigned Terrain as the ground surface.

For output already generated, **Ground Existing Decorations (Renderer)** adjusts each direct prefab root in the Decoration Container without rerolling X/Z, rotation or scale. It supports Undo and refreshes existing auto-follow bindings when that tool is enabled. Skipped roots have no supported geometry or lack Terrain coverage. Use this explicit action to repair older output; changing the option alone does not move existing decorations.

## Groups

Enable **Use Groups**. Total Decorations is the total object count, not the number of groups. A target group size is chosen between Min/Max Group Size. All member pairs must be at least Group Min Distance apart and at most Group Max Distance apart, measured horizontally in world metres. Therefore Group Max Distance is the group's maximum diameter, not a radius around its center.

Min Distance separates objects belonging to different groups. Tight filters, a saturated group, or the last remainder can produce groups smaller than the requested minimum; the tool reports the result instead of promising impossible counts. Mixed prefabs use the existing weighted list. Generated root names carry group numbers. No extra group parent is inserted, so each placed prefab root remains available for Terrain Follow and existing bake workflows.

## Painted path / Terrain Layer edges

Enable **Use Layer Edge**, select the Terrain Layer used to paint the path, then choose:

- **Outside**: pivots outside that painted layer, beside the path.
- **Inside**: pivots inside the selected layer near its boundary.
- **Both**: either side.

Edge Threshold defines membership in the layer (default 0.5). Edge Min/Max Distance set the allowed band from the contour in world metres. The contour is reconstructed from alphamap threshold crossings; spatial indexing finds the nearest contour segment. Precision depends on paint resolution and blend transitions. Terrain borders alone are not layer contours: a layer covering the entire Terrain has no internal boundary. Mesh/spline roads need a painted layer underneath to use this mode.

All slope, height, exclusion, obstacle, group and area filters still apply. For example, Inside plus exclusion of the same path layer can legitimately yield no results. Distances constrain object pivots, not the outer silhouette: large models can extend over a path. Use a larger minimum edge offset for large prefabs. Group spacing can also prevent a wide group from fitting into a narrow edge band.

## Compatibility and validation

Existing serialized field names and script GUID remain unchanged. Groups and edge mode default off, preserving the scatter workflow. Placement is planned from cached prefab geometry and paint data; no temporary scene instances are required for Preview. Non-uniformly scaled/rotated parent hierarchies can shear generated meshes, so keep the generator/container scale uniform for the closest geometry-bound match. Prefab roots move as a whole; child visuals are never adjusted by planning.

The planner preserves Unity's global Random state. The request budget is capped at one million trials. Large object counts, dense obstacles and high-resolution paint maps increase planning time. There is no automatic per-frame replanning; explicitly rebuild a stale preview.

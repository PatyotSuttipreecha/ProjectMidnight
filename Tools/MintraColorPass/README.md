# Mintra base color pass — applied in Painter

## Reference palette revision (v02)

Saved `F:/Art/ProjectMidnight/Character/Mintra/Mintra_Painting_ColorPass_v02.spp` on 2026-11-01 after assigning the revised Head, Body and Cloth BaseColor maps through Painter UI. References are `ColorPass_v01/Mintra-Jacket-Orthographic-Symmetric-v2.png` and `Mintra-Tank-Orthographic-Symmetric-v2.png`. Generated maps are in `ColorPass_v02/`; existing Roughness bindings were retained. The generator now outputs this revision. The v01 SPP and original SPP remain available.

This revision adjusts the jacket from green toward brown-grey olive, lightens the shared warm skin palette, adjusts charcoal/indigo/brown clothing colors and adds subtle procedural fabric grain. It is a palette correction, not a finished reproduction of the concept: eyes, brows, lips, detailed denim stitching, pockets, accessories and hair are still incomplete or absent. Existing skin/cloth intersections remain. No reference-image projection or detailed hand painting was performed.

## Previous v01 pass

Output textures: `F:/Art/ProjectMidnight/Character/Mintra/ColorPass_v01/`.

Painter copy: `F:/Art/ProjectMidnight/Character/Mintra/Mintra_Painting_ColorPass_v01.spp`. Original Mintra_Painting.spp was not modified. Six PNG resources were imported into the copy, then assigned through Painter's UI to Fill Layers in Head, Body and Cloth. Base Color and Roughness are enabled; Height, Metallic and Normal overrides are disabled. Colors were reviewed on the model and the SPP was saved on 2026-11-01.

2048px BaseColor and Roughness maps for Head, Body and Cloth, generated against the actual UV triangles. Body/head use the same warm base skin color, eyes have a light placeholder color without iris detail. Cloth regions distinguish the olive jacket, dark inner top, blue-grey trousers and brown footwear. This is an initial base color study, without hand-painted face detail, makeup, fabric weave, wear or high-poly bake compositing. Not a finished character texture.

UV comparison between Mintra_Bake_Lowpoly.fbx and the SPP source Mintra_Combine_Lowpoly.fbx passed, per material and triangle UV coordinates. See `ColorPass_v01/uv-match-validation.json`. Resources imported in Painter are recorded in painter-import-verification.json.

Each matching Texture Set now has a Fill Layer using Mintra_[Head/Body/Cloth]_BaseColor and its corresponding Roughness resource. Existing texture-set mesh maps and the original Cloth layer were retained.

The optional Blender preview was a temporary reassembly of separately positioned FBX meshes. It shows skin/cloth intersections and is NOT an accepted final appearance or a corrected character mesh. Source FBX and source Blender files were not modified. Do not use that preview as production geometry.

The previous computer-use startup failure is resolved. Layer assignments were completed with Computer Use in Painter 6.2.2. The current pass establishes basic colors and roughness; detailed eyes, skin and fabric painting remain future work. Existing skin/cloth mesh intersections are visible and were not repaired in this texture-only task.

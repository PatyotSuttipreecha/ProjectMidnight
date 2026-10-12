# Rin's evidence camera

Prefab: `Assets/Prefab/Props/RinEvidenceCamera/Rin_EvidenceCamera.prefab`.

Compact personal digital camera, approximately 112 × 67 × 58 mm including the extended lens, plus wrist strap. Front faces +Z, up is +Y. Origin is at body center. 30 mesh parts, 4,280 triangles. Native Unity meshes and URP materials; no runtime scripts or generator dependency.

Separate Visual/Lens, Visual/Screen, Visual/Controls and Visual/Strap groups, a BoxCollider around the body/lens, InteractionPoint and GripPoint transforms. Collider is solid by default; configure trigger/physics according to the game's pickup system. The screen is dark: no fabricated photo or evidence text is attached.

This is a visual evidence prop. It does not automatically add inventory data, photo capture, mission progression, pickup logic or a rendered screen. Use the existing interaction/inventory system to wire those behaviors. No existing gameplay scene or mission data is changed.

Tools stay outside Assets. `generate.cjs` writes the source geometry and isolated Unity builder. The model uses flat material colors with modeled lens rings, controls, flash, screen and strap; there are no baked textures or LODs.

Validation project: `Library/RinEvidenceCameraValidation`. Run Unity 6000.2.4f1 with executeMethod `RinEvidenceCameraBuilder.Run`. Export only after camera-success.txt exists. Preserve delivered GUIDs when making revisions. Preview scene is for visual inspection.


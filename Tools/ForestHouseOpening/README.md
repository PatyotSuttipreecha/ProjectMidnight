# Forest and abandoned house blockout

Deliverable: `Assets/Prefab/Environment/ForestHouseOpening/ForestHouseOpening_Blockout.prefab`.

Drag the prefab into a scene at position (0,0,0), rotation (0,0,0), scale (1,1,1). Units are meters. Start at the `07_GameplayPlacementMarkers_NoLogic/PlayerSpawn` marker, around (-26,0.1,-27). Position an existing gameplay player using its own pivot/CharacterController height; this marker is the floor location, not a universal player pivot.

The forest has a winding main path and a returning optional branch. The house is centered at (0,0,78), with open front/rear doorways, a 2.5 m clear central corridor and four side rooms. Door openings are 1.8 m wide. Ground, walls, furniture, trunks and targets have colliders; foliage and path overlays do not.

The prefab uses native Unity primitive meshes and URP materials. No ProBuilder package, runtime scripts, camera, lights or generator are required by the prefab. Generator source stays under Tools, outside the main Assets folder. Roof geometry is grouped under `05_AbandonedHouse/Roof_ToggleForEditing`; disable that group for editing the interior.

Gold blocks are visible loot placeholders. Empty named transforms are placement markers. Replace these with existing pickup/weapon prefabs and wire tutorial triggers separately. Targets are static geometry, without damage or tutorial completion logic. The prefab does not include a player, AI, tutorial UI, NavMesh, interaction system or save system. This is a playable-scale environment blockout, not a finished realistic forest art pass.

`ForestHouseOpening_Preview.unity` is a separate inspection scene with preview lighting/camera; it does not change existing gameplay scenes. Preview pipeline assets are supplied for that preview and must not be assigned to the main project settings automatically.

Generated and checked with Unity 6000.2.4f1 in `Library/ForestHouseOpeningValidation`. The generator validates forest ground support, capsule clearance through the central house route, and absence of runtime scripts. Actual movement and third-person camera handling still require Play Mode testing with the project's player.

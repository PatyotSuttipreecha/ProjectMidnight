# Isan abandoned house — Low poly

Prefab: `Assets/Prefab/Environment/IsanHouseLowPoly/Prefab_Environment_IsanAbandonedHouse_LowPoly.prefab`.

Reference: GDD : Project Midnight, read 12 October 2026, sections Game Overview / World / Art Style. https://docs.google.com/document/d/1ufxru2Ga86p7VeXSrHsUFOnki28bvIF5OHRsApONs8c/edit

The GDD specifies old rural Isan timber houses near fictional Ban Don Lap in eastern Ubon, muted aged timber and rusty metal, evidence exploration, and a rear forest escape route. Local `Docs/ConceptArt/Narrative-v2/world-reference-v3-dark.png` supplies the gabled timber house, elevated veranda, railings and stair silhouette. This model interprets that reference as one explorable raised house; the GDD does not specify exact building measurements or a floor plan. It is not an exact architectural reconstruction or a finished ritual room.

Main building 10 × 8 m, front veranda 10 × 2 m, floor top at 1.8 m, ridge about 6.85 m. Front faces -Z, Y up. Front and rear stairs have 0.18 m rises and 0.32 m treads, width 2.1 m. Open front/rear doors have 1.9 m clear frames. Evidence room at front-left, bed area rear-left, open living/shelf space on right. Central passage runs from front to rear.

Drag prefab into a scene with unit scale. Colliders are on timber floors/walls/posts/stairs/furniture. Use the project's player for a stair and camera Play Mode check; automated route checks do not prove third-person camera behavior. Bake scene NavMesh after placement if enemies need to enter.

Groups separate stilts, floorboards, exterior walls, partitions, roof, veranda/stairs, furniture, open door pivots, and empty placement markers. Disable `05_Roof_ToggleForInterior` for editing. `09_PlacementMarkers/RinEvidenceCamera` is an empty location on the evidence table; it is not a pickup or mission component. Doors are posed open geometry without DoorController. Attach existing systems separately if needed.

Materials are native URP flat-color materials; wooden plank variation and roof seams are modeled geometry. No textures, LODs, runtime scripts, lighting or generator dependencies on the prefab. The low-poly detail is carried by box geometry and two native gable meshes. No changes to gameplay scenes, GDD or the earlier ForestHouseOpening prefab.

`IsanHouse_Preview.unity` includes inspection lighting and camera. Preview renderer/pipeline assets are only for inspection and should not overwrite the main project's graphics settings. Offline generator is stored under Tools, outside gameplay Assets. Validation is isolated in Library/IsanHouseValidation.

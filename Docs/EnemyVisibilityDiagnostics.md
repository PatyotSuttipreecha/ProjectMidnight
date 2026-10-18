# Enemy visual pop investigation — 2026-10-18

In recording `Screen Recording 2026-10-18 010341.mp4`, frame inspection around 12.63–12.67 seconds confirms an enemy abruptly becomes visible on previously open ground. The video alone does not identify its actor instance or birth time, so it cannot conclusively distinguish a new spawn, root discontinuity or delayed skin rendering. The earlier explanation attributing this solely to continuous spawning was insufficient.

Changes:

- Enemy startup sets Animator culling to Always Animate and SkinnedMeshRenderer Update When Offscreen on, so NavMesh/procedural bones do not keep stale offscreen animation/skin bounds. This trades some offscreen animation/skinning work for reliable moving-character visibility.
- Automatic Spawn Point attempts run after Cinemachine camera positioning in LateUpdate, rather than testing the previous view in Update. Initial batches and explicitly invoked SpawnOne calls remain immediate.
- F8 debug focus shows actor age and source Spawn Point. A new actor has a small age; an existing one retains its age on re-entry.
- A large actor-root displacement in one frame records ROOT JUMP for three seconds and logs a warning when enemy debug is enabled. Diagnostics do not move/correct the actor.

Verification covers offscreen skin preparation, baking a moved bone without relocating its root, spawn age/source and recording a simulated root discontinuity without changing position. Runtime/editor compilation passed. Isolated checks do not prove the exact cause in the recorded gameplay scene; replay that scene with F8 to confirm whether the pop is resolved and identify any remaining actor discontinuity.

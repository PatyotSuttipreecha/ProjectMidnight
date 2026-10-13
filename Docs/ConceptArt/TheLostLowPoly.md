# The Lost — simple 3D visual prototype

Source: Design - Documentation chat, latest completed summary read 13 October 2026. Accepted set: Unarmed, Machete, Wood Club, Heavy Axe, Stone, Torch. All enemies share an unarmed Base Module; equipment changes attacks and can be dropped to return to Base. This document proposes the visual body; it does not add extra accepted lore or implement behavior.

## Body design

One approximately 1.77 m human-scale silhouette: faded olive shirt, dark grey trousers, dark shoes, dusty muted skin, short dark hair, dark eye sockets. Slightly lowered arms and asymmetrical shirt patches suggest a worn civilian appearance. Human proportions and recognizable rural tools suit the project's Isan setting. No glowing eyes, ritual symbols, gore or special powers are added. The simple flat face is a placeholder, not a final face or anatomy pass.

## Readable equipment

| Variant | Visual cue | Animation direction from the accepted summary |
|---|---|---|
| Unarmed | Both hands visible | Reach, strike and briefly restrain |
| Machete | Long blade in right hand | Shared sweeping motion with club; distinct damage |
| Wood Club | Thick rough timber | Same sweeping motion as machete |
| Heavy Axe | Long haft and large metal head | Long anticipation, heavy strike, slow recovery |
| Stone | Single faceted stone | Prepare and throw from distance, then return to Base |
| Torch | Wrapped stick and warm flame silhouette | Club-like sweep plus burning status |

## Delivered assets

Six independent native Unity prefabs under `Assets/Prefab/Character/TheLostLowPoly`. Approximately 792–892 triangles each. Materials are URP flat colors; no textures or LODs. Root uses +Y up, +Z forward, ground-level origin. Visual/Weapon is separate from body mesh parts. WeaponGripPoint is a static placement marker, not a hand bone.

These are static visual prototypes to review silhouette and equipment, not animation-ready characters. No armature, skinning weights, Avatar, Animator, AI, combat or collider components are supplied. Do not replace the functioning Enemy prefab with these until they are rigged and configured. Torch flame is static geometry; there is no light, particle effect or damage. Weapon groups can be hidden or detached for visual prototyping, but disarm behavior still belongs to gameplay code.

Next production work: approve proportions, make connected deformation-friendly topology and proper hands, create a Humanoid rig and weights, validate shared animations, then attach weapons to the right-hand socket and configure enemy variants. Preserve the established enemy AI prefab and its references during integration.

Generator and source recipe: `Tools/TheLostLowPoly`. Isolated Unity validation: `Library/TheLostLowPolyValidation`. `TheLost_Preview.unity` displays all six variants in the order above. This task does not change the GDD or message other chats.

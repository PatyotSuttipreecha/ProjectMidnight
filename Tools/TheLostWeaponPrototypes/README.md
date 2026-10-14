# Five modular The Lost weapon prototypes

Folder: Assets/Prefab/Weapon/TheLostPrototypes.

Five standalone prefabs: Machete, WoodClub, HeavyAxe, Stone, Torch. Each has its own native mesh assets and copied URP materials; no dependency on the character mesh assets. Root origin and GripPoint are at the holding location, Y up. Machete/club/axe extend toward +Y; torch flame also points +Y. Stone grip is centered on the held rock.

Separate parts under Parts: MacheteGrip/MacheteBlade; AxeHandle/AxeHead/AxeCuttingEdge; TorchHandle/TorchWrap/Flame_StaticPlaceholder. The wooden club and stone are naturally single-piece meshes. Each weapon is a separate prefab too. Mesh geometry is in root coordinates, so individual mesh object origins coincide with the grip; move vertices or create a pivot parent for a new articulation point.

Each root has a BoxCollider. Disable held-weapon collision or set the correct layer when attaching to a hand. Add Rigidbody and gameplay handling when implementing drops. There is no attack damage, projectile, burning status, fire light, pickup logic or weapon-disarm code in these visual assets. Flame is static low-poly geometry.

Generated from the approved prototype weapon shapes, with no changes to existing character/rig prefabs. Use one new prefab beneath a hand socket, then adjust the grip orientation to the animation. Original character variants already contain their own weapon visuals; hide those to avoid duplicates.

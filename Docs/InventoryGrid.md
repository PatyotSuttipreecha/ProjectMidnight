# Inventory grid

## อาวุธตอนเริ่มเกม

Player prefab เริ่มด้วย Pistol อย่างเดียว ปรับรายการที่ PlayerController > Starting Loadout > Starting Weapon Items ได้ ส่วน Weapon Inventory Items เป็นรายการจับคู่โมเดลกับ SO ที่ระบบรองรับ จึงต้องคง Shotgun ไว้เพื่อให้เก็บและ Equip ภายหลังได้ การใส่ SO ในรายการจับคู่จะไม่มอบปืนนั้นตอนเริ่มเกมอีกต่อไป

The existing Playground InventoryManager and InventoryUI are used directly. The bag is 4×4; no new runtime auto-created Canvas is required.

## Controls

- I: open/close the inventory.
- Left drag: move an item. Green means the placement is valid; red means occupied or outside the bag.
- R while dragging: rotate 90 degrees, when Allow Rotation is enabled on the ItemSO.
- Escape while dragging: cancel. Invalid placement also returns the item to its original position.
- Drag onto the same ItemSO: transfer units into the other stack up to its limit; excess stays in the source stack.
- Right click: open the context menu. Choose Equip/Use, Examine, Drop or Close. Medicine/ammo Use consumes one unit. Examine never consumes an item.
- Drop opens confirmation before dropping the entire stack. Escape or clicking outside closes the menu. Shift + right click now also opens the menu rather than dropping immediately.

## Item settings

Select an ItemSO under Assets/Script/Inventory&ItemSO/ItemSO. Width/Height determine the footprint; Max Stack limits units per footprint. Weapons always occupy separate footprints.

Examples included: Pistol 2×1, Shotgun 4×1, Bandage 1×1 (five units per stack), 9mmAmmo 1×1 (six packs per stack, ten rounds per pack).

Pistol and Shotgun assets currently use text labels; assign Icon sprites for artwork. Right click a weapon to equip it without consuming or freeing its footprint. The equipped item shows an Equipped label. Number keys 1/2 select the corresponding owned Pistol/Shotgun; 4 selects unarmed. Switching or dropping the equipped weapon during reload is blocked. Pistol and Shotgun world pickup prefabs are already assigned on their SOs.

PlayerController's Weapon Inventory Items array maps ItemSO definitions to the corresponding Weapons array indices. The Player prefab includes Pistol and Shotgun mappings. These starting weapons are registered once at startup, including inactive holstered weapons. Registration waits for InventoryManager; if a starting weapon cannot fit, it is unavailable and a warning reports the configuration problem. Slots without ItemSO mappings cannot be equipped through the inventory. Slot 1 currently points to the same Barehand object as slot 0 and has no knife mapping.

Owned firearms remain in the bag while equipped. Moving or rotating their footprint does not unequip them. Removing an equipped weapon from the bag returns the player to unarmed. Using ammo requires a matching owned weapon, not just an inactive gun object on the character.

## World weapons and ammunition

- Drag Pistol_Pickup or Shotgun_Pickup from Assets/Prefab/Weapon/WorldPickups into a scene. The prefabs have a 1.4-metre pickup trigger and a kinematic Rigidbody for trigger detection.
- Mesh visuals are built from Weapon Visual Prefab at Play Mode startup, without copying Guns or other gameplay scripts. To preview a placed scene instance before playing, open its PickupItem component context menu and choose Build Weapon Preview. Selected pickups display the trigger radius as a Gizmo.
- Approach a weapon and press F. A nearby weapon displays its pickup prompt. If the whole item cannot fit, the pickup stays in the world and the inventory is unchanged.
- Shift + right click a weapon in the bag to drop it with its actual InventoryItemData instance. The world visual is positioned above nearby solid ground. This is a stationary pickup, not a simulated thrown weapon.
- Each weapon instance keeps its magazine/reserve counts. Switching between two copies of the same weapon loads the selected copy's state into the held weapon model. Shooting, reload completion and ammo addition update the bound instance.
- Ammo use targets the equipped compatible weapon first, otherwise an owned compatible weapon in the bag. Filling a holstered weapon does not change a different equipped weapon's state.
- Fresh scene weapons start with Starting Magazine Ammo / Starting Reserve Ammo from ItemSO (Pistol 12/36, Shotgun 5/20). Starting character weapons capture their actual serialized Guns values instead. Dropped weapons retain their existing values.
- Adding a different weapon type requires a matching Weapons / Weapon Inventory Items binding on PlayerController, its ItemSO, visual source, and world pickup prefab.

Attach PickupItem to a world pickup with a trigger collider; assign Item Data and Quantity (number of units/packs). Pickups now enter the bag first, including ammunition. A pickup that cannot fully fit leaves both the world object and existing bag stacks unchanged.

ItemSO stores shared definitions. InventoryItemData stores per-instance quantity, size and rotation; runtime changes do not modify the SO. ResizeGrid cancels if all existing items cannot fit.

## Validation

Run Tools/InventoryGridChecks/run.ps1 for 22 core checks, including independent ammo instances, drop/recollect identity, and full-bag preservation. Unity services are stubbed for these checks. Runtime/Editor C# compilation verifies integration; trigger detection, world mesh appearance, held weapon binding and UI behavior still need Unity Play Mode verification.

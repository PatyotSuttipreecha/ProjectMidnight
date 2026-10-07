# Central Interaction

PlayerInteraction selects one target and handles input/prompt for PickupItem, DocumentPickup and DoorController. Default key: F.

## Configuration
System Modification > Player Configuration > Interaction:
- Interact Key: default F for all supported interactions
- Max Distance: global reach limit (default 3 m); target Interaction Distance also applies
- Interaction Camera: optional; defaults to Camera.main
- Interaction Mask: include interactables and walls that should block access
- Prompt Text: optional TMP UI reference; otherwise a screen prompt is displayed
- Show Gizmos: reach sphere and line to selected object

Player prefab has the component; PlayerController adds it at startup if missing on an older scene object.
Doors require aiming at a solid collider under their DoorController. Pickups/documents can be selected while nearby and visible on screen; aiming directly wins, otherwise selection combines screen center proximity and distance. Solid objects between camera and target block interaction; player colliders and the selected object's own colliders are ignored. The prompt and F use the same selector; one press invokes one object only.

Legacy item/document trigger callbacks, per-object input keys and individual prompts have been replaced. Keep pickup colliders for scene geometry/authoring, but entering the trigger is no longer the eligibility condition. Reach is controlled by distance settings instead. Old serialized collectKey/interactKey fields no longer control input. Existing item TMP text is hidden; assign the shared Prompt Text to customize the new prompt. Collider and pickup data still need valid setup.

Inventory open, player death, disabled player and timeScale 0 block interaction. Collection failure and missing keys report through the central prompt. One shared key does not yet include hold-to-use or rebinding UI.

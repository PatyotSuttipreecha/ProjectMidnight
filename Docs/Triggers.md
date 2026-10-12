# World Trigger

Create a Trigger through System Modification > Trigger Configuration or add WorldTrigger to a dedicated GameObject. PointNote, BoxCollider and a kinematic Rigidbody are provided on that same object. Choose its Note Data asset from the catalog, or create a new data record; scene placement never comes from another scene marker.

Scene Notes is a game-wide data catalog. Creating data does not create a Trigger. Place Runtime Trigger instantiates a local trigger referencing the selected record. Title, descriptions and mission planning come from the SO; activation, Collider Center/Size and UnityEvents stay on the local WorldTrigger. Metadata does not automatically gate gameplay. WorldTrigger stores no Note reference or Note data. Configure data through the separate PointNote component; WorldTrigger Inspector contains only activation, events and volume settings.

WorldTrigger detects an active, living PlayerController on the entering collider or its parents. Multiple colliders of the same player count as one entry/exit. Other actors are ignored. Its required kinematic Rigidbody allows overlap events with the CharacterController player; ensure Physics layer collision settings permit the two layers to interact.

- Armed: whether activation is permitted; Arm / Disarm can be wired from another UnityEvent.
- Trigger Once: On Triggered fires once per component session unless Reset Trigger is called. State is not saved to disk.
- Cooldown: seconds before repeat activation when Trigger Once is disabled.
- Retry While Inside: off by default; enable to retry activation after arming or when blocked input ends while the player remains inside. With repeat activation enabled it repeats according to cooldown; a zero cooldown can fire each physics step.
- Allow While Gameplay Blocked: off by default; Inventory and document modal input blocking prevent activation, but entry/exit notifications still fire.
- On Player Enter / Exit: presence events on first contact and last exit, independent of armed/one-shot settings.
- On Triggered: the gated gameplay event. Use this for Timeline Play, spawning, door actions, enabling objects, etc.
- Activate: manually tries activation using the player currently inside. TryActivate(player) is the explicit script API.
- Show Gizmos / Gizmo Color: volume fill and outline; gray when disarmed, green after one-shot activation.

Example: select or place the Cutscene_01 data record, add World Trigger to its placement, adjust its volume across the path and connect On Triggered to a PlayableDirector's Play method. This invokes Timeline only; it does not switch cameras, stop player input, restore controls, or complete Missions automatically. Those require a cutscene controller/event wiring. Trigger disabling clears contacts without sending a synthetic exit event. Avoid disabling/re-enabling a trigger around a player already inside unless you accept a new presence entry. Activation is entry-based by default; arming while inside requires Retry While Inside or Activate.

No scene objects, Timeline assets or existing mission data are automatically altered by installing the scripts.

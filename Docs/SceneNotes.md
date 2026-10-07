# Point Notes / Scene Notes

Attach `PointNote` to any GameObject (Add Component > Level Design > Point Note). It is an authoring annotation, separate from collectible story documents.

Open `System Modification > Scene Notes`:
- Open Scenes: notes in loaded scenes, including inactive objects and the currently open Prefab Stage
- Prefab Assets: notes inside prefabs under the specified folder, including inactive children and nested prefabs
- Scenes + Prefabs: both lists; scene instances and prefab source entries are intentionally separate
- Search title, note content, hierarchy path and scene/prefab path
- Filter status: Info / Todo / Issue / Done
- Select an entry to see text, world/local position and hierarchy
- Select / Frame navigates to scene objects; Open Prefab opens the source for editing; Ping locates the object/asset
- Add Note to Selected Object attaches the component, or Create Note Point creates a marker at the Scene View pivot

Scene note fields can be edited in this window with Undo and scene/prefab overrides. Prefab asset entries are read-only here: edit in Prefab Mode then save and Refresh. Save scenes normally (Ctrl+S) to persist notes. Search scans prefabs only when refreshing or changing scope, not every frame. Folder changes and hierarchy changes require Refresh. Unopened scene assets are not scanned.

Gizmo colors follow Point Type. In Scene Notes, expand Point Type Colors to adjust Marker, Label background (including opacity) and Label text for each type. Settings are saved in ProjectSettings/PointNoteColors.asset and shared across scenes/prefabs. Reset restores the default for that type. Defaults use dark label backgrounds and white text. Enable Override Colors on an individual note to use its own colors. Status remains visible in the label. Show Gizmos / Show Label, marker size, label font size and offset are adjustable per note. Full note content remains in the Inspector/window to keep scene labels compact. Enable Gizmos in Scene View to see markers.

Runtime/Editor compile passed. Interactive Editor operation has not been visually verified.

## Point types and mission planning
Choose New Point Type before creating a marker, or change Point Type on an existing note. Types: General, Mission, Objective, Collectible, Loot, EnemySpawn, Patrol, Interaction, Puzzle, Trigger, Audio, SafeZone, Location. Use Point Type Filter to narrow the list.
Mission / Objective / Puzzle notes show Mission / Objective Design fields in the Inspector and Scene Notes: Mission ID, Objective ID, objective type (Collect/Reach/Interact/ReadDocument/Defeat/Other), description, required count and optional objective. Other types hide this section; switching types preserves previously entered data. Related Content can reference ItemSO, DocumentSO, a prefab or a scene object. These fields describe the intended content; they do not spawn items, grant quests, count collections or complete missions during play.
Example: Point Type Collectible, Mission ID M01, Objective ID M01_KEY, Objective Type Collect, Required Count 1, Related Item = door key, Note = place the key upstairs after the document clue. Several design points may share a mission ID.
Status is the annotation's work status: Info (reference), Todo (planned work), Issue (problem to fix), Done (design work complete). Done does not mean the player completed an objective. Point Type provides the grouping/filter; Category and Tags have been removed.
Search also includes mission/objective IDs, objective description and related item/document names.

## Mission relationships and hierarchy names

### Objective order
On an Objective, assign Prerequisite Objectives in the Inspector or Scene Notes. Empty means a starting objective; multiple entries mean all are required first. For A -> B -> C, leave A empty, put A in B's prerequisites, then put B in C's prerequisites. Use references to other Objective points in the same scene and with the same Mission ID. Array order alone still does not define dependencies. Solid arrows show prerequisite -> dependent; dotted arrows show Mission -> Objective membership. Selected-only display, link labels and maximum visible links can be adjusted on each Objective. Selecting a point shows its incoming links and direct dependents' links. Warnings cover empty/duplicate/self/wrong-type/cross-scene references and Mission ID mismatches; an error reports dependency cycles including indirect cycles. The Mission inspector also validates its assigned objectives. This is design metadata, not a runtime completion/unlocking system.
Auto Name In Hierarchy updates the object's name to `[PointType] Title` when editing through the Point Note Inspector or Scene Notes. Existing untouched notes can use Sync hierarchy name and mission IDs. Disable the option to keep a custom object name. Renames and ID synchronization support Undo and prefab instance overrides.

For a Mission point, set a descriptive Mission ID (for example `M01_ESCAPE_HOSPITAL`), expand Objectives, set Size to the desired number and drag Objective Point Note components into the slots. Assigned objectives shows the valid count. Each objective belongs to one mission in the same scene or Prefab Stage. Mission ID propagates to those objectives; an empty Objective ID receives `<MissionID>_OBJ_01`, etc. Existing Objective IDs are preserved; use descriptive values such as `M01_FIND_KEY`. Duplicate references, invalid slots, duplicate Objective IDs and mismatched Mission IDs show warnings. Shared objectives with multiple owning missions are not automatically synchronized. Removing a slot removes the visual link but preserves the objective's existing IDs.

Show Objective Links draws dotted arrows from mission to objectives. Links Only When Selected defaults to enabled: select the mission or one of its objectives to reveal that group's links. Show Link Labels is off by default; Max Visible Links defaults to 20. Turn off Show Objective Links or Show Gizmos to hide links entirely. Array order is a design list, not gameplay prerequisite logic. Required Count means the amount to collect/perform within one objective, not the mission's objective count. Mission references still describe design; they do not run quests during play.

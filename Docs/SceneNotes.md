# Scene Notes — game-wide data catalog

PointNoteData is the authoritative ScriptableObject record. Scene Notes lists every PointNoteData asset in the project, including unplaced records and records intended for unopened scenes. Records have a stable internal record ID, Point Type, Title, Status, Note, Location/region and Planned Scene. Mission/Objective/Puzzle fields are shown conditionally; Mission/Objective descriptions remain separate. Mission objectives and prerequisite relationships reference data assets, not scene objects. Related Item/Document/Prefab are asset references.

PointNote is a placement component with a Note Data reference. Its position/rotation, scene Related Object and gizmo settings are local. Multiple scene/prefab points may share one record; editing the record changes all references. Deleting a placement does not delete its record. Position and UnityEvents are never stored in the data asset. References may span chapters/scenes; Gizmos show edges only between available matching placements in the same loaded scene. For multiple placements of the same record in one scene, graph resolution uses the first matching placement.

## Workflow
1. Open System Modification > Scene Notes (All Game Data).
2. Create New Note Data adds an asset under Assets/Data/PointNotes; it does not create a scene object.
3. Set Location and Planned Scene for the intended area; use search and Location/Point Type/Status filters. List order is Mission ID (natural order), Mission before its objectives, then Objective ID/Title. Entries use two compact lines; tooltips show details.
4. Place Note Point creates a scene placement referencing the selected record. Trigger records also offer Place Runtime Trigger. Bind Data to Selected GameObject assigns the record to an existing selected object's PointNote (adding the component if missing).
5. A new WorldTrigger automatically has a PointNote on the same GameObject. In either Inspector, choose Note Data using the object picker, or Create Data from This Note to create/bind an asset. No separate scene Note source or position copying is needed.
6. Edit note data through Scene Notes or the PointNote Inspector. Configure activation, volume and events separately in the WorldTrigger Inspector or Trigger Configuration.
7. Save scenes/prefabs and assets normally. No runtime Mission completion or Save/Load is added.

The Loaded Placements section lists references in currently loaded scenes/Prefab Stage; absence there does not mean the record is unused across the whole project. Location/Planned Scene describe design intent, not an automatically maintained index of every physical placement in unopened scenes.

## Existing notes
Old inline serialized fields are retained as hidden compatibility fields via FormerlySerializedAs. Existing notes continue to display their previous values until linked/imported; they are not automatically overwritten by choosing a different record. Import Notes from Open Scenes creates asset records, binds current points and preserves objective/prerequisite references (including connected legacy points). It marks scene changes for saving. Create Data from This Note imports an individual note and its outgoing relationships.

Import All Saved Scenes / Prefabs scans Assets for existing PointNote script references, loads matching prefabs/scenes, imports legacy records and saves the updated assets. Already linked records are reused. Open dirty scenes and the open Prefab Stage are skipped to protect unsaved work; import those separately and save normally. Import is incremental, so a failure may leave already imported records/assets in place; inspect the Console and rerun after addressing the error. Importing a scene with missing dependencies requires those dependencies to be fixed first. Old Related Object remains local; only persistent prefab references can be copied to the data asset.

## Naming, status and links
Auto Name In Hierarchy provides two-way synchronization: [PointType] Title. Renaming a linked placement changes its shared record's Title; all linked points follow. Disable the setting to keep an independent object name. Status remains authoring work status (Info/Todo/Issue/Done), never gameplay progress. Category/Tags are removed.

Mission data lists Objective Data records. Set a meaningful Mission ID; objective IDs are generated only when blank. Existing IDs are preserved. Empty Prerequisite Objectives means a starting objective; all listed prerequisites must happen first. For A -> B -> C, B references A and C references B. Validation checks distinct records, type, Mission ID consistency and cycles without needing scene placements. Dotted arrows show membership; solid arrows show prerequisite -> dependent. Selecting a placed Mission reveals its placed objectives' prerequisite links. Selected-only display, optional link labels, maximum visible links and per-type colors reduce clutter.

## Primary and Related Note Data
Point Note เลือก Primary Note Data หนึ่งรายการเพื่อกำหนดชื่อ ประเภท และสี ในข้อมูลหลักเพิ่ม Related Note Data ได้หลายรายการ เช่น Primary = cutscene_01 (Trigger) และ Related = intothewood_reach_01 (Objective) โดยไม่ต้องสร้างจุดซ้ำในฉาก

ความสัมพันธ์บันทึกใน PointNoteData กลาง จึงเห็นเหมือนกันทุกจุดที่อ้างข้อมูลหลักนี้ เปิดข้อมูลที่เกี่ยวข้องด้วยปุ่ม Open ได้ทั้งใน Point Note, Scene Notes และ Inspector ของ asset ข้อมูลที่เกี่ยวข้องยังคงมีประเภท/คำอธิบายของตัวเอง ไม่รวมเป็นข้อมูลหลัก และไม่สั่งให้ Objective สำเร็จหรือเล่น Timeline อัตโนมัติ


### Related Notes in Scene View
เปิด Show Gizmos, Show Label และ Show Related Notes เพื่อแสดงข้อมูลที่เกี่ยวข้องใต้ชื่อหลัก โดยแสดงประเภทและชื่อจาก Related Note Data โดยตรง ไม่ต้องมีจุดวางของข้อมูลที่เกี่ยวข้อง แสดงไม่เกิน Max Related Note Labels (ค่าเริ่มต้น 3) พร้อมจำนวนที่เหลือ ข้ามรายการว่าง/ซ้ำ/ตัวเอง หากฉากรกเปิด Related Notes Only When Selected ให้แสดงเฉพาะตอนเลือกจุด สีและชื่อ Hierarchy ยังใช้ Primary Note Data


## Scene Configuration Menu
เข้าเครื่องมือผ่าน System Modification > Scene Configuration > Scene Notes และสลับการแสดง Note ทั้งหมดผ่าน Show Point Notes ส่วนหน้าต่าง Trigger อยู่ที่ Scene Configuration > Triggers


## Prefab Prefix and Data Asset Naming
จุดที่มีชื่อขึ้นต้น Prefab_ หรือเป็น instance ของ prefab ชื่อขึ้นต้น Prefab_ จะคง prefix และเปลี่ยนเฉพาะ Title เช่น Prefab_Evidence_RinCamera -> Prefab_Evidence_RonCamera ระบบจำ prefix ใน Hierarchy Name Prefix ซึ่งแก้เองได้เมื่อชื่อมี underscore หลายส่วน จุดธรรมดายังใช้ [Point Type] Title หากชื่อจุดถูกเปลี่ยนเป็นรูปแบบเก่าแล้ว กด Sync hierarchy name เพื่อกู้ prefix จาก prefab ต้นทาง

Note Data ใช้ทุกโฟลเดอร์ใต้ Assets ตามด้วย PointType และ Title เช่น Assets/Data/PointNotes/RinCamera.asset -> Data_PointNotes_Objective_RinCamera.asset เมื่อแก้ Title หรือกด Sync จึงเปลี่ยนชื่อให้ โดยไม่เปลี่ยน GUID หรือช่อง Title เป็นชื่อยาว ส่วนชื่อไฟล์ prefab ใน Project ไม่ถูกเปลี่ยนจากการแก้ชื่อ instance ในฉาก


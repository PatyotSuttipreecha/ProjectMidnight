# Documents and Evidence

ใช้ Catalog, Pickup, Examine, การล็อก input และสถานะการอ่านร่วมกัน หลักฐานมี EvidenceSO แยกจาก DocumentSO แต่สืบทอดข้อมูลพื้นฐานเดียวกัน ไม่ใช้ช่อง Inventory และไม่เพิ่ม EvidenceCollection อีกชุด

## Create and place

1. ไปที่ System Modification > ItemLab > Evidence แล้วกด Create Evidence (หรือ Assets > Create > Documents > Evidence)
2. Information: ตั้ง title, author (ใช้เป็นที่มาหรือเจ้าของ), summary และ icon
3. 3D Model: ใส่ Inspection Prefab และมุมเริ่มต้น โมเดลนี้ใช้กับ Examine
4. Details / Images: เขียนข้อความใน Pages และรูปใน Detail Images รูป index 0 ตรงกับข้อความหน้า 0 ถ้ามีรูปมากกว่าข้อความ หน้าที่เหลือยังเปิดดูรูปได้
5. Scene / UI: เลือกหรือค้นหา Document Collection แล้วเพิ่มข้อมูลเข้า Catalog เพื่อรองรับการคืนสถานะจาก IDs
6. กดวาง Pickup ที่จุดกลาง Scene View สำหรับ Evidence จะสร้าง GameObject ใหม่พร้อม trigger collider, DocumentPickup และ Point Note และคัดลอก MeshFilter/MeshRenderer จาก Inspection Prefab เป็น visual โดยไม่คัดลอกสคริปต์

หากมี RinCamera วางอยู่แล้ว ไม่ต้องวางซ้ำ: เพิ่ม DocumentPickup บนกล้องและเลือก EvidenceSO ในช่อง Document (ช่องนี้รองรับทั้งสองชนิด) Point Note เพิ่มอัตโนมัติ เลือก Objective เป็น Primary ได้ และช่อง Related Document / Evidence ใน Note Data รับ EvidenceSO ได้เช่นกัน โมเดลที่มีอยู่ในฉากไม่ถูกเขียนทับ

World visual ที่สร้างให้อัตโนมัติรองรับ static mesh สำหรับวัตถุอย่างกล้อง ไม่สร้าง SkinnedMeshRenderer/rig ถ้าต้องการ visual เฉพาะให้วางโมเดลเองใต้จุด Pickup

## Player flow

กด F > Examine 3D > View Details > อ่านข้อความ/ดูรูปและเปลี่ยนหน้า > Back กลับโมเดล > Close เพื่อเก็บเข้า Catalog ปิด host โดยระบบหรือเปลี่ยนฉากโดยไม่ได้กด Close จะยกเลิกการเก็บเหมือน Document เดิม

ปุ่ม Catalog ใน Inventory เปิดหน้าต่างเดียวกัน มีแท็บ Documents / Evidence และDropdown เลือกข้อมูลด้านขวาแทน Previous/Next Document ส่วนตอนหยิบของใหม่จะซ่อนแท็บเพื่อไม่ให้เปลี่ยนจากของที่กำลังตรวจ หลักฐานใช้สถานะ Reviewed / Not reviewed ส่วนเอกสารใช้ Read / Unread

## Editable UI and persistence

แก้ UI ได้ที่ Assets/Prefab/UI/Prefab_UI_DocumentLibrary.prefab: Documents Tab, Evidence Tab และ Evidence Detail Image มี references ใน DocumentLibraryView หน้า Evidence Details มีโมเดล 3D หมุน/ซูมได้ด้านซ้าย ข้อความด้านขวา และรูปประกอบเหนือข้อความเมื่อมีภาพ ส่วน Document ใช้หน้าอ่านเต็มพื้นที่

เครื่องมือ Upgrade Catalog UI เพิ่มองค์ประกอบที่ขาดให้ prefab นี้อย่างเดียว ทำงานหลัง compile ใน Editor หรือเรียกผ่าน System Modification > ItemLab > Upgrade Catalog UI หากกำลังเปิด prefab นี้อยู่จะข้ามเพื่อไม่ชนการแก้ใน Prefab Mode ให้ปิดก่อนแล้วเรียกเมนู หากใช้ view prefab ของตัวเองต้องเพิ่มและผูก references เหล่านี้ด้วย

ID และ CaptureState/RestoreState ใช้ระบบเดียวกับ Document เดิม ไม่เปลี่ยน IDs ของเอกสารเก่าและรองรับข้อมูลเซฟเดิมที่ version 1 แต่ยังไม่ใช่ระบบ Save/Load ลงไฟล์ทั้งเกม การโหลดต้องมี assets ที่ใช้ใน Catalog การเปิดดู/เก็บยังไม่ทำ Objective สำเร็จอัตโนมัติ

Catalog และ Evidence ใช้ layout โมเดลซ้าย/ข้อมูลขวาตั้งแต่เปิด Evidence แสดงรายละเอียดทันที จึงไม่มีปุ่ม View Details/Back to Model ซ้ำขั้นตอน ส่วน Document กด Read เพื่ออ่านหน้าเต็ม และ Back กลับ Catalog ได้

## Current Catalog and pickup layout
Evidence pickup starts with a centered rotatable model, matching Document pickup. View Details opens the separate detail view; Back to model or Esc returns to the model. Closing commits collection.
Catalog shows the model on the left and a persistent vertical scrollable record list on the right. Selecting a record does not open its details. Read / View Details opens the reader separately. Customize Catalog List and Catalog Row Template in Assets/Prefab/UI/Prefab_UI_DocumentLibrary.prefab.

## Cutscene after collecting
On the scene Evidence object's DocumentPickup component, add an On Collected listener referencing an active separate CutsceneController and select CutsceneController.Play. Assign its PlayableDirector Timeline and bindings. The event runs only after a successful pickup preview close and collection, with the preview input lock released. Reading/details Back or the first Esc from the reader returns to the model; closing that model commits collection and invokes the event. Cancelling by disabling the UI does not collect or invoke it. Keep the cutscene controller outside the pickup object because the pickup is deactivated on collection. Opening a collected record from Catalog does not invoke this pickup event.

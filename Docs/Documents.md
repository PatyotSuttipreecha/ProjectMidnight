# ระบบเอกสารและ Side Story

เอกสารแยกจาก ItemSO และ Inventory Grid ไม่ใช้ช่องกระเป๋า ไม่เป็นกอง และไม่มีคำสั่งใช้/ทิ้ง สถานะเก็บและอ่านอยู่ใน DocumentCollection ส่วน DocumentSO เก็บข้อมูลต้นแบบเท่านั้น

## ทดลองใน Playground

มี Document System และ Document Pickup ตัวอย่างวางไว้ใกล้จุดเริ่มผู้เล่น (ประมาณ X=10.91, Y=0.20, Z=2.08)

1. เดินเข้าใกล้เอกสารแล้วกด **E** เพื่อเก็บ ปุ่มนี้แยกจาก F ที่เก็บไอเทมทั่วไป ปรับ Collect Key บน DocumentPickup ได้
2. กด **I** เปิดกระเป๋า แล้วกด **Documents**
3. เลื่อน Previous document / Next document เพื่อเลือกเอกสารที่ค้นพบ แสดงสถานะ Read / Unread และผู้เขียน
4. ลากเมาส์ซ้ายหมุนโมเดล ใช้ลูกล้อซูม หรือ Reset view
5. กด Read document เพื่ออ่าน มี Previous page / Next page และเลื่อนข้อความยาวได้
6. Back to model หรือ Esc ขณะอ่านกลับไปดูโมเดล Close ปิดหน้าต่างกลับกระเป๋า Esc ขณะดูโมเดลปิดหน้าต่างเช่นกัน

การเปิดเอกสารผ่าน Inventory ใช้การล็อกการเคลื่อนที่/กล้องของ Inventory เดิม หากปิด Inventory หน้าต่างเอกสารจะปิดตาม เอกสารที่เก็บแล้วไม่เพิ่มจำนวนไอเทมใน Grid และจุดเก็บที่ใช้ DocumentSO เดียวกันไม่ให้เก็บซ้ำ

## ตั้งค่าเอกสาร

เปิด **System Modification > Document Configuration** สร้าง/เลือก DocumentSO แล้วปรับ:

- Title, Author, Summary และ Icon
- Inspection Prefab และ Inspection Rotation สำหรับโมเดล 3D หากไม่มีโมเดลยังอ่านได้ ใช้ Icon หรือข้อความแทน
- Pages: แต่ละช่องเป็นหนึ่งหน้า ใช้ขึ้นบรรทัดใหม่ตามต้องการ หน้าอ่านเลื่อนข้อความได้

รหัสเอกสารสร้างอัตโนมัติและต้องไม่ซ้ำ หาก Duplicate asset แล้วรหัสซ้ำ หน้าต่างตั้งค่ามีคำเตือนและปุ่มสร้างรหัสใหม่ให้สำเนา อย่าเปลี่ยนรหัสเอกสารเดิมที่ใช้กับเซฟแล้ว

กด **วาง Document Pickup ที่จุดกลาง Scene View** เพื่อสร้าง Prefab instance และผูกเอกสารที่เลือก พร้อมเพิ่มเข้า Catalog ของ DocumentCollection ในฉาก เลื่อนตำแหน่งวัตถุและรัศมี SphereCollider ได้ตามต้องการ ต้องมี DocumentSystem.prefab ในฉากด้วย

World Pickup ตัวอย่างแสดงกระดาษทั่วไป หากต้องการรูปร่างเฉพาะในแมพ ให้ปรับ child visual ของจุดเก็บ ส่วน Inspection Prefab คือโมเดลที่แสดงในหน้าตรวจ

## ออกแบบ UI ใน Edit Mode

- `Assets/Prefab/UI/DocumentLibrary.prefab`: หน้าตรวจ 3D และหน้าอ่าน อยู่ใน Model View / Reading View เปิด Active ของ Reading View เพื่อออกแบบหน้าอ่านได้
- `Assets/Prefab/UI/DocumentsButton.prefab`: ปุ่มเปิดเอกสาร เป็น nested prefab ใน InventoryPanel
- `Assets/Prefab/Documents/DocumentPickup.prefab`: จุดเก็บและโมเดลในแมพ
- `Assets/Prefab/Documents/DocumentModel.prefab`: กระดาษตัวอย่างที่หมุนดูได้ทั้งสองด้าน
- `Assets/Script/Documents/Data/GuardNote.asset`: เนื้อหาเอกสารตัวอย่างสองหน้า

คงการเชื่อมช่อง DocumentLibraryView ไว้เมื่อจัดตำแหน่งหรือย้ายวัตถุ หากแทนด้วยวัตถุใหม่ให้ลากเชื่อมใหม่ ฟอนต์ UI ใช้ TMP และต้องเลือก Font Asset ที่รองรับภาษาไทยเมื่อเขียนเอกสารภาษาไทย ตัวอย่างเริ่มต้นเป็นภาษาอังกฤษ

## เชื่อม Save/Load

DocumentCollection อยู่ต่อเมื่อเปลี่ยนฉากระหว่างเล่น แต่ยังไม่เขียนไฟล์เซฟอัตโนมัติ เมื่อหยุดเกมสถานะจะถูกเริ่มใหม่ มี API สำหรับเชื่อมระบบเซฟ:

```csharp
var state = DocumentCollection.Instance.CaptureState();
string json = JsonUtility.ToJson(state);
// ระบบ Save หลักนำ json ไปบันทึกพร้อมข้อมูลผู้เล่น
var loaded = JsonUtility.FromJson<DocumentCollection.SaveState>(json);
DocumentCollection.Instance.RestoreState(loaded);
```

ลงทะเบียน DocumentSO ทั้งหมดที่ต้องอ่านย้อนหลังใน Catalog เพื่อให้รหัสที่โหลดจากเซฟแปลงกลับเป็นเอกสารได้ จุดเก็บใหม่จะซ่อนตัวเองหาก Collection มีรหัสนั้นแล้ว ควรโหลดสถานะก่อนเข้าแมพ/เริ่มการเก็บของรอบใหม่

## หน้าต่างจัดการเอกสารใน Editor
เปิด `System Modification > Document Configuration`
- รายการด้านซ้ายค้นหาจากชื่อเอกสาร ชื่อ Asset หรือผู้เขียนได้
- แถบเครื่องมือสร้างเอกสาร ทำสำเนา บันทึก และแสดงตำแหน่ง Asset; สำเนาได้รับรหัสใหม่เพื่อแยกสถานะการเก็บ
- แท็บ **ข้อมูล**: ชื่อ ผู้เขียน สรุป ไอคอน และตรวจรหัสซ้ำ
- แท็บ **หน้าเอกสาร**: เพิ่ม ทำสำเนา ลบ เลื่อนลำดับ และแก้เนื้อหาทีละหน้า รองรับ Undo / Redo
- แท็บ **โมเดล 3D**: เลือก Inspection Prefab และมุมเริ่มต้น พร้อมพรีวิวโมเดล Asset ที่ลากหมุนได้
- แท็บ **ฉาก / UI**: ค้นหาหรือเพิ่ม Document System, เพิ่มเอกสารเข้า Catalog, วาง Pickup ที่ Scene View pivot และเปิด UI / Pickup Prefab เพื่อออกแบบ
- ปิด **ตาม Selection** หากต้องการคงเอกสารที่กำลังแก้ขณะเลือก Asset อื่น
การวาง Pickup ใช้ภาพใน Pickup Prefab; หากต้องการให้ภาพในแมพตรงกับเอกสารแต่ละชนิด ให้ปรับโมเดลลูกของ Pickup ที่วางไว้ด้วย

อัปเดต Interaction กลาง: ใช้ F ผ่าน PlayerInteraction แทนปุ่มรายวัตถุเดิม ดู Docs/Interaction.md สำหรับระยะ การเลือกเป้าหมาย และ UI prompt

## เปิดเอกสารก่อนบันทึกการเก็บ
กด F ที่เอกสารในแมพจะเปิด Examine ทันที หมุน/ซูมและกด Read ได้ เมื่อกด Close หรือ Esc จากหน้าโมเดล ระบบจึง Collect เอกสารและซ่อนจุดเก็บ ถ้าอ่านระหว่างพรีวิวจะบันทึก Read พร้อมกัน Esc จากหน้าอ่านกลับหน้าโมเดลก่อน
ระหว่างเปิดพรีวิวหยุด input เดิน/กล้อง/ยิง/Interaction ของผู้เล่น แต่ไม่หยุดโลกหรือศัตรู; ใช้ UI Prefab ของ Documents เดิมผ่าน host ที่เปิดได้แม้ Inventory ปิด
Document Pickup > Pickup View Prefab เป็น override แบบ optional หากเว้นจะหา viewPrefab จาก Documents UI ในฉาก ต้องมี Canvas ที่ active และ DocumentCollection หากขาดจะรายงานข้อผิดพลาดและไม่เก็บไอเทม
การ Disable host โดยระบบเป็นการยกเลิก ไม่บันทึกการเก็บและคืน input ส่วน Catalog คือข้อมูลเอกสารที่รู้จัก ซึ่งแยกจาก collected/read state; Collect จะเพิ่มข้อมูลเข้า runtime catalog lookup เมื่อปิดพรีวิว

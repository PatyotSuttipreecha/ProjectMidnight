# การตรวจไอเทมและ Context Menu

## ตั้งค่าใน Editor

เปิด **System Modification > Item Configuration**

- แท็บ Item Settings: ค้นหา เลือก หรือสร้าง ItemSO ปรับชื่อ คำอธิบาย รูป ขนาด จำนวนต่อกอง การรักษา กระสุน World Pickup และโมเดล Examine พร้อมดูโมเดลต้นแบบใน Editor
- แท็บ Inventory / Examine UI: กดค้นหา UI ในฉากเพื่อเลือก Inventory Panel ใหม่ แม้วัตถุซ่อนอยู่ ปรับ cellSize/spacing ฟอนต์ชื่อบนช่อง และความไวหมุน/ซูม พร้อมปุ่มเปิด Prefab ของแต่ละหน้าต่าง
- เลือก UI ใน Hierarchy เพื่อเปิด Inspector ของ component เดิมได้ ค่าบนวัตถุในฉากต้องบันทึก Scene ส่วน ItemSO เป็น asset บันทึกด้วยปุ่มบันทึก ItemSO หรือ Unity Save
- ออกแบบหน้าต่างใน Edit Mode โดยเปิด Prefab ใน `Assets/Prefab/UI`: `InventoryPanel.prefab` คือกระเป๋า, `InventoryContextMenu.prefab` คือเมนูคลิกขวา, `InventoryDropConfirmation.prefab` คือหน้ายืนยัน และ `ItemExamine.prefab` คือหน้าโมเดล 3D ปรับ RectTransform, Image, TMP และ Button ใน Prefab Mode ได้โดยตรง ระบบเติมข้อความ/รูปและเชื่อมคำสั่งปุ่มเมื่อเปิดหน้าต่าง
- Playground มี `Canvas > Inventory Panel` เป็น Prefab instance ใหม่ เชื่อมกับ Player แล้ว กดปุ่มแสดง Panel ใน Item Configuration หรือเปิด Active ใน Inspector เพื่อจัดหน้าตา แล้วบันทึก Scene หากอยากใช้หน้าตาเดียวกันทุกฉาก ให้แก้ Prefab หรือ Apply Override ที่ต้องการ ระบบซ่อน Panel เมื่อเริ่มเกมและเปิดด้วย I
- โครง Grid มีช่องตัวอย่าง 4×4 สำหรับดูใน Edit Mode ตอนเล่นระบบสร้างช่องตามขนาดกระเป๋าจริง ปรับหน้าตาของช่องจริงที่ `SlotPrefab.prefab` และพื้นหลังไอเทมที่ `ItemUIPrefab.prefab` ไม่วางองค์ประกอบถาวรใต้ Inventory Grid เพราะลูกของ Grid จะถูกสร้างใหม่
- เก็บการอ้างอิงใน InventoryPopupView ให้ครบเมื่อเปลี่ยนชื่อ/ย้ายองค์ประกอบ หากแทนวัตถุเดิมด้วยวัตถุใหม่ ให้ลากเชื่อมช่องนั้นอีกครั้ง เมนูคลิกขวาใช้ Menu Panel ที่ anchor กลางและ pivot ซ้ายบนเพื่อจัดตำแหน่งข้างเมาส์
- ค่าที่แก้บน InventoryUI ตอน Play Mode เป็นค่าชั่วคราว แต่ค่าของ ItemSO คงอยู่หลังหยุดเล่น รองรับ Undo ผ่าน SerializedObject

เปิด Inventory ด้วย I แล้วคลิกขวาที่ช่องใดก็ได้ของไอเทม จะเปิดเมนูคำสั่ง ไม่ใช้ของหรือทิ้งของทันที

- **Equip**: เลือกถือปืน ไอเทมยังใช้พื้นที่เดิม ปืนที่ถืออยู่แสดง Equipped
- **Use**: ใช้ยาหนึ่งชิ้นหรือกระสุนหนึ่งแพ็ก ปุ่มที่ใช้ไม่ได้แสดงเหตุผล เช่น ไม่สามารถรักษา ไม่มีปืนที่เข้ากัน หรือกำลังรีโหลด
- **Examine**: แสดงโมเดล 3D กลางจอพร้อมชื่อ ขนาด จำนวน และคำอธิบาย ลากเมาส์ซ้ายเพื่อหมุน ลูกล้อเพื่อซูม และ Reset view เพื่อกลับมุมเริ่มต้น ปืนแสดงกระสุนในแม็กและสำรอง ยาแสดงค่ารักษา กระสุนแสดงจำนวนต่อแพ็กและประเภทปืนที่ใช้ได้
- **Drop**: เปิดหน้าถามยืนยันก่อนทิ้งทั้งกอง ระบบยังตรวจ Pickup Prefab และไม่ให้ทิ้งปืนที่ถือระหว่างรีโหลด
- **Close / Escape**: ปิดหน้าต่างโดยไม่เปลี่ยนไอเทม คลิกด้านนอกปิดได้เฉพาะ Context Menu และยืนยัน Drop หน้า Examine ปิดด้วย Close หรือ Escape

คำอธิบายแก้ที่ Description ของ ItemSO โมเดลแก้ที่ Inspection Prefab และมุมเริ่มต้นแก้ที่ Inspection Rotation หากไม่กำหนดโมเดล จะใช้ Weapon Visual Prefab หรือโมเดลจาก Pickup Prefab หากไม่มี Mesh จะแสดง Icon แทน

ระบบคัดลอกเฉพาะ Mesh และ Material ไม่เปิดสคริปต์ Collider หรือ Rigidbody ของไอเทมต้นแบบ จัดวัตถุไว้กลางจอและปรับสเกลอัตโนมัติ ใช้กล้องและ RenderTexture แยกพร้อมไฟสำหรับตรวจดู และคืนทรัพยากรเมื่อปิดหน้า ใช้ URP ของโปรเจกต์นี้ โมเดลภาพถ่ายควรมี Mesh และ Material ทั้งด้านหน้าและหลังถ้าต้องการหมุนอ่านทั้งสองด้าน

เมนูใช้ Canvas ของ Inventory และปิดเมื่อปิดกระเป๋าหรือข้อมูลกระเป๋าเปลี่ยน ฟอนต์หน้าต่างแก้บน TMP ของ Prefab แต่ละตัว ส่วน Menu Font บน InventoryUI ใช้กับข้อความในช่องไอเทม หากเขียนคำอธิบายภาษาไทย ให้ใช้ TMP Font Asset ที่รองรับอักษรไทย

เพิ่มคำอธิบายตัวอย่างให้ Pistol, Shotgun, Bandage และ 9mmAmmo แล้ว เมนูปิดและการตรวจไอเทมไม่ต้องมี Pickup Prefab แต่การทิ้งต้องมี

ตรวจคอมไพล์ Runtime/Editor และตรวจ logic กระเป๋าด้วยชุดทดสอบแยก แสง สี Material การหมุน ซูม ฟอนต์ และการคลิกจริงยังต้องตรวจใน Unity Play Mode

สำหรับ UI Prefab ตรวจเพิ่มเติมด้วย Unity import ในโปรเจกต์แยก พบว่าทั้ง 4 Prefab โหลดได้ มี view/panel references ครบ และไม่มี missing scripts การตรวจนี้ใช้สคริปต์จำลองสำหรับ serialization จึงไม่ได้ยืนยัน gameplay ในฉากหลัก

แก้ปัญหาภาพโมเดลว่าง: RawImage ของ 3D Item View ต้องมี UV Rect ขนาด Width=1, Height=1 โดย Rect ในไฟล์ Prefab ใช้ serializedVersion 2 ค่าศูนย์จะอ่านเพียงพิกเซลโปร่งใสที่มุมภาพ สคริปต์คืนค่า UV ที่มีขนาดศูนย์ตอนเปิด Examine แล้ว ตรวจเพิ่มด้วยสคริปต์ Preview จริงและ PC URP Renderer ในโปรเจกต์แยก เห็นโมเดลผ่าน RawImage บน Canvas หลังแก้

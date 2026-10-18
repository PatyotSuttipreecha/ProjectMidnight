# หน้าต่าง System Modification

หน้าต่างตั้งค่าใช้หัวหน้าต่าง การ์ด แท็บ และรายการเลือกแบบเดียวกัน โดยใช้สีแยกหมวด:

| สี / หมวด | หน้าต่าง |
| --- | --- |
| เขียว / Enemy Lab | Enemy Settings, Profiles, Patrol Routes, Spawn Points, Groups, Weapon Modules, Shared Combat Settings |
| ส้ม / Armory Lab | ตั้งค่าปืน Accuracy, Breathing, Recoil และ Shotgun Pellets |
| น้ำเงิน / ItemLab | Item Settings, Inventory / Examine UI, Documents, Evidence และ Item Spawn Points |
| ม่วง / Player | Player Configuration และ Loadout / Inventory / Interaction |
| เหลือง / Scene Configuration | Scene Notes, Door Configuration และ Triggers |
| แดง / Debug | Cheat Menu สำหรับทดสอบใน Play Mode |

## การใช้งาน

- เมนูเข้าหน้าต่างอยู่ใน System Modification ตามเดิม
- หัวหน้าต่างแสดงหมวด โหมด Edit/Play และชื่อเป้าหมายปัจจุบันเมื่อมีการเลือก
- แท็บปรับจำนวนคอลัมน์ตามความกว้าง เช่น Enemy Lab ย่อเป็นสองแถวได้เมื่อหน้าต่างแคบ
- Enemy Profile แสดงค่าของแต่ละหมวดเป็นการ์ด ยังเลือก Settings Category และค้นหาช่องได้ตามเดิม รวมคำอธิบาย Hidden Settings
- Scene Notes, Item และ Document/Evidence มีรายการเลือกที่เน้นแถวปัจจุบันชัดขึ้น
- ปุ่ม Locate ในแถบล่างชี้เป้าหมายปัจจุบันใน Unity; ปุ่ม Save / Create / Apply เฉพาะระบบยังอยู่ในเนื้อหาของหน้าต่างนั้น
- Cheat Menu มีพื้นที่เลื่อน เพื่อเข้าถึง Survival, Movement และ Enemy Debug เมื่อหน้าต่างเตี้ย
- Document/Evidence มีขนาดขั้นต่ำ 720 × 500 เพื่อรักษาพื้นที่รายการและรายละเอียดสองฝั่ง

## โค้ดที่ใช้ร่วมกัน

Assets/Editor/ModificationGUI.cs จัดสี หัวหน้าต่าง การ์ด แท็บ Section และแถวรายการ ใช้เฉพาะ Editor เมื่อต้องการปรับหน้าตาของเครื่องมือชุดนี้ให้แก้ส่วนกลางดังกล่าว ส่วน Shared Combat Settings มีการจัดวางเฉพาะสำหรับการ์ดสรุปโควตาและโอกาสวิ่ง

## การตรวจรอบ 17 ตุลาคม 2026

คอมไพล์ Editor ผ่าน และตรวจ diff / โครงสร้าง scope ของหน้าต่างที่แก้แล้ว ยังไม่ได้ตรวจภาพของหน้าต่างใหม่ทั้งชุดใน Unity เนื่องจากเครื่องมือเปิดหน้าต่างอัตโนมัติไม่ตอบสนองระหว่างรอบนี้ ไม่ได้ปรับพฤติกรรม gameplay จากงานจัดหน้าต่างนี้

## Player Configuration: Aiming movement
เปิด System Modification > Player Configuration > Player > Aiming movement
- Aim Move Multiplier: สัดส่วนเทียบ Walk Speed (0–1) ค่าเริ่ม 0.5
- Aim Backward Penalty (m/s): หักความเร็วเมื่อถอยหลังขณะเล็ง ค่าเริ่ม 0.2; ผลลัพธ์ไม่ต่ำกว่า 0
- Aim Move Speed / Aim Backward Speed: ค่าอ่านอย่างเดียวจากสูตร ปรับ Walk Speed แล้วอัปเดตตาม
Player Prefab ปัจจุบันเดิน 1 m/s จึงเล็งเดิน 0.5 และเล็งถอย 0.3 m/s เหมือนเดิม
Sprint ไม่ทำงานระหว่างเล็ง และ enemy slow/hold จะคูณภายหลัง; ค่าหน้าต่างเป็นค่าก่อน effects และการชน
แก้ใน Play Mode เป็นค่าชั่วคราว แก้ที่ prefab ใน Edit Mode เพื่อบันทึกถาวร
Runtime/Editor คอมไพล์ผ่านสำหรับช่องปรับนี้ ยังไม่ได้ทดสอบ input จริงในฉากหลัก

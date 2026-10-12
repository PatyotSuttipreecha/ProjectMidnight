# Item Spawn Points

1. ใน Hierarchy ใช้ Create > Gameplay > Item Spawn Point หรือ Add Component > Item Spawn Point บนจุดที่วางไว้
2. เลือก Item Data (ItemSO) โดยตรง หรือกรอง Browse Type / Search Items แล้วกด Choose Item from Library
3. ประเภท Health, Weapon, Ammo, Key, Collection, Resources และ Amulets ใช้ข้อมูลจาก ItemSO ที่เลือก ไม่ต้องตั้งประเภทซ้ำบนจุด Spawn
4. ตั้ง Quantity และ Pickup Prefab ใน ItemSO หากต้องการโมเดลเฉพาะจุด ให้ใส่ Pickup Prefab Override ซึ่งต้องมี PickupItem และ collider
5. Spawn On Start สร้างของเมื่อเริ่มเล่น หากต้องการสร้างเมื่อเข้า Trigger ให้ปิดค่านี้ แล้วเชื่อม WorldTrigger > On Triggered > ItemSpawnPoint.Spawn()

Spawn Once เปิดไว้เป็นค่าเริ่มต้น: ทอยโอกาสได้ครั้งเดียวต่อรอบการเล่น แม้ผลเป็นไม่เกิดของ ปิดเพื่ออนุญาตเรียก Spawn() อีกหลังเก็บของเดิมแล้ว จุดที่มีของอยู่จะไม่สร้างซ้ำ แม้ปิด Spawn Once ไม่ได้สร้างใหม่อัตโนมัติ

Local Offset และ Rotation Offset กำหนดตำแหน่ง/มุมเทียบกับจุด Spawn มี Gizmo สีส้มบอกตำแหน่ง ของที่สร้างแยกจากจุด Spawn เพื่อให้เก็บได้แม้จุดถูกปิดภายหลัง ปืนยังใช้ BuildWeaponVisual ของ PickupItem เดิมซึ่งอาจจัดตำแหน่งลงพื้นตอน Start

ใช้ World Pickup Prefab ที่สะอาดและไม่มีโมเดลของไอเทมอื่นฝังไว้ การเปลี่ยน ItemSO เปลี่ยนข้อมูลการเก็บของ แต่ไม่สลับ mesh ที่ติดอยู่ใน prefab ให้อัตโนมัติ Inspection Prefab ใช้สำหรับ Examine ไม่ใช่ prefab ของที่เก็บในฉาก

ระบบนี้สร้าง ItemSO สำหรับ Inventory ส่วน DocumentSO ใช้ DocumentPickup และ Catalog แยกตามระบบเอกสารเดิม สถานะ Spawn ยังไม่มีการบันทึกข้ามรอบเกม

## Random by Type
เลือก Selection Mode = Random By Type แล้วกำหนด Random Item Type เช่น Health หรือ Ammo กด Fill Pool from Project (Matching Type) เพื่อค้นหา ItemSO ในโปรเจคและบันทึกกลุ่มสุ่มไว้ในจุด Spawn สามารถลบหรือเพิ่มสมาชิกใน Random Pool เองได้

สุ่มหนึ่งชนิดต่อการ Spawn โดยสมาชิกที่ผ่านเงื่อนไขมีโอกาสเท่ากัน ข้ามรายการว่าง รหัสอ้างอิงซ้ำ ประเภทไม่ตรง และรายการไม่มี Pickup Prefab (ยกเว้นมี Override) หากไม่มีรายการที่ใช้ได้จะไม่ Spawn และไม่ใช้สิทธิ์ Spawn Once

การค้นหาทำใน Editor และบันทึก asset references จึงใช้ในเกม Build ได้ หากเพิ่ม ItemSO ใหม่ให้กด Fill Pool อีกครั้ง ปุ่มนี้แทนที่รายการเดิม Quantity และ Spawn Once ยังทำงานเหมือนโหมด Fixed Item


## ItemLab Menu
รวมเครื่องมือไว้ที่ System Modification > ItemLab: Item Settings, Inventory and Examine UI, Documents, Spawn Points และ Create > Item Spawn Point หน้าต่าง Spawn Points เลือกจุดในฉากที่เปิดอยู่และปรับค่าด้วย Inspector เดียวกับ component ได้


## Location Randomization
จุดเดี่ยว: Spawn Chance 0–100% สุ่มก่อนสร้างของ Spawn Once ใช้ได้หนึ่งการทอยต่อรอบเกม แม้ทอยแล้วไม่เกิดของ (Has Spawned แสดงเฉพาะสร้างสำเร็จ ส่วน Has Rolled แสดงว่าได้ทอยแล้ว)

แบบกลุ่ม: System Modification > ItemLab > Create > Item Spawn Group แล้วนำ Item Spawn Point เป็นลูกของกลุ่ม ตั้ง Points To Select เพื่อสุ่มเลือกจุดไม่ซ้ำจากจุดที่เปิดใช้งานและมีไอเทมที่ใช้ได้ กลุ่มซ้อนจัดการลูกของตัวเอง จุดในกลุ่มไม่เรียก Spawn On Start ของตัวเอง และต้องเรียก ItemSpawnGroup.Spawn() จาก Trigger แทนจุด

Apply Point Chances ปิด: สร้างที่จุดที่เลือกโดยไม่ทอยเปอร์เซ็นต์อีก เปิด: แต่ละจุดที่ถูกเลือกทอย Spawn Chance ของตัวเอง จำนวนของจึงอาจน้อยกว่า Points To Select ไม่เลือกจุดทดแทนเมื่อทอยไม่ผ่าน Roll Once ป้องกันกลุ่มทอยใหม่ในรอบเกมเดียวกัน

ค่าเริ่มต้นของโอกาสคือ 100% เพื่อรักษาพฤติกรรมจุดเดิม ผลสุ่มและสถานะยังไม่บันทึก Save/Load


## Point Note
Item Spawn Point ใหม่มี Point Note บน GameObject เดียวกันอัตโนมัติ ทั้งจากเมนูและ Add Component หากเป็น Note ว่างใหม่จะตั้ง Point Type = Loot และ Title ตามชื่อจุด โดยไม่เขียนทับ Note/Data เดิม เลือก Note Data ที่เคยสร้างไว้ หรือกด Create Data from This Note ใน component Point Note เพื่อบันทึกเข้าคลังกลาง ไม่สร้าง SO ซ้ำให้อัตโนมัติ จุดเก่าที่ไม่มี Note ให้เพิ่ม Point Note เอง


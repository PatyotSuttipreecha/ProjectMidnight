# เสียงที่ศัตรูได้ยิน
รองรับเฉพาะ Walk / Run / Gunshot ไม่เชื่อมเสียงประตู

ปรับระยะเดิน/วิ่งและช่วงเวลาได้ใน System Modification > Player Configuration > ผู้เล่น ค่าเริ่มต้นเดิน 3 เมตร/0.5 วินาที วิ่ง 8 เมตร/0.3 วินาที การส่งเสียงอิงการเคลื่อนที่จริงและ CharacterController ติดพื้น ไม่ขึ้นกับ Animation Event จึงไม่เงียบเมื่อคลิปไม่มี event ไม่ส่งตอน Inventory เปิดหรือเลือดหมด
ปรับ Guns > Gunshot Noise Radius หรือ Armory Lab ค่าเริ่มต้น 25 เมตร หนึ่ง event ต่อหนึ่งนัด รวมถึง shotgun ที่แตกหลาย pellet ไม่ส่งจากยิงแม็กว่าง

Enemy Lab > Detection บน Enemy Profile:
- Can Hear: เปิด/ปิด
- Hearing Multiplier: คูณรัศมีเสียง เช่น 0.5 ได้ยินครึ่งระยะ
- Noise Search Duration: เวลารอค้นหลังถึงจุดเสียง ค่าเริ่ม 3 วินาที
- Noise Travel Timeout: จำกัดเวลาที่ใช้ไปตรวจ ค่าเริ่ม 15 วินาที

ศัตรูใช้ Search (สีม่วง) ไปยังตำแหน่งเสียงล่าสุดที่ NavMesh เดินถึงได้ ถ้าไม่มีทางสมบูรณ์จะไม่รับเสียงนั้น มองเห็นผู้เล่นแล้วเปลี่ยน Chase/Attack ตามระบบเดิม; ถ้าไม่พบจะกลับ Patrol เสียงไม่ทำให้รู้ตำแหน่งผู้เล่นแบบติดตามตลอด และไม่แทรกการไล่ล่าที่ทำงานอยู่

รอบแรกใช้ระยะทรงกลม ไม่ลดเสียงผ่านกำแพงและไม่ทำระบบ acoustic occlusion เสียงที่ได้ยินใหม่จะอัปเดตจุดตรวจและเริ่มเวลาตรวจใหม่ การรับเสียงนี้เป็นข้อมูล AI แยกจาก AudioClip/ระดับความดังลำโพง
คอมไพล์ Runtime/Editor ผ่าน ยังไม่ได้ตรวจการตอบสนอง AI ใน Playground Play Mode จริง

## Hearing Gizmos
เปิด Gizmos ใน Scene View แล้วเลือก X Bot / PlayerController เพื่อดูเสียงเดินสีฟ้าและวิ่งสีส้ม เลือก Guns เพื่อดูเสียงปืนสีม่วง วงทรงกลมและวงแนวนอนพร้อมตัวเลขหน่วยเมตรจะแสดงเมื่อเลือกเท่านั้น
เลือก Enemy เพื่อดูวงระยะได้ยินจาก EyePosition ซึ่งใช้รัศมีเสียงของ Player คูณ Hearing Multiplier ต้องมี Player ในฉากหรือกำหนด Hearing Preview Player สำหรับเสียงปืนใน Edit Mode ให้กำหนด Hearing Preview Gun; ใน Play Mode ใช้ปืนที่ผู้เล่นถือเป็นค่าอัตโนมัติ
Show Noise Gizmos และ Show Hearing Gizmos เปิด/ปิดวงได้ วงศัตรูเป็นระยะของเสียงแต่ละชนิดจากผู้เล่นตัวอย่าง ไม่ใช่รัศมีการได้ยินเดียวสำหรับทุกเสียง และไม่ยืนยันว่าตำแหน่งมีทาง NavMesh ไปถึง

## ระยะการได้ยินแยกชนิดบน Enemy Profile
Enemy Lab > Detection ปรับ Walk Hearing Radius / Run Hearing Radius / Gunshot Hearing Radius แยกกันได้ ค่าศูนย์ปิดการได้ยินชนิดนั้น Can Hear ปิดทั้งหมด
ระยะจริง = min(รัศมีจากผู้เล่นหรือปืน, ระยะบน Enemy Profile) × Hearing Multiplier Gizmos ของ Enemy ใช้สูตรเดียวกับการรับเสียง จึงต้องปรับระยะเสียงของผู้เล่น/ปืนด้วยหากต้องการให้ได้ยินไกลกว่าแหล่งเสียงที่ตั้งไว้

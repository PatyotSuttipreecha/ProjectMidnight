# ยาฮีลตามเวลา
Item Configuration > เลือก ItemSO ชนิด Health > Healing
- Total Healing (HP): จำนวนเลือดรวม ไม่ใช่จำนวนต่อ Tick
- Healing Duration: 0 = ทันที, 5 = กระจายฮีลตลอด 5 วินาที
- Tick Interval: ช่วงฮีล เช่น 0.5 วินาที; Tick สุดท้ายเติมยอดที่เหลือ แม้ระยะเวลาไม่หารลงตัว
ตัวอย่าง Pain Killer asset ใน ItemSO/Heal ตั้ง 30 HP / 5 วินาที / Tick 0.5 จึงได้ 3 HP ต่อ Tick รวม 10 ครั้ง เริ่มเพิ่มหลังรอ Tick แรก ตัวอย่างยังไม่มี Icon, Inspection Prefab หรือ Pickup Prefab ต้องกำหนดก่อนนำไปวาง/ทิ้งในแมพ
ยาเดิมที่ Duration เป็น 0 ยังฮีลทันที ใช้หนึ่งชิ้นเมื่อเริ่มเอฟเฟกต์ ไม่ลดจำนวนต่อ Tick ระหว่างมีเอฟเฟกต์ฮีลตามเวลาจะใช้แบบเดียวกันซ้ำไม่ได้ แต่ใช้ยาทันทีได้ ไม่ฮีลเกิน Max Health และหยุดเมื่อเต็ม/ตาย/Player ถูก Disable เวลานับตาม Time.timeScale จึงพักเมื่อเกมหยุดเวลา การเปิด Inventory ไม่ยกเลิกเอฟเฟกต์
ตรวจคอมไพล์ Runtime/Editor ผ่าน และ Inventory core 27 checks ผ่าน (Player ใน core checks เป็น stub ตรวจเฉพาะการใช้/ลดไอเทม ไม่ยืนยัน coroutine ตามเวลา) ยังไม่ได้ทดสอบ Tick ใน Play Mode จริง

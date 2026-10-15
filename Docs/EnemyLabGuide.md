# Enemy Lab — คู่มือการตั้งค่าและช่องที่ซ่อน

## Pressure direction — 15 ตุลาคม 2026

ในหมวด Group มี Pressure Orbit Mode: Random (ค่าเริ่มต้น), Clockwise และ CounterClockwise โหมด Random สุ่มซ้าย/ขวาแยกต่อตัวเมื่อเข้า Pressure แล้วคงทิศไว้ตาม Pressure Direction Duration (เริ่มต้น 2–4 วินาที) ก่อนสุ่มใหม่ การสุ่มใหม่อาจได้ทิศเดิม ไม่บังคับสลับทุกครั้ง และยังหันมองผู้เล่นตามเดิม Live AI / Animation แสดง Orbit Direction ขณะ Pressure

Pressure Direction Duration ซ่อนเมื่อเลือกทิศคงที่ เพราะไม่มีการสุ่มระยะเวลาทิศ ทั้ง Mode และ Duration ซ่อนเมื่อปิด Coordinate Attacks เช่นเดียวกับค่าของวง Pressure อื่น ๆ ค่าที่ซ่อนยังเก็บอยู่

## สาเหตุของ Blend ต่ำ

Profile Machete มีค่า Custom Walk Reference Speed = 2 m/s ขณะที่ Pressure Orbit Speed = 0.6 m/s สูตรเดิมให้ Blend = 0.25 × 0.6 / 2 = 0.075 จึงให้น้ำหนัก Idle สูง แม้ตัวละครเคลื่อนอยู่

เปิด Auto Animation Reference Speeds เพื่ออิง Walk Speed และ Chase Speed × Boost Multiplier โดยตรง ไม่ต้องตั้งความเร็วอ้างอิงซ้ำ ส่วน Keep Walk Blend While Moving ให้การเคลื่อนที่ที่เกิน Animation Stop Speed ใช้ Blend อย่างน้อย 0.25 การเริ่ม/หยุดยังนุ่มตาม Locomotion Damping ค่าหยุดเริ่มต้น 0.03 m/s ป้องกันการขยับเท้าจาก jitter เล็ก ๆ ค่าอ้างอิงเดิมยังเก็บอยู่หากต้องการกลับไปปรับแบบ Custom

ใน Animator พบเส้น Any State ไป zombie_dead2 และ zombie_attack2_1 ที่ไม่มีเงื่อนไข จึงเอาเส้นเข้าอัตโนมัติสองเส้นออก โดยคง State และคลิปไว้ สคริปต์ยังเรียกท่าโจมตี/ตายได้ ไม่ควรใช้เส้น Any State ไม่มีเงื่อนไขกับ State เหล่านี้

## แบ่งหน้าที่ของข้อมูล

| Workspace | ใช้ตั้งอะไร |
|---|---|
| Enemy | ตัวในฉาก/Prefab: Profile, Target, Start AI, Route, Gizmos และข้อมูลทดสอบขณะเล่น |
| Profiles | ค่าศัตรูร่วม ตั้งได้โดยไม่ต้องเลือกตัวในฉาก ใช้หน้าตั้งค่าเดียวกับ Inspector ของ SO |
| Routes | วาง/ย้าย/Generate จุดเดิน กำหนดลำดับและเวลารอ พร้อมตรวจ NavMesh |
| Spawns | Prefab, ตารางเกิด, Target, Route, Aggression override และ Group |
| Groups | สมาชิกในฉาก/จุดเกิด และการเปิด–ปิด Aggressive ทั้งกลุ่ม |
| Modules | ระยะ ความเสียหาย จังหวะโจมตี การหลุด และผลพิเศษของอาวุธ |

Profile มีหมวด Overview, Movement, Animation, Senses, Group, Combat, Physics, Loot และ All ค้นหาด้วย Find Setting ได้ข้ามหมวด การค้นหาจะแสดงช่องที่ซ่อนด้วยแบบอ่านอย่างเดียว เปิด feature หรือ Show Advanced Settings ก่อนแก้ รายการ Hidden Settings ในแต่ละหมวดแสดงชื่อ เหตุผล และ Tooltip ของช่องที่ซ่อน

Combat ใน Profile คือค่ามือเปล่า/ฐาน หลังอาวุธหลุดกลับมาใช้ค่านี้ ส่วนอาวุธที่ถืออยู่ใช้ค่าจาก Module จึงเป็นค่าคนละชุดอย่างตั้งใจ ไม่ใช่ค่าโจมตีซ้ำที่ต้องแก้ทั้งสองที่ แก้ Module เดียวจะมีผลกับทุก Profile ที่อ้างอิงมัน

## ซ่อนตาม Feature — ไม่มีการลบค่า

| เงื่อนไข | ช่องที่ซ่อน | หน้าที่ |
|---|---|---|
| Auto Animation Reference Speeds เปิด | Animation Walk Speed, Animation Run Speed | ความเร็วอ้างอิงแบบกำหนดเองสำหรับน้ำหนัก Walk/Run; ระบบอัตโนมัติใช้ค่าการเดิน AI แทน |
| Use Locomotion Blend Tree ปิด | Auto references, Keep Walk Blend, Stop Speed, ความเร็วอ้างอิง, damping/transition และชื่อ State/parameter ทั้งหมดของ tree | ควบคุมน้ำหนักท่า ทิศเดิน การเปลี่ยนท่าและ binding ของ Animator |
| Use Locomotion Blend Tree เปิด | Run Animation | ชื่อ State สำหรับสลับ Walk/Run แบบเก่า; tree ใหม่ใช้ Blend แทน |
| Use Chase Rhythm ปิด | Chase Walk Duration, Chase Boost Duration | ช่วงเวลาสุ่มของจังหวะเดิน/เร่ง |
| Use Chase Rhythm เปิด | Pressure Speed Multiplier | ตัวคูณความเร็ว Direct approach แบบเก่า; rhythm ใหม่กำหนดความเร็วเป็นช่วงแทน |
| Use Approach Decisions ปิด | Decision Interval, Approach Weights, Flank Angle, Pressure Speed Multiplier | ระยะเวลาคงรูปแบบ น้ำหนัก Direct/Flank/Burst มุมอ้อมและตัวคูณแบบเก่า |
| Coordinate Attacks ปิด | Max Concurrent Attackers, Engagement Distance, Pressure Ring Radius/Orbit Speed/Orbit Step/Wait Timeout/Retreat Cooldown, Attack Turn Timeout/Rest | จัดสิทธิ์โจมตี วงกดดันและเวลารอ/ถอนตัว |
| Spread Chase Targets ปิด | Chase Approach Radius | ระยะ offset ของตำแหน่งเข้าประชิด |
| Separate Nearby Enemies ปิด | Separation Radius, Separation Speed | ระยะห่างและความเร็วการแยกตัวจากเพื่อน |
| Can Hear ปิด | Hearing Multiplier, Walk/Run/Gunshot Hearing Radius, Noise Search Duration, Noise Travel Timeout | ตรวจเสียง จำกัดระยะ และเวลาเดินหา/ค้นหาจุดเสียง |
| Use Lost Combat ปิด | Starting Weapon, Detect Anticipation, Base Hit Arc/Recovery/Attack Animation, Restraint Duration/Multiplier, Player Hit Protection และ inline Module | โหลดอาวุธ การเตรียมตรวจพบ โจมตีฐาน รั้งผู้เล่น และป้องกัน Lost hit ซ้ำ |
| Use Lost Combat เปิด | Attack Cooldown | รอบโจมตีแบบเก่า; Lost ใช้ Preparation + Windup + Recovery ของฐาน/อาวุธแทน |
| Restraint Duration = 0 | Restraint Speed Multiplier | ตัวคูณลดความเร็วผู้เล่นระหว่างถูกรั้ง |
| Enable Hit Reaction ปิด | Hit Push Impulse, Physics Duration, Recovery Duration, Reaction Blend, Animation Support, Max Angle | แรงและเวลา partial ragdoll จากการถูกยิง; Root Knockback/Stagger ยังตั้งแยกได้ |
| Use Ragdoll ปิด | Death Blend Duration/Animation Drive/Hybrid Push Scale, Gravity Scale, Linear/Angular Damping, Death Push Impulse, Settle Delay | การผสมท่าตายกับฟิสิกส์และแรง/การหยุดนิ่งของศพ; Death Animation, Lead-in และ Corpse Lifetime ยังมีได้ |
| ทั้ง Ragdoll และ Hit Reaction ปิด | Physics Mass Multiplier, Bone Mass Overrides | น้ำหนักร่าง/รายกระดูกสำหรับฟิสิกส์ |

Loot ใช้ editor เปอร์เซ็นต์ชุดเดียว: Drop Chance คือโอกาสเกิดของ จากนั้น Loot Table รวม 100% เลือกหนึ่งชิ้น Legacy Loot Prefab จะแสดงเฉพาะเมื่อตารางว่าง เพื่อรองรับข้อมูลเก่า Loot Offset คือระยะเลื่อนตำแหน่งของที่ดรอป

## ย้ายไป Advanced — เปิดกลับได้

| ช่อง | หน้าที่ |
|---|---|
| Chase Repath Interval | ความถี่คำนวณเส้นทางไล่ตาม |
| Locomotion State | ชื่อ State ที่มี Blend Tree |
| Locomotion Blend / Horizontal / Vertical Parameter | ชื่อ float ที่รับความเร็วและทิศเดิน; ปัจจุบัน Vertical สะกด Verticle ตาม Animator |
| Locomotion Attack Parameter | ชื่อ bool สำหรับการโจมตี |
| Locomotion Attack State | ท่าโจมตี fallback เมื่อ Module ไม่มี State ที่ใช้ได้ |
| Noise Travel Timeout | เวลาสูงสุดเดินไปยังจุดเสียงก่อนเลิกค้นหา |
| Flank Angle | มุมอ้อมเป้าหมาย |
| Pressure Speed Multiplier | ตัวคูณ Direct approach แบบเดิม ใช้เมื่อปิด Chase Rhythm |
| Chase Approach Radius | ระยะกระจายจุดเข้าประชิด |
| Vary Avoidance Priority | สลับลำดับการหลบของ NavMeshAgent ระหว่างตัว |
| Hit Animation Support | แรงช่วยรักษาท่าแอนิเมชันขณะรับแรงยิง |
| Hit Max Angle | มุมสูงสุดที่กระดูกเบี่ยงใน hit reaction |
| Death Animation Drive | แรงช่วยรักษาท่าตายระหว่าง blend กับ ragdoll |
| Bone Mass Overrides | กำหนดน้ำหนักฐานรายกระดูก |

เมื่อใช้ Shared Settings ค่า legacy ของ component จะไม่แสดงซ้ำใน Enemy Lab หากยังเป็น legacy จะมี Legacy Component Tuning ให้เปิดแก้ และ Promote Current Settings เพื่อคัดลอกค่าปัจจุบันไป Profile การ Promote เป็นคำสั่งแยก ไม่ทำอัตโนมัติ ไม่ลบข้อมูล component เดิม

ถ้ามี Route แล้ว เครื่องมือวาง/Generate จะอยู่ที่ Routes workspace กด Edit Assigned Patrol Route ได้ทันที ส่วนจุดเดินแบบเก่าที่ยังไม่มี Route อยู่ใน Legacy Patrol Points / Generation และแยกออกเป็น Route ได้ Gizmos/preview ของแต่ละตัวอยู่ใน Instance Gizmos / Previews

## ตรวจใน Play Mode

เลือกศัตรูจริงที่กำลังเล่นใน Enemy workspace หรือ Inspector ดู Live AI / Animation: State, โหมด/สิทธิ์โจมตี, adapter Connected หรือไม่, Measured Movement, Blend Target/Current, ทิศ X/Z, Agent Speed/Velocity และสถานะ Path

ถ้ากำลังเคลื่อน แต่ Blend Target ต่ำกว่า 0.25 ให้ตรวจว่าปิด Keep Walk Blend หรือใช้อ้างอิง Custom หรือไม่ ถ้า Measured Movement = 0 ให้ตรวจ path, isStopped, การพัก AI และการติดสิ่งกีดขวาง ไม่ควรบังคับท่าเดินให้ขยับเมื่อ Agent ไม่ได้เคลื่อนจริง

ปุ่ม Start/Pause AI และเปิด–ปิด Aggressive ใช้ได้ใน Play Mode โดยไม่ถูกล็อกรวมกับ editor ตั้งค่า ส่วน Profile/Module tuning ถูกล็อกระหว่างเล่นเพื่อไม่เผลอเปลี่ยน SO ถาวร ช่อง isAlert/isAttacking เดิมย้ายจากช่องแก้ได้ไปเป็นข้อมูลสถานะอ่านอย่างเดียว

มีคำเตือนสำหรับ reference speed ไม่ตรง, stop threshold สูงเกิน walk speed, stop distance เกินระยะมือเปล่า, obstacle mask ว่าง, โมดูลถูกใส่แต่ปิด Lost Combat, และ Animator transition/state ผิด การตรวจนี้ช่วยหาค่าที่ขัดกัน แต่ยังต้องดูภาพเดิน/เท้าไถลและท่าด้านข้างในฉากจริง

## Blend Tree hierarchy (15 October 2026)

ลำดับใหม่: EnemyType → WeaponType → Speed → Horizontal/Verticle โดย TheLost ใช้ EnemyType = 0 และ Speed: Idle = 0, Walk = 0.5, Run = 1 สคริปต์ส่งค่าจากความเร็วเคลื่อนที่จริงและเลือก WeaponType ตามอาวุธปัจจุบัน: Unarm 0, Machete 0.2, Log 0.4, Torch 0.6, Stone 0.8, Axe 1 เมื่ออาวุธหลุดจะเลือก Unarm ทันที กิ่ง Log/Stone/Axe ที่ยังว่างใช้ Motion จาก Unarm ชั่วคราว สามารถเปลี่ยนคลิปภายในกิ่งนั้นเมื่อมีแอนิเมชันพร้อมได้ ส่วน TheWatcher/TheNameLess/TheKeeper ยังเป็นกิ่งว่างและยังไม่ได้เปิดใช้งาน

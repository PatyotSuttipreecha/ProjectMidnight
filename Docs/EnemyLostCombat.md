# The Lost — Base Combat และ Weapon Modules

อัปเดต 13 ตุลาคม 2026 จากสเปกที่ส่งโดย Design - Documentation เป็นฐานทดลอง ไม่ใช่แอนิเมชันหรือสมดุลขั้นสุดท้าย

## โครงสร้าง

ทุก loadout ใช้ EnemyController เดียวกันสำหรับ Patrol/Idle, Detect, Search และ Chase อาวุธเป็น EnemyWeaponSO ที่ EnemyWeaponRuntime ถือไว้เฉพาะ instance เมื่อหลุดหรือใช้หินหมด Current Weapon กลับเป็น null แล้วใช้การโจมตีมือเปล่าของ EnemyDataSO การเปลี่ยน runtime ไม่แก้ Starting Weapon ใน SO และไม่มีระบบหยิบอาวุธคืน

เปิด Use Lost Combat เพื่อใช้วงจรใหม่ Profile เดิมที่ปิดค่านี้ยังใช้การโจมตีเดิม ไม่เปลี่ยนฉากหรือ prefab ที่มีอยู่โดยอัตโนมัติ ถ้าใช้ Profile แบบเก่าให้เปิด Use Shared Settings ด้วยเพื่อให้ค่าพื้นฐานจาก SO ถูกใช้ครบ

## ทดลองใช้งาน

1. ไปที่ Assets/Data/Enemies/TheLost เลือก Profile Unarmed, Machete, Log, HeavyAxe, Stone หรือ Torch และใส่ใน Enemy Controller ของ prefab/instance ที่ต้องการทดลอง
2. ปรับค่าพื้นฐานที่ System Modification > Enemy Lab > Enemy Settings หรือ Profiles หมวด The Lost - Base and Loadout
3. Starting Weapon ว่างคือมือเปล่า อาวุธอยู่ในโฟลเดอร์ Weapons ปรับได้ผ่าน System Modification > Enemy Lab > Weapon Modules หรือ Inspector ของ SO
4. Preset ใช้ Obstacle Mask = Ground + Obstacle ตาม Layer ของโปรเจกต์ วัตถุที่ต้องบังสายตา/การตีต้องอยู่ใน layer ที่เลือก หากฉากใช้ layer อื่นให้ปรับ mask โดยไม่รวม Player/Enemy hitbox
5. ใช้ NavMesh ที่ bake แล้วและ Target ที่อ้างถึง PlayerController ตัวจริง Stopping Distance ต้องต่ำกว่าระยะโจมตีมือเปล่า เพื่อยังประชิดได้หลังอาวุธหลุด
6. Held Model เป็น prefab สำหรับภาพอย่างเดียว ใส่แล้วจัด Local Position/Rotation/Scale ของ module ส่วน Weapon Anchor ใน EnemyWeaponRuntime ว่างจะใช้ Humanoid Right Hand ถ้าไม่มี rig ที่รองรับจะใช้ root จึงควรกำหนด anchor เอง

Preset ทั้งหกใช้ฐานเดียวกันและเริ่มด้วยผู้เข้าตีพร้อมกันหนึ่งตัว ยังเป็นค่าเริ่มต้นสำหรับทดลอง ไม่ใช่ค่าที่พิสูจน์สมดุลในฉากจริง

## วงจรโจมตี

- Detect: หยุดก่อนยืนยันการเห็นใหม่หรือตรวจเสียงตาม Detect Anticipation การได้ยินยังทราบจุดเสียงเท่านั้น เสียงใหม่ระหว่างตรวจไม่เริ่มท่าหยุดฟังซ้ำทุกครั้ง
- Preparation: หยุด ตั้งทิศ และรอตามค่าของ Base หรือ Module ตรวจระยะ/สายตา/สถานะผู้เล่นอีกครั้งก่อนลงมือ ถ้าหนีออกจากระยะให้ยกเลิก
- Attack: ปรับทิศครั้งสุดท้ายแล้วล็อกทิศตรวจ melee ตาม Hit Arc ไม่หมุนตัวตามเป้าหมายตลอด windup เมื่อครบเวลา ตรวจระยะ สิ่งบังและมุมอีกครั้ง
- Recovery: รอหลัง hit attempt ทั้งโดนและพลาด แยกจากเวลาเตรียม/ลงมือ สำหรับ Lost ค่า Base Recovery ใช้แทนความหมายของ legacy Attack Cooldown
- การคว้ามือเปล่า: Damage และลดความเร็วเคลื่อนชั่วครู่ ไม่มีการล็อก input, กล้อง, QTE หรือผูกตัวละครเข้ากับมือ ค่านี้เป็น prototype ของการรั้ง ยังต้องทำท่าคว้าและภาพตอบสนองให้ตรงกัน

## Loadouts และค่าทดลอง

| Loadout | ระยะ | Damage | Preparation / Windup / Recovery | การทำงาน |
|---|---:|---:|---|---|
| Unarmed | 2 | 8–12 | 0.35 / 0.5 / 0.8 วินาที | คว้าใน cone 90° ลดความเร็วเหลือ 0.65 เท่า 0.4 วินาที |
| Machete | 2.8 | 15–22 | 0.45 / 0.5 / 0.85 | Sweep cone 110° |
| Log | 2.8 | 12–18 | 0.45 / 0.5 / 0.85 | ใช้ Sweep state เดียวกับ Machete |
| HeavyAxe | 3 | 25–35 | 1 / 0.65 / 1.3 | เตรียมและพักนาน cone 80° |
| Stone | 10 | 8–12 | 0.55 / 0.4 / 0.8 | หนึ่งก้อน วิถี ballistic แบบ sweep collision |
| Torch | 2.8 | 10–15 | 0.45 / 0.5 / 0.85 | Sweep เดียวกับ Log พร้อมไฟ 3 วินาที 2 Damage/วินาที |

Machete, Log และ Torch ตั้งชื่อ Animator state เดียวกันคือ TheLost_Sweep; HeavyAxe ใช้ TheLost_HeavyAxe และ Stone ใช้ TheLost_Throw ใน Enemy.controller ที่ตรวจยังมีเพียง zombie_idle1, zombie_walk2, zombie_attack2_1 และ zombie_dead2 จึง fallback เป็น zombie_attack2_1 เมื่อ state ใหม่ไม่มี ไม่ได้สร้างหรือยืนยันท่ากวาด/ขว้างเฉพาะในงานนี้

โมเดลอาวุธและ Burn Effect ในชุด preset ยังว่าง หินไม่มี Projectile Model จะใช้ sphere ขนาดเล็กสำหรับทดสอบ สามารถใส่ prefab ภาพและ VFX จริงภายหลัง ห้ามใส่ prefab ที่มี logic ยิง/โจมตีเอง เพราะ module เป็นผู้ตรวจ Damage ส่วน collider ของภาพอาวุธ/หินจะถูกปิดไม่ให้เกิดการชนซ้ำ

## อาวุธหลุด

โอกาสหลุดคิดจาก HitBox ที่กระสุนโดน: Hand, LowerArm, UpperArm ใช้ Arm Drop Chance ส่วนอื่นใช้ Other Drop Chance Preset = 35% และ 5% ต่อการสุ่มที่อนุญาต ระยะเว้น Disarm Roll Interval 0.2 วินาทีป้องกันลูกปรายสุ่มซ้ำทุก pellet ต้องตั้ง HitBoxPart ให้ถูกต้อง

อาวุธหลุดจะยกเลิก coroutine ของท่า คืนสิทธิ์กลุ่ม และคงเวลาพักก่อนกลับมาโจมตี Base ไม่เปลี่ยนเป็นคว้าทันที ถ้ามี Held Model จะปล่อยเป็น prop ภาพตกและ settle ตาม Obstacle Mask ไม่กีดขวาง AI ไม่เป็น pickup ลบตาม Dropped Lifetime ค่า 0 คงไว้ เมื่อตายก็ปล่อยอาวุธเช่นกัน

## หิน

ล็อกจุดเล็งเมื่อเริ่ม windup แล้วปล่อยจากจุดหน้าตัว ไม่เปลี่ยนทิศตามมินหลังล็อก เลือกความเร็วและแรงโน้มถ่วงจาก Module ตรวจ overlap ณ ตำแหน่งเริ่มและ sphere sweep ตลอดวิถี ผนังบังได้ ผู้เล่นเคลื่อนออกจากแนวแล้วพลาดได้

ใช้หินหมดเมื่อปล่อย อาวุธใน instance กลับ Base แต่สิทธิ์โจมตียังคงอยู่จน projectile จบและผ่าน recovery เพื่อไม่ให้ตัวอื่นเริ่มโจมตีทับทันที หาก AI ถูก pause, disable, ตาย หรือถูก stagger ระบบ prototype จะยกเลิกหินที่ยังบินอยู่ด้วยเพื่อจบ threat กับสิทธิ์พร้อมกัน พฤติกรรมนี้เป็นข้อจำกัดตั้งใจของต้นแบบ สามารถเปลี่ยนเป็น projectile ที่คงอยู่และถือ reservation แยกภายหลังได้

## ไฟ การรั้ง และป้องกันโดนซ้ำ

EnemyPlayerEffects อยู่บน GameObject ที่มี PlayerController ใช้ร่วมทุก Lost attacker:

- ไฟหนึ่ง instance ต่อผู้เล่น โดนซ้ำ refresh เวลาตาม duration ใหม่ ไม่เพิ่ม DPS หลายชุด ไฟไม่ล็อกเดิน ใส่ Burn Effect ได้หากต้องการภาพเปลวไฟ
- Restraint ลดความเร็วใน HandleMovement โดยไม่ปรับค่า Move Speed ต้นฉบับและไม่ล็อก input มีค่าต่ำสุด 0.25 เพื่อไม่หยุดตัว
- Player Hit Protection เริ่มต้น 0.75 วินาที ป้องกัน Lost melee/stone ครั้งถัดไปหลังโดน และไม่สั้นกว่าระยะรั้ง จึงไม่ต่อการรั้งซ้ำขณะยังติดผล
- ผลไม่ทำงานกับผู้เล่นเลือดหมดหรือถูกล็อก Cutscene และถูกล้างเมื่อ disable/death/cutscene ไม่มี Game Over ใหม่ในงานนี้
- การป้องกันนี้ไม่ใช่ invincibility ทุกระบบ การเรียก TakeDamage จากระบบอื่นหรือศัตรู Legacy ยังไม่ผ่าน protection นี้ ควรใช้ Lost Profiles ทุกตัวใน encounter ที่ต้องการกฎเดียวกัน

## พฤติกรรมฝูงและงานที่ยังรอ

ใช้ EnemyAttackCoordinator เดิมร่วม Base, อาวุธ และการขว้าง Limit เริ่ม 1 และใช้ค่าเดียวกันทุก profile ใน encounter ตัวอื่นยังมี Pressure/Separation และค้นทางจริง Preset ปิด spread chase targets และไม่ให้น้ำหนัก Flank เพื่อเริ่มจากการตามเป็นหลัก ยังไม่ได้เปลี่ยน Pressure ring เป็นระบบเลือกพื้นที่ตามแรงขับโดยสมบูรณ์

เสียงปืนส่ง GameplayNoise ที่มีอยู่แล้ว ศัตรูที่ได้ยินไปตรวจต้นเสียงและเริ่มตามเมื่อพบ ไม่มีการสร้าง encounter Chaos, spawn เพิ่ม, Timeline หรือเหตุหน้าผาอัตโนมัติในรอบนี้ งานฉากเหล่านั้นต้องเชื่อมภายหลัง

สิ่งที่รอ: โมเดลถืออาวุธ, grip/IK, ชุด animation sweep/axe/throw/grab, reaction ผู้เล่น, เสียงเตือน, VFX ไฟ, จังหวะตาม Animation Event และการปรับสมดุลในฉากจริง การตรวจรอบนี้ยังใช้เวลาจาก config ไม่อ่านตำแหน่งอาวุธจริงหรือทำ collision ตามใบมีด

## การตรวจสอบ

Tools/LostCombatChecks/run.ps1 ใช้ Unity Play Mode โปรเจกต์แยกใน Library/EnemySpawningValidation ใช้ EnemyController, coordinator, module, effects และ projectile จริง พร้อม PlayerController จำลองสำหรับรับ Damage ไม่ยืนยันภาพ Animator, rig หรือ CharacterController ของฉากหลัก คอมไพล์ Runtime/Editor แยกเพื่อเช็คการเชื่อม PlayerController ตัวจริง

ผลล่าสุด: 41 checks ผ่าน รวม import Profile/Module references, Detect, hit/slow/protection, disarm และยกเลิก Damage, ขวานเตรียม/พัก, หลบด้านข้าง, หินใช้ครั้งเดียว/ถือสิทธิ์/ชนกำแพง/ไม่ตามเป้าหมาย, ไฟ refresh ไม่เพิ่ม DPS, เป้าหมาย Cutscene และรับเสียงซ้ำ ชุด GroupCombatChecks เดิมผ่านอีก 10 checks

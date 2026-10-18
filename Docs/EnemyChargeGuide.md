# Combat / Charge flow

## การทำงาน
1. Detect -> สุ่มคนเดิน/วิ่งเข้าหาแบบเหลื่อมเวลา ตาม cap เดิม
2. ใครถึง pressure radius ก่อนรับสิทธิ์เข้าประชิด; คนอื่นเดินกดดันรอ
3. ผู้มี attack turn สามารถ Charge โดยไม่ต้องมี pressure slot จากระยะกลางได้ หากมองเห็น มีทางตรงบน NavMesh และ cooldown พร้อม (คนถือหินไม่ Charge)
4. ChargePreparation หันตามผู้เล่นและเตือน -> Charge วิ่งไปตำแหน่งที่บันทึกไว้ ไม่มี homing -> ถึงระยะจึงใช้ท่าโจมตีเดิม
5. พุ่งไม่ถึง/หลบพ้น/ทางตัน -> Recovery -> คืน attack turn และรอ cooldown
6. โจมตี Charge โดนจริง -> restraint + onChargeHit -> ล็อกการเริ่มโจมตีทั้งกลุ่ม และกัน hit จากตัวอื่นที่เริ่มท่าไปแล้ว
7. จบล็อกโดย timeout หรือ EnemyPlayerEffects.EndChargeLock() -> เว้น grace period ก่อนโจมตีได้อีก
8. ผู้เล่นวิ่งหนี -> ลดความเร็วลงเป็นเดินตามอย่างค่อยเป็นค่อยไป ชาร์จ/โจมตีที่เริ่มแล้วจบได้ -> หยุดวิ่งครบ settle delay แล้วสุ่มรอบใหม่

## ตั้งค่า
Enemy Profile > Combat > Charge: ความสามารถ ระยะ ความเร็ว เวลา cooldown และชื่อ Animator state ของแต่ละ profile
Shared Combat > Escape: Slowdown duration
Shared Combat > Attack: Charge master switch และกติกาล็อกผู้เล่น/ช่วงพักหลังล็อก

ค่าเริ่ม: ระยะ 2.5–6 m (ต้องนอก melee reach และมี pressure ownership), telegraph 0.45 s, speed 4 m/s, run สูงสุด 1.2 s, miss recovery 0.8 s, cooldown 5 s, hold 0.8 s, grace 1 s, escape slowdown 0.8 s.
Charge ไม่ต้องรอ pressure ownership: ใช้ระยะของ profile เพื่อขอ attack turn แต่ยังคุมจำนวนผู้โจมตีและช่องว่างเริ่มโจมตีร่วมกัน
Escape pause ลดการไล่ของกลุ่ม แต่ไม่ห้ามศัตรูที่อยู่ในระยะโจมตีหรือผู้เล่นกำลัง sprint เข้าหาตอบโต้

## Event และการล็อก
EnemyController.onChargeHit เรียกเฉพาะ damage ที่สำเร็จ ต่อ Timeline/กล้องในอนาคตได้
EnemyPlayerEffects.BeginChargeLock(seconds) กำหนดเวลาล็อกแบบมี fallback; EndChargeLock() จบก่อนเวลาได้
Lock movement เปิด: movement multiplier เป็น 0 ระหว่าง hold; ปิด: ใช้ตัวคูณ restraint (0.25–1)
กล้องยังหมุนได้ ไม่มี animation กัด/กล้อง cinematic อัตโนมัติในรอบนี้
ถ้าต้องการ event ยาว ให้ BeginChargeLock(duration) กำหนดเวลาตาม event แล้ว EndChargeLock() เมื่อจบ ไม่มี infinite lock
ผู้เล่นตาย/disabled/เข้า cutscene protection จะ clear effects เพื่อไม่ให้ล็อกค้าง

## Animation
ใส่ preparationAnimation / runAnimation / attackAnimation เป็นชื่อ Animator state บน layer 0 ใน profile
หากว่างหรือหา state ไม่พบ จะใช้ Idle / locomotion / ท่าจาก Weapon Module ตามเดิม; ระบบไม่สร้าง animation clip ให้เอง
Ranged Support ใช้ระบบปาหินเดิม รอบนี้ไม่ได้เพิ่มระบบจัดตำแหน่งพลปาหินใหม่

## Diagram
Docs/Design/Enemy-Combat-Charge.drawio เปิดใน Draw.io และแก้ได้

## การตรวจสอบ
Runtime และ Editor คอมไพล์ผ่าน; ชุดทดสอบ Unity Play Mode แยก 256 checks ผ่าน (production AI/NavMesh/projectile/effects, PlayerController เป็น damage/input stub)
ครอบคลุม telegraph -> charge, fixed destination เมื่อผู้เล่นหลบ, miss recovery, confirmed hit/event, movement lock, block group attack, event end/grace และ timeout fallback
ยังต้องทดสอบความรู้สึกและแอนิเมชันในฉาก gameplay จริง เพราะไม่มี clip charge/กัดเฉพาะในรอบนี้

## Group กับ Shared Combat
Group ใน Enemy Profile: วิธีเดินวน การแยกตัว ระยะขอ turn และเพดาน turn ที่ profile ขอ; profile ที่แชร์เป้าหมายเดียวกันแข่งขัน turn ร่วมกัน ไม่ได้มีโควตาแยก profile (ใช้ค่าต่ำสุดของ turn ที่กำลังถืออยู่)
Shared Combat: กติกากลางต่อผู้เล่นข้าม profile/spawn group ได้แก่ runner cap, chance/stagger, pressure radius, replacement, escape, start interval และล็อก/ช่วงพักหลังล็อก
EnemyGroup component เป็นอีกอย่าง: รวมสมาชิกและสั่ง aggression ไม่ใช่ pool สิทธิ์โจมตีแยก

## Cheats
System Modification > Cheat Menu หรือ F10 ระหว่างเล่น
Infinite Ammo: กระสุนไม่ลด ยิงต่อได้แม้แมกกาซีนเป็น 0 ไม่เปลี่ยน ItemSO หรือ reserve ammo; ปิดแล้วใช้จำนวนเดิม
เลือก ItemSO + จำนวน: Add to inventory เคารพขนาดกริด/stack และแจ้งเมื่อเต็ม; Spawn world pickup ต้องมี pickupPrefab
F10 มีค้นหารายชื่อ ItemSO ทั้ง project ใน Editor; development build แสดงเฉพาะ ItemSO ที่โหลดแล้ว
Reset All Cheats ปิด Infinite Ammo พร้อมสูตรอื่น

อัปเดตการตรวจรอบล่าสุด: 256 Play Mode checks ผ่าน รวมการเริ่ม Charge อัตโนมัติผ่าน shared attack coordinator โดยไม่มี pressure slot ขณะผู้เล่น sprint เข้าหา และห้ามตัวที่สอง Charge พร้อมกัน
Inventory core checks 27 ข้อผ่าน; สูตรกระสุนและปุ่มเสกของคอมไพล์ผ่าน แต่ยังไม่ได้ทดสอบการคลิก/ยิงด้วย input จริงในฉากหลัก
# Charge tuning update — 2026-10-17

> Current architecture: [EnemyCombatFlow.md](EnemyCombatFlow.md). The simplified single Combat Turn flow supersedes the role-based permission rules recorded below. Reserve For Mid Range is now a hidden legacy field and does not affect selection.

- Enemy Profile > Combat > Charge: `Selection Chance` (default 0.3) rolls once per `Decision Interval` (default 3 seconds, individually staggered), rather than rolling every frame. A rejected choice continues ordinary approach.
- `Reserve For Mid Range` defaults on. This is a probability preference: a close pressure owner's effective chance is `chance * (0.5 + 0.5 * chance)`, while mid-range waiters use the configured chance. It does not prohibit an otherwise reachable charge. Chance 0/1 still disables/forces selection. One preparing/running charge per target remains the limit.
- The usable start range is capped by speed, duration, acceleration and actual weapon reach, with a 0.35 m margin. It is checked again after preparation. A player can still dodge after commitment: the destination remains fixed.
- Contact uses the charge's existing telegraph instead of replaying ordinary melee preparation/windup. Damage/lock still require range, facing and the normal target protection checks.
- Shared Combat > Charge: `Hit restraint`, `Lock movement on hit`, `Hold camera FOV`, and `After-lock grace` control confirmed contact. During a hold, player attacks and camera look pause; the camera zooms toward the configured FOV. Release restores look/input and the preceding FOV; other enemies remain blocked through grace.
- `EnemyPlayerEffects.onChargeLockStarted/onChargeLockEnded` and `EnemyController.onChargeHit` are available for authored bite/camera events. This update provides a basic zoom/hold, not a newly authored bite animation or cinematic camera angle.
- Verification: 291 isolated Unity Play Mode checks passed with production AI/NavMesh/effects and a player damage stub. Main runtime/editor compilation passed. Full player camera presentation still needs testing in the gameplay scene.

## Charge eligibility correction — 2026-10-17

- Removed the blanket exclusion of all pressure owners: only close owners are kept in ordinary approach. This prevents small encounters from having no eligible chargers.
- A selected mid-range charger can replace an uncommitted, non-close attack reservation. Committed attacks and immediate melee defenders keep their turns; the global attack-start spacing and one-charge limit remain unchanged.
- Escape Walk still suppresses pursuers behind the player. A selected visible charge threat inside the player's forward movement/interception cone can reach the attack decision instead of returning early to Escape Walk.
- Gameplay Charge status separates reachable range, close-role approach, cached walk choice, escape suppression, cooldown and attack turn waits.
- 293 isolated Unity Play Mode checks passed, including normal Update starting a charge with Reserve For Mid Range enabled, without directly invoking PerformCharge. Runtime and editor compilation passed. Actual gameplay crowd behaviour still needs a scene playtest.

## Saved-profile regression correction — 2026-10-17

- Torch's saved Min Distance was 5 m, but speed 4 m/s, duration 1.2 s, acceleration 8 m/s² and weapon reach 1.5 m produced an effective maximum of 4.95 m. Increased its saved duration to 1.5 s, preserving Min Distance 5 and Max Distance 6. The reachable band is now 5–6 m.
- The hard close-role exclusion overlapped Machete's valid charge band. Replaced that exclusion with the preference described above.
- Accepted decisions persist while waiting for the shared attack clock, even after crossing the close-approach boundary. Decisions clear when leaving the physical charge band, pausing the actor, or committing the charge. Rejected decisions remain interval-based.
- Debug status and Enemy Profile warnings explicitly identify an impossible charge band. Debug also distinguishes waiting for the shared start clock.
- Regression checks load the saved Machete/Torch profiles with their actual chance, timing, acceleration and module reach, and verify both autonomous Update entry and physical movement after preparation. No forced 100% selection is substituted in these saved-profile cases.
- Verification: 306 checks passed in isolated Unity Play Mode with production AI/NavMesh/modules/effects and a player damage stub. Actual saved Machete and Torch both reached Charge and moved at the configured speed of 4 m/s. Runtime/editor compilation and targeted whitespace checks passed. Full scene crowd/camera behaviour remains a gameplay playtest.


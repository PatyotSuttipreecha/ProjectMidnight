# Story Lighting — แสงตามเหตุการณ์

## ฉากและข้อมูล
Scenes_Gameplay มี `Scene Lighting System` ใต้หมวด Lighting อ้างอิง Directional Light เดิม และ Volume แยกที่ priority 10 เฉพาะ post-exposure/color filter ไม่เปลี่ยน Profile ของ Global Volume เดิม

`Assets/Data/Lighting` มี Initial ที่จับจากสภาพฉากตอนติดตั้ง และตัวอย่าง Evening / Dusk / Night / Dawn ตัวอย่างเหล่านี้เป็นจุดเริ่มปรับ ไม่ใช่แสง final ที่ตรวจทุกพื้นที่แล้ว ฉากเริ่มด้วย Initial เพื่อรักษาบรรยากาศเดิม ไม่มีเวลาเดินเอง ไม่มีเปลี่ยน preset อัตโนมัติระหว่างเล่น

## ใช้งาน
### Realtime Preview ใน Editor
หน้าต่าง Lighting Configuration หา controller ในฉากให้เมื่อเปิด เลือก Evening / Dusk / Night / Dawn จากปุ่ม แล้วเปิด **Live Preview in Editor** ปรับค่าที่ **Edit Preset** ในหน้าต่างเดียวกันและดู Scene View ได้ทันทีโดยไม่ต้องเข้า Play Mode **Preview Blend** ใช้เทียบระหว่างแสงเดิมก่อนเริ่ม Preview กับ Preset ที่เลือก ไม่ใช่นาฬิกาหรือการเซฟสถานะเวลา

หากแสง/หมอก/Post Processing ไม่ชัด กด **Enable Lighting / Fog / Post Effects in Scene View** เปิดตัวเลือก Scene View ให้ครบ การใช้แสงแบบ baked ยังมีข้อจำกัดเดิม

กด **Stop Preview / Restore Original Scene** เพื่อกลับแสงเดิม ตัว Preview ใช้สำเนาชั่วคราวของ Skybox และ Volume Profile และคืนค่าก่อนบันทึกฉาก เข้า Play Mode ปิดหน้าต่าง หรือ reload สคริปต์ **การปรับค่าของ Preset เป็นการแก้ SO จริงและ Undo ได้** แม้แสง Preview ในฉากจะคืนค่า ส่วนการบันทึกฉากจะหยุด Preview เพื่อไม่บันทึกค่าทดลองลงใน Light/RenderSettings

เปิด `System Modification > Scene Configuration > Lighting Configuration` แล้วเลือก controller/preset กด Blend หรือ Apply instantly ใน Play Mode เพื่อทดลอง ตั้งค่ารายละเอียดที่ LightingPreset asset: แสงหลัก มุม สี แสงแวดล้อม หมอก ท้องฟ้า และ post-exposure

ในฉากมี `Lighting Cue - Evening/Dusk/Night/Dawn` ใต้ระบบกลาง เชื่อม `WorldTrigger.OnTriggered` หรือ Timeline Signal Receiver ไปที่ `LightingCue.Play()` ได้เลย `Duration Override` ติดลบใช้เวลาจาก preset; 0 เปลี่ยนทันที หรือเรียก `SceneLightingController.BlendTo(preset)` / `ApplyInstant(preset)` โดยตรง

การเรียก preset ใหม่ระหว่างเปลี่ยนจะเริ่มจากค่าที่เห็นขณะนั้น ไม่กระโดดกลับค่าต้นทาง ใช้ unscaled time เป็นค่าเริ่มต้นจึงเปลี่ยนต่อได้ระหว่าง pause การปิด controller คืนค่าที่จับไว้ และใช้สำเนา Skybox/Profile ตอนเล่นเพื่อไม่เขียน asset ต้นฉบับ มี Event เริ่ม/จบ transition; เมื่อเปลี่ยนเป้าหมายกลางทาง Event จบเป็นของเป้าหมายล่าสุดเท่านั้น

## Box Volume เฉพาะพื้นที่
กด `Create Local Box Volume at Scene View` ได้วัตถุพร้อม BoxCollider แบบ Trigger ขนาดเริ่มต้น 10×5×10 เมตร และ Volume แบบ local, priority 20, blend distance 3 เมตร พร้อม Profile ใหม่เฉพาะพื้นที่ ตั้งขนาด/ตำแหน่งใน Inspector และเปิด override เฉพาะค่าที่ต้องการ ไม่ต้องเพิ่ม WorldTrigger เพื่อทำการ blend นี้

Volume ประเมินตามตำแหน่ง **กล้อง/Volume Trigger ของกล้อง** ไม่ใช่ collider ผู้เล่น กล้องมุมบุคคลที่สามอาจข้ามขอบห้องก่อน/หลังผู้เล่น จึงต้องเผื่อขนาด Box และ Blend Distance เปิด Post Processing บนกล้อง และตรวจ Volume Layer Mask ให้รวม layer ของ Volume (ตัวสร้างใช้ Default)

Local Volume เหมาะกับ Exposure, Color Adjustments, White Balance, Bloom, Vignette และเอฟเฟกต์ URP ที่รองรับ มันไม่เปลี่ยน Directional Light, ambient หรือ RenderSettings fog เฉพาะกล่อง หมอกในระบบนี้เป็นหมอกระยะทางทั่วฉาก หากต้องการหมอกเฉพาะห้องให้ใช้เอฟเฟกต์หมอกแยกต่างหาก

## ข้อจำกัด
- ใช้ controller กลางที่ active หนึ่งตัวต่อโลก/ชุดฉาก; ระวัง additive scene มีหลายตัวเขียน RenderSettings พร้อมกัน
- การเปลี่ยนแสง realtime ไม่ rebake lightmaps/light probes/reflection probes จึงไม่รับประกันว่าวัตถุ baked จะมืดลงตาม preset ต้องจัด baking/probes แยกเมื่อทำด่านจริง
- Skybox เปลี่ยน tint/exposure เฉพาะ shader ที่มี `_SkyTint` หรือ `_Tint` และ `_Exposure` ไม่สลับวัสดุท้องฟ้าหรือ cubemap อัตโนมัติ
- Ambient mode และ Fog mode เป็นค่าประเภทเลือก ไม่สามารถ interpolate ได้ จึงสลับตอนจบ transition ใช้ mode เดียวกันหากต้องการเปลี่ยนต่อเนื่องสนิท
- Volume ที่ priority สูงกว่าระบบกลางอาจ override ค่าที่เปิดไว้ได้ ตั้งลำดับให้ชัด ไม่ปรับ global profile ทุกอันพร้อมกัน

## ทดสอบ
`Tools/LightingChecks/run.ps1` เปิด Unity 6000.2.4f1 แบบแยก ใช้ production controller และ URP จริง ตรวจ Initial, transition, retarget, unscaled time, asset ไม่ถูกแก้, คืนค่าหลัง disable และ Local Box Volume ภายใน/ภายนอก/ขอบ blend โดยไม่เปลี่ยนฉากหลัก ผลตรวจภาพ แสง baked และความเหมาะสมของความมืดยังต้องประเมินจาก Gameplay จริง

ผลวันที่ 1 พฤศจิกายน 2026: ผ่าน 15 checks และ Runtime/Editor compile ผ่าน ติดตั้งพร้อมบันทึก controller และสี่ cue ลง Scenes_Gameplay แล้ว

## เครื่องมือเพิ่มเติม — Initial, Duplicate, Compare และ Bounding

- **Apply as Initial Lighting + Save Scene**: หยุด Preview แล้วตั้ง Initial Preset พร้อมนำแสงจริงลง Light/RenderSettings ของฉาก จึงเห็นแสงเริ่มต้นตรงกันทั้ง Editor และ Play Mode สร้างสำเนา Skybox/Profile เป็น Asset ของฉาก ไม่เขียนต้นฉบับร่วม ปุ่มนี้บันทึกฉากรวมการแก้ไขที่ค้างอยู่ด้วย
- รองรับ Undo การเปลี่ยนค่าฉาก (ต้อง Save อีกครั้งเพื่อให้ Undo ถูกบันทึกลงไฟล์) Asset สำเนาที่สร้างไว้ยังอยู่หลัง Undo เพื่อไม่ทำลายการอ้างอิงอื่น
- **Duplicate / Save Preset As**: สร้าง SO อิสระก่อนทดลอง เปลี่ยนค่าฉบับสำเนาไม่เปลี่ยนต้นฉบับ
- **A — Before / B — Selected Preset**: สลับแสงก่อน Preview กับ Preset ปัจจุบัน เลือก Compare A เพิ่มได้เพื่อเทียบ Preset สองชุด หรือใช้ Preview Blend เทียบระหว่างกลาง
- **Diagnostics**: ตรวจ Scene View Lighting/Fog/Post Effects/Skybox, กล้อง Post Processing/Volume Layer Mask, แสง baked และ global volume ที่ priority เท่ากันหรือสูงกว่า
- **Area management**: รายการ Local Volume/Lighting Bounding เลือกและ Frame ได้ เปิด Profile ได้ แสดง Priority และระบุ Local Volume ที่ไม่มี Collider
- Local Box Volume มีเส้นขอบและเส้นอ้างอิง Blend Distance ใน Scene View เส้นนอกเป็นเพียงแนวช่วยอ่าน ขอบเขต blend จริงของ URP วัดจากระยะใกล้สุดถึง Collider ซึ่งอาจมีมุมโค้ง

### Lighting Bounding แยกจาก Trigger

กด **Create Lighting Bounding at Scene View** จะได้กล่องอิสระพร้อม controller และ Preset ที่เลือกอยู่ ปรับ Center/Size ได้ใน Inspector หรือใช้ handle ที่ขอบใน Scene View หมุน/ย่อขยายด้วย Transform ได้ ไม่ต้องเพิ่ม WorldTrigger, PointNote, Rigidbody หรือ Collider

ใส่ **Target** เป็น Player หรือกล้องตามที่ต้องการ ถ้าเว้นว่างจะหา object tag Player ก่อน แล้วจึง Main Camera การตรวจเกิดใน Play Mode ผ่านตำแหน่งในกล่อง; ไม่ใช้ OnTriggerEnter จึงไม่ผูกกับระบบ Trigger เดิม หากอยากดูแสงตอนออกแบบให้กด **Preview this area's preset in Editor**

กล่องนี้เปลี่ยน **แสงทั่วโลกของ controller** เช่นเข้าสู่โซนป่าลึกแล้วเปลี่ยนเป็น Night ไม่ใช่ระบบปรับแสงเฉพาะผิววัตถุในกล่อง ถ้าต้องการเฉพาะห้องและการ blend ตามกล้อง ให้ใช้ Local Volume แทน

- Priority เลือกกล่องที่ทำงานเมื่อเข้าหลายกล่องพร้อมกัน; Priority เท่ากันใช้ลำดับที่คงที่ใน session
- Enter Duration คุมเวลาเปลี่ยนตอนเข้า ไม่มี transition ตอนออก
- Trigger Once เปิดเป็นค่าเริ่มต้น: ทำงานครั้งเดียวต่อรอบการเล่น ป้องกันการย้อนทางแล้วกลับแสงเก่า ปิดเพื่อให้ทำงานทุกครั้งที่เข้าใหม่
- เข้า Bounding แล้วเปลี่ยนเป็นแสงหลักค้างไว้ ออกหรือย้อนกลับไม่คืนค่าเดิม
- Cue เนื้อเรื่องที่เรียกภายหลังเปลี่ยนแสงต่อได้ทันที แม้ยังยืนในกล่องที่ทำงานแล้ว
- Disable/Destroy Bounding ไม่ย้อนแสงที่เปลี่ยนไปแล้ว
- แนะนำให้ Bounding ที่ใช้ controller เดียวกันใช้ Target คนเดียวกัน เพราะ controller เปลี่ยนแสงโลกได้หนึ่งสภาพในแต่ละขณะ

ผลการตรวจล่าสุด: 34 checks ผ่าน ครอบคลุม Edit Mode preview, Duplicate, Initial commit/Undo, URP Volume blending, runtime transitions, Bounding แบบหมุน/scale, การเปลี่ยนถาวร, การเดินย้อนกลับ, การเรียก Cue ต่อ และการลบพื้นที่ระหว่างใช้งาน ยังต้องประเมินหน้าตา/ความสว่างในฉากจริงและ lightmaps/probes ตามเดิม

### ความหมายของถาวร
Bounding เปลี่ยนแสงค้างในรอบการเล่นปัจจุบันเท่านั้น ไม่แก้ Initial Preset และยังไม่บันทึกสถานะข้ามการปิดเกม ต้องเชื่อม Save/Load เพิ่มภายหลัง `ResetActivation()` ใช้เปิดสิทธิ์ให้กล่องทำงานใหม่ได้โดยคำสั่ง ไม่เปลี่ยนแสงทันที

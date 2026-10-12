# Shared Asset Naming

รูปแบบ MainFolder_SubFolder_Type(optional)_Name ใช้โฟลเดอร์สองระดับแรกใต้ Assets โดยไม่รวม Assets และไม่ใส่ทุกชั้นย่อย Type ใช้เมื่อหมวดที่นำมาใช้ยังไม่บอกประเภท

ตัวอย่าง:
- Assets/Prefab/Evidence: Prefab_Evidence_RinCamera
- Assets/Data/Evidence: Data_Evidence_RinCamera
- Assets/Data/Documents: Data_Documents_GuardNote
- Assets/Data/PointNotes: Data_PointNotes_Objective_FindRinCamera
- Assets/Data/Items/Weapons: Data_Items_Weapon_Pistol

Name ใช้ Title ของ Note/Document/Evidence หรือ Item Name ของ ItemSO ชื่อแสดงผู้เล่นไม่ถูกแทนด้วยชื่อ asset ยาว ข้อมูลชนิดอื่น เช่น SoundSO/EnemyData ยังไม่ถูกเชื่อมกับเครื่องมือนี้

เครื่องมือ ItemLab และ Scene Notes ตั้งชื่อด้วยกติกานี้เมื่อสร้างหรือแก้ข้อมูล ส่วนการแก้ asset ด้วย Inspector ปกติ/โค้ดภายนอก ให้เลือก assets ใน Project แล้วเรียก System Modification > Naming > Sync Selected Assets เมนูนี้รองรับ ItemSO, DocumentSO, EvidenceSO, PointNoteData และ prefab assets แต่ไม่ไล่เปลี่ยนทุกไฟล์ในโฟลเดอร์ที่เลือก

เมื่อย้ายโฟลเดอร์ ให้ใช้ Sync Selected Assets เพื่อเปลี่ยน prefix ตามที่เก็บใหม่ Prefab ใช้ชื่อเฉพาะท้ายชื่อเดิม และต้องปิด Prefab Mode ของไฟล์นั้นก่อน Sync การซิงก์ชื่อ prefab asset เป็นคำสั่งแยกจากการเปลี่ยน Title ของ Point Note ใน instance ชื่อ instance ยังคงใช้ prefix ที่ตั้งไว้ใน Hierarchy Name Prefix

ระบบใช้ AssetDatabase.RenameAsset จึงรักษา GUID/references เมื่อชื่อชนกันใช้เลขท้าย และไม่เติมเลขใหม่ทุกครั้งที่ Sync ไม่ทำงานระหว่าง Play Mode

"""Generate editable draw.io pages from the Design chat's The Lost draft."""
from pathlib import Path
import xml.etree.ElementTree as E

OUT = Path(__file__).resolve().parents[1] / 'Docs' / 'Design'
OUT.mkdir(parents=True, exist_ok=True)
doc = E.Element('mxfile', host='app.diagrams.net', modified='2026-10-13', version='24.7.17', type='device')

def page(name, subtitle, width=1200, height=1400):
    d = E.SubElement(doc, 'diagram', name=name, id=name.replace(' ', '-'))
    model = E.SubElement(d, 'mxGraphModel', dx='1200', dy='900', grid='1', gridSize='10', guides='1', tooltips='1', connect='1', arrows='1', fold='1', page='1', pageScale='1', pageWidth=str(width), pageHeight=str(height), math='0', shadow='0')
    root = E.SubElement(model, 'root')
    E.SubElement(root, 'mxCell', id='0')
    E.SubElement(root, 'mxCell', id='1', parent='0')
    node(root, 'title', name, 40, 20, width-80, 45, 'text', '#ffffff', 26)
    node(root, 'subtitle', subtitle, 40, 70, width-80, 65, 'text', '#ffffff', 14)
    return root

def node(root, key, label, x, y, w=250, h=85, shape='box', color='#dae8fc', font=16):
    style = 'rounded=1;whiteSpace=wrap;html=0;align=center;verticalAlign=middle;spacing=10;fontFamily=Tahoma;strokeWidth=2;'
    if shape == 'diamond': style += 'rhombus;rounded=0;'
    if shape == 'text': style = 'text;whiteSpace=wrap;html=0;align=left;verticalAlign=middle;spacing=5;strokeColor=none;'
    style += f'fillColor={color};strokeColor=#475569;fontColor=#182230;fontSize={font};'
    c = E.SubElement(root, 'mxCell', id=key, value=label, style=style, vertex='1', parent='1')
    E.SubElement(c, 'mxGeometry', x=str(x), y=str(y), width=str(w), height=str(h), attrib={'as':'geometry'})

def edge(root, key, source, target, label='', color='#475569', points=None):
    ports = {
        'e5': 'exitX=1;exitY=0.25;entryX=0;entryY=0.25;',
        'e8': 'exitX=0;exitY=0.75;entryX=1;entryY=0.75;',
        'e10': 'exitX=1;exitY=0.25;entryX=0;entryY=0.25;',
        'e11': 'exitX=0;exitY=0.75;entryX=1;entryY=0.75;',
        'a7': 'exitX=0.25;exitY=1;entryX=0.25;entryY=0;',
        'a8': 'exitX=0.75;exitY=0;entryX=0.75;entryY=1;',
    }.get(key, '')
    c = E.SubElement(root, 'mxCell', id=key, value=label, style=f'edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;html=0;endArrow=block;endFill=1;strokeWidth=2;strokeColor={color};fontFamily=Tahoma;fontSize=13;fontColor=#182230;labelBackgroundColor=#ffffff;'+ports, edge='1', parent='1', source=source, target=target)
    g = E.SubElement(c, 'mxGeometry', relative='1', attrib={'as':'geometry'})
    if points:
        a=E.SubElement(g, 'Array', attrib={'as':'points'})
        for x,y in points: E.SubElement(a,'mxPoint',x=str(x),y=str(y))

r=page('01 — The Lost: Behavior', 'DESIGN DRAFT • 13 Oct 2026\nแรงขับ: ตามสิ่งที่อาจนำออกจากป่า → พยายามคว้ามินไว้ • แผนภาพนี้เป็นเป้าหมายดีไซน์ ไม่ใช่สถานะโค้ดทั้งหมด')
nodes=[
('idle','IDLE / PATROL\nเดินวน • คลำประตู • พึมพำ',420,150,300,85,'box','#e2e8f0'),
('detect','DETECT\nหยุดฟัง / มือค้าง / หันหาเสียง\nยังไม่รู้ตำแหน่งมินแน่นอน',420,280,300,90,'box','#fff2cc'),
('confirm','ยืนยันว่าเห็นมิน?',440,420,260,100,'diamond','#fff2cc'),
('search','SEARCH\nตรวจต้นเสียง / จุดที่เห็นล่าสุด\nไม่มองทะลุกำแพง',70,590,280,100,'box','#e1d5e7'),
('chase','CHASE\nเดินตาม → ส่งสัญญาณก่อนเร่ง\nตัดสายตาได้ • ไม่วาร์ปตาม',420,590,300,100,'box','#dae8fc'),
('return','LOST TARGET / RETURN\nค้นบริเวณจุดล่าสุด\nไม่พบตามเวลาที่กำหนด → กลับวน',70,780,280,100,'box','#e2e8f0'),
('ready','ระยะถึง + ทางโล่ง\nพร้อมลงมือ + สิทธิ์ว่าง?',440,740,260,110,'diamond','#fff2cc'),
('pressure','FOLLOW / PRESSURE\nยังเดินตาม แต่ชะลอเมื่อถูกขวาง\nรักษาระยะจากมินและเพื่อน',810,745,310,105,'box','#d5e8d4'),
('prep','PREPARATION\nตั้งเท้า • ยกมือ • เอนตัว\nยังไม่ทำ Damage / ผู้เล่นหลบได้',420,900,300,95,'box','#ffe6cc'),
('attack','ATTACK\nคว้าหนึ่งครั้ง / ทิศและระยะจำกัด\nไม่หมุนตามมินเต็มตัว',420,1040,300,95,'box','#f8cecc'),
('recovery','RECOVERY\nมือค้าง / เสียหลัก / ตั้งตัวใหม่\nเปิดช่องให้หนี ก่อนโจมตีครั้งถัดไป',420,1180,300,95,'box','#d5e8d4'),
('note','ทุกช่วง: ถูกยิง → Hit reaction / ตาย → Death (ดูหน้า 02)\nDetect, การคว้าล็อกทิศ และค้นรอบจุดล่าสุดเป็นเป้าหมายดีไซน์ที่ต้องพัฒนาต่อ\nค่าระยะ เวลา และจำนวนคนเข้าตีต้องทดสอบในเกม ไม่กำหนดตัวเลขตายตัวในผังนี้',40,1300,1120,80,'text','#ffffff')]
for n in nodes: node(r,*n)
for args in [('e1','idle','detect','ได้ยิน / เห็นการเคลื่อนไหว'),('e2','detect','confirm'),('e3','confirm','chase','ใช่'),('e4','confirm','search','ไม่ใช่ แต่มีจุดให้ตรวจ'),('e5','search','chase','พบมิน'),('e6','search','return','ไม่พบ'),('e7','return','idle','กลับวน', '#64748b',[(30,825),(30,190)]),('e8','chase','search','เสียสายตา'),('e9','chase','ready'),('e10','ready','pressure','ยังไม่พร้อม'),('e11','pressure','ready','ประเมินใหม่'),('e12','ready','prep','พร้อม'),('e13','prep','attack','ลงมือ'),('e14','attack','recovery','โดนหรือพลาด'),('e15','recovery','chase','คืนสิทธิ์ / ยังเห็น', '#475569',[(760,1225),(760,640)]),('e16','prep','chase','เป้าหมายพ้นระยะ / ยกเลิก', '#a16207',[(390,947),(390,640)])]: edge(r,*args)

r=page('02 — Group & Interruptions', 'DESIGN DRAFT • ฝูงไม่มีหัวหน้าและไม่วางแผนล้อม • แต่ละตัวรับรู้ข้อมูลของตัวเอง\nเสียงปืนทำให้ไปตรวจต้นเสียง การเห็นมินจึงทำให้เริ่มตาม',1200,1100)
for n in [
('noise','เสียงปืน / เสียงที่รับรู้ได้',60,170,290,70,'box','#fff2cc'),
('investigate','ไปตรวจต้นเสียง\nไม่รู้ตำแหน่งมินปัจจุบันอัตโนมัติ',60,290,290,95,'box','#e1d5e7'),
('seen','เห็นมินแล้ว?',75,435,260,100,'diamond','#fff2cc'),
('pursue','แต่ละตัวตามมิน\nเลี่ยงสิ่งกีดขวาง / ไม่ทะลุเพื่อน',60,590,290,95,'box','#dae8fc'),
('notfound','SEARCH → RETURN\nค้นบริเวณต้นเสียง / ไม่พบตามเวลา\nกลับวงจร Idle (หน้า 01)',60,780,290,110,'box','#e2e8f0'),
('eligible','เข้าถึงได้ + ระยะถึง\nพักครบ + มีสิทธิ์โจมตี?',440,590,285,110,'diamond','#fff2cc'),
('wait','ตัวอื่นยังเคลื่อนกดดัน\nชะลอ / เลี่ยง / คลำเมื่อทางตัน\nไม่ทำวงล้อมสมบูรณ์',440,780,285,110,'box','#d5e8d4'),
('turn','PREP → ATTACK → RECOVERY\nเริ่มต้นทดลอง 1 ตัวเข้าตี\nค่อยทดสอบ 2 ตัวถ้าอ่านทัน',820,585,310,115,'box','#f8cecc'),
('release','คืนสิทธิ์ให้ตัวอื่น\nจบ / ยกเลิก / stagger / ตาย\nปิด AI / หมดเวลาเข้าประชิด',820,785,310,105,'box','#d5e8d4'),
('bullet','ถูกยิงระหว่างมีชีวิต',440,170,285,70,'box','#ffe6cc'),
('hit','HIT REACTION\nสะดุ้ง ณ จุดโดน / ผลักเล็กน้อย\nหนักพอ → ยกเลิกท่าและตั้งหลัก',440,290,285,105,'box','#ffe6cc'),
('dead','HP หมด → DEATH\nหยุด AI / คืนสิทธิ์ / ท่าตายหรือ Ragdoll\nLoot และอายุศพตาม Profile',820,170,310,105,'box','#f8cecc'),
('rules','กติกาที่ต้องรักษา\n• มีสัญญาณก่อนคว้า และช่องว่างหลังพลาด\n• ไม่คว้าซ้อนจนผู้เล่นเสียการควบคุมต่อเนื่อง\n• ฝูงกดดันจากตำแหน่งจริง ไม่แชร์ตำแหน่งมินแบบรู้ทุกอย่าง\n• ความต่างเกิดจากทิศที่เข้ามา / ระยะ / จังหวะ ไม่ใช่สุ่มทุกเฟรม',60,930,1070,135,'text','#ffffff')]: node(r,*n)
for a in [('a1','noise','investigate'),('a2','investigate','seen'),('a3','seen','pursue','ใช่'),('a4','seen','notfound','ไม่พบ','#64748b',[(30,485),(30,835)]),('a5','pursue','eligible'),('a6','eligible','turn','พร้อม'),('a7','eligible','wait','ไม่พร้อม'),('a8','wait','eligible','ประเมินใหม่'),('a9','turn','release'),('a10','release','eligible','ตัวที่เข้าถึงได้ถัดไป','#475569',[(770,840),(770,730),(580,730)]),('a11','bullet','hit','ยังมีชีวิต'),('a12','bullet','dead','เลือดหมด'),('a13','hit','eligible','ตั้งหลักแล้วประเมินใหม่'),('a14','dead','release','คืนสิทธิ์','#64748b',[(1160,222),(1160,838)])]:edge(r,*a)

r=page('03 — Variants & Sources', 'The Lost • วงจรพื้นฐานร่วมกัน แต่ภัยที่ผู้เล่นต้องรับมือต่างกัน\nรูปแบบย่อยด้านล่างเป็นข้อเสนอจากแชท Design ยังไม่ถือว่าได้อนุมัติหรือทำครบแล้ว',1200,1000)
node(r,'base','พื้นฐานร่วม: เดินวน → รับรู้ → ค้นหา → ตาม → พยายามรั้งมิน → พัก / กลับวน',60,170,1080,80,'box','#dae8fc')
node(r,'unarmed','มือเปล่า\n\nตามเข้าระยะใกล้\nยกมือก่อนคว้า\nคว้าพลาดแล้วเปิดช่องให้หนี\n\nรับมือ: รักษาระยะ / เคลื่อนข้าง',60,330,320,230,'box','#d5e8d4')
node(r,'stick','ถือไม้เท้า\n\nคลำทางขณะเดิน\nกวาดไม้รั้งในระยะไกลกว่า\nเตรียมท่านานให้เห็น\n\nรับมือ: รอพลาดแล้วผ่านด้านข้าง',440,330,320,230,'box','#ffe6cc')
node(r,'torch','ถือไฟฉาย\n\nหยุดตรวจและกวาดแสง\nพบมิน → ร้องให้ตัวใกล้เคียงตรวจ\nแล้วตามเข้าคว้า\n\nรับมือ: ใช้ฉากบัง / เปลี่ยนตำแหน่ง',820,330,320,230,'box','#fff2cc')
for i,t in enumerate(['unarmed','stick','torch']):edge(r,'v'+str(i),'base',t)
node(r,'scope','ขอบเขตต้นแบบ\nเริ่มจาก The Lost มือเปล่า 1 แบบก่อน: เดินตาม / เตือนก่อนคว้า / พลาดแล้วพัก\nไม่จับล็อกยาวหรือบังคับ QTE ในต้นแบบแรก\nสิ่งที่ติดตัวต้องเปลี่ยนวิธีรับมือจริง ไม่ใช่แค่เปลี่ยนโมเดล',60,630,1080,130,'box','#e2e8f0')
node(r,'source','แหล่งที่มาและสถานะ\n• Design - Documentation: การสนทนา 12 Oct 2026 เรื่องผู้สูญหาย, วงจรพฤติกรรม, Chaos และรูปแบบย่อย\n• GDD : Project Midnight: โลกเครือหลงและฉากเปิด (เอกสารที่อ่านยังไม่มีวงจร The Lost ล่าสุด)\n• Docs/EnemyGroupCombat.md และ EnemyController.cs: ใช้เทียบฐานเทคนิค ไม่ใช้แทนดีไซน์\n\nผังจัดทำ 13 Oct 2026 • เป็นภาพรวมเป้าหมายสำหรับคุยและทำต้นแบบ\nยังไม่ได้แก้โค้ด AI ให้ตรงผัง หรือกำหนด timing / damage / จำนวนศัตรูขั้นสุดท้าย',60,790,1080,170,'text','#ffffff',14)

path=OUT/'TheLost-Behavior.drawio'
E.indent(doc)
E.ElementTree(doc).write(path,encoding='utf-8',xml_declaration=True)
check=E.parse(path)
ids=[]
for diagram in check.getroot():
    cells=diagram.findall('./mxGraphModel/root/mxCell')
    keys={c.get('id') for c in cells}
    for c in cells:
        if c.get('edge'):
            assert c.get('source') in keys and c.get('target') in keys
    ids.append((diagram.get('name'),len(cells)))
print(path)
print(ids)

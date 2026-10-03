Add-Type -AssemblyName System.Drawing
$outDir = $PSScriptRoot
function MakeDiagram($name, $title, $cards, $note) {
    $bmp = [System.Drawing.Bitmap]::new(1400,760)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.TextRenderingHint = 'AntiAliasGridFit'
    $g.Clear([System.Drawing.Color]::FromArgb(20,27,34))
    $white = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(235,241,245))
    $muted = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(177,196,208))
    $accent = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(117,208,186))
    $panel = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(34,47,58))
    $titleFont = [System.Drawing.Font]::new('Tahoma',34,[System.Drawing.FontStyle]::Bold)
    $headFont = [System.Drawing.Font]::new('Tahoma',25,[System.Drawing.FontStyle]::Bold)
    $bodyFont = [System.Drawing.Font]::new('Tahoma',23)
    $smallFont = [System.Drawing.Font]::new('Tahoma',20)
    $g.DrawString('PROJECT MIDNIGHT  /  CURRENT MECHANICS',$smallFont,$accent,48,30)
    $g.DrawString($title,$titleFont,$white,48,85)
    $count=$cards.Count
    $width=[int]((1304-($count-1)*38)/$count)
    for($i=0;$i -lt $count;$i++) {
        $x=48+$i*($width+38)
        $g.FillRectangle($panel,$x,190,$width,370)
        $g.FillRectangle($accent,$x,190,$width,6)
        $g.DrawString(('0'+($i+1)),$headFont,$accent,$x+22,215)
        $g.DrawString($cards[$i][0],$headFont,$white,[System.Drawing.RectangleF]::new($x+22,270,$width-44,88))
        $g.DrawString($cards[$i][1],$bodyFont,$muted,[System.Drawing.RectangleF]::new($x+22,365,$width-44,180))
        if($i -lt $count-1) {
            $pen=[System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(117,208,186),3)
            $pen.EndCap='ArrowAnchor'
            $g.DrawLine($pen,$x+$width+4,375,$x+$width+33,375)
            $pen.Dispose()
        }
    }
    $g.DrawString($note,$smallFont,$white,[System.Drawing.RectangleF]::new(48,600,1300,105))
    $g.DrawString('ผังอธิบายจากโค้ด • 2 ต.ค. 2026 • ไม่ใช่ภาพหน้าจอเกม',$smallFont,$muted,48,717)
    $bmp.Save((Join-Path $outDir ($name+'.png')),[System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
}
MakeDiagram 'movement' 'การเคลื่อนที่และโหมดควบคุม' @(
    @('WASD / เดิน',"ทิศทางเดินอิงกล้อง`nตัวละครหันตาม`nทิศทางการเคลื่อนที่"),
    @('Shift / วิ่ง',"กดค้างพร้อมเดิน`nวิ่งได้เมื่อไม่ได้เล็ง`nยังไม่มี Stamina"),
    @('คลิกขวา / เล็ง',"กล้องเข้าใกล้`nหันตามกล้อง`nเดินช้าลงขณะเล็ง")
) 'I เปิดกระเป๋า: หยุดการควบคุมตัวละครและปล่อยเคอร์เซอร์ แต่ศัตรูยังทำงานต่อ'
MakeDiagram 'combat' 'ยิง • ความแม่นยำ • กระสุน' @(
    @('คลิกขวา / เล็ง',"เล็งนิ่งนานขึ้น`nเป้าหุบลง`nเดินทำให้เป้ากว้างขึ้น"),
    @('คลิกซ้าย / ยิง',"ต้องอยู่ในโหมดเล็ง`nใช้ 1 นัดต่อการคลิก`nเกิดแรงถีบและเป้าขยาย"),
    @('R / บรรจุ',"รอเวลาบรรจุ`nย้ายกระสุนสำรอง`nเข้าแมกกาซีน")
) 'เป้าส่ายตามการหายใจและจุดยิงใช้ตำแหน่งเดียวกัน • กระสุนชน Hitbox แล้วสร้างความเสียหาย'
MakeDiagram 'enemy' 'ศัตรู: จากลาดตระเวนสู่การโจมตี' @(
    @('ลาดตระเวน',"สุ่มจุดบน NavMesh`nหยุดรอเมื่อถึงจุด`nแล้วเลือกจุดต่อไป"),
    @('ตรวจพบ / ไล่ตาม',"ตรวจระยะ + มุมมอง`nตรวจสิ่งกีดขวาง`nจำตำแหน่งที่เห็นล่าสุด"),
    @('โจมตี / กลับ',"เตรียมโจมตีในระยะ`nเช็กระยะอีกครั้ง`nก่อนลดเลือดผู้เล่น")
) 'เมื่อไม่เห็นผู้เล่น: ตามตำแหน่งล่าสุด → รอครบเวลาสูญเสียเป้าหมาย → กลับลาดตระเวน'
MakeDiagram 'inventory' 'เก็บของ • กระเป๋า • ฟื้นเลือด' @(
    @('F / เก็บของ',"เข้าเขตวัตถุ`nข้อความเก็บของแสดง`nกด F เพื่อรับไอเทม"),
    @('แยกเส้นทาง',"กระสุน → สำรองปืน`nของทั่วไป → ช่องกริด`nเต็มแล้วของยังอยู่ในฉาก"),
    @('I / ใช้ของ',"เปิดกระเป๋า`nคลิกขวาของรักษา`nฟื้นเลือดและใช้ของหมด")
) 'กริดเริ่มต้นในโค้ด 4 × 4 ปรับได้ • ไอเทมกินพื้นที่ตามขนาด • ยังไม่มีลากจัดหรือหมุนไอเทม'

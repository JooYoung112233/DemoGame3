Add-Type -AssemblyName System.Drawing
$root='E:\personalProject\Demo3\demo6'
$ev=Join-Path $root '검증\정수리-시안적용-v9'
$font=New-Object System.Drawing.Font('Malgun Gothic',23)
$small=New-Object System.Drawing.Font('Malgun Gothic',17)
$white=[System.Drawing.Brushes]::White
function LoadImage([string]$p) { return [System.Drawing.Image]::FromFile($p) }
function DrawFull($g,$im,$x,$y,$w,$h) { $g.DrawImage($im,[System.Drawing.Rectangle]::new($x,$y,$w,$h)) }
$ref=LoadImage (Join-Path $root '승인.png')
$before=LoadImage (Join-Path $ev 'paired-latest-lamp-lit\1080\lamp-before.png')
$after=LoadImage (Join-Path $ev 'paired-latest-lamp-lit\1080\lamp-after.png')
$canvas=New-Object System.Drawing.Bitmap(2880,1230)
$g=[System.Drawing.Graphics]::FromImage($canvas)
$g.Clear([System.Drawing.Color]::FromArgb(20,22,25))
$g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$titles=@('승인 시안 · 구성 기준','적용 전 · 최신 조명 유지','적용 후 · 입구 E / 시작 장비')
$ims=@($ref,$before,$after)
for($i=0;$i -lt 3;$i++) {
 $x=$i*960
 $g.DrawString($titles[$i],$font,$white,$x+20,14)
 DrawFull $g $ims[$i] $x 65 960 540
 $g.DrawString('아래: 원본 픽셀 1:1 부분 확대 (밝기 보정 없음)',$small,$white,$x+20,622)
 $sx=if($i -eq 0){356}else{480}
 $sy=if($i -eq 0){175}else{230}
 $g.DrawImage($ims[$i],[System.Drawing.Rectangle]::new($x,665,960,480),[System.Drawing.Rectangle]::new($sx,$sy,960,480),[System.Drawing.GraphicsUnit]::Pixel)
}
$g.DrawString('실제 Unity 1080p 같은 프레임 전후 비교 · 카메라/조명/충돌 동일 · 넓은 기존 방 배치와 시야 범위는 유지',$small,$white,24,1166)
$canvas.Save((Join-Path $ev '비교-승인시안-1080p.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$canvas.Dispose();$ref.Dispose();$before.Dispose();$after.Dispose()
$before=LoadImage (Join-Path $ev 'paired-latest-default\720\pillar-before.png')
$after=LoadImage (Join-Path $ev 'paired-latest-default\720\pillar-after.png')
$canvas=New-Object System.Drawing.Bitmap(2560,810)
$g=[System.Drawing.Graphics]::FromImage($canvas);$g.Clear([System.Drawing.Color]::FromArgb(20,22,25))
$g.DrawString('720p 적용 전 · 기본 조명',$font,$white,20,15)
$g.DrawString('720p 적용 후 · 기본 조명',$font,$white,1300,15)
$g.DrawImageUnscaled($before,0,65);$g.DrawImageUnscaled($after,1280,65)
$canvas.Save((Join-Path $ev '비교-실제720p.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$canvas.Dispose();$before.Dispose();$after.Dispose();$font.Dispose();$small.Dispose()
Write-Output 'Two comparison boards saved; source screenshots unmodified.'

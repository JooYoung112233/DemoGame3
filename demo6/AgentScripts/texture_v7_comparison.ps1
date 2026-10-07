$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = 'E:\personalProject\Demo3\demo6\검증\텍스처-시범-v7'
$font = [System.Drawing.Font]::new('Segoe UI',16)
$small = [System.Drawing.Font]::new('Segoe UI',12)
$brush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(225,228,235))
function Panel($graphics,$file,$x,$y,$w,$h,$sx,$sy) {
  $im = [System.Drawing.Image]::FromFile($file)
  try {$graphics.DrawImage($im,[System.Drawing.Rectangle]::new($x,$y,$w,$h),$sx,$sy,$w,$h,[System.Drawing.GraphicsUnit]::Pixel)} finally {$im.Dispose()}
}
$sheet=[System.Drawing.Bitmap]::new(1240,1000)
$g=[System.Drawing.Graphics]::FromImage($sheet)
$g.Clear([System.Drawing.Color]::FromArgb(22,24,29))
$g.DrawString('Texture pilot v7  |  BEFORE / AFTER', $font,$brush,20,14)
$g.DrawString('Native 1:1 camera crops. Same paused frame; no exposure adjustment. HUD omitted only in these pairs.', $small,$brush,20,46)
for($row=0;$row -lt 2;$row++) {
  $height=@(720,1080)[$row];$y=106+440*$row
  $sx=if($height -eq 720){390}else{680};$sy=if($height -eq 720){170}else{315}
  $g.DrawString("$height p  / BEFORE",$small,$brush,20,$y-27)
  $g.DrawString("$height p  / AFTER",$small,$brush,640,$y-27)
  Panel $g "$root\paired\$height\frozen-before-right.png" 20 $y 580 390 $sx $sy
  Panel $g "$root\paired\$height\frozen-after-right.png" 640 $y 580 390 $sx $sy
}
$sheet.Save("$root\before-after-overview.png",[System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$sheet.Dispose()
$sheet=[System.Drawing.Bitmap]::new(1240,850)
$g=[System.Drawing.Graphics]::FromImage($sheet);$g.Clear([System.Drawing.Color]::FromArgb(22,24,29))
$g.DrawString('Movement review  |  1080p native 1:1 crops', $font,$brush,20,14)
$g.DrawString('Before above / After below. Samples 0, 2, 4, 5. Live frames include ambient flicker.', $small,$brush,20,46)
for($row=0;$row -lt 2;$row++) {
 $mode=@('before','after')[$row]
 for($col=0;$col -lt 4;$col++) {
  $frame=@(0,2,4,5)[$col];$x=20+305*$col;$y=115+350*$row
  $g.DrawString("$mode  $frame",$small,$brush,$x,$y-28)
  Panel $g "$root\paired\1080\$mode-move-$frame.png" $x $y 285 300 850 390
 }
}
$sheet.Save("$root\movement-strip.png",[System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$sheet.Dispose();$font.Dispose();$small.Dispose();$brush.Dispose()
Write-Output 'Created before-after-overview.png and movement-strip.png using native-scale crops.'

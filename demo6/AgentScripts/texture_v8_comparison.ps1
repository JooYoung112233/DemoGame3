$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root='E:\personalProject\Demo3\demo6\검증\텍스처-구조-v8'
$font=[System.Drawing.Font]::new('Segoe UI',16)
$small=[System.Drawing.Font]::new('Segoe UI',12)
$brush=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(225,228,235))
foreach($spot in @('wall-corner','pillar')) {
 $sheet=[System.Drawing.Bitmap]::new(1240,1190)
 $g=[System.Drawing.Graphics]::FromImage($sheet);$g.Clear([System.Drawing.Color]::FromArgb(22,24,29))
 $g.DrawString("Environment structure v8  |  $spot",$font,$brush,20,14)
 $g.DrawString('V7 / V8  -  Native 1:1 camera crops. Same frame and lights. No exposure adjustment. Overlay HUD excluded.',$small,$brush,20,46)
 for($row=0;$row -lt 2;$row++) {
  $height=@(720,1080)[$row];$y=@(110,610)[$row];$sy=@(110,245)[$row];$h=@(450,540)[$row]
  $sx=if($spot -eq 'wall-corner'){@(420,670)[$row]}else{@(390,680)[$row]}
  for($col=0;$col -lt 2;$col++) {
   $mode=@('v7','v8')[$col];$x=20+620*$col
   $g.DrawString("$height p  / $mode",$small,$brush,$x,$y-28)
   $im=[System.Drawing.Image]::FromFile("$root\paired\$height\$spot-$mode.png")
   try {$g.DrawImage($im,[System.Drawing.Rectangle]::new($x,$y,580,$h),$sx,$sy,580,$h,[System.Drawing.GraphicsUnit]::Pixel)} finally {$im.Dispose()}
  }
 }
 $sheet.Save("$root\$spot-comparison.png",[System.Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$sheet.Dispose()
}
$font.Dispose();$small.Dispose();$brush.Dispose()
Write-Output 'Created wall-corner-comparison.png and pillar-comparison.png (native scale).'

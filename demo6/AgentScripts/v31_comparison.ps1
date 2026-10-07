Add-Type -AssemblyName System.Drawing
$v31Root=(Get-Location).Path
$v31Evidence=Join-Path $v31Root '검증\돌갑충-예고-v031'
$v31Art=Join-Path $v31Root '아트\돌갑충-예고-v031'
$v31Bitmap=New-Object System.Drawing.Bitmap 1280,1340
$v31Graphics=[System.Drawing.Graphics]::FromImage($v31Bitmap)
$v31Graphics.Clear([System.Drawing.Color]::FromArgb(23,24,27))
$v31Font=New-Object System.Drawing.Font 'Malgun Gothic',17
$v31Small=New-Object System.Drawing.Font 'Malgun Gothic',12
$v31Graphics.DrawString('공격 예고 · 게임 배율 비교', $v31Font,[System.Drawing.Brushes]::White,18,10)
$v31Graphics.DrawString('실제 카메라 56.45 px / unit · 두 그림 동일 배율 · 판정 범위·진행 시간 유지', $v31Small,[System.Drawing.Brushes]::LightGray,18,42)
$v31Shapes=@('circle','half','rect')
$v31Labels=@('원형','전방 반원','돌진 직선')
for($v31Row=0;$v31Row -lt 3;$v31Row++) {
 $v31Y=80+$v31Row*420
 $v31Graphics.DrawString(($v31Labels[$v31Row]+' | 기존'),$v31Small,[System.Drawing.Brushes]::LightGray,18,$v31Y)
 $v31Graphics.DrawString(($v31Labels[$v31Row]+' | v031'),$v31Small,[System.Drawing.Brushes]::White,658,$v31Y)
 for($v31Col=0;$v31Col -lt 2;$v31Col++) {
  $v31Suffix=if($v31Col -eq 0){'before'}else{'after'}
  $v31Image=[System.Drawing.Image]::FromFile((Join-Path $v31Evidence ($v31Shapes[$v31Row]+'-'+$v31Suffix+'-game-scale.png')))
  $v31Dest=New-Object System.Drawing.Rectangle ($v31Col*640),($v31Y+28),640,390
  $v31Src=New-Object System.Drawing.Rectangle 0,125,640,390
  $v31Graphics.DrawImage($v31Image,$v31Dest,$v31Src,[System.Drawing.GraphicsUnit]::Pixel)
  $v31Image.Dispose()
 }
}
$v31Bitmap.Save((Join-Path $v31Art 'telegraph-game-scale-before-after.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$v31Graphics.Dispose();$v31Bitmap.Dispose();$v31Font.Dispose();$v31Small.Dispose()

# Ogre v030's approved 77.108 px/unit review camera converted to the real 56.453 px/unit display scale.
$v30Bitmap=New-Object System.Drawing.Bitmap 938,515
$v30Graphics=[System.Drawing.Graphics]::FromImage($v30Bitmap)
$v30Graphics.Clear([System.Drawing.Color]::FromArgb(23,24,27))
$v30Font=New-Object System.Drawing.Font 'Malgun Gothic',12
$v30Graphics.DrawString('오우거 v030 · 실제 게임 배율 환산 확인 (56.45 px/unit)', $v30Font,[System.Drawing.Brushes]::White,12,10)
$v30Frames=@('A-slam','D-sweep')
for($v30I=0;$v30I -lt 2;$v30I++) {
 $v30Image=[System.Drawing.Image]::FromFile((Join-Path $v31Root ('검증\오우거-패턴-v030\frames\'+$v30Frames[$v30I]+'\05.png')))
 $v30Graphics.DrawImage($v30Image,($v30I*469),40,469,469)
 $v30Image.Dispose()
}
$v30Bitmap.Save((Join-Path $v31Root '검증\오우거-패턴-v030\actual-scale-check.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$v30Graphics.Dispose();$v30Bitmap.Dispose();$v30Font.Dispose()
Write-Output 'Comparison plates saved; no source art modified.'

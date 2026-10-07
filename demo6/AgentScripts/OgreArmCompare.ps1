Add-Type -AssemblyName System.Drawing
$root='E:\personalProject\Demo3\demo6'
$old=Join-Path $root '검증/오우거-인게임-v027/frames'
$new=Join-Path $root '검증/오우거-팔교정-v029/frames'
$out=Join-Path $root '아트/오우거-팔교정-v029'
$font=New-Object System.Drawing.Font 'Malgun Gothic',14
foreach($clip in @('A-slam','D-sweep')){
 $folder=Join-Path $out $clip;New-Item -ItemType Directory -Force $folder | Out-Null
 for($i=0;$i -lt 10;$i++){
  $size=469;$board=New-Object System.Drawing.Bitmap ($size*2),($size+48)
  $g=[System.Drawing.Graphics]::FromImage($board);$g.Clear([System.Drawing.Color]::FromArgb(48,51,56))
  $g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $before=[System.Drawing.Image]::FromFile((Join-Path $old ($clip+'/'+$i.ToString('D2')+'.png')))
  $after=[System.Drawing.Image]::FromFile((Join-Path $new ($clip+'/'+$i.ToString('D2')+'.png')))
  $g.DrawImage($before,0,48,$size,$size);$g.DrawImage($after,$size,48,$size,$size)
  $g.DrawString('수정 전 v027', $font,[System.Drawing.Brushes]::White,12,9)
  $g.DrawString('수정 후 v029 · 정상 속도 10프레임', $font,[System.Drawing.Brushes]::White,($size+12),9)
  $board.Save((Join-Path $folder ($i.ToString('D2')+'.png')));$g.Dispose();$board.Dispose()
  if($i -eq 0){
   $board=New-Object System.Drawing.Bitmap 1280,688;$g=[System.Drawing.Graphics]::FromImage($board);$g.Clear([System.Drawing.Color]::FromArgb(48,51,56))
   $g.DrawImage($before,0,48,640,640);$g.DrawImage($after,640,48,640,640)
   $g.DrawString('수정 전 v027 · 확대', $font,[System.Drawing.Brushes]::White,12,9)
   $g.DrawString('수정 후 v029 · 확대', $font,[System.Drawing.Brushes]::White,652,9)
   $board.Save((Join-Path $out ($clip+'-enlarged-before-after.png')));$g.Dispose();$board.Dispose()
  }
  $before.Dispose();$after.Dispose()
 }
}
$font.Dispose()

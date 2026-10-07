Add-Type -AssemblyName System.Drawing
$root='E:\personalProject\Demo3\demo6'
$dir=Join-Path $root '검증/오우거-팔교정-v029'
$clips=@('A-slam','B-charge-2','B-charge-3','B-wall','C-roar','D-sweep','D-phase2','phase2','broken-recover','transition-broken','death')
$font=New-Object System.Drawing.Font 'Arial',12
foreach($clip in $clips){
 $files=Get-ChildItem -LiteralPath (Join-Path $dir ('frames/'+$clip)) -Filter 'key-*.png' | Sort-Object Name
 $board=New-Object System.Drawing.Bitmap (320*6),(350*[Math]::Ceiling($files.Count/6))
 $g=[System.Drawing.Graphics]::FromImage($board);$g.Clear([System.Drawing.Color]::FromArgb(48,51,56))
 for($i=0;$i -lt $files.Count;$i++){
  $x=320*($i%6);$y=350*[Math]::Floor($i/6)
  $im=[System.Drawing.Image]::FromFile($files[$i].FullName)
  $g.DrawImage($im,$x,$y+25,320,320);$g.DrawString(($clip+' '+$files[$i].BaseName),$font,[System.Drawing.Brushes]::White,$x,$y);$im.Dispose()
 }
 $board.Save((Join-Path $dir ($clip+'-contact.png')));$g.Dispose();$board.Dispose()
}
$font.Dispose()

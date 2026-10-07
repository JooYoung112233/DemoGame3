Add-Type -AssemblyName System.Drawing
$mapRoot=(Get-Location).Path
$mapEv=Join-Path $mapRoot '검증\맵-화풍정리-v032'
$mapOut=Join-Path $mapRoot '아트\맵-화풍정리'
function Save-MapComparison([string]$before,[string]$after,[string]$target,[string]$title,[string]$note) {
 $left=[System.Drawing.Image]::FromFile($before)
 $right=[System.Drawing.Image]::FromFile($after)
 if($left.Width -ne $right.Width -or $left.Height -ne $right.Height){throw 'Mismatched comparison dimensions'}
 $canvas=New-Object System.Drawing.Bitmap ($left.Width*2),($left.Height+112)
 $graphics=[System.Drawing.Graphics]::FromImage($canvas)
 $graphics.Clear([System.Drawing.Color]::FromArgb(23,24,27))
 $titleFont=New-Object System.Drawing.Font 'Malgun Gothic',24
 $noteFont=New-Object System.Drawing.Font 'Malgun Gothic',17
 $graphics.DrawString($title,$titleFont,[System.Drawing.Brushes]::White,22,8)
 $graphics.DrawString($note,$noteFont,[System.Drawing.Brushes]::LightGray,22,47)
 $graphics.DrawString('적용 전',$noteFont,[System.Drawing.Brushes]::LightGray,22,80)
 $graphics.DrawString('v032 적용 후',$noteFont,[System.Drawing.Brushes]::White,($left.Width+22),80)
 $graphics.DrawImageUnscaled($left,0,112)
 $graphics.DrawImageUnscaled($right,$left.Width,112)
 $canvas.Save($target,[System.Drawing.Imaging.ImageFormat]::Png)
 $graphics.Dispose();$canvas.Dispose();$left.Dispose();$right.Dispose();$titleFont.Dispose();$noteFont.Dispose()
}
Save-MapComparison (Join-Path $mapEv 'composition\map-before-room.png') (Join-Path $mapEv 'composition\map-after-room.png') (Join-Path $mapOut 'map-v032-unity-lighting-before-after.png') '맵 v032 · 원본 Unity 조명에서 화풍 비교' '동일 조명·그림자·67.5 px/unit · 시야 덮개만 숨긴 아트 검수 · 적과 예고는 임시 외형 배치'
Save-MapComparison (Join-Path $mapEv 'map-before-game.png') (Join-Path $mapEv 'map-after-game.png') (Join-Path $mapOut 'map-v032-actual-game-before-after.png') '맵 v032 · 실제 게임 화면 비교' '원본 DungeonTest · 기존 시야·안개·광원 유지 · 추가 적/예고 없는 기본 화면 · UI 제외'
Save-MapComparison (Join-Path $mapEv 'composition\arch-before-detail.png') (Join-Path $mapEv 'composition\arch-after-detail.png') (Join-Path $mapOut 'arch-v032-unity-before-after.png') '막힌 아치 · 원본 Unity 조명에서 확대 비교' '같은 PNG 크기·피벗·투명 영역 · 같은 광원·그림자 · 검수용 시야 덮개 숨김'
Write-Output 'Three labelled Unity comparison PNGs saved at native capture resolution.'

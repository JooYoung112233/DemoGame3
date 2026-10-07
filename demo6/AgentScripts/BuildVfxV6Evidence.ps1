Add-Type -AssemblyName System.Drawing
$reviewRoot = 'E:\personalProject\Demo3\demo6\검증\이펙트-개선-v6'
$selections = Get-Content -LiteralPath (Join-Path $reviewRoot 'overview-selection.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$sheet = New-Object System.Drawing.Bitmap(1280, 1176)
$graphics = [System.Drawing.Graphics]::FromImage($sheet)
$graphics.Clear([System.Drawing.Color]::FromArgb(24, 23, 21))
$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$font = New-Object System.Drawing.Font('Arial', 13)
$brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(219, 208, 182))
try {
    for ($i = 0; $i -lt $selections.Count; $i++) {
        $x = ($i % 2) * 640
        $y = [Math]::Floor($i / 2) * 392
        $graphics.DrawString($selections[$i].label, $font, $brush, [single]($x + 12), [single]($y + 7))
        $frame = [System.Drawing.Image]::FromFile((Join-Path $reviewRoot $selections[$i].file))
        try { $graphics.DrawImage($frame, [int]$x, [int]($y + 32), 640, 360) } finally { $frame.Dispose() }
    }
    $sheet.Save((Join-Path $reviewRoot 'before-after-overview.png'), [System.Drawing.Imaging.ImageFormat]::Png)
} finally { $brush.Dispose(); $font.Dispose(); $graphics.Dispose(); $sheet.Dispose() }

# D3D GPU readback stores these rows upside down. Preserve raw captures and export an upright view.
$burstRoot = Join-Path $reviewRoot 'after\1080\burst3finisher'
$uprightRoot = Join-Path $burstRoot 'upright'
[System.IO.Directory]::CreateDirectory($uprightRoot) | Out-Null
foreach ($file in Get-ChildItem -LiteralPath $burstRoot -Filter '*.png' -File) {
    $frame = [System.Drawing.Image]::FromFile($file.FullName)
    try {
        $frame.RotateFlip([System.Drawing.RotateFlipType]::RotateNoneFlipY)
        $frame.Save((Join-Path $uprightRoot $file.Name), [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $frame.Dispose() }
}

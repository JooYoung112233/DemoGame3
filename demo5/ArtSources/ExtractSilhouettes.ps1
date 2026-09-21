Add-Type -AssemblyName System.Drawing
$source = [System.Drawing.Bitmap]::new((Join-Path $PSScriptRoot 'approved-reference.png'))
function Export-Body($name, $left, $top, $width, $height, $points) {
    $target = [System.Drawing.Bitmap]::new($width, $height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($target)
    $g.Clear([System.Drawing.Color]::Transparent)
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddPolygon([System.Drawing.PointF[]]@($points | ForEach-Object { [System.Drawing.PointF]::new($_[0],$_[1]) }))
    $g.SetClip($path)
    $g.DrawImage($source,[System.Drawing.Rectangle]::new(0,0,$width,$height),$left,$top,$width,$height,[System.Drawing.GraphicsUnit]::Pixel)
    $target.Save((Join-Path $PSScriptRoot "../Assets/Art/Tokens/$name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $path.Dispose(); $target.Dispose()
}
# Body only: colored base pixels are excluded. Unity creates a separate white tintable base.
Export-Body 'scout-body' 440 370 82 128 @(@(14,105),@(16,93),@(17,82),@(20,73),@(20,68),@(14,67),@(10,61),@(10,48),@(13,37),@(14,29),@(19,26),@(18,21),@(20,12),@(24,6),@(31,3),@(42,5),@(47,2),@(56,3),@(63,7),@(66,13),@(65,21),@(61,25),@(62,39),@(61,47),@(62,56),@(62,64),@(58,69),@(60,79),@(63,100),@(63,107),@(47,107),@(43,95),@(41,88),@(35,99),@(31,108),@(14,108))
Export-Body 'sentry-body' 616 423 83 139 @(@(12,107),@(14,97),@(14,89),@(12,83),@(6,77),@(5,63),@(6,48),@(9,37),@(14,28),@(19,27),@(18,20),@(21,15),@(27,13),@(28,7),@(35,3),@(43,4),@(46,10),@(51,10),@(56,14),@(56,20),@(51,23),@(47,25),@(49,36),@(51,44),@(52,57),@(55,64),@(53,73),@(49,76),@(51,90),@(55,107),@(56,112),@(42,112),@(36,94),@(30,97),@(28,113),@(13,113))
Export-Body 'infected-body' 999 373 77 116 @(@(10,90),@(10,77),@(13,64),@(12,57),@(8,62),@(4,58),@(7,46),@(12,31),@(17,27),@(11,22),@(10,16),@(13,8),@(20,5),@(27,5),@(33,10),@(33,17),@(29,23),@(37,27),@(44,35),@(49,49),@(54,62),@(53,69),@(48,70),@(43,57),@(42,53),@(43,68),@(49,85),@(50,91),@(38,92),@(31,76),@(26,83),@(24,94),@(12,94))
Export-Body 'supply-crate' 398 263 151 86 @(@(5,9),@(103,1),@(143,12),@(134,69),@(11,75),@(3,13))
$source.Dispose()

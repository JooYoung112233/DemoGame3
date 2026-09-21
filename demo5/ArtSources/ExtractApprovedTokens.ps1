Add-Type -AssemblyName System.Drawing
$source = [System.Drawing.Bitmap]::new((Join-Path $PSScriptRoot 'approved-reference.png'))
function Export-Token($name, $left, $top, $width, $height, $points) {
    $target = [System.Drawing.Bitmap]::new($width, $height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($target)
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $vertices = [System.Drawing.PointF[]]@($points | ForEach-Object { [System.Drawing.PointF]::new($_[0],$_[1]) })
    $path.AddPolygon($vertices)
    $graphics.SetClip($path)
    $graphics.DrawImage($source,[System.Drawing.Rectangle]::new(0,0,$width,$height),$left,$top,$width,$height,[System.Drawing.GraphicsUnit]::Pixel)
    $target.Save((Join-Path $PSScriptRoot "../Assets/Art/Tokens/$name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose(); $path.Dispose(); $target.Dispose()
}
Export-Token 'approved-scout' 440 370 82 128 @(@(7,111),@(8,106),@(14,103),@(16,93),@(17,82),@(20,73),@(20,68),@(14,67),@(10,61),@(10,48),@(13,37),@(14,29),@(19,26),@(18,21),@(20,12),@(24,6),@(31,3),@(42,5),@(47,2),@(56,3),@(63,7),@(66,13),@(65,21),@(61,25),@(62,39),@(61,47),@(62,56),@(62,64),@(58,69),@(60,79),@(63,100),@(69,105),@(75,110),@(72,117),@(61,122),@(45,125),@(28,125),@(13,121),@(7,117))
Export-Token 'approved-sentry' 616 423 83 139 @(@(4,115),@(7,109),@(12,107),@(14,97),@(14,89),@(12,83),@(6,77),@(5,63),@(6,48),@(9,37),@(14,28),@(19,27),@(18,20),@(21,15),@(27,13),@(28,7),@(35,3),@(43,4),@(46,10),@(51,10),@(56,14),@(56,20),@(51,23),@(47,25),@(49,36),@(51,44),@(52,57),@(55,64),@(53,73),@(49,76),@(51,90),@(55,107),@(64,111),@(68,119),@(65,125),@(58,130),@(45,135),@(28,135),@(13,131),@(5,126))
Export-Token 'approved-infected' 999 373 77 116 @(@(4,91),@(9,87),@(10,77),@(13,64),@(12,57),@(8,62),@(4,58),@(7,46),@(12,31),@(17,27),@(11,22),@(10,16),@(13,8),@(20,5),@(27,5),@(33,10),@(33,17),@(29,23),@(37,27),@(44,35),@(49,49),@(54,62),@(53,69),@(48,70),@(43,57),@(42,53),@(43,68),@(49,85),@(59,88),@(66,93),@(68,100),@(61,107),@(48,110),@(30,111),@(15,106),@(6,101))
$source.Dispose()

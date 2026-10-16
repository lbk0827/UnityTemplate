# Original geometric UI surfaces. Run from the repository root.
# The existing texture dimensions and GUIDs are retained; only art and slice borders change.
Add-Type -AssemblyName System.Drawing
$root = Join-Path $PSScriptRoot '../../Assets/_Project/Bounce/Content/Shop'
function Panel($name, $width, $height, $hex, $radius, $border) {
    $scale=4
    $bitmap=New-Object System.Drawing.Bitmap ($width*$scale),($height*$scale)
    $g=[System.Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode=[System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.ScaleTransform($scale,$scale)
    $brush=New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml($hex))
    $path=New-Object System.Drawing.Drawing2D.GraphicsPath
    if ($radius -gt 0) {
        $d=$radius*2
        $path.AddArc(1,1,$d,$d,180,90)
        $path.AddArc(($width-$d-1),1,$d,$d,270,90)
        $path.AddArc(($width-$d-1),($height-$d-1),$d,$d,0,90)
        $path.AddArc(1,($height-$d-1),$d,$d,90,90)
        $path.CloseFigure()
        $g.FillPath($brush,$path)
    } else { $g.FillRectangle($brush,0,0,$width,$height) }
    $output=New-Object System.Drawing.Bitmap $width,$height
    $resize=[System.Drawing.Graphics]::FromImage($output)
    $resize.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $resize.DrawImage($bitmap,0,0,$width,$height)
    if ($radius -eq 0) { $resize.Clear([System.Drawing.ColorTranslator]::FromHtml($hex)) }
    $target=Join-Path $root ($name+'.png')
    $output.Save($target,[System.Drawing.Imaging.ImageFormat]::Png)
    $resize.Dispose(); $output.Dispose(); $g.Dispose(); $path.Dispose(); $brush.Dispose(); $bitmap.Dispose()
    $meta=[IO.File]::ReadAllText($target+'.meta')
    $meta=[regex]::Replace($meta,'spriteBorder: \{[^}]+\}',"spriteBorder: {x: $border, y: $border, z: $border, w: $border}")
    [IO.File]::WriteAllText($target+'.meta',$meta)
}
Panel 'IMG_Bundle_Bg' 96 442 '#EEE6FF' 22 24
Panel 'IMG_NoADBundle_Bg' 96 442 '#EEE6FF' 22 24
Panel 'IMG_Coin_Goods_Bg' 102 396 '#EEE6FF' 22 24
Panel 'IMG_Bundle_Goods_Btn' 70 114 '#39B957' 30 32
Panel 'BTN_CoinBuy' 72 96 '#39B957' 30 32
Panel 'IMG_TimeBox' 166 48 '#FFF9EF' 20 22
Panel 'IMG_ShopBox' 248 257 '#9983D9' 0 0
foreach ($kind in @('Coin','NoAd','Package')) { Panel "IMG_ShopTitle_$kind" 144 138 '#E2D7FA' 24 26 }
foreach ($kind in @('Coin','NoAds','Package')) { Panel "IMG_ShopBar_$kind" 52 180 '#E2D7FA' 18 20 }
# The existing badge label is diagonal, so keep a diagonal ribbon silhouette.
$tag=New-Object System.Drawing.Bitmap 177,191
$g=[System.Drawing.Graphics]::FromImage($tag)
$g.SmoothingMode=[System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$brush=New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#FF7885'))
$points=[System.Drawing.Point[]]@([System.Drawing.Point]::new(0,103),[System.Drawing.Point]::new(103,0),[System.Drawing.Point]::new(177,0),[System.Drawing.Point]::new(0,177))
$g.FillPolygon($brush,$points)
$tag.Save((Join-Path $root 'IMG_Shop_Tag.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$brush.Dispose(); $g.Dispose(); $tag.Dispose()

# Original vector artwork rendered to a shared Windows/Android application icon.
# Rebuild from the project root with: powershell -File Assets/Art/GenerateAppIcon.ps1
Add-Type -AssemblyName System.Drawing
$bitmap = [System.Drawing.Bitmap]::new(1024, 1024)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.ScaleTransform(2, 2)

function Color([string]$hex) { return [System.Drawing.ColorTranslator]::FromHtml($hex) }
function RoundRect([float]$x,[float]$y,[float]$w,[float]$h,[float]$r,[string]$fill,[string]$stroke = '') {
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $d = $r * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc(($x + $w - $d), $y, $d, $d, 270, 90)
    $path.AddArc(($x + $w - $d), ($y + $h - $d), $d, $d, 0, 90)
    $path.AddArc($x, ($y + $h - $d), $d, $d, 90, 90)
    $path.CloseFigure()
    $brush = [System.Drawing.SolidBrush]::new((Color $fill))
    $graphics.FillPath($brush, $path)
    $brush.Dispose()
    if ($stroke) {
        $pen = [System.Drawing.Pen]::new((Color $stroke), 3)
        $graphics.DrawPath($pen, $path)
        $pen.Dispose()
    }
    $path.Dispose()
}
function Diamond([float]$x,[float]$y,[float]$w,[float]$h) {
    $points = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new($x, $y - $h),
        [System.Drawing.PointF]::new($x + $w, $y),
        [System.Drawing.PointF]::new($x, $y + $h),
        [System.Drawing.PointF]::new($x - $w, $y)
    )
    $brush = [System.Drawing.SolidBrush]::new((Color '#bc3544'))
    $graphics.FillPolygon($brush, $points)
    $brush.Dispose()
}
function Spade([float]$x,[float]$y,[float]$scale = 1) {
    $saved = $graphics.Save()
    $graphics.TranslateTransform($x, $y)
    $graphics.ScaleTransform($scale, $scale)
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddBezier(0,-68,25,-37,68,-13,68,18)
    $path.AddBezier(68,18,68,56,18,65,0,33)
    $path.AddBezier(0,33,-18,65,-68,56,-68,18)
    $path.AddBezier(-68,18,-68,-13,-25,-37,0,-68)
    $path.CloseFigure()
    $path.AddBezier(-8,38,-10,57,-18,77,-32,82)
    $path.AddLine(-32,82,32,82)
    $path.AddBezier(32,82,18,77,10,57,8,38)
    $path.CloseFigure()
    $brush = [System.Drawing.SolidBrush]::new((Color '#1e2b30'))
    $graphics.FillPath($brush, $path)
    $brush.Dispose()
    $path.Dispose()
    $graphics.Restore($saved)
}
function RotateCard([float]$angle,[float]$x,[float]$y) {
    $graphics.TranslateTransform($x, $y)
    $graphics.RotateTransform($angle)
    $graphics.TranslateTransform(-$x, -$y)
}

$graphics.Clear((Color '#184d40'))
RoundRect 22 22 468 468 66 '#205b4b' '#547861'
$rear = $graphics.Save()
RotateCard -12 197 241
RoundRect 84 87 244 332 21 '#174438'
RoundRect 75 75 244 332 21 '#f6edcf' '#dacda5'
Diamond 165 212 50 67
Diamond 106 115 12 16
Diamond 289 368 12 16
$graphics.Restore($rear)
$front = $graphics.Save()
RotateCard 10 332 293
RoundRect 232 145 214 316 20 '#174438'
RoundRect 225 135 214 316 20 '#fffcf2' '#dedbc9'
Spade 332 281
Spade 253 169 .18
Spade 411 416 -.18
$graphics.Restore($front)
$graphics.Dispose()
$output = [System.Drawing.Bitmap]::new(512,512)
$outputGraphics = [System.Drawing.Graphics]::FromImage($output)
$outputGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$outputGraphics.DrawImage($bitmap,0,0,512,512)
$outputGraphics.Dispose()
$bitmap.Dispose()
$output.Save((Join-Path $PSScriptRoot 'AppIcon.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$output.Dispose()

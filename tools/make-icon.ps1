# Draws the ChessCoach icon (a brass knight on a walnut tile) and writes it as a multi-size .ico.
# Usage: powershell -NoProfile -File tools\make-icon.ps1 -Out src\ChessCoach.Desktop\Assets\chesscoach.ico
param([string]$Out = "src\ChessCoach.Desktop\Assets\chesscoach.ico")

Add-Type -AssemblyName System.Drawing

function New-Tile([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([System.Drawing.Color]::Transparent)

    # rounded walnut tile
    $r = [single]($size * 0.22)
    $d = $size * 1.0 - 1
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, $r * 2, $r * 2, 180, 90)
    $path.AddArc($d - $r * 2, 0, $r * 2, $r * 2, 270, 90)
    $path.AddArc($d - $r * 2, $d - $r * 2, $r * 2, $r * 2, 0, 90)
    $path.AddArc(0, $d - $r * 2, $r * 2, $r * 2, 90, 90)
    $path.CloseFigure()
    $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml("#241B15"))
    $g.FillPath($brush, $path)

    # brass knight glyph, centred
    $fontSize = [single]($size * 0.62)
    $font = New-Object System.Drawing.Font("Segoe UI Symbol", $fontSize, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $fmt = New-Object System.Drawing.StringFormat
    $fmt.Alignment = [System.Drawing.StringAlignment]::Center
    $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
    $gold = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml("#C9A15E"))
    $g.DrawString([string][char]0x265E, $font, $gold, (New-Object System.Drawing.RectangleF 0, ([single]($size * 0.04)), $size, $size), $fmt)

    $g.Dispose()
    return $bmp
}

$sizes = 16, 32, 48, 256
$pngs = foreach ($s in $sizes) {
    $bmp = New-Tile $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    , $ms.ToArray()
}

# ICO container with PNG-compressed images (supported by Windows Vista and later)
$outPath = Join-Path (Get-Location) $Out
New-Item -ItemType Directory -Force -Path (Split-Path $outPath) | Out-Null
$stream = [System.IO.File]::Create($outPath)
$w = New-Object System.IO.BinaryWriter $stream
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$pngs[$i].Length); $w.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $w.Write($p) }
$w.Close(); $stream.Close()
"wrote $outPath ($($sizes -join ', ') px)"

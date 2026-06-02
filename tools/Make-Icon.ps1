<#
    Make-Icon.ps1 — renders the Glow logo (white "glow" on a black rounded square)
    into a multi-resolution Windows .ico and a PNG, using System.Drawing.

    Output:
        ../src/Glow/glow.ico   (used as the application/exe icon)
        ../assets/glow.png     (preview / README)

    Run:  powershell -ExecutionPolicy Bypass -File tools/Make-Icon.ps1
#>

Add-Type -AssemblyName System.Drawing

function New-GlowBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([System.Drawing.Color]::Transparent)

    $radius = [float]($size * 0.22)
    $d = $radius * 2
    $rect = New-Object System.Drawing.RectangleF(0, 0, ($size - 1), ($size - 1))
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
    $path.AddArc(($rect.Right - $d), $rect.Y, $d, $d, 270, 90)
    $path.AddArc(($rect.Right - $d), ($rect.Bottom - $d), $d, $d, 0, 90)
    $path.AddArc($rect.X, ($rect.Bottom - $d), $d, $d, 90, 90)
    $path.CloseFigure()
    $bg = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(18, 18, 20))
    $g.FillPath($bg, $path)

    $text = if ($size -ge 24) { "glow" } else { "g" }
    $fontSize = if ($size -ge 24) { [float]($size * 0.42) } else { [float]($size * 0.6) }
    $font = New-Object System.Drawing.Font("Segoe UI", $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $fg = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $fmt = New-Object System.Drawing.StringFormat
    $fmt.Alignment = [System.Drawing.StringAlignment]::Center
    $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
    $layout = New-Object System.Drawing.RectangleF(0, 0, $size, $size)
    $g.DrawString($text, $font, $fg, $layout, $fmt)

    $g.Dispose(); $bg.Dispose(); $font.Dispose(); $fg.Dispose(); $path.Dispose()
    return $bmp
}

function Get-PngBytes($bmp) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $ms.ToArray()
    $ms.Dispose()
    return ,$bytes
}

$root = Split-Path -Parent $PSScriptRoot
$icoPath = Join-Path $root "src\Glow\glow.ico"
$pngPath = Join-Path $root "assets\glow.png"

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = @()
foreach ($s in $sizes) {
    $bmp = New-GlowBitmap $s
    if ($s -eq 256) { $bmp.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png) }
    $images += [pscustomobject]@{ Size = $s; Png = (Get-PngBytes $bmp) }
    $bmp.Dispose()
}

# Assemble the ICO container (PNG-compressed entries; valid on Windows Vista+).
$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ms)
$bw.Write([uint16]0)              # reserved
$bw.Write([uint16]1)              # type: icon
$bw.Write([uint16]$images.Count)  # image count

$offset = 6 + (16 * $images.Count)
foreach ($img in $images) {
    $dim = if ($img.Size -ge 256) { 0 } else { $img.Size }
    $bw.Write([byte]$dim)         # width  (0 => 256)
    $bw.Write([byte]$dim)         # height (0 => 256)
    $bw.Write([byte]0)            # palette colors
    $bw.Write([byte]0)            # reserved
    $bw.Write([uint16]1)          # color planes
    $bw.Write([uint16]32)         # bits per pixel
    $bw.Write([uint32]$img.Png.Length)
    $bw.Write([uint32]$offset)
    $offset += $img.Png.Length
}
foreach ($img in $images) { $bw.Write($img.Png) }

$bw.Flush()
[System.IO.File]::WriteAllBytes($icoPath, $ms.ToArray())
$bw.Dispose(); $ms.Dispose()

Write-Host "Wrote $icoPath ($((Get-Item $icoPath).Length) bytes)"
Write-Host "Wrote $pngPath"

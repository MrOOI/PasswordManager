<#
.SYNOPSIS
    Renders the PassManager padlock logo into a multi-resolution Windows .ico file.
.DESCRIPTION
    Sizes up to 64 px are stored as 32-bit DIBs (maximum compatibility with resource compilers and
    installers); larger sizes are stored PNG-compressed, as supported since Windows Vista.
#>
[CmdletBinding()]
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\assets\app.ico'),
    [string]$PngPath = (Join-Path $PSScriptRoot '..\assets\app-256.png')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-RoundedRectanglePath([single]$x, [single]$y, [single]$width, [single]$height, [single]$radius) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $width - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $width - $d, $y + $height - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $height - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-LogoBitmap([int]$size) {
    $bitmap = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.Clear([System.Drawing.Color]::Transparent)
        $scale = [single]($size / 64.0)
        $g.ScaleTransform($scale, $scale)

        # Rounded square with a blue gradient (design space 64 x 64).
        $background = New-RoundedRectanglePath 1 1 62 62 14
        $gradient = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            (New-Object System.Drawing.PointF(0, 0)), (New-Object System.Drawing.PointF(64, 64)),
            [System.Drawing.Color]::FromArgb(255, 59, 130, 246), [System.Drawing.Color]::FromArgb(255, 29, 78, 216))
        $g.FillPath($gradient, $background)

        # Padlock shackle.
        $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, 5.5)
        $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $shackle = New-Object System.Drawing.Drawing2D.GraphicsPath
        $shackle.AddLine(22, 31, 22, 24)
        $shackle.AddArc(22, 14, 20, 20, 180, 180)
        $shackle.AddLine(42, 24, 42, 31)
        $g.DrawPath($pen, $shackle)

        # Padlock body and keyhole.
        $body = New-RoundedRectanglePath 15 29 34 25 5
        $g.FillPath([System.Drawing.Brushes]::White, $body)
        $keyhole = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 29, 78, 216))
        $g.FillEllipse($keyhole, 28.5, 35, 7, 7)
        $g.FillRectangle($keyhole, 30.5, 39, 3, 8)
    }
    finally {
        $g.Dispose()
    }
    return $bitmap
}

function Get-PngBytes([System.Drawing.Bitmap]$bitmap) {
    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    return , $stream.ToArray()
}

function Get-DibBytes([System.Drawing.Bitmap]$bitmap) {
    $size = $bitmap.Width
    $rect = New-Object System.Drawing.Rectangle(0, 0, $size, $size)
    $data = $bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $pixels = New-Object byte[] ($data.Stride * $size)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $pixels, 0, $pixels.Length)
    $stride = $data.Stride
    $bitmap.UnlockBits($data)

    $stream = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter($stream)
    # BITMAPINFOHEADER: height is doubled to account for the AND mask.
    $writer.Write([UInt32]40); $writer.Write([Int32]$size); $writer.Write([Int32]($size * 2))
    $writer.Write([UInt16]1); $writer.Write([UInt16]32); $writer.Write([UInt32]0)
    $writer.Write([UInt32]($size * $size * 4)); $writer.Write([Int32]0); $writer.Write([Int32]0)
    $writer.Write([UInt32]0); $writer.Write([UInt32]0)
    for ($y = $size - 1; $y -ge 0; $y--) { $writer.Write($pixels, $y * $stride, $size * 4) }
    $maskStride = [int]([Math]::Ceiling($size / 32.0) * 4)
    $writer.Write((New-Object byte[] ($maskStride * $size)))
    $writer.Flush()
    return , $stream.ToArray()
}

$fullPath = [System.IO.Path]::GetFullPath($OutputPath)
New-Item -ItemType Directory -Force -Path (Split-Path $fullPath) | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path ([System.IO.Path]::GetFullPath($PngPath))) | Out-Null

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$images = @()
foreach ($size in $sizes) {
    $bitmap = New-LogoBitmap $size
    try {
        if ($size -le 64) { $images += , (Get-DibBytes $bitmap) } else { $images += , (Get-PngBytes $bitmap) }
        if ($size -eq 256) { $bitmap.Save([System.IO.Path]::GetFullPath($PngPath), [System.Drawing.Imaging.ImageFormat]::Png) }
    }
    finally { $bitmap.Dispose() }
}

$file = [System.IO.File]::Create($fullPath)
$writer = New-Object System.IO.BinaryWriter($file)
try {
    $writer.Write([UInt16]0); $writer.Write([UInt16]1); $writer.Write([UInt16]$sizes.Count)
    $offset = 6 + (16 * $sizes.Count)
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dimension = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([UInt16]1); $writer.Write([UInt16]32)
        $writer.Write([UInt32]$images[$i].Length); $writer.Write([UInt32]$offset)
        $offset += $images[$i].Length
    }
    foreach ($image in $images) { $writer.Write($image) }
}
finally {
    $writer.Dispose()
}

Write-Host "Icon written to $fullPath"

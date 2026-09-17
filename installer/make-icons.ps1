$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path $PSScriptRoot -Parent
$pngPath = Join-Path $root "src\UI\Assets\cuebox.png"
$icoPath = Join-Path $root "src\UI\Assets\cuebox.ico"
$wizDir = Join-Path $PSScriptRoot "art"
New-Item -ItemType Directory -Force -Path $wizDir | Out-Null

$src = [System.Drawing.Image]::FromFile($pngPath)
$script:src = $src
$sizes = 16, 24, 32, 48, 64, 128, 256
$blobs = New-Object System.Collections.Generic.List[byte[]]
$dims = New-Object System.Collections.Generic.List[int]

function New-Square([int]$size) {
  $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.Clear([System.Drawing.Color]::Transparent)
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
  $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $g.DrawImage($script:src, 0, 0, $size, $size)
  $g.Dispose()
  return $bmp
}

foreach ($s in $sizes) {
  $bmp = New-Square $s
  $ms = New-Object System.IO.MemoryStream
  $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
  $blobs.Add($ms.ToArray())
  $dims.Add($s)
  $bmp.Dispose()
  $ms.Dispose()
}

$count = $blobs.Count
$header = 6 + (16 * $count)
$offset = $header
$fs = [System.IO.File]::Open($icoPath, [System.IO.FileMode]::Create)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$count)
for ($i = 0; $i -lt $count; $i++) {
  $s = $dims[$i]
  $len = $blobs[$i].Length
  $bw.Write([byte]$(if ($s -ge 256) { 0 } else { $s }))
  $bw.Write([byte]$(if ($s -ge 256) { 0 } else { $s }))
  $bw.Write([byte]0)
  $bw.Write([byte]0)
  $bw.Write([uint16]1)
  $bw.Write([uint16]32)
  $bw.Write([uint32]$len)
  $bw.Write([uint32]$offset)
  $offset += $len
}
foreach ($b in $blobs) { $bw.Write($b) }
$bw.Flush()
$fs.Dispose()

function Save-Bmp($bmp, $path) {
  $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Bmp)
  $bmp.Dispose()
}

$left = New-Object System.Drawing.Bitmap 164, 314, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$g = [System.Drawing.Graphics]::FromImage($left)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.Clear([System.Drawing.Color]::FromArgb(18, 20, 26))
$g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(201, 163, 106))), 0, 0, 6, 314)
$icon = New-Square 96
$g.DrawImage($icon, 34, 86, 96, 96)
$icon.Dispose()
$font = New-Object System.Drawing.Font "Segoe UI", 14, ([System.Drawing.FontStyle]::Bold)
$brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(201, 163, 106))
$sf = New-Object System.Drawing.StringFormat
$sf.Alignment = [System.Drawing.StringAlignment]::Center
$g.DrawString("CUEBOX", $font, $brush, (New-Object System.Drawing.RectangleF 0, 196, 164, 32), $sf)
$g.Dispose()
Save-Bmp $left (Join-Path $wizDir "wizard.bmp")

$small = New-Object System.Drawing.Bitmap 55, 55, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$g = [System.Drawing.Graphics]::FromImage($small)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.Clear([System.Drawing.Color]::FromArgb(18, 20, 26))
$icon = New-Square 47
$g.DrawImage($icon, 4, 4, 47, 47)
$icon.Dispose()
$g.Dispose()
Save-Bmp $small (Join-Path $wizDir "wizard-small.bmp")

$src.Dispose()
Copy-Item $icoPath (Join-Path $wizDir "cuebox.ico") -Force
Write-Host "Wrote $icoPath"
Write-Host "Wrote wizard art in $wizDir"

Add-Type -AssemblyName System.Drawing

$srcPath = "C:\Users\PC\.gemini\antigravity\brain\2cac9276-16d9-4723-8202-32b2ff4964de\.user_uploaded\media_1792424636112_9e541790.png"
$src = [System.Drawing.Bitmap]::FromFile($srcPath)

# Bounding box coordinates from earlier test:
# X: [37, 151], Y: [428, 591]
$cropX = 37
$cropY = 428
$cropW = 151 - 37 + 1   # 115
$cropH = 591 - 428 + 1  # 164

# Create high-res 512x512 square transparent canvas
$canvasSize = 512
$targetBmp = New-Object System.Drawing.Bitmap($canvasSize, $canvasSize, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($targetBmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g.Clear([System.Drawing.Color]::Transparent)

# Fit into square canvas with 40px margin (height = 512 - 80 = 432)
$scale = 432.0 / $cropH
$scaledW = [int]($cropW * $scale)
$scaledH = [int]($cropH * $scale)
$destX = [int](($canvasSize - $scaledW) / 2)
$destY = [int](($canvasSize - $scaledH) / 2)

$srcRect = New-Object System.Drawing.Rectangle($cropX, $cropY, $cropW, $cropH)
$destRect = New-Object System.Drawing.Rectangle($destX, $destY, $scaledW, $scaledH)

$g.DrawImage($src, $destRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
$g.Dispose()
$src.Dispose()

# Create Assets directory if not exists
$assetsDir = "C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\Assets"
if (!(Test-Path $assetsDir)) {
    New-Item -ItemType Directory -Path $assetsDir -Force | Out-Null
}

$pngPath = Join-Path $assetsDir "logo.png"
$targetBmp.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Output "Saved 512x512 PNG to $pngPath"

# Function to save .ico with multiple sizes (16, 24, 32, 48, 64, 128, 256)
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$icoPngList = New-Object System.Collections.Generic.List[byte[]]

foreach ($s in $sizes) {
    $bmpSmall = New-Object System.Drawing.Bitmap($s, $s, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $gSmall = [System.Drawing.Graphics]::FromImage($bmpSmall)
    $gSmall.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $gSmall.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $gSmall.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $gSmall.Clear([System.Drawing.Color]::Transparent)
    
    $destS = New-Object System.Drawing.Rectangle(0, 0, $s, $s)
    $srcS = New-Object System.Drawing.Rectangle(0, 0, $canvasSize, $canvasSize)
    $gSmall.DrawImage($targetBmp, $destS, $srcS, [System.Drawing.GraphicsUnit]::Pixel)
    $gSmall.Dispose()
    
    $ms = New-Object System.IO.MemoryStream
    $bmpSmall.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $icoPngList.Add($ms.ToArray())
    $ms.Dispose()
    $bmpSmall.Dispose()
}

$targetBmp.Dispose()

# Write Windows ICO file format with embedded PNGs (standard Vista+ format)
$icoPath = Join-Path $assetsDir "app.ico"
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)

# ICONDIR header: Reserved (0), Type (1 = ICO), Count
$bw.Write([UInt16]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]$sizes.Count)

# Calculate image data offset (6 header + 16 * count)
$offset = 6 + (16 * $sizes.Count)

for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $pngBytes = $icoPngList[$i]
    
    # Width (byte, 0 means 256)
    $wByte = if ($s -ge 256) { [byte]0 } else { [byte]$s }
    # Height (byte, 0 means 256)
    $hByte = if ($s -ge 256) { [byte]0 } else { [byte]$s }
    
    $bw.Write($wByte)
    $bw.Write($hByte)
    $bw.Write([byte]0) # Color palette count (0 = no palette)
    $bw.Write([byte]0) # Reserved
    $bw.Write([UInt16]1) # Color planes
    $bw.Write([UInt16]32) # Bits per pixel
    $bw.Write([UInt32]$pngBytes.Length) # Image size in bytes
    $bw.Write([UInt32]$offset) # Offset to image data
    
    $offset += $pngBytes.Length
}

# Write each image payload
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $bw.Write($icoPngList[$i])
}

$bw.Flush()
$bw.Close()
$fs.Close()

Write-Output "Saved multi-resolution ICO to $icoPath"

Add-Type -AssemblyName System.Drawing

$srcPath = "C:\Users\PC\.gemini\antigravity\brain\2cac9276-16d9-4723-8202-32b2ff4964de\.user_uploaded\media_1792424636112_9e541790.png"
$src = [System.Drawing.Bitmap]::FromFile($srcPath)

Write-Output "PixelFormat: $($src.PixelFormat)"
$p0 = $src.GetPixel(10, 10)
Write-Output "Pixel at (10,10): A=$($p0.A), R=$($p0.R), G=$($p0.G), B=$($p0.B)"

$pLogo = $src.GetPixel(95, 510)
Write-Output "Pixel at logo center (95,510): A=$($pLogo.A), R=$($pLogo.R), G=$($pLogo.G), B=$($pLogo.B)"

$src.Dispose()

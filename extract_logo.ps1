Add-Type -AssemblyName System.Drawing

$srcPath = "C:\Users\PC\.gemini\antigravity\brain\2cac9276-16d9-4723-8202-32b2ff4964de\.user_uploaded\media_1792424636112_9e541790.png"
$src = [System.Drawing.Bitmap]::FromFile($srcPath)

$minX = $src.Width
$maxX = 0
$minY = $src.Height
$maxY = 0

for ($y = 0; $y -lt $src.Height; $y++) {
    for ($x = 0; $x -lt $src.Width; $x++) {
        $c = $src.GetPixel($x, $y)
        # Non-white pixel
        if ($c.A -gt 20 -and ($c.R -lt 245 -or $c.G -lt 245 -or $c.B -lt 245)) {
            if ($x -lt $minX) { $minX = $x }
            if ($x -gt $maxX) { $maxX = $x }
            if ($y -lt $minY) { $minY = $y }
            if ($y -gt $maxY) { $maxY = $y }
        }
    }
}

Write-Output "BBox: X=[$minX, $maxX], Y=[$minY, $maxY], Width=$($maxX - $minX + 1), Height=$($maxY - $minY + 1)"
$src.Dispose()

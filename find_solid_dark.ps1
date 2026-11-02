$lines = Get-Content 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\MainWindow.xaml'
$solidDark = @()
for ($i = 0; $i -lt $lines.Length; $i++) {
    $line = $lines[$i]
    if ($line -match 'Background="(#[0-9a-fA-F]{6})"') {
        $color = $matches[1]
        # check if solid dark (starts with #0, #1, #2)
        if ($color -match '^#[0-2]') {
            $solidDark += ("Line " + ($i + 1) + " (" + $color + "): " + $line.Trim())
        }
    }
}
Write-Output ("Total solid dark backgrounds: " + $solidDark.Count)
$solidDark | Select-Object -First 30

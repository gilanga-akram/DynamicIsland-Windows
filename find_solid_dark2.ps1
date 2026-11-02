$lines = Get-Content 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\MainWindow.xaml'
$solidDark = @()
for ($i = 0; $i -lt $lines.Length; $i++) {
    $line = $lines[$i]
    if ($line -match 'Background="(#[0-9a-fA-F]{6})"') {
        $color = $matches[1]
        if ($color -match '^#[0-2]') {
            $solidDark += ("Line " + ($i + 1) + " (" + $color + "): " + $line.Trim())
        }
    }
}
$solidDark | Select-Object -Skip 30

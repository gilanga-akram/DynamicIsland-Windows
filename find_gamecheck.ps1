$lines = Get-Content 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\MainWindow.xaml.cs'
for ($i = 0; $i -lt $lines.Length; $i++) {
    if ($lines[$i] -match '_gameCheckTimer = new') {
        for ($j = [Math]::Max(0, $i - 2); $j -lt [Math]::Min($lines.Length, $i + 10); $j++) {
            Write-Output ("Line " + ($j + 1) + ": " + $lines[$j])
        }
        break
    }
}

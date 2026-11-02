$lines = Get-Content 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\MainWindow.xaml'
for ($i = 0; $i -lt $lines.Length; $i++) {
    if ($lines[$i] -match 'NotchFillPath|NotchBorderPath') {
        Write-Output ("Line " + ($i + 1) + ": " + $lines[$i].Trim())
    }
}

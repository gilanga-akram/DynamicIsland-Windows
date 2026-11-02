$lines = Get-Content 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\MainWindow.xaml'
for ($i = 0; $i -lt [Math]::Min(150, $lines.Length); $i++) {
    if ($lines[$i] -match 'TextPrimary|TextSecondary|CardBg') {
        Write-Output ("Line " + ($i + 1) + ": " + $lines[$i].Trim())
    }
}

$lines = Get-Content 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\MainWindow.xaml.cs'
for ($i = 0; $i -lt $lines.Length; $i++) {
    if ($lines[$i] -match '_notchFillCollapsed') {
        Write-Output ("Line " + ($i + 1) + ": " + $lines[$i])
    }
}

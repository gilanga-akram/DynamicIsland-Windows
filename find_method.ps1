$lines = Get-Content 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\MainWindow.xaml.cs'
for ($i = 0; $i -lt $lines.Length; $i++) {
    if ($lines[$i] -match 'void UpdateIslandVisualGlass') {
        for ($j = $i; $j -lt [Math]::Min($lines.Length, $i + 35); $j++) {
            Write-Output $lines[$j]
        }
        break
    }
}

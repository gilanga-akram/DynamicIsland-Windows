$lines = Get-Content 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\MainWindow.xaml'
for ($i = 0; $i -lt $lines.Length; $i++) {
    if ($lines[$i] -match 'x:Key="ToolCardBtn"') {
        for ($j = $i; $j -lt [Math]::Min($lines.Length, $i + 40); $j++) {
            Write-Output $lines[$j]
        }
        break
    }
}

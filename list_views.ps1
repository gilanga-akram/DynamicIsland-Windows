$c = Get-Content 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\MainWindow.xaml'
for ($i = 0; $i -lt $c.Length; $i++) {
    if ($c[$i] -match 'x:Name="View') {
        Write-Output ("$i : " + $c[$i].Trim())
    }
}

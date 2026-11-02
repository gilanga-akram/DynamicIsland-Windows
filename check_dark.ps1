$c = Get-Content 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\MainWindow.xaml' -Raw
$patterns = @('1A2840', '242434', '262632', '383842', 'F2080812')
foreach ($p in $patterns) {
    [regex]::Matches($c, ".*$p.*") | ForEach-Object {
        Write-Output ("MATCH " + $p + " -- " + $_.Value.Trim())
    }
}

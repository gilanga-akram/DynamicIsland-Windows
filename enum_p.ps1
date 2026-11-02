$procs = Get-Process | Where-Object { $_.ProcessName -match "discord|steam" }
foreach ($p in $procs) {
    if ($p.MainWindowTitle -or $p.MainWindowHandle -ne 0) {
        Write-Output "[$($p.ProcessName)] PID=$($p.Id) Title='$($p.MainWindowTitle)' Handle=$($p.MainWindowHandle)"
    }
}

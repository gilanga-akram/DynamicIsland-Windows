$p = Start-Process -FilePath "C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\publish_self_contained\DynamicIsland.exe" -PassThru
Start-Sleep -Seconds 4
Write-Output "PID: $($p.Id) HasExited: $($p.HasExited)"
if ($p.HasExited) {
    Write-Output "ExitCode: $($p.ExitCode)"
}

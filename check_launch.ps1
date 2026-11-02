$proc = Start-Process -FilePath "C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\publish_self_contained\DynamicIsland.exe" -PassThru
Start-Sleep -Seconds 2
Write-Output "PID: $($proc.Id)"
Write-Output "HasExited: $($proc.HasExited)"
if ($proc.HasExited) {
    Write-Output "ExitCode: $($proc.ExitCode)"
}

try {
    $proc = New-Object System.Diagnostics.Process
    $proc.StartInfo.FileName = "C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\publish_self_contained\DynamicIsland.exe"
    $proc.StartInfo.UseShellExecute = $false
    $proc.StartInfo.RedirectStandardOutput = $true
    $proc.StartInfo.RedirectStandardError = $true
    $proc.Start() | Out-Null
    
    $proc.WaitForExit(4000)
    Write-Output "HasExited: $($proc.HasExited) ExitCode: $($proc.ExitCode)"
    $out = $proc.StandardOutput.ReadToEnd()
    $err = $proc.StandardError.ReadToEnd()
    Write-Output "STDOUT: $out"
    Write-Output "STDERR: $err"
} catch {
    Write-Output "EXCEPTION: $_"
}

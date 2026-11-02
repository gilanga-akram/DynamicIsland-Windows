$sh = New-Object -ComObject WScript.Shell
$sc = $sh.CreateShortcut("C:\Users\PC\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Inno Setup 6\Inno Setup Compiler.lnk")
Write-Host "Inno target: $($sc.TargetPath)"
$iscc = Join-Path (Split-Path $sc.TargetPath) "ISCC.exe"
Write-Host "ISCC target: $iscc (Exists: $(Test-Path $iscc))"
if (Test-Path $iscc) {
    Set-Location 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island'
    & $iscc installer.iss
    Write-Host "Installer compiled successfully to E:\Installer"
}

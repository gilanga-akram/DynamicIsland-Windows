$wsh = New-Object -ComObject WScript.Shell
$sc = $wsh.CreateShortcut("C:\Users\PC\Desktop\Dynamic Island.lnk")
$sc.TargetPath = "C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\publish\DynamicIsland.exe"
$sc.WorkingDirectory = "C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\publish"
$sc.IconLocation = "C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\publish\DynamicIsland.exe,0"
$sc.Description = "Dynamic Island Notch Pro"
$sc.Save()
Write-Host "Shortcut generated successfully"

Set-Location 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island'

Stop-Process -Name 'DynamicIsland' -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 600

& 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe' installer.iss

Start-Process '.\publish_self_contained\DynamicIsland.exe'
Start-Sleep -Seconds 2
Get-Process -Name 'DynamicIsland'

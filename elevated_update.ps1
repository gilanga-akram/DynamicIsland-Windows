Stop-Process -Name DynamicIsland -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500
Copy-Item 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\publish_self_contained\DynamicIsland.exe' 'C:\Program Files\Dynamic Island Pro\DynamicIsland.exe' -Force
Copy-Item 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\Assets\*' 'C:\Program Files\Dynamic Island Pro\Assets\' -Recurse -Force -ErrorAction SilentlyContinue

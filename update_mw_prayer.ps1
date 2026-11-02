$path = 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\MainWindow.xaml.cs'
$lines = [System.IO.File]::ReadAllLines($path)
for ($i = 0; $i -lt $lines.Length; $i++) {
    if ($lines[$i].Trim() -eq 'DockManager.Instance.Initialize(Settings?.EnableFloatingDock ?? true);') {
        $lines[$i] = "            DockManager.Instance.Initialize(Settings?.EnableFloatingDock ?? true);`r`n            PrayerManager.Instance.CalculateDailyPrayers();"
    }
}
[System.IO.File]::WriteAllLines($path, $lines)
Write-Output "MainWindow.xaml.cs updated with PrayerManager init"

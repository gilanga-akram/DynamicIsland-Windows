$path = 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\MainWindow.xaml.cs'
$lines = [System.IO.File]::ReadAllLines($path)
$list = [System.Collections.Generic.List[string]]::new()
foreach ($l in $lines) {
    $list.Add($l)
}

# 1. Add UpdateIslandVisibility method right before UpdatePomodoroVisibility
$pomoIdx = -1
for ($i=0; $i -lt $list.Count; $i++) {
    if ($list[$i].Contains('public void UpdatePomodoroVisibility(bool isVisible)')) {
        $pomoIdx = $i
        break
    }
}

if ($pomoIdx -ne -1) {
    $methodStr = @"
        public void UpdateIslandVisibility(bool enabled)
        {
            if (IslandRootGrid != null)
            {
                IslandRootGrid.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            }
        }

"@
    $list.Insert($pomoIdx, $methodStr)
    Write-Output "Added UpdateIslandVisibility method at line $pomoIdx"
}

# 2. Add Settings.PropertyChanged listener for EnableDynamicIsland
for ($i=0; $i -lt $list.Count; $i++) {
    if ($list[$i].Contains('SoundManager.Enabled = Settings.EnableSoundEffects;')) {
        $propStr = @"
                else if (ev?.PropertyName == nameof(Settings.EnableDynamicIsland))
                {
                    Dispatcher.Invoke(() => UpdateIslandVisibility(Settings.EnableDynamicIsland));
                }
"@
        $list.Insert($i + 2, $propStr)
        Write-Output "Added PropertyChanged listener at line $i"
        break
    }
}

[System.IO.File]::WriteAllLines($path, $list.ToArray(), [System.Text.Encoding]::UTF8)
Write-Output "FINISHED WRITING MainWindow.xaml.cs"

$path = 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\MainWindow.xaml.cs'
$lines = [System.IO.File]::ReadAllLines($path)
$list = [System.Collections.Generic.List[string]]::new($lines)

# Find Settings.PropertyChanged
$idx = -1
for ($i=0; $i -lt $list.Count; $i++) {
    if ($list[$i].Trim() -eq 'SoundManager.Enabled = Settings.EnableSoundEffects;') {
        $idx = $i
        break
    }
}

if ($idx -ne -1) {
    # After line $idx + 1 ('}'), insert else if (ev?.PropertyName == nameof(Settings.EnableDynamicIsland))
    $insertLines = @(
        "                else if (ev?.PropertyName == nameof(Settings.EnableDynamicIsland))",
        "                {",
        "                    Dispatcher.Invoke(() => UpdateIslandVisibility(Settings.EnableDynamicIsland));",
        "                }"
    )
    $list.InsertRange($idx + 2, $insertLines)
    Write-Output "Inserted dynamic island property changed listener"
}

# Also find UpdatePomodoroVisibility to add UpdateIslandVisibility
$pomoIdx = -1
for ($i=0; $i -lt $list.Count; $i++) {
    if ($list[$i].Contains('public void UpdatePomodoroVisibility(bool isVisible)')) {
        $pomoIdx = $i
        break
    }
}

if ($pomoIdx -ne -1) {
    $funcLines = @(
        "        public void UpdateIslandVisibility(bool enabled)",
        "        {",
        "            if (IslandRootGrid != null)",
        "            {",
        "                IslandRootGrid.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;",
        "            }",
        "        }",
        ""
    )
    $list.InsertRange($pomoIdx, $funcLines)
    Write-Output "Inserted UpdateIslandVisibility method"
}

# Also in Window_Loaded, apply UpdateIslandVisibility(Settings?.EnableDynamicIsland ?? true);
for ($i=0; $i -lt $list.Count; $i++) {
    if ($list[$i].Contains('UpdatePomodoroVisibility(Settings?.ShowPomodoroWidget ?? true);')) {
        $list.Insert($i, "            UpdateIslandVisibility(Settings?.EnableDynamicIsland ?? true);")
        Write-Output "Inserted UpdateIslandVisibility call in Window_Loaded"
        break
    }
}

[System.IO.File]::WriteAllLines($path, $list.ToArray(), [System.Text.Encoding]::UTF8)
Write-Output "MainWindow.xaml.cs UPDATED SUCCESSFULLY"

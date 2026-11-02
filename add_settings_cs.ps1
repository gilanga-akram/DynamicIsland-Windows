$path = 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\SettingsWindow.xaml.cs'
$lines = [System.IO.File]::ReadAllLines($path)
$list = [System.Collections.Generic.List[string]]::new()
foreach ($l in $lines) {
    $list.Add($l)
}

# 1. Initialize ToggleEnableDynamicIsland in LoadSettings
for ($i=0; $i -lt $list.Count; $i++) {
    if ($list[$i].Contains('if (ToggleSoundEffects != null)')) {
        $list.Insert($i, "                if (ToggleEnableDynamicIsland != null) ToggleEnableDynamicIsland.IsChecked = _settings.EnableDynamicIsland;")
        Write-Output "Added ToggleEnableDynamicIsland initialization in LoadSettings"
        break
    }
}

# 2. Add event handler ToggleEnableDynamicIsland_Click
for ($i=0; $i -lt $list.Count; $i++) {
    if ($list[$i].Contains('private void ToggleFloatingDock_Click')) {
        $handlerBlock = @"
        private void ToggleEnableDynamicIsland_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.EnableDynamicIsland = ToggleEnableDynamicIsland.IsChecked == true;
                _mainWindow?.UpdateIslandVisibility(_settings.EnableDynamicIsland);
            }
        }

"@
        $list.Insert($i, $handlerBlock)
        Write-Output "Added ToggleEnableDynamicIsland_Click handler"
        break
    }
}

[System.IO.File]::WriteAllLines($path, $list.ToArray(), [System.Text.Encoding]::UTF8)
Write-Output "SUCCESS WRITING SettingsWindow.xaml.cs"

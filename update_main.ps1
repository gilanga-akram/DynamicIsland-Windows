$path = 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\MainWindow.xaml.cs'
$content = [System.IO.File]::ReadAllText($path)
$target = @"
            Settings.PropertyChanged += (s, ev) =>
            {
                if (ev?.PropertyName == nameof(Settings.EnableSoundEffects))
                {
                    SoundManager.Enabled = Settings.EnableSoundEffects;
                }
            };
"@
$replacement = @"
            Settings.PropertyChanged += (s, ev) =>
            {
                if (ev?.PropertyName == nameof(Settings.EnableSoundEffects))
                {
                    SoundManager.Enabled = Settings.EnableSoundEffects;
                }
                else if (ev?.PropertyName == nameof(Settings.EnableDynamicIsland))
                {
                    Dispatcher.Invoke(() => UpdateIslandVisibility(Settings.EnableDynamicIsland));
                }
            };
"@
if ($content.Contains($target)) {
    $content = $content.Replace($target, $replacement)
    [System.IO.File]::WriteAllText($path, $content)
    Write-Output "SUCCESS"
} else {
    Write-Output "TARGET NOT FOUND DIRECTLY"
}

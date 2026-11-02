$path = 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\SettingsWindow.xaml.cs'
$lines = [System.IO.File]::ReadAllLines($path)
for ($i = 0; $i -lt $lines.Length; $i++) {
    if ($lines[$i].Trim() -eq '_settings.PrayerCity = item.Content?.ToString() ?? "Jakarta (WIB)";') {
        $lines[$i] = "                _settings.PrayerCity = item.Content?.ToString() ?? `"Jakarta (WIB)`";`r`n                PrayerManager.Instance.CalculateDailyPrayers();"
    }
    if ($lines[$i].Trim() -eq 'if (_settings != null) _settings.PrayerNotification = TogglePrayerNotif.IsChecked == true;') {
        $lines[$i] = "            if (_settings != null)`r`n            {`r`n                _settings.PrayerNotification = TogglePrayerNotif.IsChecked == true;`r`n                PrayerManager.Instance.CalculateDailyPrayers();`r`n            }"
    }
}
[System.IO.File]::WriteAllLines($path, $lines)
Write-Output "Successfully modified SettingsWindow.xaml.cs by line replacement"

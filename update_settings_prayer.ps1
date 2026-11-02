$path = 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\SettingsWindow.xaml.cs'
$content = [System.IO.File]::ReadAllText($path)
$target = @"
        private void CmbPrayerCity_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_settings != null && CmbPrayerCity?.SelectedItem is ComboBoxItem item)
            {
                _settings.PrayerCity = item.Content?.ToString() ?? "Jakarta (WIB)";
            }
        }

        private void TogglePrayerNotif_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null) _settings.PrayerNotification = TogglePrayerNotif.IsChecked == true;
        }
"@

$replace = @"
        private void CmbPrayerCity_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_settings != null && CmbPrayerCity?.SelectedItem is ComboBoxItem item)
            {
                _settings.PrayerCity = item.Content?.ToString() ?? "Jakarta (WIB)";
                PrayerManager.Instance.CalculateDailyPrayers();
            }
        }

        private void TogglePrayerNotif_Click(object sender, RoutedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.PrayerNotification = TogglePrayerNotif.IsChecked == true;
                PrayerManager.Instance.CalculateDailyPrayers();
            }
        }
"@

$content = $content.Replace($target.Replace("`r`n", "`n"), $replace.Replace("`r`n", "`n"))
$content = $content.Replace($target, $replace)
[System.IO.File]::WriteAllText($path, $content)
Write-Output "Done replacing in SettingsWindow.xaml.cs"

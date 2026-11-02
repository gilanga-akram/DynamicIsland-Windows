$path = 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\QuickControlWindow.xaml.cs'
$content = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)

# 1. Update RefreshState to check real Bluetooth state
$oldVol = @"
            // 3. Volume
            if (!_isUpdatingSliders)
            {
                _isUpdatingSliders = true;
                if (Application.Current.MainWindow is MainWindow mwMain && mwMain.Audio != null)
                {
                    int currentVol = mwMain.Audio.MasterOutputVolume;
                    SliderVolume.Value = currentVol;
                    TxtVolumeVal.Text = currentVol.ToString();
                }
                _isUpdatingSliders = false;
            }
        }
"@

$newVol = @"
            // 3. Volume
            if (!_isUpdatingSliders)
            {
                _isUpdatingSliders = true;
                if (Application.Current.MainWindow is MainWindow mwMain && mwMain.Audio != null)
                {
                    int currentVol = mwMain.Audio.MasterOutputVolume;
                    SliderVolume.Value = currentVol;
                    TxtVolumeVal.Text = currentVol.ToString();
                }
                _isUpdatingSliders = false;
            }

            // 4. Bluetooth Real State
            _ = UpdateBluetoothUIAsync();
        }

        private async Task UpdateBluetoothUIAsync()
        {
            try
            {
                _isBluetoothOn = await BluetoothService.IsBluetoothOnAsync();
                UpdateBluetoothVisual();
            }
            catch { }
        }

        private void UpdateBluetoothVisual()
        {
            if (BdBtTile == null) return;
            if (_isBluetoothOn)
            {
                BdBtTile.Background = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                if (TxtBtHeader != null) TxtBtHeader.Foreground = new SolidColorBrush(Color.FromRgb(17, 17, 17));
                if (TxtBtStatus != null) TxtBtStatus.Foreground = new SolidColorBrush(Color.FromRgb(70, 70, 70));
                if (IconBt != null) IconBt.Fill = new SolidColorBrush(Color.FromRgb(17, 17, 17));
                if (TxtBtArrow != null) TxtBtArrow.Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85));
                if (TxtBtStatus != null) TxtBtStatus.Text = "Nyala";
            }
            else
            {
                BdBtTile.Background = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
                if (TxtBtHeader != null) TxtBtHeader.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                if (TxtBtStatus != null) TxtBtStatus.Foreground = new SolidColorBrush(Color.FromRgb(180, 190, 205));
                if (IconBt != null) IconBt.Fill = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                if (TxtBtArrow != null) TxtBtArrow.Foreground = new SolidColorBrush(Color.FromRgb(180, 190, 205));
                if (TxtBtStatus != null) TxtBtStatus.Text = "Mati";
            }
        }
"@

# 2. Update BtnBluetoothTile_Click and ApplyBluetoothState
$oldBt = @"
        private bool _isBluetoothOn = true;

        private void BtnBluetoothTile_Click(object sender, RoutedEventArgs e)
        {
            _isBluetoothOn = !_isBluetoothOn;
            ApplyBluetoothState();
        }

        private void ApplyBluetoothState()
        {
            try
            {
                string verb = _isBluetoothOn ? "Enable" : "Disable";
                string cmd = `$"Get-PnpDevice | Where-Object {{$_.Class -eq 'Bluetooth' -and $_.FriendlyName -notlike '*enumerator*'}} | {verb}-PnpDevice -Confirm:$false";
                var psi = new ProcessStartInfo("powershell.exe",
                    `$"-WindowStyle Hidden -Command \"{cmd}\"")
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true
                };
                Process.Start(psi);
            }
            catch { }

            // Update tile color immediately
            BdBtTile.Background = _isBluetoothOn
                ? new SolidColorBrush(Color.FromRgb(255, 255, 255))
                : new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
            TxtBtStatus.Text = _isBluetoothOn ? "Nyala" : "Mati";
        }
"@

$newBt = @"
        private bool _isBluetoothOn = false;

        private async void BtnBluetoothTile_Click(object sender, RoutedEventArgs e)
        {
            _isBluetoothOn = !_isBluetoothOn;
            UpdateBluetoothVisual();

            bool success = await BluetoothService.SetBluetoothStateAsync(_isBluetoothOn);
            if (!success)
            {
                // If direct Windows Radio API not accessible without prompt, open Bluetooth settings smoothly
                BluetoothService.OpenWindowsBluetoothSettings();
            }
            
            // Re-verify actual state after 1.5s
            await Task.Delay(1500);
            _ = UpdateBluetoothUIAsync();
        }
"@

$content = $content.Replace($oldVol.Replace("`r`n", "`n"), $newVol.Replace("`r`n", "`n"))
$content = $content.Replace($oldBt.Replace("`r`n", "`n"), $newBt.Replace("`r`n", "`n"))

[System.IO.File]::WriteAllText($path, $content, [System.Text.Encoding]::UTF8)
Write-Output "QuickControlWindow.xaml.cs updated successfully"

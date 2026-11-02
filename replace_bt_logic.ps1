$path = 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\QuickControlWindow.xaml.cs'
$lines = [System.IO.File]::ReadAllLines($path, [System.Text.Encoding]::UTF8)

$startIndex = -1
$endIndex = -1

for ($i = 0; $i -lt $lines.Length; $i++) {
    if ($lines[$i] -match 'private bool _isBluetoothOn = true;') {
        $startIndex = $i
    }
    if ($startIndex -ge 0 -and $lines[$i] -match 'TxtBtStatus\.Text = _isBluetoothOn \? "Nyala" : "Mati";') {
        $endIndex = $i + 1 # include closing brace
        break
    }
}

if ($startIndex -ge 0 -and $endIndex -ge $startIndex) {
    $newLines = @(
        '        private bool _isBluetoothOn = false;',
        '',
        '        private async void BtnBluetoothTile_Click(object sender, RoutedEventArgs e)',
        '        {',
        '            _isBluetoothOn = !_isBluetoothOn;',
        '            UpdateBluetoothVisual();',
        '',
        '            bool success = await BluetoothService.SetBluetoothStateAsync(_isBluetoothOn);',
        '            if (!success)',
        '            {',
        '                BluetoothService.OpenWindowsBluetoothSettings();',
        '            }',
        '',
        '            await Task.Delay(1200);',
        '            _ = UpdateBluetoothUIAsync();',
        '        }'
    )

    $finalList = New-Object System.Collections.Generic.List[string]
    for ($i = 0; $i -lt $lines.Length; $i++) {
        if ($i -eq $startIndex) {
            foreach ($nl in $newLines) { $finalList.Add($nl) }
            $i = $endIndex
        } else {
            $finalList.Add($lines[$i])
        }
    }

    [System.IO.File]::WriteAllLines($path, $finalList.ToArray(), [System.Text.Encoding]::UTF8)
    Write-Output "Successfully removed ApplyBluetoothState and powershell execution"
} else {
    Write-Output "Could not locate block: start=$startIndex, end=$endIndex"
}

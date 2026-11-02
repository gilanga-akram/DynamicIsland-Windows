$path = 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\QuickControlWindow.xaml'
$lines = [System.IO.File]::ReadAllLines($path, [System.Text.Encoding]::UTF8)

for ($i = 0; $i -lt $lines.Length; $i++) {
    if ($lines[$i] -match 'Data="M14\.24,12\.01L19,7\.24l-6\.5-6\.5C12\.12,0\.36,11\.5,0\.78') {
        $lines[$i] = $lines[$i].Replace('Fill="#111111"', 'Fill="#111111" x:Name="IconBt"')
    }
    if ($lines[$i] -match '<TextBlock Text="Bluetooth"') {
        $lines[$i] = $lines[$i].Replace('<TextBlock Text="Bluetooth"', '<TextBlock Text="Bluetooth" x:Name="TxtBtHeader"')
    }
}

[System.IO.File]::WriteAllLines($path, $lines, [System.Text.Encoding]::UTF8)
Write-Output "QuickControlWindow.xaml updated with IconBt and TxtBtHeader"

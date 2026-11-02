$path = 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\FloatingDockWindow.xaml.cs'
$lines = [System.IO.File]::ReadAllLines($path, [System.Text.Encoding]::UTF8)

for ($i = 0; $i -lt $lines.Length; $i++) {
    if ($lines[$i] -match 'Width = 920; Height = 86;') {
        $lines[$i] = '            Height = 86; if (Width < 640) Width = 920;'
    }
    if ($lines[$i] -match 'Width = Math\.Min\(1300, Math\.Max\(640, 420 \+ itemCount \* 43\)\);') {
        $lines[$i] = '            double sw = SystemParameters.PrimaryScreenWidth; Width = Math.Min(sw - 30, Math.Max(640, 470 + itemCount * 46));'
    }
    if ($lines[$i] -match 'TxtDockDate\.Text  = now\.ToString\("dd/MM/yyyy"\);') {
        $lines[$i] = '            TxtDockDate.Text  = now.ToString("dd/MM");'
    }
}

[System.IO.File]::WriteAllLines($path, $lines, [System.Text.Encoding]::UTF8)
Write-Output "Updated FloatingDockWindow.xaml.cs width logic and date format"

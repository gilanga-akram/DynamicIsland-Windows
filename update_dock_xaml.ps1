$path = 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\FloatingDockWindow.xaml'
$lines = [System.IO.File]::ReadAllLines($path, [System.Text.Encoding]::UTF8)

for ($i = 0; $i -lt $lines.Length; $i++) {
    if ($lines[$i] -match 'Padding="14,3,18,3"') {
        $lines[$i] = $lines[$i].Replace('Padding="14,3,18,3"', 'Padding="14,3,26,3"')
    }
    if ($lines[$i] -match 'Margin="2,0,8,0"') {
        $lines[$i] = $lines[$i].Replace('Margin="2,0,8,0"', 'Margin="4,0,12,0"')
    }
}

[System.IO.File]::WriteAllLines($path, $lines, [System.Text.Encoding]::UTF8)
Write-Output "Updated padding & margin in FloatingDockWindow.xaml"

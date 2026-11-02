$path = 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\MainWindow.xaml.cs'
$lines = [System.IO.File]::ReadAllLines($path)
$list = [System.Collections.Generic.List[string]]::new()
foreach ($l in $lines) { $list.Add($l) }

for ($i=70; $i -le 85; $i++) {
    if ($list[$i].Trim() -eq '{' -and $list[$i+1].Contains('public SettingsManager Settings')) {
        $list.RemoveAt($i)
        Write-Output "Removed duplicate { at line $i"
        break
    }
}

[System.IO.File]::WriteAllLines($path, $list.ToArray(), [System.Text.Encoding]::UTF8)

$path = 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\MainWindow.xaml.cs'
$lines = [System.IO.File]::ReadAllLines($path)
$list = [System.Collections.Generic.List[string]]::new()
foreach ($l in $lines) { $list.Add($l) }

for ($i=65; $i -le 75; $i++) {
    if ($list[$i].Contains('DropShadowEffect _glassShadowExpanded')) {
        $list[$i] = $list[$i].Replace('DropShadowEffect', 'System.Windows.Media.Effects.DropShadowEffect')
        Write-Output "Qualified DropShadowEffect type with namespace at line $i"
    }
    if ($list[$i].Contains('new DropShadowEffect')) {
        $list[$i] = $list[$i].Replace('new DropShadowEffect', 'new System.Windows.Media.Effects.DropShadowEffect')
        Write-Output "Qualified new DropShadowEffect with namespace at line $i"
    }
}

[System.IO.File]::WriteAllLines($path, $list.ToArray(), [System.Text.Encoding]::UTF8)

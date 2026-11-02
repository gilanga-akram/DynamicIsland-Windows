$path = 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\MainWindow.xaml.cs'
$lines = [System.IO.File]::ReadAllLines($path)
$list = [System.Collections.Generic.List[string]]::new()
foreach ($l in $lines) { $list.Add($l) }

for ($i=45; $i -le 60; $i++) {
    if ($list[$i].Trim() -eq 'new GradientStopCollection') {
        $list.Insert($i + 1, "            {")
        Write-Output "Added missing { after new GradientStopCollection at line $i"
        break
    }
}

[System.IO.File]::WriteAllLines($path, $list.ToArray(), [System.Text.Encoding]::UTF8)

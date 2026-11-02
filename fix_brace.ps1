$path = 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\MainWindow.xaml.cs'
$lines = [System.IO.File]::ReadAllLines($path)
$list = [System.Collections.Generic.List[string]]::new()
foreach ($l in $lines) { $list.Add($l) }

# Find line with "public partial class MainWindow : Window"
for ($i=40; $i -le 80; $i++) {
    if ($list[$i].Trim() -eq 'public partial class MainWindow : Window') {
        # Check if next line is not '{'
        if ($list[$i+1].Trim() -ne '{') {
            # Find the stray '{' at line 76 and remove it, and insert '{' at $i+1
            for ($j=$i; $j -le $i+40; $j++) {
                if ($list[$j].Trim() -eq '{') {
                    $list.RemoveAt($j)
                    break
                }
            }
            $list.Insert($i + 1, "    {")
            Write-Output "Fixed brace placement on MainWindow class definition"
            break
        }
    }
}

[System.IO.File]::WriteAllLines($path, $list.ToArray(), [System.Text.Encoding]::UTF8)

$content = Get-Content 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\MainWindow.xaml' -Raw
$matches = [regex]::Matches($content, 'Background="#([0-9A-Fa-f]{6,8})"')
$unique = $matches | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
$unique

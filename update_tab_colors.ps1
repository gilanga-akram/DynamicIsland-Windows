$path = 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\MainWindow.xaml.cs'
$content = [System.IO.File]::ReadAllText($path)

$target1 = 'var normalFg = new SolidColorBrush(Color.FromRgb(142, 142, 147)); // #8E8E93'
$replace1 = 'var normalFg = new SolidColorBrush(Color.FromRgb(240, 245, 255)); // Bright White #F0F5FF'
$target2 = 'var activeBg = new SolidColorBrush(Color.FromRgb(47, 47, 52)); // #2F2F34'
$replace2 = 'var activeBg = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)); // Luminous Glass Pill #78FFFFFF'
$target3 = 'var activeBorder = new SolidColorBrush(Color.FromRgb(61, 61, 69)); // #3D3D45'
$replace3 = 'var activeBorder = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)); // Vivid Rim #B4FFFFFF'

if ($content.Contains($target1)) {
    $content = $content.Replace($target1, $replace1)
    $content = $content.Replace($target2, $replace2)
    $content = $content.Replace($target3, $replace3)
    Write-Output "Updated ribbon tab active & normal colors in SetActiveView"
}

[System.IO.File]::WriteAllText($path, $content, [System.Text.Encoding]::UTF8)

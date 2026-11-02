$path = 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\QuickControlWindow.xaml'
$lines = [System.IO.File]::ReadAllLines($path, [System.Text.Encoding]::UTF8)

for ($i = 0; $i -lt $lines.Length; $i++) {
    if ($lines[$i] -match '<!-- Arrow right -->\s*<TextBlock Grid\.Column="2" Text=">" Foreground="#555555"') {
        $lines[$i] = '                                    <TextBlock Grid.Column="2" Text=">" Foreground="#555555" x:Name="TxtBtArrow" FontSize="16" FontWeight="Bold" VerticalAlignment="Center" HorizontalAlignment="Right"/>'
    } elseif ($lines[$i] -match '<TextBlock Grid\.Column="2" Text=">" Foreground="#555555"') {
        $lines[$i] = '                                    <TextBlock Grid.Column="2" Text=">" Foreground="#555555" x:Name="TxtBtArrow" FontSize="16" FontWeight="Bold" VerticalAlignment="Center" HorizontalAlignment="Right"/>'
    }
}

[System.IO.File]::WriteAllLines($path, $lines, [System.Text.Encoding]::UTF8)
Write-Output "QuickControlWindow.xaml updated with TxtBtArrow"

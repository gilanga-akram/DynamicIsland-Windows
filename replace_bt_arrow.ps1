$path = 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\QuickControlWindow.xaml'
$raw = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
$target = '<TextBlock Grid.Column="2" Text=">" Foreground="#555555" FontSize="16" FontWeight="Bold" VerticalAlignment="Center" HorizontalAlignment="Right"/>'
$replacement = '<TextBlock Grid.Column="2" Text=">" Foreground="#555555" x:Name="TxtBtArrow" FontSize="16" FontWeight="Bold" VerticalAlignment="Center" HorizontalAlignment="Right"/>'

# Only replace the second occurrence (which is inside Bluetooth tile)
$idx = $raw.IndexOf($target)
if ($idx -ge 0) {
    # check next occurrence
    $idx2 = $raw.IndexOf($target, $idx + $target.Length)
    if ($idx2 -ge 0) {
        $raw = $raw.Substring(0, $idx2) + $replacement + $raw.Substring($idx2 + $target.Length)
        [System.IO.File]::WriteAllText($path, $raw, [System.Text.Encoding]::UTF8)
        Write-Output "Successfully replaced second occurrence with TxtBtArrow"
    } else {
        Write-Output "Second occurrence not found"
    }
} else {
    Write-Output "First occurrence not found"
}

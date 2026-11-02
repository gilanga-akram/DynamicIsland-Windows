$path = 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\QuickControlWindow.xaml'
$lines = [System.IO.File]::ReadAllLines($path, [System.Text.Encoding]::UTF8)
$lines[157] = '                                    <TextBlock Grid.Column="2" Text=">" Foreground="#555555" x:Name="TxtBtArrow" FontSize="16" FontWeight="Bold" VerticalAlignment="Center" HorizontalAlignment="Right"/>'
[System.IO.File]::WriteAllLines($path, $lines, [System.Text.Encoding]::UTF8)
Write-Output "Line 157 replaced successfully"

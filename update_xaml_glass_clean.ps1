$xamlPath = 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\MainWindow.xaml'
$content = [System.IO.File]::ReadAllText($xamlPath, [System.Text.Encoding]::UTF8)

# Normalize line endings
$content = $content.Replace("`r`n", "`n")

# 1. Update TextSecondary
$content = $content.Replace('Color="#8E8E93"', 'Color="#E2E8F0"')

# 2. Update NotchFillPath & NotchBorderPath initial values
$content = $content.Replace('x:Name="NotchFillPath" Fill="#000000"', 'x:Name="NotchFillPath" Fill="#3C1A2638"')
$content = $content.Replace('x:Name="NotchBorderPath" Stroke="#25252C" StrokeThickness="1"', 'x:Name="NotchBorderPath" Stroke="#40FFFFFF" StrokeThickness="1.2"')

# 3. Direct dark hex replacements
$darkHexList = @(
    '#060608', '#1A1A22', '#261220', '#23232A', '#1A1A24', '#2C2C3A',
    '#181824', '#2A2A38', '#1E1E28', '#1E293B', '#2E1065', '#161F30',
    '#1C2E3D', '#1E2840', '#162E20', '#1A2438', '#142636', '#142E1C',
    '#0A0A10', '#163422', '#183624', '#2C2036', '#141418', '#2A2A32',
    '#1C2232', '#1C2E46', '#1E2838', '#1C2E20', '#1F1622', '#2E1C33',
    '#1C1820', '#1A2738', '#1E1E2C', '#1C2738', '#1C1E2A', '#142036',
    '#293140', '#1C1C26', '#2A2A3C', '#222230', '#221418', '#121018',
    '#1E1424', '#221832', '#1E1F38', '#152438', '#1E1E26', '#2E1C24',
    '#15151C', '#1A2B42'
)

foreach ($hex in $darkHexList) {
    $content = $content.Replace("Background=""$hex""", 'Background="#25FFFFFF"')
}

$darkBorders = @(
    '#20202E', '#282830', '#22222C', '#353545', '#30303E',
    '#3D2444', '#3A1E24', '#4A2648', '#3A3A46', '#2E1C28'
)

foreach ($hex in $darkBorders) {
    $content = $content.Replace("BorderBrush=""$hex""", 'BorderBrush="#40FFFFFF"')
}

# 4. In ViewTools, add FontFamily="Segoe UI Emoji" to ensure emojis render with rich contrast
$content = $content.Replace('Text="Kunci PC" FontSize="8.5" Foreground="{StaticResource TextPrimary}"', 'Text="Kunci PC" FontSize="9" FontWeight="SemiBold" Foreground="#FFFFFF"')
$content = $content.Replace('Text="Sleep" FontSize="8.5" Foreground="{StaticResource TextPrimary}"', 'Text="Sleep" FontSize="9" FontWeight="SemiBold" Foreground="#FFFFFF"')
$content = $content.Replace('Text="Bisukan" FontSize="8.5" Foreground="{StaticResource TextPrimary}"', 'Text="Bisukan" FontSize="9" FontWeight="SemiBold" Foreground="#FFFFFF"')
$content = $content.Replace('Text="Warna" FontSize="8.5" Foreground="{StaticResource TextPrimary}"', 'Text="Warna" FontSize="9" FontWeight="SemiBold" Foreground="#FFFFFF"')
$content = $content.Replace('Text="Snip Tool" FontSize="8.5" Foreground="{StaticResource TextPrimary}"', 'Text="Snip Tool" FontSize="9" FontWeight="SemiBold" Foreground="#FFFFFF"')
$content = $content.Replace('Text="Clipboard" FontSize="8.5" Foreground="{StaticResource TextPrimary}"', 'Text="Clipboard" FontSize="9" FontWeight="SemiBold" Foreground="#FFFFFF"')
$content = $content.Replace('Text="Task Mgr" FontSize="8.5" Foreground="{StaticResource TextPrimary}"', 'Text="Task Mgr" FontSize="9" FontWeight="SemiBold" Foreground="#FFFFFF"')
$content = $content.Replace('Text="Settings" FontSize="8.5" Foreground="{StaticResource TextPrimary}"', 'Text="Settings" FontSize="9" FontWeight="SemiBold" Foreground="#FFFFFF"')

# Convert back to CRLF
$content = $content.Replace("`n", "`r`n")
[System.IO.File]::WriteAllText($xamlPath, $content, [System.Text.Encoding]::UTF8)
Write-Output "MainWindow.xaml updated successfully."

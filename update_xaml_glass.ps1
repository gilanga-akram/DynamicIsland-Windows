$xamlPath = 'C:\Users\PC\.gemini\antigravity\scratch\dynamic-island\MainWindow.xaml'
$content = [System.IO.File]::ReadAllText($xamlPath, [System.Text.Encoding]::UTF8)

# 1. Update TextSecondary to high-contrast crisp silver-white
$content = $content.Replace('<SolidColorBrush x:Key="TextSecondary" Color="#8E8E93" />', '<SolidColorBrush x:Key="TextSecondary" Color="#E2E8F0" />')

# 2. Update NotchFillPath & NotchBorderPath initial values
$content = $content.Replace('<Path x:Name="NotchFillPath" Fill="#000000" IsHitTestVisible="False" SnapsToDevicePixels="True"/>', '<Path x:Name="NotchFillPath" Fill="#2E1A2638" IsHitTestVisible="False" SnapsToDevicePixels="True"/>')
$content = $content.Replace('<Path x:Name="NotchBorderPath" Stroke="#25252C" StrokeThickness="1" IsHitTestVisible="False" SnapsToDevicePixels="True"/>', '<Path x:Name="NotchBorderPath" Stroke="#35FFFFFF" StrokeThickness="1.2" IsHitTestVisible="False" SnapsToDevicePixels="True"/>')

# 3. Update remaining solid dark backgrounds to frosted glass
$replacements = @{
    'Background="#060608"' = 'Background="#25FFFFFF"'
    'Background="#1A1A22"' = 'Background="#25FFFFFF"'
    'Background="#261220"' = 'Background="#25FFFFFF"'
    'Background="#23232A"' = 'Background="#25FFFFFF"'
    'Background="#1A1A24"' = 'Background="#20FFFFFF"'
    'Background="#2C2C3A"' = 'Background="#30FFFFFF"'
    'Background="#181824"' = 'Background="#20FFFFFF"'
    'Background="#2A2A38"' = 'Background="#25FFFFFF"'
    'Background="#1E1E28"' = 'Background="#25FFFFFF"'
    'Background="#1E293B"' = 'Background="#28FFFFFF"'
    'Background="#2E1065"' = 'Background="#28FFFFFF"'
    'Background="#161F30"' = 'Background="#25FFFFFF"'
    'Background="#1C2E3D"' = 'Background="#28FFFFFF"'
    'Background="#1E2840"' = 'Background="#28FFFFFF"'
    'Background="#162E20"' = 'Background="#28FFFFFF"'
    'Background="#1A2438"' = 'Background="#28FFFFFF"'
    'Background="#142636"' = 'Background="#28FFFFFF"'
    'Background="#142E1C"' = 'Background="#28FFFFFF"'
    'Background="#0A0A10"' = 'Background="#20FFFFFF"'
    'Background="#163422"' = 'Background="#28FFFFFF"'
    'Background="#183624"' = 'Background="#28FFFFFF"'
    'Background="#2C2036"' = 'Background="#28FFFFFF"'
    'Background="#141418"' = 'Background="#20FFFFFF"'
    'Background="#2A2A32"' = 'Background="#30FFFFFF"'
    'Background="#1C2232"' = 'Background="#25FFFFFF"'
    'Background="#1C2E46"' = 'Background="#28FFFFFF"'
    'Background="#1E2838"' = 'Background="#28FFFFFF"'
    'Background="#1C2E20"' = 'Background="#28FFFFFF"'
    'Background="#1F1622"' = 'Background="#25FFFFFF"'
    'Background="#2E1C33"' = 'Background="#28FFFFFF"'
    'Background="#1C1820"' = 'Background="#25FFFFFF"'
    'Background="#1A2738"' = 'Background="#28FFFFFF"'
    'Background="#1E1E2C"' = 'Background="#25FFFFFF"'
    'Background="#1C2738"' = 'Background="#28FFFFFF"'
    'Background="#1C1E2A"' = 'Background="#25FFFFFF"'
    'Background="#142036"' = 'Background="#25FFFFFF"'
    'Background="#293140"' = 'Background="#25FFFFFF"'
    'Background="#1C1C26"' = 'Background="#25FFFFFF"'
    'Background="#2A2A3C"' = 'Background="#28FFFFFF"'
    'Background="#222230"' = 'Background="#25FFFFFF"'
    'Background="#221418"' = 'Background="#25FFFFFF"'
    'Background="#121018"' = 'Background="#20FFFFFF"'
    'Background="#1E1424"' = 'Background="#25FFFFFF"'
    'Background="#221832"' = 'Background="#25FFFFFF"'
    'Background="#1E1F38"' = 'Background="#28FFFFFF"'
    'Background="#152438"' = 'Background="#28FFFFFF"'
    'Background="#1E1E26"' = 'Background="#25FFFFFF"'
    'Background="#2E1C24"' = 'Background="#25FFFFFF"'
    'Background="#15151C"' = 'Background="#20FFFFFF"'
    'Background="#1A2B42"' = 'Background="#28FFFFFF"'
    'BorderBrush="#20202E"' = 'BorderBrush="#40FFFFFF"'
    'BorderBrush="#282830"' = 'BorderBrush="#40FFFFFF"'
    'BorderBrush="#22222C"' = 'BorderBrush="#40FFFFFF"'
    'BorderBrush="#353545"' = 'BorderBrush="#40FFFFFF"'
    'BorderBrush="#30303E"' = 'BorderBrush="#40FFFFFF"'
    'BorderBrush="#3D2444"' = 'BorderBrush="#40FFFFFF"'
    'BorderBrush="#3A1E24"' = 'BorderBrush="#40FFFFFF"'
    'BorderBrush="#4A2648"' = 'BorderBrush="#40FFFFFF"'
    'BorderBrush="#3A3A46"' = 'BorderBrush="#40FFFFFF"'
    'BorderBrush="#2E1C28"' = 'BorderBrush="#40FFFFFF"'
}

foreach ($key in $replacements.Keys) {
    $content = $content.Replace($key, $replacements[$key])
}

# 4. In ViewTools (Quick Actions), ensure emoji textblocks have FontFamily Segoe UI Emoji and crisp contrast
$content = $content.Replace('<TextBlock Text="🔒" FontSize="15" HorizontalAlignment="Center"/>', '<TextBlock Text="🔒" FontFamily="Segoe UI Emoji" FontSize="15" HorizontalAlignment="Center"/>')
$content = $content.Replace('<TextBlock Text="🌙" FontSize="15" HorizontalAlignment="Center"/>', '<TextBlock Text="🌙" FontFamily="Segoe UI Emoji" FontSize="15" HorizontalAlignment="Center"/>')
$content = $content.Replace('<TextBlock Text="🔇" FontSize="15" HorizontalAlignment="Center"/>', '<TextBlock Text="🔇" FontFamily="Segoe UI Emoji" FontSize="15" HorizontalAlignment="Center"/>')
$content = $content.Replace('<TextBlock Text="🎨" FontSize="15" HorizontalAlignment="Center"/>', '<TextBlock Text="🎨" FontFamily="Segoe UI Emoji" FontSize="15" HorizontalAlignment="Center"/>')
$content = $content.Replace('<TextBlock Text="✂️" FontSize="15" HorizontalAlignment="Center"/>', '<TextBlock Text="✂️" FontFamily="Segoe UI Emoji" FontSize="15" HorizontalAlignment="Center"/>')
$content = $content.Replace('<TextBlock Text="📋" FontSize="15" HorizontalAlignment="Center"/>', '<TextBlock Text="📋" FontFamily="Segoe UI Emoji" FontSize="15" HorizontalAlignment="Center"/>')
$content = $content.Replace('<TextBlock Text="📊" FontSize="15" HorizontalAlignment="Center"/>', '<TextBlock Text="📊" FontFamily="Segoe UI Emoji" FontSize="15" HorizontalAlignment="Center"/>')
$content = $content.Replace('<TextBlock Text="⚙️" FontSize="15" HorizontalAlignment="Center"/>', '<TextBlock Text="⚙️" FontFamily="Segoe UI Emoji" FontSize="15" HorizontalAlignment="Center"/>')

[System.IO.File]::WriteAllText($xamlPath, $content, [System.Text.Encoding]::UTF8)
Write-Output "MainWindow.xaml updated successfully."
